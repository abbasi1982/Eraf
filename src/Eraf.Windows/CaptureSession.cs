namespace Eraf.Windows;

/// <summary>A finished voice recording: M4A bytes exactly as encoded, plus how long it ran.</summary>
public sealed class AudioClip
{
    /// <summary>Anything shorter than this is treated as a failed recording (an accidental tap).</summary>
    public const ulong MinDurationMs = 300;

    public AudioClip(byte[] m4aBytes, ulong durationMs)
    {
        M4aBytes = m4aBytes;
        DurationMs = durationMs;
    }

    public byte[] M4aBytes { get; }

    public ulong DurationMs { get; }
}

/// <summary>
/// Everything one capture session holds: the photo and, once recorded, the voice note. In memory only
/// (no temp files), owned by the page rather than a global. M6 will save exactly these bytes.
/// </summary>
public sealed class CaptureSession
{
    public CaptureSession(ImageSession photo) => Photo = photo;

    public ImageSession Photo { get; }

    public AudioClip? Audio { get; private set; }

    /// <summary>True when both parts a v1 ERAF file needs are present.</summary>
    public bool IsComplete => Audio is not null && Photo.JpegBytes.Length > 0;

    public void SetAudio(AudioClip clip) => Audio = clip;

    public void ClearAudio() => Audio = null;

    /// <summary>Drops the photo and the voice note.</summary>
    public void Clear()
    {
        Photo.Clear();
        Audio = null;
    }
}
