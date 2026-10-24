using Eraf.Format;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>
/// M3-M4 screen: obtain a JPEG (import or camera), preview it, and record, play, replace or discard a
/// voice note. Both live in a <see cref="CaptureSession"/> owned by this page. Saving comes in M6 and the
/// full session state machine in M5.
/// </summary>
public sealed partial class MainPage : Page
{
    private CaptureSession? _session;
    private CameraService? _camera;
    private AudioRecorder? _recorder;
    private ClipPlayer? _player;
    private bool _playing;
    private readonly DispatcherTimer _recordTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainPage()
    {
        InitializeComponent();
        _recordTimer.Tick += OnRecordTimerTick;
        Unloaded += (_, _) =>
        {
            StopCamera();
            DiscardEverything();
        };
        ShowIdle();
    }

    // ---------------------------------------------------------------- photo

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
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
            IdlePanel.Visibility = Visibility.Collapsed;
            PhotoPanel.Visibility = Visibility.Collapsed;
            CameraPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnTakePhotoClick(object sender, RoutedEventArgs e)
    {
        if (_camera is null)
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
            ShowIdle();
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
        ShowIdle();
    }

    private void OnDiscardClick(object sender, RoutedEventArgs e)
    {
        ClearMessage();
        DiscardEverything();
        ShowIdle();
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
            ShowIdle();
            ShowError(photo.SourceName is null
                ? "The photo could not be displayed. Try again."
                : $"\"{photo.SourceName}\" looks like a JPEG but could not be opened. It may be damaged.");
            return;
        }

        // A new photo starts a new session: any earlier photo and voice note are dropped.
        DropRecordingAndPlayback();
        _session?.Clear();
        _session = new CaptureSession(photo);
        PhotoInfo.Text = Describe(photo);
        IdlePanel.Visibility = Visibility.Collapsed;
        CameraPanel.Visibility = Visibility.Collapsed;
        PhotoPanel.Visibility = Visibility.Visible;
        ShowVoiceState();
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
        if (_session is null || _recorder is not null)
        {
            return;
        }

        ClearMessage();
        StopPlayback();
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
            _recordTimer.Start();
        }
        finally
        {
            RecordButton.IsEnabled = true;
            RerecordButton.IsEnabled = true;
            ShowVoiceState();
        }
    }

    private async void OnStopRecordClick(object sender, RoutedEventArgs e) => await FinishRecordingAsync();

    private async void OnRecordTimerTick(object? sender, object e)
    {
        if (_recorder is null)
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
            if (result.Clip is not null && _session is not null)
            {
                _player?.Dispose();
                _player = null;
                _session.SetAudio(result.Clip);            }
            else
            {
                ShowError(result.Error ?? "The recording failed. Try again.");
            }
        }
        finally
        {
            recorder.Dispose();
            StopRecordButton.IsEnabled = true;
            ShowVoiceState();
        }
    }

    private async void OnPlayClick(object sender, RoutedEventArgs e)
    {
        AudioClip? clip = _session?.Audio;
        if (clip is null)
        {
            return;
        }

        if (_playing)
        {
            StopPlayback();
            return;
        }

        try
        {
            if (_player is null)
            {
                _player = await ClipPlayer.CreateAsync(clip);
                DispatcherQueue queue = DispatcherQueue;
                _player.Ended += () => queue.TryEnqueue(StopPlayback);
                _player.Failed += message => queue.TryEnqueue(() =>
                {
                    StopPlayback();
                    ShowError($"The voice note could not be played ({message}).");
                });
            }

            _player.Play();
            _playing = true;
            PlayButton.Content = "Stop";
        }
        catch (Exception ex)
        {
            StopPlayback();
            ShowError($"The voice note could not be played ({ex.Message}).");
        }
    }

    private void OnDeleteVoiceClick(object sender, RoutedEventArgs e)
    {
        ClearMessage();
        DropPlayback();
        _session?.ClearAudio();
        ShowVoiceState();
    }

    private void StopPlayback()
    {
        _player?.Stop();
        _playing = false;
        PlayButton.Content = "Play";
    }

    private void DropPlayback()
    {
        _player?.Dispose();
        _player = null;
        _playing = false;
        PlayButton.Content = "Play";
    }

    /// <summary>Abandons a recording in progress (nothing is kept) and releases the player.</summary>
    private void DropRecordingAndPlayback()
    {
        _recordTimer.Stop();
        _recorder?.Dispose();
        _recorder = null;
        DropPlayback();
    }

    private void ShowVoiceState()
    {
        bool recording = _recorder is not null;
        AudioClip? clip = _session?.Audio;

        RecordButton.Visibility = !recording && clip is null ? Visibility.Visible : Visibility.Collapsed;
        RecordingPanel.Visibility = recording ? Visibility.Visible : Visibility.Collapsed;
        VoicePanel.Visibility = !recording && clip is not null ? Visibility.Visible : Visibility.Collapsed;

        if (recording)
        {
            RecordingStatus.Text = $"● Recording  {Format(0)} of {Format(ErafLimits.MaxAudioDurationMs)}";
        }

        if (clip is not null)
        {
            VoiceInfo.Text = $"Voice note {Format(clip.DurationMs)} (AAC M4A, {clip.M4aBytes.Length / 1024.0:N0} KB)";
        }
    }

    private static string Format(ulong ms)
    {
        var t = TimeSpan.FromMilliseconds(ms);
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }

    // ---------------------------------------------------------------- shared

    private void DiscardEverything()
    {
        DropRecordingAndPlayback();
        _session?.Clear();
        _session = null;
        PhotoImage.Source = null;
        PhotoInfo.Text = string.Empty;
        ShowVoiceState();
    }

    private void StopCamera()
    {
        CameraPreview.SetMediaPlayer(null);
        _camera?.Dispose();
        _camera = null;
    }

    private void ShowIdle()
    {
        IdlePanel.Visibility = Visibility.Visible;
        CameraPanel.Visibility = Visibility.Collapsed;
        PhotoPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowError(string message)
    {
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
