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

/// <summary>UI-free media rules shared by applications, so they match the format spec.</summary>
public static class ErafMedia
{
    /// <summary>Spec §8.1: the first three bytes are FF D8 FF.</summary>
    public static bool IsJpeg(ReadOnlySpan<byte> image) => ErafValidator.HasJpegSignature(image);

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
