using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace Eraf.Windows;

/// <summary>
/// The application window. It hosts a Frame that shows <see cref="MainPage"/> and guards closing:
/// the window never closes mid-save, and asks before closing with a photo or voice note that is not on disk.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;
    private readonly GlobalHotkey _hotkey = new();

    /// <summary>The system-wide screenshot shortcut was pressed.</summary>
    public event Action? ScreenshotHotkeyPressed;

    /// <summary>Why the shortcut could not be set up (another program owns it), or null.</summary>
    public string? HotkeyProblem { get; private set; }

    public IntPtr Handle => WinRT.Interop.WindowNative.GetWindowHandle(this);

    public bool IsMinimized => AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized };

    public void MinimizeWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }

    /// <summary>Shows the window again (restoring it if it was minimized) and takes the keyboard focus.</summary>
    public void BringToFront()
    {
        if (AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } presenter)
        {
            presenter.Restore();
        }

        AppWindow.Show();
        Activate();
        SetForegroundWindow(Handle);
    }

    public void ApplyHotkey(bool enabled)
    {
        if (!enabled)
        {
            _hotkey.Unregister();
            HotkeyProblem = null;
            return;
        }

        HotkeyProblem = _hotkey.TryRegister(Handle, out string? problem) ? null : problem;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
        AppWindow.Closing += OnAppWindowClosing;

        // Starts maximized, like a file manager. The user can still restore or resize it.
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        ApplyTheme(AppSettings.Options.Theme);
        _hotkey.Pressed += () => ScreenshotHotkeyPressed?.Invoke();
        Closed += (_, _) => _hotkey.Dispose();
        RootFrame.Navigate(typeof(MainPage));
    }

    /// <summary>
    /// Applies a theme to the whole window. "System" lets Windows decide. A colour theme picks a light or dark base, replaces the
    /// accent brushes (buttons, ticks, selection) and, where it has them, the window background and main text colour. The
    /// replacements go in the application's resources so dialogs, which live outside the window's tree, match too.
    /// </summary>
    public void ApplyTheme(ThemeChoice theme)
    {
        ThemePalette palette = ThemePalettes.For(theme);
        ResourceDictionary resources = Application.Current.Resources;

        // Windows builds every accent-coloured brush (buttons, ticks, selection) from these seven colours, so replacing them
        // recolours all of it, dialogs included. Removing them gives the user's own Windows accent back.
        void SetColour(string key, uint? rgb)
        {
            if (rgb is { } c)
            {
                resources[key] = ColorOf(c);
            }
            else
            {
                resources.Remove(key);
            }
        }

        var shades = ThemePalettes.AccentShades(palette.Accent ?? 0);
        bool tinted = palette.Accent is not null;
        SetColour("SystemAccentColor", palette.Accent);
        SetColour("SystemAccentColorLight1", tinted ? shades.Light1 : null);
        SetColour("SystemAccentColorLight2", tinted ? shades.Light2 : null);
        SetColour("SystemAccentColorLight3", tinted ? shades.Light3 : null);
        SetColour("SystemAccentColorDark1", tinted ? shades.Dark1 : null);
        SetColour("SystemAccentColorDark2", tinted ? shades.Dark2 : null);
        SetColour("SystemAccentColorDark3", tinted ? shades.Dark3 : null);
        RootGrid.Background = palette.Background is { } bg ? new SolidColorBrush(ColorOf(bg)) : null;

        ElementTheme target = theme == ThemeChoice.System ? ElementTheme.Default : palette.Dark ? ElementTheme.Dark : ElementTheme.Light;
        // Brushes already in use only look themselves up again when the theme really changes. Setting the other value and the
        // wanted one back to back is merged into "no change", so the wanted one is set on a later turn of the UI loop.
        RootGrid.RequestedTheme = target == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () => RootGrid.RequestedTheme = target);
    }

    private static global::Windows.UI.Color ColorOf(uint rgb) =>
        global::Windows.UI.Color.FromArgb(0xFF, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    /// <summary>The theme dialogs should use so they match the window.</summary>
    public ElementTheme CurrentTheme => RootGrid.RequestedTheme;

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || RootFrame.Content is not MainPage page)
        {
            return;
        }

        if (page.IsSaving)
        {
            args.Cancel = true;
            page.NotifySavingInProgress();
            return;
        }

        if (!page.HasUnsavedWork)
        {
            return;
        }

        // Hold the close, ask, and close again once the user has agreed (the flag stops a second prompt).
        args.Cancel = true;
        if (await page.ConfirmDiscardAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }
}
