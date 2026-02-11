using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.Core.Models;
using iRacingOverlay.Core.Services;
using iRacingOverlay.WPF.Models;
using iRacingOverlay.WPF.Services;

namespace iRacingOverlay.WPF.Pages;

public partial class SessionsPage : UserControl
{
    private readonly SessionConfigService _sessionConfig;
    private bool _suppressControlEvents = true;

    public SessionsPage(SessionConfigService sessionConfig)
    {
        InitializeComponent();
        _sessionConfig = sessionConfig;
        _sessionConfig.SessionCategoryChanged += OnSessionChanged;

        SyncSessionPanel();
        _suppressControlEvents = false;
    }

    // ── Session changed ─────────────────────────────────────────────

    private void OnSessionChanged(object? sender, SessionCategory category)
    {
        Dispatcher.Invoke(UpdateSessionIndicator);
    }

    private void UpdateSessionIndicator()
    {
        var cat = _sessionConfig.CurrentCategory;
        TxtSessionIndicator.Text = cat == SessionCategory.Unknown ? "—" : cat.ToString();
        TxtSessionIndicator.Foreground = cat switch
        {
            SessionCategory.Practice => new SolidColorBrush(Color.FromRgb(0, 188, 212)),
            SessionCategory.Qualifying => new SolidColorBrush(Color.FromRgb(171, 71, 188)),
            SessionCategory.Race => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
            SessionCategory.Warmup => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
            _ => new SolidColorBrush(Color.FromRgb(136, 136, 136)),
        };
    }

    // ── Auto-detect toggle ──────────────────────────────────────────

    private void ChkAutoDetect_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        _sessionConfig.IsEnabled = ChkAutoDetectSession.IsChecked == true;
        _sessionConfig.Save();
    }

    // ── Preset toggles ──────────────────────────────────────────────

    private void SessionPresetToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents) return;
        if (sender is not CheckBox chk) return;
        if (chk.Tag is not string tag) return;

        var parts = tag.Split(':');
        if (parts.Length != 2) return;
        if (!Enum.TryParse<SessionCategory>(parts[0], out var cat)) return;
        if (!Enum.TryParse<WidgetType>(parts[1], out var wt)) return;

        var preset = _sessionConfig.GetPreset(cat);
        if (preset == null) return;
        preset.WidgetVisibility[wt] = chk.IsChecked == true;
        _sessionConfig.Save();
    }

    // ── Full sync ───────────────────────────────────────────────────

    public void SyncSessionPanel()
    {
        _suppressControlEvents = true;
        try
        {
            ChkAutoDetectSession.IsChecked = _sessionConfig.IsEnabled;
            UpdateSessionIndicator();

            foreach (var cat in new[] { SessionCategory.Practice, SessionCategory.Qualifying,
                                         SessionCategory.Race, SessionCategory.Warmup })
            {
                var preset = _sessionConfig.GetPreset(cat);
                if (preset == null) continue;
                foreach (var (wt, visible) in preset.WidgetVisibility)
                {
                    var chkName = $"ChkSession_{cat}_{wt}";
                    if (FindName(chkName) is CheckBox chk)
                        chk.IsChecked = visible;
                }
            }
        }
        finally { _suppressControlEvents = false; }
    }

    public void Dispose()
    {
        _sessionConfig.SessionCategoryChanged -= OnSessionChanged;
    }
}
