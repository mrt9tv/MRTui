using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;

namespace iRacingOverlay.WPF.Utils;

/// <summary>
/// Manages global (system-wide) hotkey registration using Windows API
/// Allows hotkeys to work even when application is not in focus
/// </summary>
public class GlobalHotkey : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private readonly Window _window;
    private readonly int _hotkeyId;
    private readonly ILogger<GlobalHotkey>? _logger;
    private HwndSource? _source;
    private bool _isRegistered = false;

    public event EventHandler? HotkeyPressed;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>
    /// Modifier key flags for Windows API
    /// </summary>
    [Flags]
    public enum ModifierKeys : uint
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Win = 8
    }

    public GlobalHotkey(Window window, int hotkeyId = 1, ILogger<GlobalHotkey>? logger = null)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _hotkeyId = hotkeyId;
        _logger = logger;

        // Set up the WndProc hook immediately if window is already loaded
        if (_window.IsLoaded)
        {
            SetupHook();
        }
        else
        {
            // Otherwise wait for window to load
            _window.Loaded += Window_Loaded;
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        SetupHook();
        _window.Loaded -= Window_Loaded;
    }

    private void SetupHook()
    {
        // Get the window handle
        var helper = new WindowInteropHelper(_window);
        _source = HwndSource.FromHwnd(helper.Handle);

        if (_source != null)
        {
            _source.AddHook(WndProc);
            _logger?.LogDebug("WndProc hook installed for hotkey ID {HotkeyId}", _hotkeyId);
        }
        else
        {
            _logger?.LogError("Failed to get HwndSource for hotkey ID {HotkeyId}", _hotkeyId);
        }
    }

    /// <summary>
    /// Window message processor - intercepts WM_HOTKEY messages
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == _hotkeyId)
        {
            _logger?.LogDebug("WM_HOTKEY received for ID {HotkeyId}", _hotkeyId);
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Register a global hotkey
    /// </summary>
    /// <param name="modifierKey">Modifier key (Ctrl, Alt, Shift, None)</param>
    /// <param name="key">Main key</param>
    /// <returns>True if registration succeeded</returns>
    public bool Register(string modifierKey, string key)
    {
        // Unregister existing hotkey first
        Unregister();

        // Get window handle
        var helper = new WindowInteropHelper(_window);
        IntPtr hwnd = helper.Handle;

        if (hwnd == IntPtr.Zero)
        {
            _logger?.LogWarning("Window handle is null, cannot register hotkey");
            return false;
        }

        // Convert modifier string to Windows API flags
        ModifierKeys modifier = modifierKey switch
        {
            "Ctrl" => ModifierKeys.Control,
            "Alt" => ModifierKeys.Alt,
            "Shift" => ModifierKeys.Shift,
            "None" => ModifierKeys.None,
            _ => ModifierKeys.None
        };

        // Convert key string to virtual key code
        uint vkCode = GetVirtualKeyCode(key);
        if (vkCode == 0)
        {
            _logger?.LogWarning("Invalid key: {Key}", key);
            return false;
        }

        // Register the hotkey
        _isRegistered = RegisterHotKey(hwnd, _hotkeyId, (uint)modifier, vkCode);

        if (_isRegistered)
        {
            _logger?.LogInformation("Registered hotkey: {Modifier} + {Key} (VK: 0x{VkCode:X})", modifierKey, key, vkCode);
        }
        else
        {
            _logger?.LogWarning("Failed to register hotkey: {Modifier} + {Key}", modifierKey, key);
        }

        return _isRegistered;
    }

    /// <summary>
    /// Unregister the currently registered hotkey
    /// </summary>
    public void Unregister()
    {
        if (!_isRegistered)
            return;

        var helper = new WindowInteropHelper(_window);
        IntPtr hwnd = helper.Handle;

        if (hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(hwnd, _hotkeyId);
            _logger?.LogDebug("Hotkey unregistered");
        }

        _isRegistered = false;
    }

    /// <summary>
    /// Convert WPF Key enum to Windows virtual key code
    /// </summary>
    private uint GetVirtualKeyCode(string keyString)
    {
        // Try to parse as WPF Key enum
        if (!Enum.TryParse<Key>(keyString, true, out var key))
        {
            return 0;
        }

        // Convert WPF Key to virtual key code
        return (uint)KeyInterop.VirtualKeyFromKey(key);
    }

    public void Dispose()
    {
        Unregister();

        if (_source != null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        if (_window != null)
        {
            _window.Loaded -= Window_Loaded;
        }
    }
}
