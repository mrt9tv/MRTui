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

    // ── Rows ──────────────────────────────────────────────────────────
    //
    // Every setting is a row: label and description on the left, the control on
    // the right. The description used to live in a tooltip, which on a page of
    // forty switches meant nobody read it. A row also gives the pointer a whole
    // strip to hit rather than a 42 px switch.

    private static Style Res(string key) => (Style)Application.Current.MainWindow!.FindResource(key);
    private static Brush BrushRes(string key) => (Brush)Application.Current.MainWindow!.FindResource(key);

    /// <summary>
    /// A hoverable row with the label block on the left and <paramref name="control"/>
    /// on the right. Returns the row; the caller decides what a click on it does.
    /// </summary>
    private static Border Row(WidgetSetting s, FrameworkElement control, double controlMinWidth = 0)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        text.Children.Add(new TextBlock { Text = s.Label, Style = Res("T.Body"), TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrWhiteSpace(s.Description))
        {
            text.Children.Add(new TextBlock
            {
                Text = s.Description,
                Style = Res("T.Caption"),
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        grid.Children.Add(text);

        control.VerticalAlignment = VerticalAlignment.Center;
        if (controlMinWidth > 0) control.MinWidth = controlMinWidth;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        var row = new Border
        {
            Child = grid,
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(-10, 0, -10, 2),
            CornerRadius = (CornerRadius)Application.Current.MainWindow!.FindResource("CR.Control"),
            Background = Brushes.Transparent,
        };
        row.MouseEnter += (_, _) => row.Background = BrushRes("SurfaceHover");
        row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
        return row;
    }

    private FrameworkElement BuildToggle(WidgetSetting s)
    {
        var box = new CheckBox
        {
            Style = Res("Switch"),
            IsChecked = s.GetBool?.Invoke() ?? false,
        };

        void Apply(object _, RoutedEventArgs __)
        {
            if (_loading) return;
            s.SetBool?.Invoke(box.IsChecked == true);
            Changed?.Invoke();
        }

        box.Checked += Apply;
        box.Unchecked += Apply;

        var row = Row(s, box);
        row.Cursor = System.Windows.Input.Cursors.Hand;

        // The whole row toggles. The switch handles its own clicks; only a click
        // that reached the row unhandled flips it from here.
        row.MouseLeftButtonUp += (_, e) =>
        {
            if (e.Handled) return;
            box.IsChecked = box.IsChecked != true;
            e.Handled = true;
        };

        return row;
    }

    private FrameworkElement BuildSlider(WidgetSetting s)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock { Text = s.Label, Style = Res("T.Body") };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        double current = s.GetValue?.Invoke() ?? 0;

        var readout = new TextBlock
        {
            Text = Format(s, current),
            Style = Res("T.Data"),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 46,
            TextAlignment = TextAlignment.Right,
        };
        Grid.SetRow(readout, 0);
        Grid.SetColumn(readout, 1);
        grid.Children.Add(readout);

        int sliderRow = 1;
        if (!string.IsNullOrWhiteSpace(s.Description))
        {
            var desc = new TextBlock
            {
                Text = s.Description,
                Style = Res("T.Caption"),
                Margin = new Thickness(0, 2, 0, 0),
            };
            Grid.SetRow(desc, 1);
            Grid.SetColumnSpan(desc, 2);
            grid.Children.Add(desc);
            sliderRow = 2;
        }

        var slider = new Slider
        {
            Style = Res("Slider.Mrt"),
            Minimum = s.Min,
            Maximum = s.Max,
            Value = Math.Clamp(current, s.Min, s.Max),
            TickFrequency = s.Step,
            IsSnapToTickEnabled = s.Step > 0,
            Margin = new Thickness(0, 8, 0, 0),
        };
        Grid.SetRow(slider, sliderRow);
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
        var combo = new ComboBox();
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

        return Row(s, combo, controlMinWidth: 170);
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

        if (string.IsNullOrWhiteSpace(s.Description)) return button;

        // Action with a description: the caption sits beside the button rather
        // than hiding in a tooltip, like every other row.
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        button.Margin = new Thickness(0);
        button.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(button);
        var caption = new TextBlock
        {
            Text = s.Description,
            Style = Res("T.Caption"),
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(caption, 1);
        grid.Children.Add(caption);
        return grid;
    }

    private static FrameworkElement BuildNote(WidgetSetting s) => new TextBlock
    {
        Text = s.Label,
        Style = (Style)Application.Current.MainWindow!.FindResource("T.Caption"),
        Margin = new Thickness(0, 0, 0, 10),
    };
}
