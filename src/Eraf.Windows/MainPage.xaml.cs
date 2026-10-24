using Eraf.Format;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>
/// Capture screen. What is shown always follows <see cref="SessionWorkflow.State"/>, and every action first
/// asks the workflow whether it is legal. The photo and voice note live in a <see cref="CaptureSession"/>
/// owned by this page. Saving is wired in M6.
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly SessionWorkflow _workflow = new();
    private CaptureSession? _session;
    private CameraService? _camera;
    private AudioRecorder? _recorder;
    private ClipPlayer? _player;
    private readonly DispatcherTimer _recordTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainPage()
    {
        InitializeComponent();
        _workflow.Changed += (_, _) => Render();
        _recordTimer.Tick += OnRecordTimerTick;
        Unloaded += (_, _) => ReleaseEverything();
        Render();
    }

    // ---------------------------------------------------------------- rendering

    /// <summary>Maps the workflow state onto the panels and buttons. No decisions are made here.</summary>
    private void Render()
    {
        SessionState state = _workflow.State;
        bool recording = state == SessionState.RecordingAudio;

        IdlePanel.Visibility = Show(state == SessionState.Idle);
        CameraPanel.Visibility = Show(state == SessionState.CapturingPhoto);
        PhotoPanel.Visibility = Show(state != SessionState.Idle && state != SessionState.CapturingPhoto);

        RecordButton.Visibility = Show(!recording && !_workflow.HasAudio);
        RecordingPanel.Visibility = Show(recording);
        VoicePanel.Visibility = Show(!recording && _workflow.HasAudio);
        PlayButton.Content = state == SessionState.AudioPreview ? "Stop" : "Play";
        SaveButton.IsEnabled = _workflow.CanSave;

        if (recording)
        {
            RecordingStatus.Text = $"● Recording  {Format(_recorder?.ElapsedMs ?? 0)} of {Format(ErafLimits.MaxAudioDurationMs)}";
        }

        if (_session?.Audio is { } clip)
        {
            VoiceInfo.Text = $"Voice note {Format(clip.DurationMs)} (AAC M4A, {clip.M4aBytes.Length / 1024.0:N0} KB)";
        }
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

    /// <summary>Start over: drops the photo and voice note and returns to idle (refused while a save is running).</summary>
    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        if (!_workflow.CanApply(SessionEvent.StartOver))
        {
            return;
        }

        ClearMessage();
        ReleaseEverything();
        _workflow.TryApply(SessionEvent.StartOver);
    }

    /// <summary>Previews the session's own bytes, so what is shown is what a later save would store.</summary>
    private async Task ShowPhotoAsync(ImageSession photo)
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
        }
        catch (Exception)
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
            _ => $"Camera photo (JPEG, {size})",
        };
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
            RecorderStartResult result = await AudioRecorder.StartAsync();
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
            ShowError("The recording reached the 10 minute limit and was stopped.");
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

    // ---------------------------------------------------------------- save (M6)

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        // Only reachable in ReadyToSave. The actual write, and the Saving/Saved/Error states, come in M6.
        ShowInfo("Saving .eraf files arrives in a later update. Your photo and voice note are ready.");
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

    private void SetBusy(bool busy)
    {
        ImportButton.IsEnabled = !busy;
        CaptureButton.IsEnabled = !busy;
    }
}
