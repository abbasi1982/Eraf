namespace Eraf.Windows;

// This file must stay free of WinUI / WinRT references: tests/Eraf.Format.Tests compiles it directly
// (linked source) so the transition table can be unit-tested without a UI.

/// <summary>Where a capture session is. See <see cref="SessionWorkflow"/> for the legal transitions.</summary>
public enum SessionState
{
    /// <summary>Nothing chosen yet.</summary>
    Idle,

    /// <summary>The camera is live; no photo yet.</summary>
    CapturingPhoto,

    /// <summary>A photo is held; no voice note yet.</summary>
    PhotoPreview,

    /// <summary>The microphone is recording (a previous voice note, if any, is kept until this succeeds).</summary>
    RecordingAudio,

    /// <summary>The voice note is playing back.</summary>
    AudioPreview,

    /// <summary>A photo and a valid voice note are held: everything a v1 ERAF file needs.</summary>
    ReadyToSave,

    /// <summary>A save is running (driven by M6).</summary>
    Saving,

    /// <summary>The last save succeeded (driven by M6).</summary>
    Saved,

    /// <summary>A save failed; the media is still held (driven by M6/M7).</summary>
    Error,
}

/// <summary>Things that can happen to a session. Each is either legal in the current state or rejected.</summary>
public enum SessionEvent
{
    /// <summary>The camera preview started.</summary>
    StartCamera,

    /// <summary>The camera was cancelled or failed before a photo was taken.</summary>
    CancelCamera,

    /// <summary>A usable photo was obtained (imported or captured).</summary>
    PhotoReady,

    /// <summary>The microphone started recording.</summary>
    StartRecording,

    /// <summary>The recording stopped and produced a valid voice note.</summary>
    RecordingSucceeded,

    /// <summary>The recording failed, was empty or too short; no new voice note exists.</summary>
    RecordingFailed,

    /// <summary>Playback of the voice note started.</summary>
    PlaybackStarted,

    /// <summary>Playback stopped, ended or failed.</summary>
    PlaybackStopped,

    /// <summary>The voice note was thrown away; the photo stays.</summary>
    DiscardAudio,

    /// <summary>Everything (photo and voice note) is thrown away and the session returns to idle.</summary>
    StartOver,

    /// <summary>A save began.</summary>
    BeginSave,

    /// <summary>The save finished.</summary>
    SaveSucceeded,

    /// <summary>The save failed.</summary>
    SaveFailed,

    /// <summary>The user acknowledged a save error; the media is still there to retry.</summary>
    AcknowledgeError,
}

/// <summary>
/// One session's state machine: pure logic, no UI, no I/O. It tracks whether a photo and a voice note
/// are held and refuses events that are illegal in the current state, leaving state and flags untouched.
/// Owned by the page (one per session), not a process-wide singleton.
///
/// Transitions (event: from → to):
///   StartCamera        Idle → CapturingPhoto
///   CancelCamera       CapturingPhoto → Idle
///   PhotoReady         Idle, CapturingPhoto → PhotoPreview
///   StartRecording     PhotoPreview, ReadyToSave, AudioPreview → RecordingAudio
///   RecordingSucceeded RecordingAudio → ReadyToSave
///   RecordingFailed    RecordingAudio → ReadyToSave if a voice note is still held, else PhotoPreview
///   PlaybackStarted    ReadyToSave → AudioPreview
///   PlaybackStopped    AudioPreview → ReadyToSave
///   DiscardAudio       ReadyToSave, AudioPreview → PhotoPreview
///   StartOver          every state except Saving → Idle
///   BeginSave          ReadyToSave → Saving
///   SaveSucceeded      Saving → Saved
///   SaveFailed         Saving → Error
///   AcknowledgeError   Error → ReadyToSave
/// </summary>
public sealed class SessionWorkflow
{
    public SessionState State { get; private set; } = SessionState.Idle;

    /// <summary>True while a photo is held.</summary>
    public bool HasPhoto { get; private set; }

    /// <summary>True while a valid voice note is held (it survives a failed re-record).</summary>
    public bool HasAudio { get; private set; }

    /// <summary>True only when a v1 ERAF file could be saved from what is held.</summary>
    public bool CanSave => State == SessionState.ReadyToSave && HasPhoto && HasAudio;

    /// <summary>Raised after every accepted transition with (from, to). A same-state event still raises it.</summary>
    public event Action<SessionState, SessionState>? Changed;

    /// <summary>Whether <paramref name="sessionEvent"/> would be accepted right now.</summary>
    public bool CanApply(SessionEvent sessionEvent) => Evaluate(State, HasPhoto, HasAudio, sessionEvent) is not null;

    /// <summary>Applies the event if legal. Returns false (and changes nothing) if it is illegal here.</summary>
    public bool TryApply(SessionEvent sessionEvent)
    {
        Outcome? outcome = Evaluate(State, HasPhoto, HasAudio, sessionEvent);
        if (outcome is not { } next)
        {
            return false;
        }

        SessionState from = State;
        State = next.State;
        HasPhoto = next.HasPhoto;
        HasAudio = next.HasAudio;
        Changed?.Invoke(from, State);
        return true;
    }

    /// <summary>The result of a legal transition.</summary>
    internal readonly record struct Outcome(SessionState State, bool HasPhoto, bool HasAudio);

    /// <summary>The whole transition table in one place. Null means the event is illegal in that state.</summary>
    internal static Outcome? Evaluate(SessionState state, bool hasPhoto, bool hasAudio, SessionEvent sessionEvent)
    {
        Outcome Same(SessionState to) => new(to, hasPhoto, hasAudio);

        switch (sessionEvent)
        {
            case SessionEvent.StartCamera:
                return state == SessionState.Idle ? Same(SessionState.CapturingPhoto) : null;

            case SessionEvent.CancelCamera:
                return state == SessionState.CapturingPhoto ? Same(SessionState.Idle) : null;

            case SessionEvent.PhotoReady:
                // A new photo only starts a session: from idle or a live camera, never over an existing one.
                return state is SessionState.Idle or SessionState.CapturingPhoto
                    ? new Outcome(SessionState.PhotoPreview, true, false)
                    : null;

            case SessionEvent.StartRecording:
                return state is SessionState.PhotoPreview or SessionState.ReadyToSave or SessionState.AudioPreview
                    ? Same(SessionState.RecordingAudio)
                    : null;

            case SessionEvent.RecordingSucceeded:
                return state == SessionState.RecordingAudio ? new Outcome(SessionState.ReadyToSave, true, true) : null;

            case SessionEvent.RecordingFailed:
                return state == SessionState.RecordingAudio
                    ? Same(hasAudio ? SessionState.ReadyToSave : SessionState.PhotoPreview)
                    : null;

            case SessionEvent.PlaybackStarted:
                return state == SessionState.ReadyToSave ? Same(SessionState.AudioPreview) : null;

            case SessionEvent.PlaybackStopped:
                return state == SessionState.AudioPreview ? Same(SessionState.ReadyToSave) : null;

            case SessionEvent.DiscardAudio:
                return state is SessionState.ReadyToSave or SessionState.AudioPreview
                    ? new Outcome(SessionState.PhotoPreview, true, false)
                    : null;

            case SessionEvent.StartOver:
                // Never while a save is running: that could throw away media mid-write.
                return state == SessionState.Saving ? null : new Outcome(SessionState.Idle, false, false);

            case SessionEvent.BeginSave:
                return state == SessionState.ReadyToSave && hasPhoto && hasAudio ? Same(SessionState.Saving) : null;

            case SessionEvent.SaveSucceeded:
                return state == SessionState.Saving ? Same(SessionState.Saved) : null;

            case SessionEvent.SaveFailed:
                return state == SessionState.Saving ? Same(SessionState.Error) : null;

            case SessionEvent.AcknowledgeError:
                return state == SessionState.Error ? Same(SessionState.ReadyToSave) : null;

            default:
                return null;
        }
    }
}
