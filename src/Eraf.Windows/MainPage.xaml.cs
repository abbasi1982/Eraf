using Eraf.Format;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>
/// Capture, save, open and export screen. What is shown always follows <see cref="SessionWorkflow.State"/>,
/// and every action first asks the workflow whether it is legal. The photo and voice note live in a
/// <see cref="CaptureSession"/> owned by this page.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly SessionWorkflow _workflow = new();
    private CaptureSession? _session;
    private CameraService? _camera;
    private AudioRecorder? _recorder;
    private ClipPlayer? _player;
    private string? _savedLabel;
    private bool _dialogOpen;
    private readonly DispatcherTimer _recordTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainPage()
    {
        InitializeComponent();
        _workflow.Changed += (_, _) => Render();
        _recordTimer.Tick += OnRecordTimerTick;
        Unloaded += (_, _) => ReleaseEverything();
        Blade.FolderChosen += async path => await GoToFolderAsync(path);
        FileList.EntryActivated += async entry => await OpenEntryAsync(entry);
        FileList.EntrySelected += entry =>
        {
            _selectedEntry = entry;
            Render();
        };
        Loaded += async (_, _) =>
        {
            await RefreshMicrophonesAsync();
            if (_history.Current is null)
            {
                await ShowFolderAsync(StartFolder(), addToHistory: true);
            }

            FocusNextControl();
        };
        Render();
    }

    /// <summary>True when closing the window or starting over would lose a photo or voice note that is not on disk.</summary>
    public bool HasUnsavedWork => _workflow.HasUnsavedWork;

    /// <summary>True while a save is writing; the window must not close and nothing may be discarded.</summary>
    public bool IsSaving => _workflow.State == SessionState.Saving;

    public void NotifySavingInProgress() =>
        ShowInfo("A save is in progress. Please wait for it to finish.");

    /// <summary>Asks whether to throw away unsaved work. Never stacks two dialogs.</summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (_dialogOpen)
        {
            return false;
        }

        _dialogOpen = true;
        try
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "Discard unsaved work?",
                Content = "Your photo and voice note have not been saved to an ERAF file. If you continue they will be lost.",
                PrimaryButtonText = "Discard",
                CloseButtonText = "Keep working",
                DefaultButton = ContentDialogButton.Close,
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    // ---------------------------------------------------------------- rendering

    /// <summary>Maps the workflow state onto the panels and buttons. No decisions are made here.</summary>
    private void Render()
    {
        SessionState state = _workflow.State;
        bool recording = state == SessionState.RecordingAudio;
        bool busy = state == SessionState.Saving || _attachingAudio;

        FileList.Visibility = Show(state == SessionState.Idle);
        CameraPanel.Visibility = Show(state == SessionState.CapturingPhoto);
        PhotoPanel.Visibility = Show(state != SessionState.Idle && state != SessionState.CapturingPhoto);

        RecordButton.Visibility = Show(!recording && !_workflow.HasAudio);
        AttachAudioButton.Visibility = Show(!recording && !_workflow.HasAudio);
        RecordingPanel.Visibility = Show(recording);
        MicrophoneCombo.Visibility = Show(!recording);
        MicrophoneCombo.IsEnabled = !busy;
        VoicePanel.Visibility = Show(!recording && _workflow.HasAudio);
        PlayButton.Content = state == SessionState.AudioPreview ? "Stop" : "Play";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(PlayButton, state == SessionState.AudioPreview ? "Stop voice note" : "Play voice note");

        // Starting a photo is only possible from the file list; navigation is blocked while something is being written or converted.
        bool startable = state == SessionState.Idle && !_importBusy;
        ImportButton.IsEnabled = startable;
        CaptureButton.IsEnabled = startable;
        OpenFileButton.IsEnabled = startable;
        MakeErafButton.IsEnabled = startable && _selectedEntry is { Kind: EntryKind.Photo or EntryKind.Eraf };
        bool navigable = !busy && !_importBusy;
        BackNavButton.IsEnabled = navigable && _history.CanBack;
        ForwardNavButton.IsEnabled = navigable && _history.CanForward;
        UpNavButton.IsEnabled = navigable && _history.Current is { } here && FolderLister.Parent(here) is not null;
        RefreshNavButton.IsEnabled = navigable;
        AddressBox.IsEnabled = navigable;
        Blade.IsEnabled = navigable;
        SaveButton.IsEnabled = _workflow.CanSave && !_attachingAudio;
        DismissErrorButton.Visibility = Show(state == SessionState.Error);
        ExportPhotoButton.IsEnabled = !busy && !recording && _session is not null;
        ExportAudioButton.IsEnabled = !busy && !recording && _workflow.HasAudio;
        foreach (Button b in new[] { RecordButton, RerecordButton, AttachAudioButton, ReplaceAudioFileButton, TrimButton, PlayButton, DeleteVoiceButton, DiscardButton })
        {
            b.IsEnabled = !busy;
        }

        SaveStatus.Text = state switch
        {
            SessionState.Saving => "Saving...",
            SessionState.Error => "Save failed. Nothing on disk was changed, and your photo and voice note are still here.",
            _ when _workflow.IsSaved => _savedLabel ?? "Saved",
            _ when _workflow.HasPhoto => "Not saved yet",
            _ => string.Empty,
        };

        if (recording)
        {
            RecordingStatus.Text = $"● Recording  {Format(_recorder?.ElapsedMs ?? 0)} of {Format(ErafLimits.MaxAudioDurationMs)}";
        }

        if (_session?.Audio is { } clip)
        {
            string length = clip.DurationMs == 0 ? "length not recorded" : Format(clip.DurationMs);
            VoiceInfo.Text = $"Voice note ({length}, AAC M4A, {clip.M4aBytes.Length / 1024.0:N0} KB)";
        }

        // Move keyboard focus to the natural next control, but only when the step changed (not on every refresh).
        if (state != _focusedState || _workflow.HasAudio != _focusedHasAudio)
        {
            _focusedState = state;
            _focusedHasAudio = _workflow.HasAudio;
            DispatcherQueue.TryEnqueue(FocusNextControl);
        }
    }

    private SessionState? _focusedState;
    private bool _focusedHasAudio;

    /// <summary>Keyboard users should land on the control they most likely want next after each step.</summary>
    private void FocusNextControl()
    {
        if (_dialogOpen)
        {
            return;
        }

        Control? target = _workflow.State switch
        {
            SessionState.Idle => null,
            SessionState.CapturingPhoto => TakePhotoButton,
            SessionState.RecordingAudio => StopRecordButton,
            SessionState.Error => DismissErrorButton,
            SessionState.Saving => null,
            _ when !_workflow.HasAudio => RecordButton,
            _ when _workflow.CanSave => SaveButton,
            _ => PlayButton,
        };
        if (_workflow.State == SessionState.Idle)
        {
            FileList.FocusList();
            return;
        }

        target?.Focus(FocusState.Programmatic);
    }

    private static Visibility Show(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    // ---------------------------------------------------------------- photo

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (!_workflow.CanApply(SessionEvent.PhotoReady))
        {
            return;
        }

        ClearMessage();
        SetBusy(true);
        try
        {
            ImportResult result = await ImageImportService.PickAndImportAsync(App.MainWindow!);
            if (result.Session is not null)
            {
                await ShowPhotoAsync(result.Session);
            }
            else if (result.Error is not null)
            {
                ShowError(result.Error);
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnCameraClick(object sender, RoutedEventArgs e)
    {
        if (!_workflow.CanApply(SessionEvent.StartCamera))
        {
            return;
        }

        ClearMessage();
        SetBusy(true);
        try
        {
            CameraStartResult result = await CameraService.StartAsync();
            if (result.Camera is null)
            {
                ShowError(result.Error ?? "The camera could not be started.");
                return;
            }

            _camera = result.Camera;
            CameraPreview.SetMediaPlayer(_camera.Preview);
            _workflow.TryApply(SessionEvent.StartCamera);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnTakePhotoClick(object sender, RoutedEventArgs e)
    {
        if (_camera is null || _workflow.State != SessionState.CapturingPhoto)
        {
            return;
        }

        ClearMessage();
        TakePhotoButton.IsEnabled = false;
        try
        {
            byte[] jpeg = await _camera.CapturePhotoAsync();
            StopCamera();
            await ShowPhotoAsync(new ImageSession(jpeg, ImageOrigin.Captured, null));
        }
        catch (Exception ex)
        {
            StopCamera();
            _workflow.TryApply(SessionEvent.CancelCamera);
            ShowError($"The photo could not be taken ({ex.Message}). Try again, or import a photo instead.");
        }
        finally
        {
            TakePhotoButton.IsEnabled = true;
        }
    }

    private void OnCancelCameraClick(object sender, RoutedEventArgs e)
    {
        StopCamera();
        _workflow.TryApply(SessionEvent.CancelCamera);
    }

    /// <summary>Back to files: drops the photo and voice note and returns to the file list. Asks first if something would be lost.</summary>
    private async void OnDiscardClick(object sender, RoutedEventArgs e) => await LeaveSessionAsync();

    /// <summary>
    /// Ends the current session if there is one (asking first when it holds unsaved work). True when the page is
    /// now at the file list, false when the user kept working or a save is still running.
    /// </summary>
    private async Task<bool> LeaveSessionAsync()
    {
        if (_workflow.State == SessionState.Idle)
        {
            return true;
        }

        if (!_workflow.CanApply(SessionEvent.StartOver))
        {
            return false;
        }

        if (_workflow.HasUnsavedWork && !await ConfirmDiscardAsync())
        {
            return false;
        }

        // The state may have moved while the dialog was open (for example a recording auto-stopped).
        if (!_workflow.CanApply(SessionEvent.StartOver))
        {
            return false;
        }

        ClearMessage();
        ReleaseEverything();
        _savedLabel = null;
        _workflow.TryApply(SessionEvent.StartOver);
        // The folder may now hold a file that was just saved.
        _ = ShowFolderAsync(_history.Current ?? StartFolder(), addToHistory: false);
        return true;
    }

    /// <summary>Decodes the session's own bytes into the preview, so what is shown is what would be saved.</summary>
    private async Task<bool> TryPreviewAsync(ImageSession photo)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(photo.JpegBytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            PhotoImage.Source = bitmap;
            return true;
        }
        catch (Exception)
        {
            PhotoImage.Source = null;
            return false;
        }
    }

    /// <summary>Shows a newly imported or captured photo and starts a session with it.</summary>
    private async Task ShowPhotoAsync(ImageSession photo)
    {
        if (!await TryPreviewAsync(photo))
        {
            // The bytes looked like a JPEG but could not be displayed, so they are not kept.
            photo.Clear();
            _workflow.TryApply(SessionEvent.CancelCamera); // only legal if the camera was live; otherwise stays Idle
            ShowError(photo.SourceName is null
                ? "The photo could not be displayed. Try again."
                : $"\"{photo.SourceName}\" looks like a JPEG but could not be opened. It may be damaged.");
            return;
        }

        _session = new CaptureSession(photo);
        PhotoInfo.Text = Describe(photo);
        if (!_workflow.TryApply(SessionEvent.PhotoReady))
        {
            // Not legal in this state (for example a photo arriving while one is already held): drop it.
            _session.Clear();
            _session = null;
            PhotoImage.Source = null;
        }
    }

    private static string Describe(ImageSession session)
    {
        string size = $"{session.JpegBytes.Length / 1024.0:N0} KB";
        return session.Origin switch
        {
            ImageOrigin.ImportedJpeg => $"{session.SourceName} (JPEG, {size}, kept as is)",
            ImageOrigin.ConvertedToJpeg => $"{session.SourceName} (converted to JPEG, {size})",
            ImageOrigin.OpenedFile => $"Photo from {session.SourceName} (JPEG, {size})",
            _ => $"Camera photo (JPEG, {size})",
        };
    }

    // ---------------------------------------------------------------- open / save / export

    private async void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        if (!_workflow.CanApply(SessionEvent.FileOpened))
        {
            return;
        }

        ClearMessage();
        SetBusy(true);
        try
        {
            (string? path, string? pickError) = await FilePickers.PickOpenAsync(App.MainWindow!, new[] { ".eraf" });
            if (pickError is not null)
            {
                ShowError(pickError);
                return;
            }

            if (path is null)
            {
                return; // cancelled
            }

            await OpenErafPathAsync(path);
        }
        finally
        {
            SetBusy(false);
        }
    }

    /// <summary>Opens a chosen .eraf file: full validation (spec 9) off the UI thread, then show it. A bad file is a message, never an exception.</summary>
    private async Task OpenErafPathAsync(string path)
    {
        if (!_workflow.CanApply(SessionEvent.FileOpened))
        {
            return;
        }

        ClearMessage();
        SetBusy(true);
        try
        {
            // Full validation (spec §9) happens off the UI thread; a bad file comes back as a message, never an exception.
            OpenResult result = await Task.Run(() => SessionFile.Open(path));
            if (result.Session is null)
            {
                ShowError(result.Error ?? "The file could not be opened.");
                return;
            }

            string name = Path.GetFileName(path);
            if (!await TryPreviewAsync(result.Session.Photo))
            {
                result.Session.Clear();
                ShowError($"The photo inside \"{name}\" could not be displayed. The file may be damaged.");
                return;
            }

            _session = result.Session;
            PhotoInfo.Text = Describe(_session.Photo);
            _savedLabel = $"Opened: {name}";
            if (!_workflow.TryApply(SessionEvent.FileOpened))
            {
                _session.Clear();
                _session = null;
                PhotoImage.Source = null;
                _savedLabel = null;
            }
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!_workflow.CanSave || _session is null)
        {
            return;
        }

        ClearMessage();

        // Choose the target first, while still in ReadyToSave: cancelling the dialog is not a failure.
        (string? path, string? pickError) = await FilePickers.PickSaveAsync(
            App.MainWindow!, SuggestedErafName(), "ERAF file", ".eraf", _sessionFolder);
        if (pickError is not null)
        {
            ShowError(pickError);
            return;
        }

        if (path is null || !_workflow.CanSave || _session is null)
        {
            return;
        }

        CaptureSession session = _session;
        byte[] bytes;
        try
        {
            bytes = SessionFile.Build(session, DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            ShowError($"The file could not be built ({ex.Message}). Nothing was saved.");
            return;
        }

        if (!_workflow.TryApply(SessionEvent.BeginSave))
        {
            return;
        }

        try
        {
            // Temp file beside the target, flushed, re-validated as an ERAF file, then swapped in (spec §11).
            // The save dialog has already asked about overwriting, so replacing is intended.
            await Task.Run(() => AtomicFile.WriteAllBytes(
                path, bytes, replaceExisting: true,
                verify: temp => ErafReader.Validate(File.ReadAllBytes(temp)) is null));
        }
        catch (Exception ex)
        {
            _workflow.TryApply(SessionEvent.SaveFailed);
            ShowError($"The file could not be saved. {SaveErrorText.Describe(ex)} Nothing on disk was changed, and your photo and voice note are still here.");
            return;
        }

        _savedLabel = $"Saved: {Path.GetFileName(path)}";
        _workflow.TryApply(SessionEvent.SaveSucceeded);

        // ADR-008: a hint, never a reason to refuse the save.
        if (!ErafMedia.JpegHasEndMarker(session.Photo.JpegBytes))
        {
            ShowMessage($"Saved to {path}. The file is valid, but the photo looks incomplete (it ends earlier than a complete JPEG normally does) and may be partly missing.", InfoBarSeverity.Warning);
        }
        else
        {
            ShowMessage($"Saved to {path}.", InfoBarSeverity.Success);
        }
    }

    private void OnDismissErrorClick(object sender, RoutedEventArgs e)
    {
        ClearMessage();
        _workflow.TryApply(SessionEvent.AcknowledgeError);
    }

    private async void OnExportPhotoClick(object sender, RoutedEventArgs e) =>
        await ExportAsync(_session?.Photo.JpegBytes, "Photo", ".jpg", "JPEG image");

    private async void OnExportAudioClick(object sender, RoutedEventArgs e) =>
        await ExportAsync(_session?.Audio?.M4aBytes, "Voice note", ".m4a", "AAC voice note");

    /// <summary>Writes the original payload bytes, unchanged, to a file the user chooses. Not a save: the workflow state is untouched.</summary>
    private async Task ExportAsync(byte[]? bytes, string baseName, string extension, string typeLabel)
    {
        if (bytes is null || bytes.Length == 0
            || _workflow.State is SessionState.Saving or SessionState.RecordingAudio)
        {
            return;
        }

        ClearMessage();
        (string? path, string? pickError) = await FilePickers.PickSaveAsync(App.MainWindow!, baseName, typeLabel, extension);
        if (pickError is not null)
        {
            ShowError(pickError);
            return;
        }

        if (path is null)
        {
            return;
        }

        try
        {
            await Task.Run(() => AtomicFile.WriteAllBytes(
                path, bytes, replaceExisting: true,
                verify: temp => File.ReadAllBytes(temp).AsSpan().SequenceEqual(bytes)));
            ShowMessage($"Exported to {path}.", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowError($"The file could not be exported. {SaveErrorText.Describe(ex)} Nothing on disk was changed.");
        }
    }

    // ---------------------------------------------------------------- voice note

    private async void OnRecordClick(object sender, RoutedEventArgs e)
    {
        if (_recorder is not null || !_workflow.CanApply(SessionEvent.StartRecording))
        {
            return;
        }

        ClearMessage();
        EndPlayback();
        RecordButton.IsEnabled = false;
        RerecordButton.IsEnabled = false;
        try
        {
            // The list may be out of date (a microphone was plugged in or removed since it was filled).
            await RefreshMicrophonesAsync();
            RecorderStartResult result = await AudioRecorder.StartAsync((MicrophoneCombo.SelectedItem as MicrophoneOption)?.Id);
            if (result.Recorder is null)
            {
                ShowError(result.Error ?? "The microphone could not be started.");
                return;
            }

            // An earlier clip (if any) is kept until this recording succeeds.
            _recorder = result.Recorder;
            if (!_workflow.TryApply(SessionEvent.StartRecording))
            {
                _recorder.Dispose();
                _recorder = null;
                return;
            }

            _recordTimer.Start();
        }
        finally
        {
            RecordButton.IsEnabled = true;
            RerecordButton.IsEnabled = true;
        }
    }

    // ---------------------------------------------------------------- file browser

    private readonly FolderHistory _history = new();
    private BrowserEntry? _selectedEntry;
    private string? _sessionFolder;

    /// <summary>The last folder used, if it still exists, otherwise Pictures, otherwise the user's home folder.</summary>
    private static string StartFolder()
    {
        string? last = AppSettings.Get("LastFolder");
        if (last is not null && Directory.Exists(last))
        {
            return last;
        }

        string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Directory.Exists(pictures) ? pictures : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Goes to a folder from the tree, address box or a double-click, leaving any open photo first.</summary>
    private async Task GoToFolderAsync(string path)
    {
        if (!await LeaveSessionAsync())
        {
            return;
        }

        await ShowFolderAsync(path, addToHistory: true);
    }

    /// <summary>Reads a folder off the UI thread and shows it in the list.</summary>
    private async Task ShowFolderAsync(string path, bool addToHistory)
    {
        FolderListing listing = await Task.Run(() => FolderLister.List(path));
        if (addToHistory)
        {
            _history.Navigate(path);
        }

        AddressBox.Text = _history.Current ?? path;
        FileList.SetListing(listing.Entries, listing.Error,
            "Nothing here yet. Open a folder with photos or ERAF files, or use the camera.");
        AppSettings.Set("LastFolder", path);
        Render();
    }

    private async Task OpenEntryAsync(BrowserEntry entry)
    {
        if (_workflow.State != SessionState.Idle || _importBusy)
        {
            return;
        }

        switch (entry.Kind)
        {
            case EntryKind.Folder:
                await GoToFolderAsync(entry.FullPath);
                break;
            case EntryKind.Eraf:
                _sessionFolder = Path.GetDirectoryName(entry.FullPath);
                await OpenErafPathAsync(entry.FullPath);
                break;
            default:
                _sessionFolder = Path.GetDirectoryName(entry.FullPath);
                await ImportPhotoPathAsync(entry.FullPath);
                break;
        }
    }

    /// <summary>Starts a session with a photo chosen in the list (same rules as the Import button).</summary>
    private async Task ImportPhotoPathAsync(string path)
    {
        if (!_workflow.CanApply(SessionEvent.PhotoReady))
        {
            return;
        }

        ClearMessage();
        SetBusy(true);
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(path);
            ImportResult result = await ImageImportService.ImportFileAsync(file);
            if (result.Session is not null)
            {
                await ShowPhotoAsync(result.Session);
            }
            else if (result.Error is not null)
            {
                ShowError(result.Error);
            }
        }
        catch (Exception)
        {
            ShowError($"\"{Path.GetFileName(path)}\" could not be opened. It may have been moved or is in use.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnMakeErafClick(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry is { Kind: EntryKind.Photo or EntryKind.Eraf } entry)
        {
            await OpenEntryAsync(entry);
        }
        else
        {
            ShowInfo("Select a photo in the list first.");
        }
    }

    private async void OnBackNavClick(object sender, RoutedEventArgs e) => await StepHistoryAsync(_history.Back);

    private async void OnForwardNavClick(object sender, RoutedEventArgs e) => await StepHistoryAsync(_history.Forward);

    private async Task StepHistoryAsync(Func<string?> step)
    {
        if (!await LeaveSessionAsync())
        {
            return;
        }

        if (step() is { } path)
        {
            await ShowFolderAsync(path, addToHistory: false);
        }
    }

    private async void OnUpNavClick(object sender, RoutedEventArgs e)
    {
        if (_history.Current is { } here && FolderLister.Parent(here) is { } parent)
        {
            await GoToFolderAsync(parent);
        }
    }

    private async void OnRefreshNavClick(object sender, RoutedEventArgs e)
    {
        if (await LeaveSessionAsync())
        {
            await ShowFolderAsync(_history.Current ?? StartFolder(), addToHistory: false);
        }
    }

    private async void OnAddressKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != global::Windows.System.VirtualKey.Enter)
        {
            return;
        }

        e.Handled = true;
        string typed = AddressBox.Text.Trim().Trim('"');
        if (typed.Length > 0 && Directory.Exists(typed))
        {
            await GoToFolderAsync(Path.GetFullPath(typed));
        }
        else
        {
            ShowError("That folder does not exist. Check the path and try again.");
            AddressBox.Text = _history.Current ?? string.Empty;
        }
    }

    private void OnFilterChanged(object sender, TextChangedEventArgs e) => FileList.SetFilter(FilterBox.Text);

    /// <summary>The photo's own name makes the best default for the ERAF file next to it.</summary>
    private string SuggestedErafName()
    {
        string? source = _session?.Photo.SourceName;
        return string.IsNullOrWhiteSpace(source)
            ? $"ERAF {DateTime.Now:yyyy-MM-dd HHmm}"
            : Path.GetFileNameWithoutExtension(source);
    }

    // ---------------------------------------------------------------- audio file as the voice note

    private bool _attachingAudio;

    /// <summary>Uses a saved audio file as the voice note (converted to AAC M4A if needed). A failure keeps any earlier voice note.</summary>
    private async void OnAttachAudioClick(object sender, RoutedEventArgs e)
    {
        if (_recorder is not null || _attachingAudio || _session is null || !_workflow.CanApply(SessionEvent.AudioAttached))
        {
            return;
        }

        ClearMessage();
        EndPlayback();
        _attachingAudio = true;
        Render();
        try
        {
            AudioImportResult result = await AudioImportService.PickAndImportAsync(App.MainWindow!);
            if (result.Clip is not null)
            {
                // The page may have moved on while the file was being converted (for example Start over).
                if (_session is null || !_workflow.CanApply(SessionEvent.AudioAttached))
                {
                    return;
                }

                _player?.Dispose();
                _player = null;
                _session.SetAudio(result.Clip);
                _workflow.TryApply(SessionEvent.AudioAttached);
            }
            else if (result.Error is not null)
            {
                ShowError(result.Error);
            }
        }
        finally
        {
            _attachingAudio = false;
            Render();
        }
    }

    /// <summary>Cuts the voice note down to the part the user keeps. Cancelling or failing leaves the voice note as it was.</summary>
    private async void OnTrimClick(object sender, RoutedEventArgs e)
    {
        AudioClip? clip = _session?.Audio;
        if (clip is null || _recorder is not null || _attachingAudio || _dialogOpen || !_workflow.CanApply(SessionEvent.AudioAttached))
        {
            return;
        }

        ClearMessage();
        EndPlayback();
        TrimRange? range;
        _dialogOpen = true;
        try
        {
            range = await TrimDialog.ShowAsync(XamlRoot, clip);
        }
        finally
        {
            _dialogOpen = false;
        }

        if (range is null)
        {
            return;
        }

        _attachingAudio = true;
        Render();
        try
        {
            AudioImportResult result = await AudioTrimService.TrimAsync(clip, range.Value);
            if (result.Clip is not null)
            {
                if (_session is null || !ReferenceEquals(_session.Audio, clip) || !_workflow.CanApply(SessionEvent.AudioAttached))
                {
                    return; // the session changed while trimming
                }

                _player?.Dispose();
                _player = null;
                _session.SetAudio(result.Clip);
                _workflow.TryApply(SessionEvent.AudioAttached);
            }
            else if (result.Error is not null)
            {
                ShowError(result.Error);
            }
        }
        finally
        {
            _attachingAudio = false;
            Render();
        }
    }

    // ---------------------------------------------------------------- microphone choice

    private IReadOnlyList<MicrophoneOption> _microphones = Array.Empty<MicrophoneOption>();
    private bool _fillingMicrophones;

    /// <summary>Re-reads the device list and keeps the user's choice if that device is still there.</summary>
    private async Task RefreshMicrophonesAsync()
    {
        IReadOnlyList<MicrophoneOption> found = await MicrophoneService.ListAsync();
        string? wanted = (MicrophoneCombo.SelectedItem as MicrophoneOption)?.Id ?? MicrophoneService.LoadPreferredId();
        if (_microphones.Count > 0 && MicrophoneChoice.SameList(_microphones, found))
        {
            return;
        }

        MicrophoneOption chosen = MicrophoneChoice.Resolve(found, wanted);
        _fillingMicrophones = true;
        try
        {
            _microphones = found;
            MicrophoneCombo.ItemsSource = found;
            MicrophoneCombo.SelectedItem = chosen;
        }
        finally
        {
            _fillingMicrophones = false;
        }
    }

    private async void OnMicrophoneDropDownOpened(object? sender, object e)
    {
        // Pick up a microphone that was just plugged in. Rebuilding only happens when the list changed.
        await RefreshMicrophonesAsync();
    }

    private void OnMicrophoneSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_fillingMicrophones && MicrophoneCombo.SelectedItem is MicrophoneOption option)
        {
            MicrophoneService.SavePreferredId(option.Id);
        }
    }

    private async void OnStopRecordClick(object sender, RoutedEventArgs e) => await FinishRecordingAsync();

    private async void OnRecordTimerTick(object? sender, object e)
    {
        if (_recorder is null || _workflow.State != SessionState.RecordingAudio)
        {
            return;
        }

        if (_recorder.ElapsedMs >= ErafLimits.MaxAudioDurationMs)
        {
            await FinishRecordingAsync();
            ShowMessage("The recording reached the 10 minute limit and was stopped. It has been kept.", InfoBarSeverity.Informational);
            return;
        }

        RecordingStatus.Text = $"● Recording  {Format(_recorder.ElapsedMs)} of {Format(ErafLimits.MaxAudioDurationMs)}";
    }

    private async Task FinishRecordingAsync()
    {
        AudioRecorder? recorder = _recorder;
        if (recorder is null)
        {
            return;
        }

        _recordTimer.Stop();
        _recorder = null;
        StopRecordButton.IsEnabled = false;
        try
        {
            RecorderStopResult result = await recorder.StopAsync();
            if (_workflow.State != SessionState.RecordingAudio || _session is null)
            {
                return; // the session was abandoned while stopping: nothing to keep
            }

            if (result.Clip is not null)
            {
                _player?.Dispose();
                _player = null;
                _session.SetAudio(result.Clip);
                _workflow.TryApply(SessionEvent.RecordingSucceeded);
            }
            else
            {
                ShowError(result.Error ?? "The recording failed. Try again.");
                _workflow.TryApply(SessionEvent.RecordingFailed);
            }
        }
        finally
        {
            recorder.Dispose();
            StopRecordButton.IsEnabled = true;
        }
    }

    private async void OnPlayClick(object sender, RoutedEventArgs e)
    {
        if (_workflow.State == SessionState.AudioPreview)
        {
            EndPlayback();
            return;
        }

        AudioClip? clip = _session?.Audio;
        if (clip is null || !_workflow.CanApply(SessionEvent.PlaybackStarted))
        {
            return;
        }

        try
        {
            if (_player is null)
            {
                _player = await ClipPlayer.CreateAsync(clip);
                DispatcherQueue queue = DispatcherQueue;
                _player.Ended += () => queue.TryEnqueue(EndPlayback);
                _player.Failed += message => queue.TryEnqueue(() =>
                {
                    EndPlayback();
                    ShowError($"The voice note could not be played ({message}).");
                });
            }

            if (_workflow.TryApply(SessionEvent.PlaybackStarted))
            {
                _player.Play();
            }
            else
            {
                DropPlayer(); // the session changed while the player was loading
            }
        }
        catch (Exception ex)
        {
            EndPlayback();
            ShowError($"The voice note could not be played ({ex.Message}).");
        }
    }

    private void OnDeleteVoiceClick(object sender, RoutedEventArgs e)
    {
        if (!_workflow.CanApply(SessionEvent.DiscardAudio))
        {
            return;
        }

        ClearMessage();
        DropPlayer();
        _session?.ClearAudio();
        _workflow.TryApply(SessionEvent.DiscardAudio);
    }

    /// <summary>Stops the player and, if the workflow was previewing the note, leaves that state.</summary>
    private void EndPlayback()
    {
        _player?.Stop();
        _workflow.TryApply(SessionEvent.PlaybackStopped);
    }

    private void DropPlayer()
    {
        _player?.Dispose();
        _player = null;
    }

    private static string Format(ulong ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    // ---------------------------------------------------------------- shared

    /// <summary>Releases every device and drops all media. Does not touch the workflow state.</summary>
    private void ReleaseEverything()
    {
        _recordTimer.Stop();
        _recorder?.Dispose();
        _recorder = null;
        DropPlayer();
        StopCamera();
        _session?.Clear();
        _session = null;
        PhotoImage.Source = null;
        PhotoInfo.Text = string.Empty;
        VoiceInfo.Text = string.Empty;
    }

    private void StopCamera()
    {
        CameraPreview.SetMediaPlayer(null);
        _camera?.Dispose();
        _camera = null;
    }

    private void ShowError(string message) => ShowMessage(message, InfoBarSeverity.Error);

    private void ShowInfo(string message) => ShowMessage(message, InfoBarSeverity.Informational);

    private void ShowMessage(string message, InfoBarSeverity severity)
    {
        MessageBar.Severity = severity;
        MessageBar.Message = message;
        MessageBar.IsOpen = true;
    }

    private void ClearMessage() => MessageBar.IsOpen = false;

    private bool _importBusy;

    /// <summary>True while a picker, a conversion or a camera start is running: the start buttons and navigation are off.</summary>
    private void SetBusy(bool busy)
    {
        _importBusy = busy;
        Render();
    }
}
