namespace Eraf.Format;

/// <summary>Version 1 constants from ERAF_FORMAT_SPEC.md §3, §7 and §8.</summary>
public static class ErafLimits
{
    public const ushort FormatVersion = 1;
    public const int HeaderLength = 64;
    public const int Sha256Length = 32;

    public const long MaxFileBytes = 256L * 1024 * 1024;
    public const long MaxImageBytes = 64L * 1024 * 1024;
    public const long MaxAudioBytes = 64L * 1024 * 1024;
    public const ulong MaxAudioDurationMs = 600_000;

    /// <summary>Smallest image payload that can carry the JPEG signature (FF D8 FF).</summary>
    public const int MinImageBytes = 3;

    /// <summary>Smallest audio payload that can carry the box type of a leading ftyp box (bytes 4-7).</summary>
    public const int MinAudioBytes = 8;

    public const ushort MediaTypeJpeg = 1;
    public const ushort MediaTypeAudioMp4 = 1;
    public const ushort CodecAacLc = 1;
}
