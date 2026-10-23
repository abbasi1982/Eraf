using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Eraf.Format;

/// <summary>
/// Writes ERAF v1 files. Every file it emits passes <see cref="ErafReader"/>: input that could not
/// produce a valid file is refused with <see cref="ErafFormatException"/> carrying the matching spec error.
/// Media types, codec, version, header length and reserved fields are not configurable, so unsupported
/// IDs, nonzero reserved fields and unknown flags cannot be emitted.
/// </summary>
public static class ErafWriter
{
    /// <summary>Builds a complete file in memory.</summary>
    public static byte[] Write(ReadOnlySpan<byte> image, ReadOnlySpan<byte> audio, ErafWriteOptions? options = null)
    {
        options ??= new ErafWriteOptions();
        Validate(image, audio, options);

        int integrityLength = ErafLimits.Sha256Length
            * ((options.IncludeImageSha256 ? 1 : 0) + (options.IncludeAudioSha256 ? 1 : 0));
        byte[] file = new byte[checked(ErafLimits.HeaderLength + integrityLength + image.Length + audio.Length)];

        WriteHeader(file, image.Length, audio.Length, options);

        int offset = ErafLimits.HeaderLength;
        if (options.IncludeImageSha256)
        {
            SHA256.HashData(image, file.AsSpan(offset, ErafLimits.Sha256Length));
            offset += ErafLimits.Sha256Length;
        }

        if (options.IncludeAudioSha256)
        {
            SHA256.HashData(audio, file.AsSpan(offset, ErafLimits.Sha256Length));
            offset += ErafLimits.Sha256Length;
        }

        image.CopyTo(file.AsSpan(offset));
        audio.CopyTo(file.AsSpan(offset + image.Length));
        return file;
    }

    /// <summary>
    /// Writes a complete file to <paramref name="destination"/>. Atomic replacement (temp file, flush, move)
    /// is the caller's responsibility; see spec §11.
    /// </summary>
    public static void Write(Stream destination, ReadOnlySpan<byte> image, ReadOnlySpan<byte> audio, ErafWriteOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(Write(image, audio, options));
    }

    private static void Validate(ReadOnlySpan<byte> image, ReadOnlySpan<byte> audio, ErafWriteOptions options)
    {
        if (image.Length == 0 || audio.Length == 0)
        {
            throw new ErafFormatException(ErafError.EmptyPayload);
        }

        if (image.Length > ErafLimits.MaxImageBytes
            || audio.Length > ErafLimits.MaxAudioBytes
            || options.AudioDurationMs > ErafLimits.MaxAudioDurationMs)
        {
            throw new ErafFormatException(ErafError.LimitExceeded);
        }

        long total = ErafLimits.HeaderLength + (long)image.Length + audio.Length
            + (long)ErafLimits.Sha256Length * ((options.IncludeImageSha256 ? 1 : 0) + (options.IncludeAudioSha256 ? 1 : 0));
        if (total > ErafLimits.MaxFileBytes)
        {
            throw new ErafFormatException(ErafError.LimitExceeded);
        }

        // Covers ImagePayloadLength < 3 and AudioPayloadLength < 8 as well as wrong content.
        if (!ErafValidator.HasJpegSignature(image) || !ErafValidator.HasFtypSignature(audio))
        {
            throw new ErafFormatException(ErafError.InvalidPayloadSignature);
        }
    }

    private static void WriteHeader(Span<byte> file, int imageLength, int audioLength, ErafWriteOptions options)
    {
        ushort flags = (ushort)((options.IncludeImageSha256 ? 1 : 0) | (options.IncludeAudioSha256 ? 2 : 0));

        file[0] = 0x45; // 'E'
        file[1] = 0x52; // 'R'
        file[2] = 0x41; // 'A'
        file[3] = 0x46; // 'F'
        BinaryPrimitives.WriteUInt16LittleEndian(file[4..], ErafLimits.FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(file[6..], ErafLimits.HeaderLength);
        BinaryPrimitives.WriteUInt16LittleEndian(file[8..], flags);
        BinaryPrimitives.WriteUInt16LittleEndian(file[10..], 0); // Reserved0
        BinaryPrimitives.WriteUInt16LittleEndian(file[12..], ErafLimits.MediaTypeJpeg);
        BinaryPrimitives.WriteUInt16LittleEndian(file[14..], ErafLimits.MediaTypeAudioMp4);
        BinaryPrimitives.WriteUInt16LittleEndian(file[16..], ErafLimits.CodecAacLc);
        BinaryPrimitives.WriteUInt16LittleEndian(file[18..], 0); // Reserved1
        BinaryPrimitives.WriteUInt64LittleEndian(file[20..], (ulong)imageLength);
        BinaryPrimitives.WriteUInt64LittleEndian(file[28..], (ulong)audioLength);
        BinaryPrimitives.WriteUInt64LittleEndian(file[36..], options.AudioDurationMs);
        BinaryPrimitives.WriteUInt64LittleEndian(file[44..], options.CreatedUtcUnixMs);
        BinaryPrimitives.WriteUInt32LittleEndian(file[52..], Crc32.Compute(file[..52]));
        // Reserved2 (56-63) stays zero: the buffer is freshly allocated.
    }
}
