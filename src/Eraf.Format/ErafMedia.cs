namespace Eraf.Format;

/// <summary>What an application should do with a candidate image before it can become an ERAF payload.</summary>
public enum ImageImportAction
{
    /// <summary>The bytes already start with the JPEG signature: keep them untouched (no recompression).</summary>
    KeepAsJpeg,

    /// <summary>Not a JPEG: decode and re-encode once as JPEG, then call <see cref="ErafMedia.PlanImageImport"/> on the result.</summary>
    ConvertToJpeg,

    /// <summary>No bytes.</summary>
    RejectEmpty,

    /// <summary>Larger than <see cref="ErafLimits.MaxImageBytes"/>, so it could never be saved in v1.</summary>
    RejectTooLarge,
}

/// <summary>Result of checking a finished voice recording against the v1 limits and signature.</summary>
public enum AudioCheck
{
    Ok,

    /// <summary>No bytes: a failed or empty recording must never be treated as success.</summary>
    Empty,

    /// <summary>Larger than <see cref="ErafLimits.MaxAudioBytes"/>.</summary>
    TooLarge,

    /// <summary>Longer than <see cref="ErafLimits.MaxAudioDurationMs"/>.</summary>
    TooLong,

    /// <summary>Does not look like an MPEG-4 audio file (no "ftyp" box type at bytes 4-7).</summary>
    NotM4a,
}

/// <summary>UI-free media rules shared by applications, so they match the format spec.</summary>
public static class ErafMedia
{
    /// <summary>Spec §8.1: the first three bytes are FF D8 FF.</summary>
    public static bool IsJpeg(ReadOnlySpan<byte> image) => ErafValidator.HasJpegSignature(image);

    /// <summary>
    /// Whether a JPEG ends the way a complete one does: an end-of-image marker (FF D9) within its last
    /// <see cref="EndMarkerWindow"/> bytes (some encoders pad the end). This is a hint for a non-blocking
    /// warning (ADR-008), never a validity rule: the format accepts any payload that passes the spec's checks.
    /// </summary>
    public static bool JpegHasEndMarker(ReadOnlySpan<byte> jpeg)
    {
        ReadOnlySpan<byte> tail = jpeg.Length > EndMarkerWindow ? jpeg[^EndMarkerWindow..] : jpeg;
        for (int i = 0; i + 1 < tail.Length; i++)
        {
            if (tail[i] == 0xFF && tail[i + 1] == 0xD9)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>How many trailing bytes <see cref="JpegHasEndMarker"/> searches.</summary>
    public const int EndMarkerWindow = 64;

    /// <summary>Spec §8.1: bytes 4-7 of the audio payload are ASCII "ftyp".</summary>
    public static bool IsM4a(ReadOnlySpan<byte> audio) => ErafValidator.HasFtypSignature(audio);

    /// <summary>
    /// Checks a finished recording before it is kept. <paramref name="durationMs"/> is the recorded length
    /// (0 if unknown). Only checks what the format enforces; whether the stream really is AAC-LC and
    /// decodes is up to the media stack.
    /// </summary>
    public static AudioCheck CheckAudio(long length, ulong durationMs, ReadOnlySpan<byte> bytes)
    {
        if (length <= 0)
        {
            return AudioCheck.Empty;
        }

        if (length > ErafLimits.MaxAudioBytes)
        {
            return AudioCheck.TooLarge;
        }

        if (durationMs > ErafLimits.MaxAudioDurationMs)
        {
            return AudioCheck.TooLong;
        }

        return IsM4a(bytes) ? AudioCheck.Ok : AudioCheck.NotM4a;
    }

    /// <summary>
    /// Decides how to treat an image about to be imported. <paramref name="length"/> is the full size in
    /// bytes (usable before reading the file) and <paramref name="head"/> holds at least its first three
    /// bytes. A result of <see cref="ImageImportAction.ConvertToJpeg"/> must be planned again after
    /// conversion: the converted output is subject to the same limit and signature check.
    /// </summary>
    public static ImageImportAction PlanImageImport(long length, ReadOnlySpan<byte> head)
    {
        if (length <= 0)
        {
            return ImageImportAction.RejectEmpty;
        }

        if (length > ErafLimits.MaxImageBytes)
        {
            return ImageImportAction.RejectTooLarge;
        }

        return IsJpeg(head) ? ImageImportAction.KeepAsJpeg : ImageImportAction.ConvertToJpeg;
    }
}
