namespace Eraf.Windows;

// Pure logic: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>Names and rules for photos that come from the screen or the clipboard.</summary>
public static class ScreenshotRules
{
    /// <summary>The global shortcut that starts a region screenshot from anywhere (an F-key combination, so it takes nothing typed).</summary>
    public const string HotkeyLabel = "Ctrl+Shift+F9";

    public static string ScreenshotName(DateTime now) => $"Screenshot {now:yyyy-MM-dd HHmmss}";

    public static string ClipboardName(DateTime now) => $"Clipboard {now:yyyy-MM-dd HHmmss}";

    /// <summary>
    /// Whether to offer "make an ERAF from the picture that was just copied": the option is on and the app is at the file list with
    /// nothing else going on. It never interrupts a photo being worked on, a dialog, a conversion or the Settings page.
    /// </summary>
    public static bool CanOffer(bool optionOn, bool atFileList, bool busy, bool somethingOpen) =>
        optionOn && atFileList && !busy && !somethingOpen;

    /// <summary>A window worth listing in the "capture a window" picker: it has a title and a usable size and is not minimized.</summary>
    public static bool IsCapturableWindow(string? title, int width, int height, bool minimized) =>
        !string.IsNullOrWhiteSpace(title) && width >= 50 && height >= 50 && !minimized;

    /// <summary>Whether a screenshot of this size fits the format's picture limit comfortably once saved as JPEG (a very rough guard before encoding).</summary>
    public static bool IsSaneSize(int width, int height) =>
        width > 0 && height > 0 && (long)width * height <= 16_384L * 16_384L;
}
