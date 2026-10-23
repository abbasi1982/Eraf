namespace Eraf.Format;

/// <summary>A fully validated ERAF v1 file: payloads exactly as stored, plus header metadata.</summary>
public sealed class ErafDocument
{
    internal ErafDocument(
        ReadOnlyMemory<byte> image,
        ReadOnlyMemory<byte> audio,
        ulong audioDurationMs,
        ulong createdUtcUnixMs,
        ReadOnlyMemory<byte>? imageSha256,
        ReadOnlyMemory<byte>? audioSha256)
    {
        Image = image;
        Audio = audio;
        AudioDurationMs = audioDurationMs;
        CreatedUtcUnixMs = createdUtcUnixMs;
        ImageSha256 = imageSha256;
        AudioSha256 = audioSha256;
    }

    /// <summary>Original JPEG bytes.</summary>
    public ReadOnlyMemory<byte> Image { get; }

    /// <summary>Original M4A (AAC-LC) bytes.</summary>
    public ReadOnlyMemory<byte> Audio { get; }

    /// <summary>Recording length in milliseconds; 0 means unknown. Informational only.</summary>
    public ulong AudioDurationMs { get; }

    /// <summary>Creation time, milliseconds since the Unix epoch (UTC); 0 means unknown. Informational only.</summary>
    public ulong CreatedUtcUnixMs { get; }

    /// <summary>Stored image digest if the file carried one (already verified). Corruption detection only.</summary>
    public ReadOnlyMemory<byte>? ImageSha256 { get; }

    /// <summary>Stored audio digest if the file carried one (already verified). Corruption detection only.</summary>
    public ReadOnlyMemory<byte>? AudioSha256 { get; }
}

/// <summary>Options for <see cref="ErafWriter"/>. Everything else in the header is fixed by v1.</summary>
public sealed record ErafWriteOptions
{
    /// <summary>Milliseconds, 0 = unknown, must not exceed <see cref="ErafLimits.MaxAudioDurationMs"/>.</summary>
    public ulong AudioDurationMs { get; init; }

    /// <summary>Milliseconds since the Unix epoch (UTC), 0 = unknown.</summary>
    public ulong CreatedUtcUnixMs { get; init; }

    public bool IncludeImageSha256 { get; init; }

    public bool IncludeAudioSha256 { get; init; }
}
