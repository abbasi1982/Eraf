using Eraf.Format;

namespace Eraf.Windows;

// Pure System.IO and the format library: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>Reads the photo out of a saved .eraf file so the file list can show it as the file's icon.</summary>
public static class ErafPreview
{
    /// <summary>
    /// Files bigger than this show the plain icon: a thumbnail would mean reading a lot just to draw a small picture.
    /// A normal ERAF file (a photo and a short voice note) is a few hundred KB.
    /// </summary>
    public const long MaxFileBytes = 48L * 1024 * 1024;

    /// <summary>
    /// The JPEG inside the file, or null if the file is missing, too big, in use, or not a valid ERAF file. The whole file is validated
    /// first (every check of spec section 9, hashes included), so the photo is only ever handed to a decoder after the file passed.
    /// </summary>
    public static byte[]? ReadPhoto(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ErafReader.TryRead(stream, out ErafDocument? document, out _) ? document.Image.ToArray() : null;
        }
        catch (Exception)
        {
            return null; // unreadable, locked, or vanished: just no thumbnail
        }
    }
}
