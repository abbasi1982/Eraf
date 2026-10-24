using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Eraf.Windows;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>A file activation from Windows, or failing that a .eraf path among the command-line arguments.</summary>
    private static string? FindStartupFile()
    {
        try
        {
            Microsoft.Windows.AppLifecycle.AppActivationArguments activation =
                Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            if (activation.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.File
                && activation.Data is global::Windows.ApplicationModel.Activation.IFileActivatedEventArgs files
                && files.Files.Count > 0
                && files.Files[0].Path is { Length: > 0 } activated)
            {
                return activated;
            }
        }
        catch (Exception)
        {
            // Not started by a file: fall through to the command line.
        }

        foreach (string argument in Environment.GetCommandLineArgs().Skip(1))
        {
            string path = argument.Trim('"');
            if (path.EndsWith(FileKinds.ErafExtension, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            {
                return System.IO.Path.GetFullPath(path);
            }
        }

        return null;
    }

    /// <summary>The main window, needed by pickers to get a window handle.</summary>
    public static Window? MainWindow { get; private set; }

    /// <summary>
    /// The .eraf file the app was started with (double-click in Explorer, "Open with", or a path on the command line), or null.
    /// The page opens it once it is ready and then clears it.
    /// </summary>
    public static string? StartupFile { get; set; }
    
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupFile = FindStartupFile();
        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }
}
