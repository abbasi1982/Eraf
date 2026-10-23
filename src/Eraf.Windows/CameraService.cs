using Eraf.Format;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>Outcome of starting the camera: a running service, or a message that says what to do next.</summary>
public sealed record CameraStartResult(CameraService? Camera, string? Error);

/// <summary>
/// Live camera preview and JPEG still capture through MediaCapture. Best-effort: when there is no camera
/// or access is denied, <see cref="StartAsync"/> returns an actionable message and image import stays available.
/// </summary>
public sealed class CameraService : IDisposable
{
    private readonly MediaCapture _capture;
    private readonly MediaPlayer _player;

    private CameraService(MediaCapture capture, MediaPlayer player)
    {
        _capture = capture;
        _player = player;
    }

    /// <summary>Feed this to a MediaPlayerElement to show the live preview.</summary>
    public MediaPlayer Preview => _player;

    public static async Task<CameraStartResult> StartAsync()
    {
        MediaCapture? capture = null;
        try
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            var group = groups.FirstOrDefault(g =>
                g.SourceInfos.Any(i => i.SourceKind == MediaFrameSourceKind.Color));
            if (group is null)
            {
                return new CameraStartResult(null,
                    "No camera was found. Connect a camera, or import a photo instead.");
            }

            capture = new MediaCapture();
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = group,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                MemoryPreference = MediaCaptureMemoryPreference.Auto,
            });

            var source = capture.FrameSources.Values.FirstOrDefault(s =>
                s.Info.SourceKind == MediaFrameSourceKind.Color);
            if (source is null)
            {
                capture.Dispose();
                return new CameraStartResult(null, "The camera did not provide a color video stream. Import a photo instead.");
            }

            var player = new MediaPlayer { Source = MediaSource.CreateFromMediaFrameSource(source), IsMuted = true };
            player.Play();
            return new CameraStartResult(new CameraService(capture, player), null);
        }
        catch (UnauthorizedAccessException)
        {
            capture?.Dispose();
            return new CameraStartResult(null,
                "Camera access is turned off. Allow it in Settings > Privacy & security > Camera, or import a photo instead.");
        }
        catch (Exception ex)
        {
            capture?.Dispose();
            return new CameraStartResult(null,
                $"The camera could not be started ({ex.Message}). It may be in use by another app. Import a photo instead.");
        }
    }

    /// <summary>Takes a still as JPEG bytes. Throws if the camera fails or the result is not a usable JPEG.</summary>
    public async Task<byte[]> CapturePhotoAsync()
    {
        using var stream = new InMemoryRandomAccessStream();
        await _capture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
        byte[] jpeg = await ImageImportService.ReadAllBytesAsync(stream);

        // A camera frame is held to the same rules as an imported file.
        if (ErafMedia.PlanImageImport(jpeg.Length, jpeg) != ImageImportAction.KeepAsJpeg)
        {
            throw new InvalidOperationException("The camera returned a photo that is not a valid JPEG.");
        }

        return jpeg;
    }

    public void Dispose()
    {
        _player.Pause();
        _player.Source = null;
        _player.Dispose();
        _capture.Dispose();
    }
}
