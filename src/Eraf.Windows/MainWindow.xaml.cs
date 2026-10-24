using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace Eraf.Windows;

/// <summary>
/// The application window. It hosts a Frame that shows <see cref="MainPage"/> and guards closing:
/// the window never closes mid-save, and asks before closing with a photo or voice note that is not on disk.
/// </summary>
public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;

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

        RootFrame.Navigate(typeof(MainPage));
    }

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
