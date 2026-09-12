using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace iRacingOverlay.WPF.Controls;

/// <summary>
/// A one-line text prompt in the app's own theme. WPF ships no input box, and
/// the only thing here that needs one is naming a profile.
/// </summary>
public sealed class PromptDialog : Window
{
    private readonly TextBox _input;

    private PromptDialog(Window? owner, string title, string label, string initial)
    {
        Owner = owner;
        Title = title;
        Width = 380;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = owner != null
            ? WindowStartupLocation.CenterOwner
            : WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Background = TryFindResource("Surface") as Brush ?? Brushes.White;

        var stack = new StackPanel { Margin = new Thickness(18) };

        stack.Children.Add(new TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 0, 0, 8),
            Foreground = TryFindResource("TextPrimary") as Brush ?? Brushes.Black,
        });

        _input = new TextBox { Text = initial, Padding = new Thickness(6, 4, 6, 4) };
        _input.SelectAll();
        _input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Accept();
            else if (e.Key == Key.Escape) DialogResult = false;
        };
        stack.Children.Add(_input);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0),
        };

        var ok = new Button { Content = "OK", MinWidth = 80, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => Accept();
        if (TryFindResource("Btn.Primary") is Style primary) ok.Style = primary;

        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        if (TryFindResource("Btn.Secondary") is Style secondary) cancel.Style = secondary;

        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        stack.Children.Add(buttons);

        Content = stack;
        Loaded += (_, _) => _input.Focus();
    }

    private void Accept()
    {
        if (string.IsNullOrWhiteSpace(_input.Text)) return;
        DialogResult = true;
    }

    /// <summary>Show the prompt; null when cancelled or left blank.</summary>
    public static string? Show(Window? owner, string title, string label, string initial = "")
    {
        var dialog = new PromptDialog(owner, title, label, initial);
        return dialog.ShowDialog() == true ? dialog._input.Text.Trim() : null;
    }
}
