namespace Eraf.Windows;

/// <summary>Where the session's JPEG bytes came from.</summary>
public enum ImageOrigin
{
    /// <summary>A JPEG file kept byte for byte.</summary>
    ImportedJpeg,

    /// <summary>A non-JPEG file decoded and re-encoded once as JPEG.</summary>
    ConvertedToJpeg,

    /// <summary>A still captured from the camera.</summary>
    Captured,

    /// <summary>The photo inside an opened .eraf file.</summary>
    OpenedFile,
}

/// <summary>
/// The photo for one capture session: exactly the JPEG bytes that a later milestone will save.
/// Held in memory only, so there are no temp files to clean up. Owned by the page, not a global.
/// </summary>
public sealed class ImageSession
{
    public ImageSession(byte[] jpegBytes, ImageOrigin origin, string? sourceName)
    {
        JpegBytes = jpegBytes;
        Origin = origin;
        SourceName = sourceName;
    }

    public byte[] JpegBytes { get; private set; }

    public ImageOrigin Origin { get; }

    /// <summary>Original file name for imports, null for camera captures.</summary>
    public string? SourceName { get; }

    /// <summary>Drops the image bytes. Safe to call more than once.</summary>
    public void Clear() => JpegBytes = Array.Empty<byte>();
}
