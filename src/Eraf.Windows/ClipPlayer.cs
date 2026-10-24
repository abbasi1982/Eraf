using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>
/// Plays an in-session voice note straight from memory, so there is no temp file to extract or delete.
/// <see cref="Ended"/> and <see cref="Failed"/> may be raised on a non-UI thread.
/// </summary>
public sealed class ClipPlayer : IDisposable
{
    private readonly InMemoryRandomAccessStream _stream;
    private readonly MediaPlayer _player = new();

    private ClipPlayer(InMemoryRandomAccessStream stream)
    {
        _stream = stream;
        _player.MediaEnded += (_, _) => Ended?.Invoke();
        _player.MediaFailed += (_, args) => Failed?.Invoke(args.ErrorMessage);
        _player.Source = MediaSource.CreateFromStream(_stream, "audio/mp4");
    }

    public event Action? Ended;

    public event Action<string>? Failed;

    public static async Task<ClipPlayer> CreateAsync(AudioClip clip)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(clip.M4aBytes);
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return new ClipPlayer(stream);
    }

    public void Play()
    {
        _player.PlaybackSession.Position = TimeSpan.Zero;
        _player.Play();
    }

    public void Stop()
    {
        _player.Pause();
        _player.PlaybackSession.Position = TimeSpan.Zero;
    }

    public void Dispose()
    {
        _player.Pause();
        _player.Source = null;
        _player.Dispose();
        _stream.Dispose();
    }
}
