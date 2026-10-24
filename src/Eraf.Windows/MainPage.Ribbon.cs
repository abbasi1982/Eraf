using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Eraf.Windows;

/// <summary>The ribbon: which tab is shown, which commands are available, and the keyboard shortcuts that used to belong to the menu.</summary>
public sealed partial class MainPage
{
    private bool _ribbonWasIdle = true;

    /// <summary>
    /// Enables the editor commands that make sense right now and picks the tab that fits the step: Home while choosing files,
    /// Voice note once a photo is open. The handlers re-check the state themselves, so this only guides the user.
    /// </summary>
    private void RenderRibbon(SessionState state, bool recording, bool busy)
    {
        bool inEditor = state != SessionState.Idle && state != SessionState.CapturingPhoto && !_showingSettings;
        bool free = inEditor && !recording && !busy;
        bool hasAudio = _workflow.HasAudio;
        bool playing = state == SessionState.AudioPreview;

        RecordButton.IsEnabled = free;
        RecordLabel.Text = hasAudio ? "Record again" : "Record";
        StopRecordButton.IsEnabled = recording;
        PlayButton.IsEnabled = free && hasAudio;
        PlayLabel.Text = playing ? "Stop" : "Play";
        PlayGlyph.Glyph = playing ? "" : "";
        AutomationProperties.SetName(PlayButton, playing ? "Stop voice note" : "Play voice note");
        AttachAudioButton.IsEnabled = free;
        TrimButton.IsEnabled = free && hasAudio;
        DeleteVoiceButton.IsEnabled = free && hasAudio;
        MicrophoneCombo.IsEnabled = !recording && !busy;

        DiscardButton.IsEnabled = state != SessionState.Idle && !busy;
        DismissErrorButton.IsEnabled = state == SessionState.Error;
        bool browsing = state == SessionState.Idle && !_showingSettings;
        SelectAllButton.IsEnabled = browsing;
        SelectNoneButton.IsEnabled = browsing;

        bool idle = state is SessionState.Idle or SessionState.CapturingPhoto;
        if (idle != _ribbonWasIdle)
        {
            _ribbonWasIdle = idle;
            RibbonTabs.SelectedItem = idle ? TabHome : TabVoice;
        }
    }

    private void OnRibbonTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        SelectorBarItem? chosen = sender.SelectedItem;
        HomeTab.Visibility = Show(chosen == TabHome);
        VoiceTab.Visibility = Show(chosen == TabVoice);
        ViewTab.Visibility = Show(chosen == TabView);
    }

    /// <summary>Brings the whole window in line with the options: theme, checkboxes and the toggles that show them.</summary>
    private void ApplyOptions(AppOptions options)
    {
        (App.MainWindow as MainWindow)?.ApplyTheme(options.Theme);
        ThemeSystemToggle.IsChecked = options.Theme == ThemeChoice.System;
        ThemeLightToggle.IsChecked = options.Theme == ThemeChoice.Light;
        ThemeDarkToggle.IsChecked = options.Theme == ThemeChoice.Dark;
        CheckboxToggle.IsChecked = options.ShowCheckboxes;
        SubfoldersToggle.IsChecked = options.ScanSubfolders;
        FileList.SetCheckboxes(options.ShowCheckboxes);
    }

    private void OnThemeToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Primitives.ToggleButton { Tag: string tag } && Enum.TryParse(tag, out ThemeChoice theme))
        {
            AppSettings.Options = AppSettings.Options with { Theme = theme };
        }

        // A click on the one already chosen would untick it; the options change (or not) re-draws all three.
        ApplyOptions(AppSettings.Options);
    }

    private void OnCheckboxToggleClick(object sender, RoutedEventArgs e) =>
        AppSettings.Options = AppSettings.Options with { ShowCheckboxes = CheckboxToggle.IsChecked == true };

    private void OnSubfoldersToggleClick(object sender, RoutedEventArgs e) =>
        AppSettings.Options = AppSettings.Options with { ScanSubfolders = SubfoldersToggle.IsChecked == true };

    private void OnSelectAllClick(object sender, RoutedEventArgs e) => FileList.SelectAll();

    private void OnSelectNoneClick(object sender, RoutedEventArgs e) => FileList.SelectNone();

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        _showingSettings = true;
        Render();
        SettingsView.Focus(FocusState.Programmatic);
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e) => await BatchDialogs.ShowAboutAsync(XamlRoot);

    /// <summary>Keyboard shortcuts. They work whichever ribbon tab is showing, because they belong to the page, not to a button.</summary>
    private void AddAccelerators()
    {
        void Add(VirtualKey key, VirtualKeyModifiers modifiers, Action<object, RoutedEventArgs> handler)
        {
            var accelerator = new KeyboardAccelerator { Key = key, Modifiers = modifiers };
            accelerator.Invoked += (_, args) =>
            {
                args.Handled = true;
                handler(this, new RoutedEventArgs());
            };
            KeyboardAccelerators.Add(accelerator);
        }

        Add(VirtualKey.O, VirtualKeyModifiers.Control, OnOpenFileClick);
        Add(VirtualKey.I, VirtualKeyModifiers.Control, OnImportClick);
        Add(VirtualKey.S, VirtualKeyModifiers.Control, OnSaveClick);
        Add(VirtualKey.M, VirtualKeyModifiers.Control, OnMakeErafClick);
        Add(VirtualKey.R, VirtualKeyModifiers.Control, OnRecordClick);
        Add(VirtualKey.T, VirtualKeyModifiers.Control, OnTrimClick);
        Add(VirtualKey.F5, VirtualKeyModifiers.None, OnRefreshNavClick);
        Add(VirtualKey.Left, VirtualKeyModifiers.Menu, OnBackNavClick);
        Add(VirtualKey.Right, VirtualKeyModifiers.Menu, OnForwardNavClick);
        Add(VirtualKey.Up, VirtualKeyModifiers.Menu, OnUpNavClick);
    }
}
