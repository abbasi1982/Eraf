using System.Runtime.InteropServices;

namespace Eraf.Windows;

/// <summary>
/// One system-wide keyboard shortcut (Ctrl+Shift+F9) that works even when the app is in the background. It listens for
/// WM_HOTKEY on the app window and raises <see cref="Pressed"/> on the UI thread.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0x45AF; // arbitrary, only needs to be unique inside this window
    private const uint WM_HOTKEY = 0x0312;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_NOREPEAT = 0x4000;
    private const uint VK_F9 = 0x78;
    private static readonly UIntPtr SubclassId = (UIntPtr)0x45AF;

    private delegate IntPtr SubclassProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data);

    private readonly SubclassProc _proc; // kept in a field so the garbage collector never frees it while Windows calls it
    private IntPtr _window;
    private bool _registered;

    public GlobalHotkey() => _proc = Callback;

    public event Action? Pressed;

    public bool IsRegistered => _registered;

    /// <summary>Registers the shortcut. False (with a reason) when another app already owns it.</summary>
    public bool TryRegister(IntPtr window, out string? problem)
    {
        problem = null;
        if (_registered)
        {
            return true;
        }

        if (!RegisterHotKey(window, HotkeyId, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_F9))
        {
            problem = $"The shortcut {ScreenshotRules.HotkeyLabel} is already used by another program, so it could not be set up.";
            return false;
        }

        _window = window;
        SetWindowSubclass(window, _proc, SubclassId, UIntPtr.Zero);
        _registered = true;
        return true;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        UnregisterHotKey(_window, HotkeyId);
        RemoveWindowSubclass(_window, _proc, SubclassId);
        _registered = false;
    }

    public void Dispose() => Unregister();

    private IntPtr Callback(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        if (message == WM_HOTKEY && (int)wParam == HotkeyId)
        {
            Pressed?.Invoke();
            return IntPtr.Zero;
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(IntPtr window, SubclassProc proc, UIntPtr id, UIntPtr data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(IntPtr window, SubclassProc proc, UIntPtr id);
    [DllImport("comctl32.dll")] private static extern IntPtr DefSubclassProc(IntPtr window, uint message, UIntPtr wParam, IntPtr lParam);
}
