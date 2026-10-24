using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Eraf.Windows;

/// <summary>The Settings page. Every change is applied and saved at once; nothing needs a separate Save button.</summary>
public sealed partial class SettingsPane : UserControl
{
    private bool _loading;

    public SettingsPane()
    {
        InitializeComponent();
        Show(AppSettings.Options);
        AppSettings.OptionsChanged += options => DispatcherQueue.TryEnqueue(() => Show(options));
    }

    /// <summary>The user pressed Done.</summary>
    public event Action? Closed;

    /// <summary>Puts the controls in line with the options (also used when the theme is changed from the menu).</summary>
    private void Show(AppOptions options)
    {
        _loading = true;
        try
        {
            ThemeChoices.SelectedIndex = (int)options.Theme;
            SubfoldersSwitch.IsOn = options.ScanSubfolders;
            ExistingChoices.SelectedIndex = (int)options.ExistingFiles;
            OutputChoices.SelectedIndex = (int)options.Output;
            OutputFolderBox.Text = options.OutputFolder ?? string.Empty;
            OutputFolderRow.Visibility = options.Output == OutputMode.OneFolder ? Visibility.Visible : Visibility.Collapsed;
        }
        finally
        {
            _loading = false;
        }
    }

    private void Change(Func<AppOptions, AppOptions> edit)
    {
        if (!_loading)
        {
            AppSettings.Options = edit(AppSettings.Options);
        }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e) =>
        Change(o => o with { Theme = (ThemeChoice)Math.Max(0, ThemeChoices.SelectedIndex) });

    private void OnSubfoldersToggled(object sender, RoutedEventArgs e) =>
        Change(o => o with { ScanSubfolders = SubfoldersSwitch.IsOn });

    private void OnExistingChanged(object sender, SelectionChangedEventArgs e) =>
        Change(o => o with { ExistingFiles = (ExistingFilePolicy)Math.Max(0, ExistingChoices.SelectedIndex) });

    private void OnOutputChanged(object sender, SelectionChangedEventArgs e) =>
        Change(o => o with { Output = (OutputMode)Math.Max(0, OutputChoices.SelectedIndex) });

    private async void OnChooseFolderClick(object sender, RoutedEventArgs e)
    {
        (string? path, string? error) = await FilePickers.PickFolderAsync(App.MainWindow!);
        if (path is not null)
        {
            Change(o => o with { OutputFolder = path });
        }
        else if (error is not null)
        {
            OutputFolderBox.PlaceholderText = error;
        }
    }

    private void OnClearFolderClick(object sender, RoutedEventArgs e) =>
        Change(o => o with { OutputFolder = null });

    private void OnResetClick(object sender, RoutedEventArgs e) =>
        AppSettings.Options = AppOptions.Default;

    private void OnDoneClick(object sender, RoutedEventArgs e) => Closed?.Invoke();
}
