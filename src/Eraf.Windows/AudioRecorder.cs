using System.Diagnostics;
using Eraf.Format;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Eraf.Windows;

/// <summary>Outcome of starting a recording: a running recorder, or a message that says what to do next.</summary>
public sealed record RecorderStartResult(AudioRecorder? Recorder, string? Error);

/// <summary>Outcome of stopping: a valid clip, or a message. A failed or empty recording is never a clip.</summary>
public sealed record RecorderStopResult(AudioClip? Clip, string? Error);

/// <summary>
/// Records the microphone to AAC-LC in MPEG-4 (<c>MediaEncodingProfile.CreateM4a</c>) held in memory.
/// Start/stop only: pause and resume are not offered (see DECISIONS ADR-009).
/// </summary>
public sealed class AudioRecorder : IDisposable
{
    private readonly MediaCapture _capture;
    private readonly InMemoryRandomAccessStream _stream;
    private readonly Stopwatch _clock = new();
    private string? _failure;
    private bool _stopped;

    private AudioRecorder(MediaCapture capture, InMemoryRandomAccessStream stream)
    {
        _capture = capture;
        _stream = stream;
        _capture.Failed += (_, args) => _failure = args.Message;
    }

    /// <summary>Time since recording began; frozen once stopped.</summary>
    public ulong ElapsedMs => (ulong)_clock.ElapsedMilliseconds;

    public static async Task<RecorderStartResult> StartAsync()
    {
        MediaCapture? capture = null;
        InMemoryRandomAccessStream? stream = null;
        try
        {
            var microphones = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            if (microphones.Count == 0)
            {
                return new RecorderStartResult(null,
                    "No microphone was found. Connect one to record a voice note. You can still look at the photo.");
            }

            capture = new MediaCapture();
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Audio,
            });

            stream = new InMemoryRandomAccessStream();
            var recorder = new AudioRecorder(capture, stream);
            await capture.StartRecordToStreamAsync(MediaEncodingProfile.CreateM4a(AudioEncodingQuality.Medium), stream);
            recorder._clock.Start();
            return new RecorderStartResult(recorder, null);
        }
        catch (UnauthorizedAccessException)
        {
            capture?.Dispose();
            stream?.Dispose();
            return new RecorderStartResult(null,
                "Microphone access is turned off. Allow it in Settings > Privacy & security > Microphone, then try again.");
        }
        catch (Exception ex)
        {
            capture?.Dispose();
            stream?.Dispose();
            return new RecorderStartResult(null,
                $"The microphone could not be started ({ex.Message}). It may be in use by another app.");
        }
    }

    /// <summary>Ends the recording and checks it against the v1 limits before returning it.</summary>
    public async Task<RecorderStopResult> StopAsync()
    {
        if (_stopped)
        {
            return new RecorderStopResult(null, "The recording has already been stopped.");
        }

        _stopped = true;
        _clock.Stop();
        try
        {
            await _capture.StopRecordAsync();
        }
        catch (Exception ex)
        {
            return new RecorderStopResult(null, $"The recording could not be finished ({ex.Message}). Try again.");
        }

        if (_failure is not null)
        {
            return new RecorderStopResult(null, $"The recording failed ({_failure}). Try again.");
        }

        // The limit is enforced while recording (the page stops at the cap); clamp the few ms of stop latency.
        ulong durationMs = Math.Min(ElapsedMs, ErafLimits.MaxAudioDurationMs);
        if (durationMs < AudioClip.MinDurationMs)
        {
            return new RecorderStopResult(null, "That recording was too short. Hold on a little longer and try again.");
        }

        byte[] bytes = await ImageImportService.ReadAllBytesAsync(_stream);
        return ErafMedia.CheckAudio(bytes.Length, durationMs, bytes) switch
        {
            AudioCheck.Ok => new RecorderStopResult(new AudioClip(bytes, durationMs), null),
            AudioCheck.Empty => new RecorderStopResult(null, "Nothing was recorded. Check the microphone and try again."),
            AudioCheck.TooLarge => new RecorderStopResult(null, "The recording is too large to save. Record a shorter note."),
            AudioCheck.TooLong => new RecorderStopResult(null, "The recording is longer than the 10 minute limit."),
            _ => new RecorderStopResult(null, "The recording did not produce a usable audio file. Try again."),
        };
    }

    public void Dispose()
    {
        _clock.Stop();
        _capture.Dispose();
        _stream.Dispose();
    }
}
