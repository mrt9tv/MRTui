using System;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;
using iRacingOverlay.WPF.Utils;

namespace iRacingOverlay.WPF.ViewModels;

/// <summary>
/// ViewModel for Settings view - manages global application settings
/// </summary>
public class SettingsViewModel : INotifyPropertyChanged
{
    private readonly AppSettings _settings;
    private readonly WidgetManager _widgetManager;
    private readonly Window _mainWindow;
    
    private string _selectedTab = "General";

    public event PropertyChangedEventHandler? PropertyChanged;

    public SettingsViewModel(AppSettings settings, WidgetManager widgetManager, Window mainWindow)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _widgetManager = widgetManager ?? throw new ArgumentNullException(nameof(widgetManager));
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

        // Initialize commands
        SelectTabCommand = new RelayCommand<string>(OnSelectTab);
        ResetAllCommand = new RelayCommand(OnResetAll);

        // Subscribe to AppSettings property changes to keep UI in sync
        _settings.PropertyChanged += OnSettingsPropertyChanged;

        // Subscribe to widget visibility changes to update checkbox
        _widgetManager.WidgetVisibilityChanged += OnWidgetVisibilityChanged;

        // Apply current manager settings to window
        _mainWindow.Opacity = 1.0; // Always 100% opacity
        _mainWindow.Topmost = _settings.AlwaysOnTop;
    }

    /// <summary>
    /// Handle widget visibility changes (e.g., when hotkey toggles visibility)
    /// </summary>
    private void OnWidgetVisibilityChanged(object? sender, EventArgs e)
    {
        // Update the ShowWidgets property to reflect current state
        OnPropertyChanged(nameof(ShowWidgets));
        OnPropertyChanged(nameof(VisibilityStatusIcon));
    }

    /// <summary>
    /// Handle property changes from AppSettings (e.g., when hotkey toggles lock)
    /// </summary>
    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When LockWindows changes in AppSettings, update our UI binding
        // (Widget lock is applied directly by MainWindow hotkey handler)
        if (e.PropertyName == nameof(AppSettings.LockWindows))
        {
            OnPropertyChanged(nameof(LockWidgets));
            OnPropertyChanged(nameof(LockStatusIcon));
        }
    }

    #region Properties

    /// <summary>
    /// Currently selected settings tab
    /// </summary>
    public string SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab != value)
            {
                _selectedTab = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Use metric units (true) or imperial (false)
    /// </summary>
    public bool UseMetric
    {
        get => _settings.UseMetric;
        set
        {
            if (_settings.UseMetric != value)
            {
                _settings.UseMetric = value;
                _settings.UseMetricUnits = value; // Keep duplicate in sync
                OnPropertyChanged();
                OnPropertyChanged(nameof(UnitSystemText));
                SaveAndNotify();
            }
        }
    }

    /// <summary>
    /// Display text for current unit system
    /// </summary>
    public string UnitSystemText => UseMetric ? "Metric (km/h, L, °C)" : "Imperial (mph, gal, °F)";

    /// <summary>
    /// Lock widgets (prevents dragging, enables click-through)
    /// </summary>
    public bool LockWidgets
    {
        get => _settings.LockWindows;
        set
        {
            if (_settings.LockWindows != value)
            {
                _settings.LockWindows = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LockStatusIcon));
                
                // Apply lock state to all active widgets immediately
                _widgetManager.LockAllWidgets(value);
                
                SaveAndNotify();
            }
        }
    }

    /// <summary>
    /// Lock status icon (🔓 unlocked, 🔒 locked)
    /// </summary>
    public string LockStatusIcon => LockWidgets ? "🔒" : "🔓";

    /// <summary>
    /// Show/Hide widgets toggle
    /// </summary>
    public bool ShowWidgets
    {
        get
        {
            // Check if any widgets are visible
            return _widgetManager.ActiveWidgets.Any(w => w.Value.IsVisible);
        }
        set
        {
            if (value)
            {
                _widgetManager.ShowAllWidgets();
            }
            else
            {
                _widgetManager.HideAllWidgets();
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(VisibilityStatusIcon));
        }
    }

    /// <summary>
    /// Visibility status icon (👁️ visible, 👁️‍🗨️ hidden)
    /// </summary>
    public string VisibilityStatusIcon => ShowWidgets ? "👁️" : "🚫";

    /// <summary>
    /// Modifier key for toggle lock hotkey (Ctrl, Alt, Shift, or None)
    /// </summary>
    public string ToggleLockModifier
    {
        get => _settings.ToggleLockModifier;
        set
        {
            if (_settings.ToggleLockModifier != value)
            {
                _settings.ToggleLockModifier = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HotkeyDisplayText));
            }
        }
    }

    /// <summary>
    /// Main key for toggle lock hotkey
    /// </summary>
    public string ToggleLockKey
    {
        get => _settings.ToggleLockKey;
        set
        {
            if (_settings.ToggleLockKey != value)
            {
                _settings.ToggleLockKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HotkeyDisplayText));
            }
        }
    }

    /// <summary>
    /// Formatted hotkey display text
    /// </summary>
    public string HotkeyDisplayText
    {
        get
        {
            if (ToggleLockModifier == "None")
                return ToggleLockKey;
            return $"{ToggleLockModifier} + {ToggleLockKey}";
        }
    }

    /// <summary>
    /// Modifier key for toggle visibility hotkey (Ctrl, Alt, Shift, or None)
    /// </summary>
    public string ToggleVisibilityModifier
    {
        get => _settings.ToggleVisibilityModifier;
        set
        {
            if (_settings.ToggleVisibilityModifier != value)
            {
                _settings.ToggleVisibilityModifier = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Main key for toggle visibility hotkey
    /// </summary>
    public string ToggleVisibilityKey
    {
        get => _settings.ToggleVisibilityKey;
        set
        {
            if (_settings.ToggleVisibilityKey != value)
            {
                _settings.ToggleVisibilityKey = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Keep manager window always on top
    /// </summary>
    public bool AlwaysOnTop
    {
        get => _settings.AlwaysOnTop;
        set
        {
            if (_settings.AlwaysOnTop != value)
            {
                _settings.AlwaysOnTop = value;
                OnPropertyChanged();
                
                // Apply topmost setting immediately
                _mainWindow.Topmost = value;
                
                SaveAndNotify();
            }
        }
    }

    /// <summary>
    /// Start manager window minimized to taskbar
    /// </summary>
    public bool StartMinimized
    {
        get => _settings.StartMinimized;
        set
        {
            if (_settings.StartMinimized != value)
            {
                _settings.StartMinimized = value;
                OnPropertyChanged();
                SaveAndNotify();
            }
        }
    }

    /// <summary>
    /// Application version from centralized VersionInfo
    /// </summary>
    public string AppVersion => VersionInfo.DisplayVersion;

    /// <summary>
    /// iRacing SDK version from centralized VersionInfo
    /// </summary>
    public string IracingSdkVersion => VersionInfo.IRACING_SDK_VERSION;

    #endregion

    #region Commands

    public ICommand SelectTabCommand { get; }
    public ICommand ResetAllCommand { get; }

    private void OnSelectTab(string? tabName)
    {
        if (!string.IsNullOrEmpty(tabName))
        {
            SelectedTab = tabName;
        }
    }

    private void OnResetAll()
    {
        var result = MessageBox.Show(
            "Reset all settings to defaults?\n\nThis will:\n• Set units to Metric\n• Unlock all widgets\n• Disable Always On Top\n• Disable Start Minimized",
            "Reset All Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Reset to defaults
            UseMetric = true;
            LockWidgets = false;
            AlwaysOnTop = false;
            StartMinimized = false;

            MessageBox.Show(
                "Settings reset to defaults successfully!",
                "Reset Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Called when hotkey is changed via HotkeyCapture control
    /// </summary>
    public void OnHotkeyChanged()
    {
        SaveAndNotify();

        // Re-register global hotkeys with new bindings
        if (_mainWindow is MainWindow mainWindow)
        {
            mainWindow.ReregisterGlobalHotkeys();
        }
    }

    /// <summary>
    /// Save settings and notify listeners
    /// </summary>
    private void SaveAndNotify()
    {
        _settings.Save();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Simple relay command implementation
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Generic relay command implementation
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
}
