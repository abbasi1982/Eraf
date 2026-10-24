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

    /// <summary>A photo and a valid voice note are held and not yet saved: everything a v1 ERAF file needs.</summary>
    ReadyToSave,

    /// <summary>A save is running.</summary>
    Saving,

    /// <summary>What is held is exactly what is on disk: a file was just saved or just opened.</summary>
    Saved,

    /// <summary>A save failed; the media is still held and nothing on disk was damaged.</summary>
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

    /// <summary>An .eraf file was opened and validated: photo and voice note are held and already on disk.</summary>
    FileOpened,

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

    /// <summary>A save began (a target path was chosen).</summary>
    BeginSave,

    /// <summary>The save finished and the file is on disk.</summary>
    SaveSucceeded,

    /// <summary>The save failed.</summary>
    SaveFailed,

    /// <summary>The user acknowledged a save error; the media is still there to retry.</summary>
    AcknowledgeError,

    /// <summary>A valid voice note came from an audio file or from trimming (instead of a recording) and replaces any earlier one.</summary>
    AudioAttached,
}

/// <summary>
/// One session's state machine: pure logic, no UI, no I/O. It tracks whether a photo and a voice note
/// are held and refuses events that are illegal in the current state, leaving everything untouched.
/// Owned by the page (one per session), not a process-wide singleton.
///
/// Transitions (event: from → to):
///   StartCamera        Idle → CapturingPhoto
///   CancelCamera       CapturingPhoto → Idle
///   PhotoReady         Idle, CapturingPhoto → PhotoPreview
///   FileOpened         Idle → Saved
///   StartRecording     PhotoPreview, ReadyToSave, Saved, AudioPreview → RecordingAudio
///   RecordingSucceeded RecordingAudio → ReadyToSave
///   RecordingFailed    RecordingAudio → the state recording started from (PhotoPreview, ReadyToSave or Saved)
///   PlaybackStarted    ReadyToSave, Saved → AudioPreview
///   PlaybackStopped    AudioPreview → the state playback started from (ReadyToSave or Saved)
///   DiscardAudio       ReadyToSave, Saved, AudioPreview → PhotoPreview
///   StartOver          every state except Saving → Idle
///   BeginSave          ReadyToSave → Saving
///   SaveSucceeded      Saving → Saved
///   SaveFailed         Saving → Error
///   AcknowledgeError   Error → ReadyToSave
///   AudioAttached      PhotoPreview, ReadyToSave, Saved, AudioPreview → ReadyToSave
///
/// Playing a voice note or a failed re-record never changes whether the held media is saved: both return
/// to the state they started from. Making new media (a photo, a successful recording) or discarding the
/// voice note makes the session unsaved.
/// </summary>
public sealed class SessionWorkflow
{
    public SessionState State { get; private set; } = SessionState.Idle;

    /// <summary>True while a photo is held.</summary>
    public bool HasPhoto { get; private set; }

    /// <summary>True while a valid voice note is held (it survives a failed re-record).</summary>
    public bool HasAudio { get; private set; }

    /// <summary>
    /// While in <see cref="SessionState.AudioPreview"/> or <see cref="SessionState.RecordingAudio"/>: the state
    /// to return to when that activity ends without making new media.
    /// </summary>
    public SessionState Resume { get; private set; } = SessionState.Idle;

    /// <summary>True only when a v1 ERAF file could be saved from what is held, right now.</summary>
    public bool CanSave => State == SessionState.ReadyToSave && HasPhoto && HasAudio;

    /// <summary>True when what is held is exactly what is on disk (also while previewing or re-recording from that state).</summary>
    public bool IsSaved =>
        State == SessionState.Saved
        || (State is SessionState.AudioPreview or SessionState.RecordingAudio && Resume == SessionState.Saved);

    /// <summary>True when closing or starting over would lose something: held media (or a take in progress) that is not on disk.</summary>
    public bool HasUnsavedWork => (HasPhoto || State == SessionState.RecordingAudio) && !IsSaved;

    /// <summary>Raised after every accepted transition with (from, to). A same-state event still raises it.</summary>
    public event Action<SessionState, SessionState>? Changed;

    /// <summary>Whether <paramref name="sessionEvent"/> would be accepted right now.</summary>
    public bool CanApply(SessionEvent sessionEvent) => Evaluate(Snapshot, sessionEvent) is not null;

    /// <summary>Applies the event if legal. Returns false (and changes nothing) if it is illegal here.</summary>
    public bool TryApply(SessionEvent sessionEvent)
    {
        Outcome? outcome = Evaluate(Snapshot, sessionEvent);
        if (outcome is not { } next)
        {
            return false;
        }

        SessionState from = State;
        State = next.State;
        HasPhoto = next.HasPhoto;
        HasAudio = next.HasAudio;
        Resume = next.Resume;
        Changed?.Invoke(from, State);
        return true;
    }

    private Outcome Snapshot => new(State, HasPhoto, HasAudio, Resume);

    /// <summary>A session's complete state: used as both the input and the result of a transition.</summary>
    internal readonly record struct Outcome(SessionState State, bool HasPhoto, bool HasAudio, SessionState Resume);

    /// <summary>The whole transition table in one place. Null means the event is illegal in that state.</summary>
    internal static Outcome? Evaluate(Outcome current, SessionEvent sessionEvent)
    {
        SessionState state = current.State;
        Outcome To(SessionState next) => current with { State = next, Resume = SessionState.Idle };
        Outcome ToWithResume(SessionState next, SessionState resume) => current with { State = next, Resume = resume };

        switch (sessionEvent)
        {
            case SessionEvent.StartCamera:
                return state == SessionState.Idle ? To(SessionState.CapturingPhoto) : null;

            case SessionEvent.CancelCamera:
                return state == SessionState.CapturingPhoto ? To(SessionState.Idle) : null;

            case SessionEvent.PhotoReady:
                // A new photo only starts a session: from idle or a live camera, never over an existing one.
                return state is SessionState.Idle or SessionState.CapturingPhoto
                    ? new Outcome(SessionState.PhotoPreview, true, false, SessionState.Idle)
                    : null;

            case SessionEvent.FileOpened:
                return state == SessionState.Idle
                    ? new Outcome(SessionState.Saved, true, true, SessionState.Idle)
                    : null;

            case SessionEvent.StartRecording:
                return state switch
                {
                    SessionState.PhotoPreview or SessionState.ReadyToSave or SessionState.Saved
                        => ToWithResume(SessionState.RecordingAudio, state),
                    SessionState.AudioPreview => ToWithResume(SessionState.RecordingAudio, current.Resume),
                    _ => null,
                };

            case SessionEvent.RecordingSucceeded:
                return state == SessionState.RecordingAudio
                    ? new Outcome(SessionState.ReadyToSave, true, true, SessionState.Idle)
                    : null;

            case SessionEvent.AudioAttached:
                return state is SessionState.PhotoPreview or SessionState.ReadyToSave
                    or SessionState.Saved or SessionState.AudioPreview
                    ? new Outcome(SessionState.ReadyToSave, true, true, SessionState.Idle)
                    : null;

            case SessionEvent.RecordingFailed:
                return state == SessionState.RecordingAudio ? To(current.Resume) : null;

            case SessionEvent.PlaybackStarted:
                return state is SessionState.ReadyToSave or SessionState.Saved
                    ? ToWithResume(SessionState.AudioPreview, state)
                    : null;

            case SessionEvent.PlaybackStopped:
                return state == SessionState.AudioPreview ? To(current.Resume) : null;

            case SessionEvent.DiscardAudio:
                return state is SessionState.ReadyToSave or SessionState.Saved or SessionState.AudioPreview
                    ? new Outcome(SessionState.PhotoPreview, true, false, SessionState.Idle)
                    : null;

            case SessionEvent.StartOver:
                // Never while a save is running: that could throw away media mid-write.
                return state == SessionState.Saving
                    ? null
                    : new Outcome(SessionState.Idle, false, false, SessionState.Idle);

            case SessionEvent.BeginSave:
                return state == SessionState.ReadyToSave && current.HasPhoto && current.HasAudio
                    ? To(SessionState.Saving)
                    : null;

            case SessionEvent.SaveSucceeded:
                return state == SessionState.Saving ? To(SessionState.Saved) : null;

            case SessionEvent.SaveFailed:
                return state == SessionState.Saving ? To(SessionState.Error) : null;

            case SessionEvent.AcknowledgeError:
                return state == SessionState.Error ? To(SessionState.ReadyToSave) : null;

            default:
                return null;
        }
    }
}
