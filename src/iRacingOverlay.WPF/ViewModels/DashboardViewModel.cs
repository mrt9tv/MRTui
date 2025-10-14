using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.ViewModels;

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly ITelemetryService _telemetryService;
    private readonly WidgetManager _widgetManager;
    private readonly DispatcherTimer _uptimeTimer;
    private readonly DateTime _startTime;

    private ConnectionStatus _connectionStatus;
    private string _connectionStatusText;
    private string _connectionStatusIcon;
    private string _sessionInfo;
    private int _activeWidgetCount;
    private string _activeWidgetsList;
    private string _uptimeText;
    private string _lastConnectionTime;
    private double _updateRate;

    public event PropertyChangedEventHandler? PropertyChanged;

    public DashboardViewModel(ITelemetryService telemetryService, WidgetManager widgetManager)
    {
        _telemetryService = telemetryService;
        _widgetManager = widgetManager;
        _startTime = DateTime.Now;

        // Initialize properties
        _connectionStatus = ConnectionStatus.Disconnected;
        _connectionStatusText = "iRacing Not Running";
        _connectionStatusIcon = "🔴";
        _sessionInfo = "No active session";
        _activeWidgetCount = 0;
        _activeWidgetsList = "No widgets active";
        _uptimeText = "00:00:00";
        _lastConnectionTime = "Never";
        _updateRate = 0.0;

        // Subscribe to events
        _telemetryService.StatusChanged += OnTelemetryStatusChanged;
        _widgetManager.WidgetCreated += OnWidgetCreated;
        _widgetManager.WidgetRemoved += OnWidgetRemoved;

        // Setup uptime timer
        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += UpdateUptime;
        _uptimeTimer.Start();

        // Initial update - check current telemetry status
        UpdateWidgetInfo();
        UpdateConnectionStatusFromService();
    }

    private void UpdateConnectionStatusFromService()
    {
        // Get the current status from the telemetry service
        var currentStatus = _telemetryService.Status;
        OnTelemetryStatusChanged(null, new ConnectionStatusEventArgs(currentStatus));
    }

    #region Properties

    public string ConnectionStatusIcon
    {
        get => _connectionStatusIcon;
        set { _connectionStatusIcon = value; OnPropertyChanged(); }
    }

    public string ConnectionStatusText
    {
        get => _connectionStatusText;
        set { _connectionStatusText = value; OnPropertyChanged(); }
    }

    public string SessionInfo
    {
        get => _sessionInfo;
        set { _sessionInfo = value; OnPropertyChanged(); }
    }

    public int ActiveWidgetCount
    {
        get => _activeWidgetCount;
        set { _activeWidgetCount = value; OnPropertyChanged(); }
    }

    public string ActiveWidgetsList
    {
        get => _activeWidgetsList;
        set { _activeWidgetsList = value; OnPropertyChanged(); }
    }

    public string UptimeText
    {
        get => _uptimeText;
        set { _uptimeText = value; OnPropertyChanged(); }
    }

    public string LastConnectionTime
    {
        get => _lastConnectionTime;
        set { _lastConnectionTime = value; OnPropertyChanged(); }
    }

    public double UpdateRate
    {
        get => _updateRate;
        set { _updateRate = value; OnPropertyChanged(); }
    }

    #endregion

    #region Event Handlers

    private void OnTelemetryStatusChanged(object? sender, ConnectionStatusEventArgs e)
    {
        _connectionStatus = e.Status;
        
        switch (e.Status)
        {
            case ConnectionStatus.Connected:
                ConnectionStatusIcon = "🟢";
                ConnectionStatusText = "Connected to iRacing";
                LastConnectionTime = DateTime.Now.ToString("HH:mm:ss");
                SessionInfo = "Session active - Data streaming";
                UpdateRate = 60.0;
                break;
            
            case ConnectionStatus.Connecting:
                ConnectionStatusIcon = "🟡";
                ConnectionStatusText = "Connecting to iRacing...";
                SessionInfo = "Waiting for session data...";
                UpdateRate = 0.0;
                break;
            
            case ConnectionStatus.Disconnected:
                ConnectionStatusIcon = "🔴";
                ConnectionStatusText = "iRacing Not Running";
                SessionInfo = "No active session";
                UpdateRate = 0.0;
                break;
        }
    }

    private void OnWidgetCreated(object? sender, Core.WidgetBase e)
    {
        UpdateWidgetInfo();
    }

    private void OnWidgetRemoved(object? sender, Guid e)
    {
        UpdateWidgetInfo();
    }

    private void UpdateWidgetInfo()
    {
        ActiveWidgetCount = _widgetManager.GetWidgetCount();
        
        if (ActiveWidgetCount == 0)
        {
            ActiveWidgetsList = "No widgets active";
        }
        else
        {
            var widgetTypes = _widgetManager.ActiveWidgets.Values
                .Select(w => w.GetType().Name.Replace("Widget", ""))
                .Distinct()
                .OrderBy(name => name);
            
            ActiveWidgetsList = string.Join(", ", widgetTypes);
        }
    }

    private void UpdateUptime(object? sender, EventArgs e)
    {
        var uptime = DateTime.Now - _startTime;
        UptimeText = uptime.ToString(@"hh\:mm\:ss");
    }

    #endregion

    #region INotifyPropertyChanged

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    public void Cleanup()
    {
        _uptimeTimer.Stop();
        _telemetryService.StatusChanged -= OnTelemetryStatusChanged;
        _widgetManager.WidgetCreated -= OnWidgetCreated;
        _widgetManager.WidgetRemoved -= OnWidgetRemoved;
    }
}
