using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace iRacingOverlay.WPF.Controls;

/// <summary>
/// Interactive control for capturing hotkey combinations
/// </summary>
public partial class HotkeyCapture : UserControl
{
    private bool _isCapturing = false;

    public static readonly DependencyProperty ModifierKeyProperty =
        DependencyProperty.Register(nameof(ModifierKey), typeof(string), typeof(HotkeyCapture),
            new PropertyMetadata("Ctrl", OnHotkeyChanged));

    public static readonly DependencyProperty MainKeyProperty =
        DependencyProperty.Register(nameof(MainKey), typeof(string), typeof(HotkeyCapture),
            new PropertyMetadata("W", OnHotkeyChanged));

    /// <summary>
    /// Modifier key (Ctrl, Alt, Shift, or None)
    /// </summary>
    public string ModifierKey
    {
        get => (string)GetValue(ModifierKeyProperty);
        set => SetValue(ModifierKeyProperty, value);
    }

    /// <summary>
    /// Main key (e.g., W, F12, Escape)
    /// </summary>
    public string MainKey
    {
        get => (string)GetValue(MainKeyProperty);
        set => SetValue(MainKeyProperty, value);
    }

    public event EventHandler? HotkeyChanged;

    public HotkeyCapture()
    {
        InitializeComponent();

        MouseDown += OnMouseDown;
        KeyDown += OnKeyDown;
        LostFocus += OnLostFocus;

        UpdateDisplayText();
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Enter capture mode when clicked
        _isCapturing = true;
        DisplayText.Visibility = Visibility.Collapsed;
        CaptureHintText.Visibility = Visibility.Visible;
        Focus();
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!_isCapturing)
            return;

        e.Handled = true;

        // Get the actual key (not modifier)
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier-only presses
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        // Determine modifier
        string modifier = "None";
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            modifier = "Ctrl";
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            modifier = "Alt";
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            modifier = "Shift";

        // Update properties
        ModifierKey = modifier;
        MainKey = key.ToString();

        // Exit capture mode
        ExitCaptureMode();

        // Notify listeners
        HotkeyChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnLostFocus(object sender, RoutedEventArgs e)
    {
        ExitCaptureMode();
    }

    private void ExitCaptureMode()
    {
        _isCapturing = false;
        DisplayText.Visibility = Visibility.Visible;
        CaptureHintText.Visibility = Visibility.Collapsed;
        UpdateDisplayText();
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyCapture control)
        {
            control.UpdateDisplayText();
        }
    }

    private void UpdateDisplayText()
    {
        if (ModifierKey == "None")
        {
            DisplayText.Text = MainKey;
        }
        else
        {
            DisplayText.Text = $"{ModifierKey} + {MainKey}";
        }
    }
}
