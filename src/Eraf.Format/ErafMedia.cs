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

/// <summary>What to do with an audio file the user wants to attach as the voice note.</summary>
public enum AudioImportAction
{
    /// <summary>An MPEG-4 file whose audio is already AAC: keep the bytes untouched (no re-encoding).</summary>
    KeepAsM4a,

    /// <summary>Anything else the system can decode: transcode once to AAC-LC in MPEG-4, then check the result with <see cref="ErafMedia.CheckAudio"/>.</summary>
    ConvertToM4a,

    /// <summary>No bytes.</summary>
    RejectEmpty,

    /// <summary>Larger than <see cref="ErafMedia.MaxAudioImportSourceBytes"/>: too big to even attempt converting.</summary>
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

    /// <summary>
    /// Largest audio file the app will try to convert. This is app policy, not a format rule: the converted
    /// result is still held to <see cref="ErafLimits.MaxAudioBytes"/> and <see cref="ErafLimits.MaxAudioDurationMs"/>.
    /// An uncompressed 10 minute stereo file is about 100 MB, so the source limit is deliberately larger than the payload limit.
    /// </summary>
    public const long MaxAudioImportSourceBytes = 512L * 1024 * 1024;

    /// <summary>
    /// Decides how to turn a chosen audio file into the voice-note payload. <paramref name="head"/> is the start of the file;
    /// <paramref name="platformSaysAac"/> is the platform's own probe that the file is a single AAC audio stream with no video.
    /// A kept file must still pass <see cref="CheckAudio"/>; a converted one must be planned and checked again after conversion.
    /// </summary>
    public static AudioImportAction PlanAudioImport(long length, ReadOnlySpan<byte> head, bool platformSaysAac)
    {
        if (length <= 0)
        {
            return AudioImportAction.RejectEmpty;
        }

        if (length > MaxAudioImportSourceBytes)
        {
            return AudioImportAction.RejectTooLarge;
        }

        return platformSaysAac && IsM4a(head) ? AudioImportAction.KeepAsM4a : AudioImportAction.ConvertToM4a;
    }
}
