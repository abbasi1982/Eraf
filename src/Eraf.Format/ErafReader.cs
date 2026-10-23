using System.Diagnostics.CodeAnalysis;

namespace Eraf.Format;

/// <summary>
/// Reads and validates ERAF v1 files per ERAF_FORMAT_SPEC.md §9. A document is returned only after all
/// 15 checks pass; nothing is repaired or partially opened.
/// </summary>
public static class ErafReader
{
    /// <summary>Validates and reads a complete file held in memory. Payloads in the result are slices of <paramref name="file"/>.</summary>
    public static bool TryRead(ReadOnlyMemory<byte> file, [NotNullWhen(true)] out ErafDocument? document, out ErafError error)
    {
        document = null;
        ReadOnlySpan<byte> span = file.Span;

        if (!ErafValidator.TryParseHeader(span, span.Length, out ErafHeader header, out error))
        {
            return false;
        }

        int imageOffset = header.ImageOffset;
        int audioOffset = imageOffset + (int)header.ImageLength;
        ReadOnlyMemory<byte> integrity = file.Slice(ErafLimits.HeaderLength, header.IntegrityBlockLength);
        ReadOnlyMemory<byte> image = file.Slice(imageOffset, (int)header.ImageLength);
        ReadOnlyMemory<byte> audio = file.Slice(audioOffset, (int)header.AudioLength);

        if (!ErafValidator.TryVerifyPayloads(header, integrity.Span, image.Span, audio.Span, out error))
        {
            return false;
        }

        document = BuildDocument(header, integrity, image, audio);
        return true;
    }

    /// <summary>
    /// Validates and reads a seekable stream from its current position to its end. Only the 64-byte header
    /// is read until the claimed lengths have been checked against the real stream length, so a hostile
    /// header cannot make this allocate more than the bytes actually present (at most 128 MiB).
    /// </summary>
    public static bool TryRead(Stream stream, [NotNullWhen(true)] out ErafDocument? document, out ErafError error)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must report its length (CanSeek).", nameof(stream));
        }

        document = null;
        long remaining = stream.Length - stream.Position;

        Span<byte> start = stackalloc byte[ErafLimits.HeaderLength];
        int startLength = (int)Math.Min(remaining, ErafLimits.HeaderLength);
        stream.ReadExactly(start[..startLength]);

        if (!ErafValidator.TryParseHeader(start[..startLength], remaining, out ErafHeader header, out error))
        {
            return false;
        }

        // Checks 1-13 passed: the claimed sizes are within limits and equal the real remaining length.
        byte[] integrity = new byte[header.IntegrityBlockLength];
        byte[] image = new byte[(int)header.ImageLength];
        byte[] audio = new byte[(int)header.AudioLength];
        stream.ReadExactly(integrity);
        stream.ReadExactly(image);
        stream.ReadExactly(audio);

        if (!ErafValidator.TryVerifyPayloads(header, integrity, image, audio, out error))
        {
            return false;
        }

        document = BuildDocument(header, integrity, image, audio);
        return true;
    }

    /// <summary>Like <see cref="TryRead(ReadOnlyMemory{byte}, out ErafDocument?, out ErafError)"/> but throws <see cref="ErafFormatException"/>.</summary>
    public static ErafDocument Read(ReadOnlyMemory<byte> file) =>
        TryRead(file, out ErafDocument? document, out ErafError error) ? document : throw new ErafFormatException(error);

    /// <summary>Like <see cref="TryRead(Stream, out ErafDocument?, out ErafError)"/> but throws <see cref="ErafFormatException"/>.</summary>
    public static ErafDocument Read(Stream stream) =>
        TryRead(stream, out ErafDocument? document, out ErafError error) ? document : throw new ErafFormatException(error);

    /// <summary>Runs every check and returns the first failure, or null if the file is valid.</summary>
    public static ErafError? Validate(ReadOnlyMemory<byte> file) =>
        TryRead(file, out _, out ErafError error) ? null : error;

    private static ErafDocument BuildDocument(
        in ErafHeader header,
        ReadOnlyMemory<byte> integrity,
        ReadOnlyMemory<byte> image,
        ReadOnlyMemory<byte> audio)
    {
        ReadOnlyMemory<byte>? imageSha = null;
        ReadOnlyMemory<byte>? audioSha = null;
        int offset = 0;
        if (header.HasImageSha256)
        {
            imageSha = integrity.Slice(offset, ErafLimits.Sha256Length);
            offset += ErafLimits.Sha256Length;
        }

        if (header.HasAudioSha256)
        {
            audioSha = integrity.Slice(offset, ErafLimits.Sha256Length);
        }

        return new ErafDocument(image, audio, header.AudioDurationMs, header.CreatedUtcUnixMs, imageSha, audioSha);
    }
}
