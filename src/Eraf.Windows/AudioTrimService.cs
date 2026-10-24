using Eraf.Format;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>Cuts a voice note down to the part the user keeps. The result is re-encoded to AAC-LC M4A and re-checked.</summary>
public static class AudioTrimService
{
    public static async Task<AudioImportResult> TrimAsync(AudioClip clip, TrimRange range)
    {
        if (TrimRange.Check(range.StartMs, range.EndMs, clip.DurationMs) != TrimCheck.Ok)
        {
            return AudioImportResult.Fail("That part of the voice note cannot be kept.");
        }

        try
        {
            using var input = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(input.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(clip.M4aBytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }

            input.Seek(0);
            using var output = new InMemoryRandomAccessStream();
            var transcoder = new MediaTranscoder
            {
                TrimStartTime = TimeSpan.FromMilliseconds(range.StartMs),
                TrimStopTime = TimeSpan.FromMilliseconds(range.EndMs),
            };
            PrepareTranscodeResult prepared = await transcoder.PrepareStreamTranscodeAsync(
                input, output, MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Medium));
            if (!prepared.CanTranscode)
            {
                return AudioImportResult.Fail("Windows could not trim this voice note.");
            }

            await prepared.TranscodeAsync();
            byte[] bytes = await ImageImportService.ReadAllBytesAsync(output);
            return ErafMedia.CheckAudio(bytes.Length, range.LengthMs, bytes) == AudioCheck.Ok
                ? new AudioImportResult(new AudioClip(bytes, range.LengthMs), null)
                : AudioImportResult.Fail("Trimming did not produce a usable voice note. Your original voice note was kept.");
        }
        catch (Exception)
        {
            return AudioImportResult.Fail("The voice note could not be trimmed. Your original voice note was kept.");
        }
    }
}
