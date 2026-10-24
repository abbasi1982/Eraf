using System.Runtime.InteropServices.WindowsRuntime;
using Eraf.Format;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>Turns captured pixels, or a picture on the clipboard, into JPEG bytes for a new ERAF (the format needs a JPEG).</summary>
public static class ScreenshotService
{
    /// <summary>Encoded once, at high quality, so text in a screenshot stays readable.</summary>
    public static async Task<byte[]?> EncodeAsync(CapturedFrame frame)
    {
        try
        {
            using SoftwareBitmap bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                frame.Bgra.AsBuffer(), BitmapPixelFormat.Bgra8, frame.Width, frame.Height, BitmapAlphaMode.Ignore);
            return await EncodeAsync(bitmap);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<byte[]> EncodeAsync(SoftwareBitmap bitmap)
    {
        using var stream = new InMemoryRandomAccessStream();
        var quality = new BitmapPropertySet { { "ImageQuality", new BitmapTypedValue(0.92, global::Windows.Foundation.PropertyType.Single) } };
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, quality);
        encoder.SetSoftwareBitmap(bitmap);
        await encoder.FlushAsync();
        return await ImageImportService.ReadAllBytesAsync(stream);
    }

    /// <summary>True when the clipboard holds a picture (not just a file or text). Never throws: a busy clipboard counts as "no".</summary>
    public static bool ClipboardHasPicture()
    {
        try
        {
            return Clipboard.GetContent().Contains(StandardDataFormats.Bitmap);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The paths of files copied in Explorer (not pictures), or an empty list.</summary>
    public static async Task<IReadOnlyList<string>> ClipboardFilesAsync()
    {
        try
        {
            DataPackageView view = Clipboard.GetContent();
            if (!view.Contains(StandardDataFormats.StorageItems))
            {
                return Array.Empty<string>();
            }

            return (await view.GetStorageItemsAsync()).Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
        }
        catch (Exception)
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>The clipboard's picture as JPEG, or null if there is none or it cannot be read.</summary>
    public static async Task<byte[]?> ClipboardPictureAsync()
    {
        try
        {
            DataPackageView view = Clipboard.GetContent();
            if (!view.Contains(StandardDataFormats.Bitmap))
            {
                return null;
            }

            RandomAccessStreamReference reference = await view.GetBitmapAsync();
            using IRandomAccessStreamWithContentType stream = await reference.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
            using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            byte[] jpeg = await EncodeAsync(bitmap);
            return ErafMedia.PlanImageImport(jpeg.Length, jpeg) == ImageImportAction.KeepAsJpeg ? jpeg : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
