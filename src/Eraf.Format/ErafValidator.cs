using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Eraf.Format;

/// <summary>Parsed header fields after checks 1-13 of spec §9 have passed.</summary>
internal readonly record struct ErafHeader(
    ushort Flags,
    ulong ImageLength,
    ulong AudioLength,
    ulong AudioDurationMs,
    ulong CreatedUtcUnixMs)
{
    public bool HasImageSha256 => (Flags & 1) != 0;

    public bool HasAudioSha256 => (Flags & 2) != 0;

    public int IntegrityBlockLength =>
        ErafLimits.Sha256Length * ((HasImageSha256 ? 1 : 0) + (HasAudioSha256 ? 1 : 0));

    public int ImageOffset => ErafLimits.HeaderLength + IntegrityBlockLength;
}

/// <summary>
/// The ordered rule set of spec §9. Checks 1-13 need only the first 64 bytes and the file length and
/// run before any payload is read or allocated; checks 14-15 need the payload bytes.
/// </summary>
internal static class ErafValidator
{
    private const ushort KnownFlags = 0b11;

    /// <summary>Checks 1-13. <paramref name="start"/> must hold at least the first 64 bytes when the file has them.</summary>
    public static bool TryParseHeader(ReadOnlySpan<byte> start, long fileLength, out ErafHeader header, out ErafError error)
    {
        header = default;

        // 1
        if (fileLength < ErafLimits.HeaderLength || start.Length < ErafLimits.HeaderLength)
        {
            error = ErafError.TooShort;
            return false;
        }

        // 2
        if (start[0] != 0x45 || start[1] != 0x52 || start[2] != 0x41 || start[3] != 0x46)
        {
            error = ErafError.BadMagic;
            return false;
        }

        // 3: nothing else is interpreted for an unknown version, not even the CRC.
        if (BinaryPrimitives.ReadUInt16LittleEndian(start[4..]) != ErafLimits.FormatVersion)
        {
            error = ErafError.UnsupportedVersion;
            return false;
        }

        // 4
        if (BinaryPrimitives.ReadUInt16LittleEndian(start[6..]) != ErafLimits.HeaderLength)
        {
            error = ErafError.BadHeaderLength;
            return false;
        }

        // 5: CRC covers bytes 0-51 only; Reserved2 (56-63) is outside it.
        if (BinaryPrimitives.ReadUInt32LittleEndian(start[52..]) != Crc32.Compute(start[..52]))
        {
            error = ErafError.HeaderChecksumMismatch;
            return false;
        }

        ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(start[8..]);

        // 6
        if ((flags & ~KnownFlags) != 0)
        {
            error = ErafError.UnknownFlags;
            return false;
        }

        // 7
        if (BinaryPrimitives.ReadUInt16LittleEndian(start[10..]) != 0
            || BinaryPrimitives.ReadUInt16LittleEndian(start[18..]) != 0
            || BinaryPrimitives.ReadUInt64LittleEndian(start[56..]) != 0)
        {
            error = ErafError.ReservedNonZero;
            return false;
        }

        // 8
        if (BinaryPrimitives.ReadUInt16LittleEndian(start[12..]) != ErafLimits.MediaTypeJpeg
            || BinaryPrimitives.ReadUInt16LittleEndian(start[14..]) != ErafLimits.MediaTypeAudioMp4)
        {
            error = ErafError.UnsupportedMediaType;
            return false;
        }

        // 9
        if (BinaryPrimitives.ReadUInt16LittleEndian(start[16..]) != ErafLimits.CodecAacLc)
        {
            error = ErafError.UnsupportedCodec;
            return false;
        }

        ulong imageLength = BinaryPrimitives.ReadUInt64LittleEndian(start[20..]);
        ulong audioLength = BinaryPrimitives.ReadUInt64LittleEndian(start[28..]);
        ulong durationMs = BinaryPrimitives.ReadUInt64LittleEndian(start[36..]);
        ulong createdMs = BinaryPrimitives.ReadUInt64LittleEndian(start[44..]);

        // 10
        if (imageLength == 0 || audioLength == 0)
        {
            error = ErafError.EmptyPayload;
            return false;
        }

        // 11: claimed lengths are bounded here, so the sums below cannot overflow.
        if (imageLength > (ulong)ErafLimits.MaxImageBytes
            || audioLength > (ulong)ErafLimits.MaxAudioBytes
            || durationMs > ErafLimits.MaxAudioDurationMs)
        {
            error = ErafError.LimitExceeded;
            return false;
        }

        // 12
        if (fileLength > ErafLimits.MaxFileBytes)
        {
            error = ErafError.LimitExceeded;
            return false;
        }

        // 13
        var parsed = new ErafHeader(flags, imageLength, audioLength, durationMs, createdMs);
        long expected = checked(
            ErafLimits.HeaderLength + parsed.IntegrityBlockLength + (long)imageLength + (long)audioLength);
        if (expected != fileLength)
        {
            error = ErafError.LengthMismatch;
            return false;
        }

        header = parsed;
        error = default;
        return true;
    }

    /// <summary>Checks 14-15. <paramref name="integrity"/> is the integrity block (0, 32 or 64 bytes).</summary>
    public static bool TryVerifyPayloads(
        in ErafHeader header,
        ReadOnlySpan<byte> integrity,
        ReadOnlySpan<byte> image,
        ReadOnlySpan<byte> audio,
        out ErafError error)
    {
        // 14
        if (!HasJpegSignature(image) || !HasFtypSignature(audio))
        {
            error = ErafError.InvalidPayloadSignature;
            return false;
        }

        // 15: image digest first, then audio digest.
        int offset = 0;
        if (header.HasImageSha256)
        {
            if (!SHA256.HashData(image).AsSpan().SequenceEqual(integrity.Slice(offset, ErafLimits.Sha256Length)))
            {
                error = ErafError.HashMismatch;
                return false;
            }
            offset += ErafLimits.Sha256Length;
        }

        if (header.HasAudioSha256
            && !SHA256.HashData(audio).AsSpan().SequenceEqual(integrity.Slice(offset, ErafLimits.Sha256Length)))
        {
            error = ErafError.HashMismatch;
            return false;
        }

        error = default;
        return true;
    }

    /// <summary>Spec §8.1: first three bytes are FF D8 FF.</summary>
    public static bool HasJpegSignature(ReadOnlySpan<byte> image) =>
        image.Length >= ErafLimits.MinImageBytes && image[0] == 0xFF && image[1] == 0xD8 && image[2] == 0xFF;

    /// <summary>Spec §8.1: bytes 4-7 are ASCII "ftyp".</summary>
    public static bool HasFtypSignature(ReadOnlySpan<byte> audio) =>
        audio.Length >= ErafLimits.MinAudioBytes
        && audio[4] == (byte)'f' && audio[5] == (byte)'t' && audio[6] == (byte)'y' && audio[7] == (byte)'p';
}
