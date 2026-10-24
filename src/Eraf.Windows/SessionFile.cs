using Eraf.Format;

namespace Eraf.Windows;

// Pure System.IO + Eraf.Format: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file
// directly (linked source).

/// <summary>Outcome of opening an .eraf file: a session, or a message that is safe to show the user.</summary>
public sealed record OpenResult(CaptureSession? Session, string? Error, ErafError? Code)
{
    public static OpenResult Fail(string message, ErafError? code = null) => new(null, message, code);
}

/// <summary>Turns a session into .eraf bytes and an .eraf file into a session, always through <c>Eraf.Format</c>.</summary>
public static class SessionFile
{
    /// <summary>
    /// Builds the file for a complete session: the photo and voice note exactly as held, the recording length,
    /// the creation time (UTC, a timestamp stored in the file) and SHA-256 digests of both payloads (on by
    /// default for new files; they detect accidental corruption, not tampering).
    /// </summary>
    /// <exception cref="InvalidOperationException">The session lacks a photo or a voice note.</exception>
    /// <exception cref="ErafFormatException">The writer refused the payloads (limits, signatures).</exception>
    public static byte[] Build(CaptureSession session, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!session.IsComplete || session.Audio is not { } audio)
        {
            throw new InvalidOperationException("A photo and a voice note are both required.");
        }

        return ErafWriter.Write(session.Photo.JpegBytes, audio.M4aBytes, new ErafWriteOptions
        {
            AudioDurationMs = audio.DurationMs,
            CreatedUtcUnixMs = (ulong)Math.Max(0, now.ToUnixTimeMilliseconds()),
            IncludeImageSha256 = true,
            IncludeAudioSha256 = true,
        });
    }

    /// <summary>
    /// Opens and fully validates an .eraf file (spec §9). The reader sees the real file length and reads only
    /// the 64-byte header before any size is trusted, so a hostile or huge file cannot cause a large allocation.
    /// Never throws for a bad file: every problem comes back as a message.
    /// </summary>
    public static OpenResult Open(string path)
    {
        string name = Path.GetFileName(path);
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (!ErafReader.TryRead(stream, out ErafDocument? document, out ErafError error))
            {
                return OpenResult.Fail(ErafErrorText.Describe(name, error), error);
            }

            var photo = new ImageSession(document.Image.ToArray(), ImageOrigin.OpenedFile, name);
            var session = new CaptureSession(photo);
            session.SetAudio(new AudioClip(document.Audio.ToArray(), document.AudioDurationMs));
            return new OpenResult(session, null, null);
        }
        catch (FileNotFoundException)
        {
            return OpenResult.Fail($"\"{name}\" could not be found.");
        }
        catch (UnauthorizedAccessException)
        {
            return OpenResult.Fail($"\"{name}\" cannot be read. Check that you have permission to open it.");
        }
        catch (IOException ex)
        {
            return OpenResult.Fail($"\"{name}\" could not be read ({ex.Message}).");
        }
    }
}

/// <summary>User-facing wording for each <see cref="ErafError"/> (one place, so it is tested for completeness).</summary>
public static class ErafErrorText
{
    public static string Describe(string fileName, ErafError error) => error switch
    {
        ErafError.TooShort => $"\"{fileName}\" is too small to be an ERAF file.",
        ErafError.BadMagic => $"\"{fileName}\" is not an ERAF file.",
        ErafError.UnsupportedVersion =>
            $"\"{fileName}\" was made by a newer version of ERAF than this app can open.",
        ErafError.UnsupportedMediaType or ErafError.UnsupportedCodec =>
            $"\"{fileName}\" uses a photo or audio type this app does not support.",
        ErafError.EmptyPayload => $"\"{fileName}\" is missing its photo or its voice note.",
        ErafError.LimitExceeded => $"\"{fileName}\" is larger than an ERAF file is allowed to be.",
        ErafError.LengthMismatch => $"\"{fileName}\" is incomplete or has extra data, so it cannot be trusted.",
        ErafError.InvalidPayloadSignature => $"\"{fileName}\" does not contain a usable photo and voice note.",
        ErafError.HashMismatch => $"\"{fileName}\" is damaged: its contents do not match their checksum.",
        _ => $"\"{fileName}\" is damaged or is not a valid ERAF file.",
    };
}
