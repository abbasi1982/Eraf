using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Eraf.Windows;

/// <summary>
/// Pictures from the screen and the clipboard become the photo of a new ERAF: a region (Windows snipping), the whole screen, one
/// window, a paste, a "make an ERAF from this?" offer after any copied picture, and a shortcut that works from any program.
/// After that it is an ordinary photo: record or attach a voice note, trim, save, export.
/// </summary>
public sealed partial class MainPage
{
    private bool _awaitingSnip;
    private readonly DispatcherTimer _snipTimer = new() { Interval = TimeSpan.FromSeconds(45) };
    private bool _hotkeyProblemShown;
    private DateTime _ignoreClipboardUntil = DateTime.MinValue;

    private MainWindow? Shell => App.MainWindow as MainWindow;

    private bool CanStartPhoto =>
        _workflow.CanApply(SessionEvent.PhotoReady) && !_importBusy && !_showingSettings && !_dialogOpen;

    private void SetUpScreenshots()
    {
        _snipTimer.Tick += OnSnipTimeout;
        Clipboard.ContentChanged += OnClipboardChanged;
        Unloaded += (_, _) => Clipboard.ContentChanged -= OnClipboardChanged;
    }

    private bool _hotkeyHooked;

    /// <summary>
    /// Connects the system-wide shortcut. The page is built while the window is still being built, so this runs once the page is
    /// loaded, when the window can be reached.
    /// </summary>
    private void HookHotkey()
    {
        if (Shell is { } shell && !_hotkeyHooked)
        {
            _hotkeyHooked = true;
            shell.ScreenshotHotkeyPressed += () => _ = StartRegionScreenshotAsync(fromHotkey: true);
        }

        ApplyHotkeyOption(AppSettings.Options.GlobalHotkey);
    }

    // ---------------------------------------------------------------- ribbon

    private async void OnScreenshotRegionClick(object sender, RoutedEventArgs e) => await StartRegionScreenshotAsync(fromHotkey: false);

    private async void OnScreenshotScreenClick(object sender, RoutedEventArgs e) => await CaptureScreenAsync();

    private async void OnScreenshotWindowClick(object sender, RoutedEventArgs e) => await CaptureWindowAsync();

    private async void OnPasteClick(object sender, RoutedEventArgs e) => await PasteAsync();

    // ---------------------------------------------------------------- starting a session from a picture

    private async Task StartFromPictureAsync(byte[]? jpeg, ImageOrigin origin, string name)
    {
        if (jpeg is null)
        {
            ShowError("That picture could not be read.");
            return;
        }

        _sessionFolder = _history.Current;
        ClearMessage(); // an offer such as "a picture was just copied" has been taken up
        await ShowPhotoAsync(new ImageSession(jpeg, origin, name));
    }

    // ---------------------------------------------------------------- region (Windows snipping)

    /// <summary>
    /// Hides the app, starts the Windows snipping overlay and waits for the picture it puts on the clipboard (45 seconds at most).
    /// From the shortcut the app comes to the front when the picture arrives.
    /// </summary>
    private async Task StartRegionScreenshotAsync(bool fromHotkey)
    {
        MainWindow? shell = Shell;
        if (shell is null || _awaitingSnip)
        {
            return;
        }

        if (!CanStartPhoto)
        {
            if (fromHotkey)
            {
                shell.BringToFront();
                ShowInfo("Close the photo you are working on (Back to files) before taking a new screenshot.");
            }

            return;
        }

        ClearMessage();
        _awaitingSnip = true;
        shell.MinimizeWindow();
        bool started;
        try
        {
            started = await Launcher.LaunchUriAsync(new Uri("ms-screenclip:"));
        }
        catch (Exception)
        {
            started = false;
        }

        if (!started)
        {
            _awaitingSnip = false;
            shell.BringToFront();
            ShowError("Windows snipping could not be started. Use Whole screen, or press Win+Shift+S and then Ctrl+V here.");
            return;
        }

        _snipTimer.Start();
    }

    private void OnSnipTimeout(object? sender, object e)
    {
        _snipTimer.Stop();
        if (_awaitingSnip)
        {
            _awaitingSnip = false;
            Shell?.BringToFront();
            ShowInfo("No screenshot was taken.");
        }
    }

    private async Task FinishSnipAsync()
    {
        _snipTimer.Stop();
        _awaitingSnip = false;
        // Windows changes the clipboard several times for one snip; the extra changes are not new pictures.
        _ignoreClipboardUntil = DateTime.UtcNow.AddSeconds(5);
        byte[]? jpeg = null;
        for (int attempt = 0; attempt < 4 && jpeg is null; attempt++)
        {
            jpeg = await ScreenshotService.ClipboardPictureAsync();
            if (jpeg is null)
            {
                await Task.Delay(200); // the clipboard can still be filling in
            }
        }

        Shell?.BringToFront();
        if (jpeg is null)
        {
            ShowError("The screenshot could not be read from the clipboard.");
            return;
        }

        await StartFromPictureAsync(jpeg, ImageOrigin.Screenshot, ScreenshotRules.ScreenshotName(DateTime.Now));
    }

    // ---------------------------------------------------------------- whole screen and one window

    private async Task CaptureScreenAsync()
    {
        MainWindow? shell = Shell;
        if (shell is null || !CanStartPhoto)
        {
            return;
        }

        ClearMessage();
        SetBusy(true);
        try
        {
            shell.MinimizeWindow();
            await Task.Delay(600); // let the window finish disappearing so it is not in the picture
            CapturedFrame? frame = await Task.Run(ScreenCapture.CaptureVirtualScreen);
            shell.BringToFront();
            byte[]? jpeg = frame is null ? null : await ScreenshotService.EncodeAsync(frame);
            if (jpeg is null)
            {
                ShowError("The screen could not be captured.");
                return;
            }

            await StartFromPictureAsync(jpeg, ImageOrigin.Screenshot, ScreenshotRules.ScreenshotName(DateTime.Now));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task CaptureWindowAsync()
    {
        MainWindow? shell = Shell;
        if (shell is null || !CanStartPhoto)
        {
            return;
        }

        ClearMessage();
        List<CapturableWindow> windows = await Task.Run(() => ScreenCapture.ListWindows(shell.Handle));
        if (windows.Count == 0)
        {
            ShowInfo("There is no other window to capture.");
            return;
        }

        CapturableWindow? chosen;
        _dialogOpen = true;
        try
        {
            chosen = await BatchDialogs.PickWindowAsync(XamlRoot, windows);
        }
        finally
        {
            _dialogOpen = false;
        }

        if (chosen is null)
        {
            return;
        }

        SetBusy(true);
        try
        {
            CapturedFrame? frame = await Task.Run(() => ScreenCapture.CaptureWindow(chosen.Handle));
            byte[]? jpeg = frame is null ? null : await ScreenshotService.EncodeAsync(frame);
            if (jpeg is null)
            {
                ShowError("That window could not be captured. It may have been closed or minimized.");
                return;
            }

            await StartFromPictureAsync(jpeg, ImageOrigin.Screenshot, ScreenshotRules.ScreenshotName(DateTime.Now));
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---------------------------------------------------------------- clipboard

    /// <summary>Ctrl+V and the Paste button: files copied in Explorer are treated like a drop, a picture becomes the photo.</summary>
    private async Task PasteAsync()
    {
        if (!CanStartPhoto)
        {
            return;
        }

        ClearMessage();
        IReadOnlyList<string> files = await ScreenshotService.ClipboardFilesAsync();
        if (files.Count > 0)
        {
            await HandleDropAsync(files);
            return;
        }

        SetBusy(true);
        try
        {
            byte[]? jpeg = await ScreenshotService.ClipboardPictureAsync();
            if (jpeg is null)
            {
                ShowInfo("There is no picture on the clipboard. Copy a picture or take a screenshot (Win+Shift+S) first.");
                return;
            }

            _ignoreClipboardUntil = DateTime.UtcNow.AddSeconds(2);
            await StartFromPictureAsync(jpeg, ImageOrigin.Clipboard, ScreenshotRules.ClipboardName(DateTime.Now));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnClipboardChanged(object? sender, object e) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_awaitingSnip)
        {
            _ = FinishSnipAsync();
            return;
        }

        if (DateTime.UtcNow < _ignoreClipboardUntil)
        {
            return;
        }

        bool atFileList = _workflow.State == SessionState.Idle;
        if (!ScreenshotRules.CanOffer(AppSettings.Options.OfferScreenshots, atFileList, _importBusy, _showingSettings || _dialogOpen)
            || !ScreenshotService.ClipboardHasPicture())
        {
            return;
        }

        // Only a hint with a button: the picture is not read until the user agrees.
        ShowMessageWithAction("A picture was just copied (for example a screenshot).", InfoBarSeverity.Informational, "Make ERAF", () => _ = PasteAsync());
    });

    // ---------------------------------------------------------------- shortcut problems

    /// <summary>Applies the "global shortcut" option and says so once if another program already owns the shortcut.</summary>
    private void ApplyHotkeyOption(bool enabled)
    {
        MainWindow? shell = Shell;
        if (shell is null)
        {
            return;
        }

        shell.ApplyHotkey(enabled);
        if (enabled && shell.HotkeyProblem is { } problem)
        {
            if (!_hotkeyProblemShown)
            {
                _hotkeyProblemShown = true;
                ShowMessage(problem, InfoBarSeverity.Warning);
            }
        }
        else
        {
            _hotkeyProblemShown = false;
        }
    }
}
