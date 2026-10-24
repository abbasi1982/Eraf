using Eraf.Format;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using PickerLocationId = Microsoft.Windows.Storage.Pickers.PickerLocationId;

namespace Eraf.Windows;

/// <summary>Outcome of an import attempt: a session, a user cancel, or a message safe to show.</summary>
public sealed record ImportResult(ImageSession? Session, string? Error)
{
    public bool Cancelled => Session is null && Error is null;

    public static ImportResult Cancel { get; } = new(null, null);

    public static ImportResult Fail(string message) => new(null, message);
}

/// <summary>
/// Picks an image file and turns it into JPEG bytes. JPEGs are kept untouched; anything else the
/// system can decode is converted once. Rules come from <see cref="ErafMedia"/> so they match the spec.
/// </summary>
public static class ImageImportService
{
    private static readonly string[] FileTypes =
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

    private const uint JpegHeadLength = 3;

    public static async Task<ImportResult> PickAndImportAsync(Microsoft.UI.Xaml.Window window)
    {
        // The Windows App SDK picker takes the window id directly. The older Windows.Storage.Pickers
        // FileOpenPicker failed here with E_FAIL even after InitializeWithWindow.
        var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(window.AppWindow.Id)
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        foreach (string type in FileTypes)
        {
            picker.FileTypeFilter.Add(type);
        }

        StorageFile? file;
        try
        {
            Microsoft.Windows.Storage.Pickers.PickFileResult? picked = await picker.PickSingleFileAsync();
            file = picked is null ? null : await StorageFile.GetFileFromPathAsync(picked.Path);
        }
        catch (Exception ex)
        {
            return ImportResult.Fail($"The file picker could not be opened ({ex.GetType().Name} 0x{ex.HResult:X8}: {ex.Message}).");
        }

        return file is null ? ImportResult.Cancel : await ImportFileAsync(file);
    }

    public static async Task<ImportResult> ImportFileAsync(StorageFile file)
    {
        try
        {
            ulong size = (await file.GetBasicPropertiesAsync()).Size;
            byte[] head = await ReadHeadAsync(file);

            // The size is checked before the file is read, so an oversized file never loads into memory.
            switch (ErafMedia.PlanImageImport((long)Math.Min(size, long.MaxValue), head))
            {
                case ImageImportAction.RejectEmpty:
                    return ImportResult.Fail($"\"{file.Name}\" is empty.");
                case ImageImportAction.RejectTooLarge:
                    return ImportResult.Fail(
                        $"\"{file.Name}\" is larger than {ErafLimits.MaxImageBytes / (1024 * 1024)} MB, "
                        + "which is the largest photo an ERAF file can hold.");
                case ImageImportAction.KeepAsJpeg:
                    byte[] bytes = await ReadAllBytesAsync(file);
                    return new ImportResult(new ImageSession(bytes, ImageOrigin.ImportedJpeg, file.Name), null);
                default:
                    return await ConvertAsync(file);
            }
        }
        catch (Exception ex)
        {
            return ImportResult.Fail($"\"{file.Name}\" could not be read ({ex.Message}).");
        }
    }

    private static async Task<ImportResult> ConvertAsync(StorageFile file)
    {
        byte[] jpeg;
        try
        {
            using IRandomAccessStream input = await file.OpenReadAsync();
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(input);
            using SoftwareBitmap bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);

            using var output = new InMemoryRandomAccessStream();
            BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output);
            encoder.SetSoftwareBitmap(bitmap);
            await encoder.FlushAsync();
            jpeg = await ReadAllBytesAsync(output);
        }
        catch (Exception)
        {
            return ImportResult.Fail(
                $"\"{file.Name}\" is not an image Windows can read. Choose a JPEG, PNG, BMP, GIF or TIFF file.");
        }

        // The converted output is held to the same rules as any other image.
        return ErafMedia.PlanImageImport(jpeg.Length, jpeg) switch
        {
            ImageImportAction.KeepAsJpeg =>
                new ImportResult(new ImageSession(jpeg, ImageOrigin.ConvertedToJpeg, file.Name), null),
            ImageImportAction.RejectTooLarge =>
                ImportResult.Fail($"\"{file.Name}\" is too large once converted to JPEG."),
            _ => ImportResult.Fail($"\"{file.Name}\" could not be converted to JPEG."),
        };
    }

    private static async Task<byte[]> ReadHeadAsync(StorageFile file)
    {
        using IRandomAccessStream stream = await file.OpenReadAsync();
        uint count = (uint)Math.Min(stream.Size, JpegHeadLength);
        return await ReadAsync(stream, count);
    }

    private static async Task<byte[]> ReadAllBytesAsync(StorageFile file)
    {
        using IRandomAccessStream stream = await file.OpenReadAsync();
        return await ReadAsync(stream, (uint)stream.Size);
    }

    internal static async Task<byte[]> ReadAllBytesAsync(IRandomAccessStream stream)
    {
        stream.Seek(0);
        return await ReadAsync(stream, (uint)stream.Size);
    }

    internal static async Task<byte[]> ReadAsync(IRandomAccessStream stream, uint count)
    {
        if (count == 0)
        {
            return Array.Empty<byte>();
        }

        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync(count);
        byte[] bytes = new byte[count];
        reader.ReadBytes(bytes);
        return bytes;
    }
}
