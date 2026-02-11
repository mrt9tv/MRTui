using System;
using System.Linq;
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
    private ProfileStorageService? _profileService;
    private WidgetManager? _widgetManager;
    private bool _suppressControlEvents = true;

    public SessionsPage(SessionConfigService sessionConfig)
    {
        InitializeComponent();
        _sessionConfig = sessionConfig;
        _sessionConfig.SessionCategoryChanged += OnSessionChanged;

        SyncSessionPanel();
        _suppressControlEvents = false;
    }

    /// <summary>
    /// Inject the profile service and widget manager after construction
    /// (avoids circular constructor dependency).
    /// </summary>
    public void SetProfileService(ProfileStorageService profileService, WidgetManager widgetManager)
    {
        _profileService = profileService;
        _widgetManager = widgetManager;
        _profileService.ActiveProfileChanged += (_, _) => Dispatcher.Invoke(SyncProfileList);
        SyncProfileList();
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

    // ── Profile UI ────────────────────────────────────────────────

    private void ChkProfileAutoSwitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressControlEvents || _profileService == null) return;
        _profileService.AutoSwitch = ChkProfileAutoSwitch.IsChecked == true;
        _profileService.Save();
    }

    private void BtnCreateProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_profileService == null) return;
        var name = $"Profile {_profileService.Profiles.Count + 1}";
        var profile = _profileService.CreateProfile(name);
        // If we have a widget manager, capture current state
        if (_widgetManager != null)
            _profileService.CaptureToProfile(profile.Id, _widgetManager);
        SyncProfileList();
    }

    private void BtnCaptureProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_profileService == null || _widgetManager == null) return;
        var active = _profileService.GetActiveProfile();
        if (active == null) return;
        _profileService.CaptureToProfile(active.Id, _widgetManager);
        SyncProfileList();
    }

    private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_profileService == null) return;
        var active = _profileService.GetActiveProfile();
        if (active == null) return;
        _profileService.DeleteProfile(active.Id);
        SyncProfileList();
    }

    private void ProfileSelect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string idStr) return;
        if (!Guid.TryParse(idStr, out var id)) return;
        if (_profileService == null || _widgetManager == null) return;

        _profileService.ApplyProfile(id, _widgetManager);
        SyncProfileList();
    }

    private void SyncProfileList()
    {
        if (_profileService == null) return;

        _suppressControlEvents = true;
        try
        {
            ChkProfileAutoSwitch.IsChecked = _profileService.AutoSwitch;

            var active = _profileService.GetActiveProfile();
            TxtActiveProfile.Text = active?.Name ?? "(none)";

            ProfileListPanel.Children.Clear();
            foreach (var p in _profileService.Profiles)
            {
                bool isActive = p.Id == _profileService.ActiveProfileId;
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };

                var selectBtn = new Button
                {
                    Content = isActive ? "● " + p.Name : "○ " + p.Name,
                    Tag = p.Id.ToString(),
                    FontSize = 11,
                    Foreground = isActive
                        ? (FindResource("TealPrimary") as SolidColorBrush ?? new SolidColorBrush(Color.FromRgb(0, 128, 128)))
                        : (FindResource("LightText") as SolidColorBrush ?? new SolidColorBrush(Colors.White)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Padding = new Thickness(2, 1, 6, 1),
                };
                selectBtn.Click += ProfileSelect_Click;
                row.Children.Add(selectBtn);

                // Binding indicators
                if (p.SessionBinding.HasValue)
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = $"[{p.SessionBinding.Value}]",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0),
                    });
                }
                if (!string.IsNullOrEmpty(p.CarClassBinding))
                {
                    row.Children.Add(new TextBlock
                    {
                        Text = $"[{p.CarClassBinding}]",
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Color.FromRgb(255, 128, 0)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0),
                    });
                }

                ProfileListPanel.Children.Add(row);
            }
        }
        finally { _suppressControlEvents = false; }
    }

    // ── Cleanup ─────────────────────────────────────────────────────

    public void Dispose()
    {
        _sessionConfig.SessionCategoryChanged -= OnSessionChanged;
    }
}
