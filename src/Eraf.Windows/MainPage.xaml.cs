using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>
/// M3 screen: obtain a JPEG (import or camera) and preview it. The photo lives in an
/// <see cref="ImageSession"/> owned by this page; saving and audio come in later milestones, and the
/// full session state machine arrives in M5.
/// </summary>
public sealed partial class MainPage : Page
{
    private ImageSession? _session;
    private CameraService? _camera;

    public MainPage()
    {
        InitializeComponent();
        Unloaded += (_, _) => StopCamera();
        ShowIdle();
    }

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
        DiscardSession();
        ShowIdle();
    }

    /// <summary>Previews the session's own bytes, so what is shown is what a later save would store.</summary>
    private async Task ShowPhotoAsync(ImageSession session)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(session.JpegBytes);
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
            session.Clear();
            ShowIdle();
            ShowError(session.SourceName is null
                ? "The photo could not be displayed. Try again."
                : $"\"{session.SourceName}\" looks like a JPEG but could not be opened. It may be damaged.");
            return;
        }

        _session?.Clear(); // replace any earlier photo; the new preview is already set
        _session = session;
        PhotoInfo.Text = Describe(session);
        IdlePanel.Visibility = Visibility.Collapsed;
        CameraPanel.Visibility = Visibility.Collapsed;
        PhotoPanel.Visibility = Visibility.Visible;
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

    private void DiscardSession()
    {
        _session?.Clear();
        _session = null;
        PhotoImage.Source = null;
        PhotoInfo.Text = string.Empty;
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
