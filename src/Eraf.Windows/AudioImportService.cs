using Eraf.Format;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;
using Windows.Storage.Streams;
using PickerLocationId = Microsoft.Windows.Storage.Pickers.PickerLocationId;

namespace Eraf.Windows;

/// <summary>Outcome of attaching an audio file: a clip, a user cancel, or a message safe to show.</summary>
public sealed record AudioImportResult(AudioClip? Clip, string? Error)
{
    public bool Cancelled => Clip is null && Error is null;

    public static AudioImportResult Cancel { get; } = new(null, null);

    public static AudioImportResult Fail(string message) => new(null, message);
}

/// <summary>
/// Turns a saved audio file into the voice-note payload (AAC-LC in MPEG-4). An MPEG-4 file whose audio is already AAC
/// is kept byte for byte; anything else Windows can decode is converted once with the same profile the recorder uses.
/// Rules come from <see cref="ErafMedia"/>; the result is held to the same limits as a recording.
/// </summary>
public static class AudioImportService
{
    private static readonly string[] FileTypes = { ".m4a", ".mp3", ".wav", ".wma", ".aac", ".flac", ".mp4" };

    public static async Task<AudioImportResult> PickAndImportAsync(Microsoft.UI.Xaml.Window window)
    {
        StorageFile? file;
        try
        {
            var picker = new Microsoft.Windows.Storage.Pickers.FileOpenPicker(window.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.MusicLibrary,
            };
            foreach (string type in FileTypes)
            {
                picker.FileTypeFilter.Add(type);
            }

            Microsoft.Windows.Storage.Pickers.PickFileResult? picked = await picker.PickSingleFileAsync();
            file = picked is null ? null : await StorageFile.GetFileFromPathAsync(picked.Path);
        }
        catch (Exception ex)
        {
            return AudioImportResult.Fail($"The file picker could not be opened ({ex.GetType().Name}: {ex.Message}).");
        }

        return file is null ? AudioImportResult.Cancel : await ImportFileAsync(file);
    }

    public static async Task<AudioImportResult> ImportFileAsync(StorageFile file)
    {
        string name = file.Name;
        try
        {
            ulong size = (await file.GetBasicPropertiesAsync()).Size;

            MediaEncodingProfile? profile;
            try
            {
                profile = await MediaEncodingProfile.CreateFromFileAsync(file);
            }
            catch (Exception)
            {
                profile = null;
            }

            if (profile?.Audio is null)
            {
                return AudioImportResult.Fail(
                    $"\"{name}\" is not an audio file Windows can read. Choose an M4A, MP3, WAV, WMA, AAC or FLAC file.");
            }

            ulong durationMs = (ulong)Math.Max(0, (await file.Properties.GetMusicPropertiesAsync()).Duration.TotalMilliseconds);
            if (durationMs == 0)
            {
                return AudioImportResult.Fail($"The length of \"{name}\" could not be determined, so it cannot be attached.");
            }

            if (durationMs > ErafLimits.MaxAudioDurationMs)
            {
                return AudioImportResult.Fail($"\"{name}\" is longer than the 10 minute limit for a voice note.");
            }

            if (durationMs < AudioClip.MinDurationMs)
            {
                return AudioImportResult.Fail($"\"{name}\" is too short to use as a voice note.");
            }

            byte[] head = await ReadHeadAsync(file);
            bool aac = profile.Video is null
                && string.Equals(profile.Audio.Subtype, "AAC", StringComparison.OrdinalIgnoreCase);

            byte[] bytes;
            switch (ErafMedia.PlanAudioImport((long)Math.Min(size, long.MaxValue), head, aac))
            {
                case AudioImportAction.RejectEmpty:
                    return AudioImportResult.Fail($"\"{name}\" is empty.");
                case AudioImportAction.RejectTooLarge:
                    return AudioImportResult.Fail(
                        $"\"{name}\" is too large to convert ({ErafMedia.MaxAudioImportSourceBytes / (1024 * 1024)} MB is the most the app will try).");
                case AudioImportAction.KeepAsM4a:
                    bytes = await ReadAllBytesAsync(file);
                    break;
                default:
                    byte[]? converted = await ConvertAsync(file);
                    if (converted is null)
                    {
                        return AudioImportResult.Fail($"\"{name}\" could not be converted to a voice note. Try a different file.");
                    }

                    bytes = converted;
                    break;
            }

            return ErafMedia.CheckAudio(bytes.Length, durationMs, bytes) switch
            {
                AudioCheck.Ok => new AudioImportResult(new AudioClip(bytes, durationMs), null),
                AudioCheck.TooLarge => AudioImportResult.Fail($"\"{name}\" is too large once converted. Choose a shorter file."),
                AudioCheck.TooLong => AudioImportResult.Fail($"\"{name}\" is longer than the 10 minute limit for a voice note."),
                AudioCheck.Empty => AudioImportResult.Fail($"\"{name}\" produced no audio."),
                _ => AudioImportResult.Fail($"\"{name}\" did not produce a usable voice note."),
            };
        }
        catch (Exception)
        {
            return AudioImportResult.Fail($"\"{name}\" could not be read. It may be open in another app or damaged.");
        }
    }

    /// <summary>Transcodes to AAC-LC M4A in memory with the recorder's profile. Null when Windows cannot do it.</summary>
    private static async Task<byte[]?> ConvertAsync(StorageFile file)
    {
        try
        {
            using IRandomAccessStream input = await file.OpenReadAsync();
            using var output = new InMemoryRandomAccessStream();
            var transcoder = new MediaTranscoder();
            PrepareTranscodeResult prepared = await transcoder.PrepareStreamTranscodeAsync(
                input, output, MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Medium));
            if (!prepared.CanTranscode)
            {
                return null;
            }

            await prepared.TranscodeAsync();
            return await ImageImportService.ReadAllBytesAsync(output);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<byte[]> ReadHeadAsync(StorageFile file)
    {
        using IRandomAccessStream stream = await file.OpenReadAsync();
        return await ImageImportService.ReadAsync(stream, (uint)Math.Min(stream.Size, 16));
    }

    private static async Task<byte[]> ReadAllBytesAsync(StorageFile file)
    {
        using IRandomAccessStream stream = await file.OpenReadAsync();
        return await ImageImportService.ReadAllBytesAsync(stream);
    }
}
