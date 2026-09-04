using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using iRacingOverlay.WPF.Models;

namespace iRacingOverlay.WPF.Controls;

/// <summary>
/// Builds the settings UI from <see cref="WidgetSetting"/> declarations.
///
/// One renderer for every widget means the controls, spacing, grouping and
/// disclosure behaviour are identical everywhere by construction rather than by
/// discipline — and a search box can filter across all of them, which was not
/// possible when each panel was hand-written XAML.
/// </summary>
public sealed class SettingsRenderer
{
    /// <summary>Raised after any setting changes, so the caller can persist.</summary>
    public event Action? Changed;

    /// <summary>Suppresses change events while controls are being populated.</summary>
    private bool _loading;

    /// <summary>
    /// Render <paramref name="settings"/> into <paramref name="host"/>.
    /// </summary>
    /// <param name="filter">Optional search text; only matching settings are shown.</param>
    /// <param name="isExpanded">Reads whether an advanced group is open.</param>
    /// <param name="setExpanded">Persists whether an advanced group is open.</param>
    public void Render(
        Panel host,
        IEnumerable<WidgetSetting> settings,
        string? filter,
        Func<string, bool> isExpanded,
        Action<string, bool> setExpanded)
    {
        host.Children.Clear();

        var all = settings.ToList();
        bool searching = !string.IsNullOrWhiteSpace(filter);

        if (searching)
        {
            var needle = filter!.Trim().ToLowerInvariant();
            all = all.Where(s => s.Kind != SettingKind.Note && s.SearchText.Contains(needle)).ToList();

            if (all.Count == 0)
            {
                host.Children.Add(new TextBlock
                {
                    Text = $"Nothing matches “{filter.Trim()}”.",
                    Style = (Style)host.FindResource("T.Caption"),
                    Margin = new Thickness(0, 8, 0, 0),
                });
                return;
            }
        }

        _loading = true;
        try
        {
            // Preserve declaration order of groups rather than sorting them —
            // the widget author put the important ones first.
            foreach (var group in all.GroupBy(s => s.Group))
            {
                // While searching, flatten: hiding a match behind a collapsed
                // disclosure would make the search look broken.
                bool flatten = searching;

                var basic = group.Where(s => s.Tier == SettingTier.Basic).ToList();
                var advanced = group.Where(s => s.Tier == SettingTier.Advanced).ToList();

                host.Children.Add(BuildCard(group.Key, basic, advanced, flatten, isExpanded, setExpanded));
            }
        }
        finally
        {
            _loading = false;
        }
    }

    // ── Card ──────────────────────────────────────────────────────────

    private Border BuildCard(
        string groupName,
        List<WidgetSetting> basic,
        List<WidgetSetting> advanced,
        bool flatten,
        Func<string, bool> isExpanded,
        Action<string, bool> setExpanded)
    {
        var stack = new StackPanel();

        stack.Children.Add(new TextBlock
        {
            Text = groupName.ToUpperInvariant(),
            Style = (Style)Application.Current.MainWindow!.FindResource("T.Label"),
            Margin = new Thickness(0, 0, 0, 10),
        });

        foreach (var setting in basic)
            stack.Children.Add(BuildRow(setting));

        if (advanced.Count > 0)
        {
            if (flatten)
            {
                foreach (var setting in advanced)
                    stack.Children.Add(BuildRow(setting));
            }
            else
            {
                var inner = new StackPanel();
                foreach (var setting in advanced)
                    inner.Children.Add(BuildRow(setting));

                var key = $"grp:{groupName}";
                var expander = new Expander
                {
                    Style = (Style)Application.Current.MainWindow!.FindResource("MRT.Expander"),
                    Header = new TextBlock
                    {
                        Text = $"More options ({advanced.Count})",
                        Style = (Style)Application.Current.MainWindow!.FindResource("MRT.Expander.Header"),
                    },
                    Content = inner,
                    IsExpanded = isExpanded(key),
                    Margin = new Thickness(0, 4, 0, 0),
                };
                expander.Expanded += (_, _) => setExpanded(key, true);
                expander.Collapsed += (_, _) => setExpanded(key, false);
                stack.Children.Add(expander);
            }
        }

        return new Border
        {
            Style = (Style)Application.Current.MainWindow!.FindResource("Card"),
            Margin = new Thickness(0, 0, 0, 10),
            Child = stack,
        };
    }

    // ── Rows ──────────────────────────────────────────────────────────

    private FrameworkElement BuildRow(WidgetSetting s) => s.Kind switch
    {
        SettingKind.Toggle => BuildToggle(s),
        SettingKind.Slider => BuildSlider(s),
        SettingKind.Choice => BuildChoice(s),
        SettingKind.Action => BuildAction(s),
        _ => BuildNote(s),
    };

    private FrameworkElement BuildToggle(WidgetSetting s)
    {
        var box = new CheckBox
        {
            Content = s.Label,
            Style = (Style)Application.Current.MainWindow!.FindResource("Switch"),
            IsChecked = s.GetBool?.Invoke() ?? false,
            Margin = new Thickness(0, 0, 0, 9),
            ToolTip = s.Description,
        };

        void Apply(object _, RoutedEventArgs __)
        {
            if (_loading) return;
            s.SetBool?.Invoke(box.IsChecked == true);
            Changed?.Invoke();
        }

        box.Checked += Apply;
        box.Unchecked += Apply;
        return box;
    }

    private FrameworkElement BuildSlider(WidgetSetting s)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = s.Label,
            Style = (Style)Application.Current.MainWindow!.FindResource("T.Body"),
            ToolTip = s.Description,
        };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        double current = s.GetValue?.Invoke() ?? 0;

        var readout = new TextBlock
        {
            Text = Format(s, current),
            Style = (Style)Application.Current.MainWindow!.FindResource("T.Data"),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 46,
            TextAlignment = TextAlignment.Right,
        };
        Grid.SetRow(readout, 0);
        Grid.SetColumn(readout, 1);
        grid.Children.Add(readout);

        var slider = new Slider
        {
            Style = (Style)Application.Current.MainWindow!.FindResource("Slider.Mrt"),
            Minimum = s.Min,
            Maximum = s.Max,
            Value = Math.Clamp(current, s.Min, s.Max),
            TickFrequency = s.Step,
            IsSnapToTickEnabled = s.Step > 0,
            Margin = new Thickness(0, 6, 0, 0),
            ToolTip = s.Description,
        };
        Grid.SetRow(slider, 1);
        Grid.SetColumnSpan(slider, 2);
        grid.Children.Add(slider);

        slider.ValueChanged += (_, e) =>
        {
            readout.Text = Format(s, e.NewValue);
            if (_loading) return;
            s.SetValue?.Invoke(e.NewValue);
            Changed?.Invoke();
        };

        return grid;
    }

    private static string Format(WidgetSetting s, double value) =>
        s.FormatValue?.Invoke(value) ?? ((int)value).ToString();

    private FrameworkElement BuildChoice(WidgetSetting s)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

        stack.Children.Add(new TextBlock
        {
            Text = s.Label,
            Style = (Style)Application.Current.MainWindow!.FindResource("T.Body"),
            Margin = new Thickness(0, 0, 0, 6),
            ToolTip = s.Description,
        });

        var combo = new ComboBox { ToolTip = s.Description };
        foreach (var choice in s.Choices ?? Array.Empty<string>())
            combo.Items.Add(new ComboBoxItem { Content = choice });

        int index = s.GetChoice?.Invoke() ?? 0;
        combo.SelectedIndex = Math.Clamp(index, 0, Math.Max(0, combo.Items.Count - 1));

        combo.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            if (combo.SelectedIndex < 0) return;
            s.SetChoice?.Invoke(combo.SelectedIndex);
            Changed?.Invoke();
        };

        stack.Children.Add(combo);
        return stack;
    }

    private FrameworkElement BuildAction(WidgetSetting s)
    {
        var button = new Button
        {
            Content = s.Label,
            Style = (Style)Application.Current.MainWindow!.FindResource(
                s.IsDestructive ? "Btn.Danger" : "Btn.Secondary"),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 10),
            ToolTip = s.Description,
        };

        button.Click += (_, _) =>
        {
            if (s.IsDestructive)
            {
                var confirm = MessageBox.Show(
                    $"{s.Label}?\n\n{s.Description}",
                    s.Label, MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.OK) return;
            }

            s.Invoke?.Invoke();
            Changed?.Invoke();
        };

        return button;
    }

    private static FrameworkElement BuildNote(WidgetSetting s) => new TextBlock
    {
        Text = s.Label,
        Style = (Style)Application.Current.MainWindow!.FindResource("T.Caption"),
        Margin = new Thickness(0, 0, 0, 10),
    };
}
