using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>
/// Tests the session state machine. SessionWorkflow.cs is compiled into this project as linked source
/// (it has no WinUI dependency), so these run without a UI.
/// </summary>
public class SessionWorkflowTests
{
    private static readonly SessionState Idle = SessionState.Idle;
    private static readonly SessionState Camera = SessionState.CapturingPhoto;
    private static readonly SessionState Photo = SessionState.PhotoPreview;
    private static readonly SessionState Rec = SessionState.RecordingAudio;
    private static readonly SessionState Play = SessionState.AudioPreview;
    private static readonly SessionState Ready = SessionState.ReadyToSave;
    private static readonly SessionState Saving = SessionState.Saving;
    private static readonly SessionState Saved = SessionState.Saved;
    private static readonly SessionState Error = SessionState.Error;

    /// <summary>
    /// The legal transitions, written out independently of the implementation. Anything not listed must be
    /// rejected. RecordingFailed is checked separately because its target depends on whether audio is held.
    /// </summary>
    private static readonly Dictionary<(SessionState, SessionEvent), SessionState> Legal = new()
    {
        [(Idle, SessionEvent.StartCamera)] = Camera,
        [(Camera, SessionEvent.CancelCamera)] = Idle,
        [(Idle, SessionEvent.PhotoReady)] = Photo,
        [(Camera, SessionEvent.PhotoReady)] = Photo,
        [(Idle, SessionEvent.FileOpened)] = Saved,

        [(Photo, SessionEvent.StartRecording)] = Rec,
        [(Ready, SessionEvent.StartRecording)] = Rec,
        [(Play, SessionEvent.StartRecording)] = Rec,
        [(Saved, SessionEvent.StartRecording)] = Rec,
        [(Rec, SessionEvent.RecordingSucceeded)] = Ready,
        [(Photo, SessionEvent.AudioAttached)] = Ready,
        [(Ready, SessionEvent.AudioAttached)] = Ready,
        [(Play, SessionEvent.AudioAttached)] = Ready,
        [(Saved, SessionEvent.AudioAttached)] = Ready,

        [(Ready, SessionEvent.PlaybackStarted)] = Play,
        [(Saved, SessionEvent.PlaybackStarted)] = Play,
        [(Play, SessionEvent.PlaybackStopped)] = Ready,
        [(Ready, SessionEvent.DiscardAudio)] = Photo,
        [(Play, SessionEvent.DiscardAudio)] = Photo,
        [(Saved, SessionEvent.DiscardAudio)] = Photo,

        [(Idle, SessionEvent.StartOver)] = Idle,
        [(Camera, SessionEvent.StartOver)] = Idle,
        [(Photo, SessionEvent.StartOver)] = Idle,
        [(Rec, SessionEvent.StartOver)] = Idle,
        [(Play, SessionEvent.StartOver)] = Idle,
        [(Ready, SessionEvent.StartOver)] = Idle,
        [(Saved, SessionEvent.StartOver)] = Idle,
        [(Error, SessionEvent.StartOver)] = Idle,

        [(Ready, SessionEvent.BeginSave)] = Saving,
        [(Saving, SessionEvent.SaveSucceeded)] = Saved,
        [(Saving, SessionEvent.SaveFailed)] = Error,
        [(Error, SessionEvent.AcknowledgeError)] = Ready,
    };

    /// <summary>Drives a fresh workflow into <paramref name="target"/> using only legal events.</summary>
    private static SessionWorkflow Reach(SessionState target)
    {
        SessionEvent[] path = target switch
        {
            SessionState.Idle => Array.Empty<SessionEvent>(),
            SessionState.CapturingPhoto => new[] { SessionEvent.StartCamera },
            SessionState.PhotoPreview => new[] { SessionEvent.PhotoReady },
            SessionState.RecordingAudio => new[] { SessionEvent.PhotoReady, SessionEvent.StartRecording },
            SessionState.ReadyToSave => new[] { SessionEvent.PhotoReady, SessionEvent.StartRecording, SessionEvent.RecordingSucceeded },
            SessionState.AudioPreview => new[] { SessionEvent.PhotoReady, SessionEvent.StartRecording, SessionEvent.RecordingSucceeded, SessionEvent.PlaybackStarted },
            SessionState.Saving => new[] { SessionEvent.PhotoReady, SessionEvent.StartRecording, SessionEvent.RecordingSucceeded, SessionEvent.BeginSave },
            SessionState.Saved => new[] { SessionEvent.PhotoReady, SessionEvent.StartRecording, SessionEvent.RecordingSucceeded, SessionEvent.BeginSave, SessionEvent.SaveSucceeded },
            SessionState.Error => new[] { SessionEvent.PhotoReady, SessionEvent.StartRecording, SessionEvent.RecordingSucceeded, SessionEvent.BeginSave, SessionEvent.SaveFailed },
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };

        var workflow = new SessionWorkflow();
        foreach (SessionEvent e in path)
        {
            Assert.True(workflow.TryApply(e), $"{e} should be legal on the way to {target}");
        }

        Assert.Equal(target, workflow.State);
        return workflow;
    }

    /// <summary>A session that came from opening a file (clean, photo and audio held).</summary>
    private static SessionWorkflow Opened()
    {
        var workflow = new SessionWorkflow();
        Assert.True(workflow.TryApply(SessionEvent.FileOpened));
        return workflow;
    }

    public static IEnumerable<object[]> AllCombinations() =>
        from state in Enum.GetValues<SessionState>()
        from e in Enum.GetValues<SessionEvent>()
        select new object[] { state, e };

    [Theory]
    [MemberData(nameof(AllCombinations))]
    public void EveryStateEventPairIsAcceptedOrRejectedExactlyAsTabled(SessionState state, SessionEvent e)
    {
        SessionWorkflow workflow = Reach(state);
        bool hasPhoto = workflow.HasPhoto;
        bool hasAudio = workflow.HasAudio;

        bool expectedLegal = Legal.TryGetValue((state, e), out SessionState expectedTarget)
            || (e == SessionEvent.RecordingFailed && state == Rec);
        if (e == SessionEvent.RecordingFailed && state == Rec)
        {
            expectedTarget = hasAudio ? Ready : Photo;
        }

        Assert.Equal(expectedLegal, workflow.CanApply(e));
        Assert.Equal(expectedLegal, workflow.TryApply(e));

        if (expectedLegal)
        {
            Assert.Equal(expectedTarget, workflow.State);
        }
        else
        {
            // A rejected event changes nothing.
            Assert.Equal(state, workflow.State);
            Assert.Equal(hasPhoto, workflow.HasPhoto);
            Assert.Equal(hasAudio, workflow.HasAudio);
        }
    }

    [Fact]
    public void TableCoversEveryStateAndEvent()
    {
        // Guards the test itself: adding a state or event without updating the table should fail here.
        Assert.Equal(9, Enum.GetValues<SessionState>().Length);
        Assert.Equal(16, Enum.GetValues<SessionEvent>().Length);
        Assert.Equal(32, Legal.Count);
    }

    [Fact]
    public void RecordWhileIdleWithNoPhotoIsRejected()
    {
        var workflow = new SessionWorkflow();
        Assert.False(workflow.TryApply(SessionEvent.StartRecording));
        Assert.Equal(SessionState.Idle, workflow.State);
    }

    [Fact]
    public void SaveWhileRecordingOrWithoutAudioIsRejected()
    {
        SessionWorkflow recording = Reach(Rec);
        Assert.False(recording.TryApply(SessionEvent.BeginSave));
        Assert.False(recording.CanSave);

        SessionWorkflow photoOnly = Reach(Photo);
        Assert.False(photoOnly.TryApply(SessionEvent.BeginSave));
        Assert.False(photoOnly.CanSave);
    }

    [Fact]
    public void CannotStartOverWhileSaving()
    {
        SessionWorkflow saving = Reach(Saving);
        Assert.False(saving.TryApply(SessionEvent.StartOver));
        Assert.Equal(SessionState.Saving, saving.State);
        Assert.True(saving.HasPhoto && saving.HasAudio);
    }

    [Fact]
    public void ReadyToSaveOnlyWithPhotoAndAudio()
    {
        Assert.False(Reach(Photo).CanSave);
        Assert.False(Reach(Rec).CanSave);
        Assert.False(Reach(Play).CanSave); // listening to the note: not offered for saving until it stops

        SessionWorkflow ready = Reach(Ready);
        Assert.True(ready.CanSave);
        Assert.True(ready.HasPhoto && ready.HasAudio);
    }

    [Fact]
    public void FailedFirstRecordingReturnsToPhotoPreviewWithNoAudio()
    {
        SessionWorkflow workflow = Reach(Photo);
        Assert.True(workflow.TryApply(SessionEvent.StartRecording));
        Assert.True(workflow.TryApply(SessionEvent.RecordingFailed));
        Assert.Equal(SessionState.PhotoPreview, workflow.State);
        Assert.False(workflow.HasAudio);
        Assert.False(workflow.CanSave);
    }

    [Fact]
    public void FailedReRecordingKeepsTheEarlierVoiceNote()
    {
        SessionWorkflow workflow = Reach(Ready);
        Assert.True(workflow.TryApply(SessionEvent.StartRecording));
        Assert.False(workflow.CanSave); // not while recording, even though a note is still held
        Assert.True(workflow.HasAudio);
        Assert.True(workflow.TryApply(SessionEvent.RecordingFailed));
        Assert.Equal(SessionState.ReadyToSave, workflow.State);
        Assert.True(workflow.HasAudio);
        Assert.True(workflow.CanSave);
    }

    [Fact]
    public void DiscardAudioKeepsThePhotoAndStartOverClearsBoth()
    {
        SessionWorkflow workflow = Reach(Ready);
        Assert.True(workflow.TryApply(SessionEvent.DiscardAudio));
        Assert.Equal(SessionState.PhotoPreview, workflow.State);
        Assert.True(workflow.HasPhoto);
        Assert.False(workflow.HasAudio);

        Assert.True(workflow.TryApply(SessionEvent.StartOver));
        Assert.Equal(SessionState.Idle, workflow.State);
        Assert.False(workflow.HasPhoto);
        Assert.False(workflow.HasAudio);
    }

    [Fact]
    public void NewPhotoCannotReplaceAnExistingSession()
    {
        foreach (SessionState state in new[] { Photo, Rec, Play, Ready, Saving, Saved, Error })
        {
            SessionWorkflow workflow = Reach(state);
            Assert.False(workflow.TryApply(SessionEvent.PhotoReady));
            Assert.Equal(state, workflow.State);
        }
    }

    [Fact]
    public void SaveFailureKeepsTheMediaSoItCanBeRetried()
    {
        SessionWorkflow workflow = Reach(Error);
        Assert.True(workflow.HasPhoto && workflow.HasAudio);
        Assert.True(workflow.TryApply(SessionEvent.AcknowledgeError));
        Assert.True(workflow.CanSave);
        Assert.True(workflow.TryApply(SessionEvent.BeginSave));
    }

    [Fact]
    public void OpeningAFileGivesACleanSessionWithBothParts()
    {
        SessionWorkflow w = Opened();
        Assert.Equal(Saved, w.State);
        Assert.True(w.HasPhoto && w.HasAudio);
        Assert.True(w.IsSaved);
        Assert.False(w.HasUnsavedWork);
        Assert.False(w.CanSave); // nothing new to save
    }

    [Fact]
    public void FileCanOnlyBeOpenedFromIdle()
    {
        foreach (SessionState state in Enum.GetValues<SessionState>().Where(s => s != Idle))
        {
            SessionWorkflow w = Reach(state);
            Assert.False(w.TryApply(SessionEvent.FileOpened), $"FileOpened must be rejected in {state}");
            Assert.Equal(state, w.State);
        }
    }

    [Fact]
    public void PlayingASavedSessionKeepsItSaved()
    {
        foreach (SessionWorkflow w in new[] { Opened(), Reach(Saved) })
        {
            Assert.True(w.TryApply(SessionEvent.PlaybackStarted));
            Assert.Equal(Play, w.State);
            Assert.True(w.IsSaved);
            Assert.False(w.HasUnsavedWork);

            Assert.True(w.TryApply(SessionEvent.PlaybackStopped));
            Assert.Equal(Saved, w.State); // back where it started, not ReadyToSave
            Assert.True(w.IsSaved);
        }
    }

    [Fact]
    public void PlayingAnUnsavedSessionKeepsItUnsaved()
    {
        SessionWorkflow w = Reach(Ready);
        Assert.True(w.TryApply(SessionEvent.PlaybackStarted));
        Assert.False(w.IsSaved);
        Assert.True(w.HasUnsavedWork);
        Assert.True(w.TryApply(SessionEvent.PlaybackStopped));
        Assert.Equal(Ready, w.State);
    }

    [Fact]
    public void FailedReRecordingFromASavedSessionReturnsToSaved()
    {
        SessionWorkflow w = Opened();
        Assert.True(w.TryApply(SessionEvent.StartRecording));
        Assert.Equal(Rec, w.State);
        Assert.True(w.TryApply(SessionEvent.RecordingFailed));
        Assert.Equal(Saved, w.State);
        Assert.True(w.IsSaved);
        Assert.True(w.HasAudio);
    }

    [Fact]
    public void FailedReRecordingStartedWhilePlayingReturnsToWhereItStarted()
    {
        SessionWorkflow w = Opened();
        Assert.True(w.TryApply(SessionEvent.PlaybackStarted));
        Assert.True(w.TryApply(SessionEvent.StartRecording)); // legal from AudioPreview
        Assert.True(w.TryApply(SessionEvent.RecordingFailed));
        Assert.Equal(Saved, w.State);
    }

    [Fact]
    public void EditingASavedSessionMakesItUnsaved()
    {
        SessionWorkflow rerecorded = Opened();
        Assert.True(rerecorded.TryApply(SessionEvent.StartRecording));
        Assert.True(rerecorded.TryApply(SessionEvent.RecordingSucceeded));
        Assert.Equal(Ready, rerecorded.State);
        Assert.False(rerecorded.IsSaved);
        Assert.True(rerecorded.HasUnsavedWork);
        Assert.True(rerecorded.CanSave);

        SessionWorkflow discarded = Opened();
        Assert.True(discarded.TryApply(SessionEvent.DiscardAudio));
        Assert.Equal(Photo, discarded.State);
        Assert.True(discarded.HasUnsavedWork);
    }

    [Fact]
    public void SavedStateCanBeEditedAndSavedAgain()
    {
        SessionWorkflow w = Reach(Saved);
        Assert.True(w.IsSaved);
        Assert.False(w.HasUnsavedWork);
        Assert.True(w.TryApply(SessionEvent.StartRecording));
        Assert.True(w.TryApply(SessionEvent.RecordingSucceeded));
        Assert.True(w.CanSave); // can be saved again (as a new file)
    }

    [Theory]
    [InlineData(SessionState.Idle, false)]
    [InlineData(SessionState.CapturingPhoto, false)]
    [InlineData(SessionState.PhotoPreview, true)]
    [InlineData(SessionState.RecordingAudio, true)] // reached from PhotoPreview: the photo is unsaved
    [InlineData(SessionState.AudioPreview, true)]
    [InlineData(SessionState.ReadyToSave, true)]
    [InlineData(SessionState.Saving, true)]
    [InlineData(SessionState.Saved, false)]
    [InlineData(SessionState.Error, true)]
    public void UnsavedWorkIsReportedOnlyWhereSomethingCouldBeLost(SessionState state, bool expected) =>
        Assert.Equal(expected, Reach(state).HasUnsavedWork);

    [Fact]
    public void AbandoningARetakeOfASavedSessionLosesNothingThatWasSaved()
    {
        SessionWorkflow w = Opened();
        Assert.True(w.TryApply(SessionEvent.StartRecording));
        Assert.True(w.IsSaved);            // the held media is still exactly what is on disk
        Assert.False(w.HasUnsavedWork);
    }

    [Fact]
    public void ChangedReportsFromAndToOnlyForAcceptedEvents()
    {
        var workflow = new SessionWorkflow();
        var seen = new List<(SessionState, SessionState)>();
        workflow.Changed += (from, to) => seen.Add((from, to));

        Assert.False(workflow.TryApply(SessionEvent.BeginSave));
        Assert.True(workflow.TryApply(SessionEvent.PhotoReady));
        Assert.True(workflow.TryApply(SessionEvent.StartRecording));

        Assert.Equal(new[] { (Idle, Photo), (Photo, Rec) }, seen);
    }

    [Fact]
    public void InvariantsHoldAlongALongRandomWalk()
    {
        var rng = new Random(20261024);
        var events = Enum.GetValues<SessionEvent>();
        var workflow = new SessionWorkflow();
        int accepted = 0;

        for (int step = 0; step < 50_000; step++)
        {
            SessionEvent e = events[rng.Next(events.Length)];
            SessionState before = workflow.State;
            bool savedBefore = workflow.IsSaved;
            bool legal = workflow.CanApply(e);
            bool applied = workflow.TryApply(e);
            Assert.Equal(legal, applied);
            if (applied)
            {
                accepted++;
            }
            else
            {
                Assert.Equal(before, workflow.State);
            }

            switch (workflow.State)
            {
                case SessionState.Idle:
                case SessionState.CapturingPhoto:
                    Assert.False(workflow.HasPhoto || workflow.HasAudio);
                    break;
                case SessionState.PhotoPreview:
                    Assert.True(workflow.HasPhoto && !workflow.HasAudio);
                    break;
                case SessionState.RecordingAudio:
                    Assert.True(workflow.HasPhoto);
                    break;
                default: // AudioPreview, ReadyToSave, Saving, Saved, Error
                    Assert.True(workflow.HasPhoto && workflow.HasAudio);
                    break;
            }

            // Save is only ever possible from ReadyToSave with both parts, and never otherwise.
            Assert.Equal(workflow.State == SessionState.ReadyToSave, workflow.CanSave);

            // Saved-ness: playing or a failed take never changes it; new media or a discard always clears it;
            // only a finished save or an opened file sets it.
            if (applied)
            {
                switch (e)
                {
                    case SessionEvent.PlaybackStarted:
                    case SessionEvent.PlaybackStopped:
                    case SessionEvent.RecordingFailed:
                        Assert.Equal(savedBefore, workflow.IsSaved);
                        break;
                    case SessionEvent.RecordingSucceeded:
                    case SessionEvent.DiscardAudio:
                    case SessionEvent.PhotoReady:
                    case SessionEvent.AcknowledgeError:
                        Assert.False(workflow.IsSaved);
                        break;
                    case SessionEvent.SaveSucceeded:
                    case SessionEvent.FileOpened:
                        Assert.True(workflow.IsSaved);
                        break;
                }
            }

            if (workflow.IsSaved)
            {
                Assert.True(workflow.HasPhoto && workflow.HasAudio);
            }

            // Nothing is flagged unsaved in a state that holds nothing.
            if (!workflow.HasPhoto && workflow.State != SessionState.RecordingAudio)
            {
                Assert.False(workflow.HasUnsavedWork);
            }
        }

        Assert.True(accepted > 5_000, $"only {accepted} events were accepted; the walk is not exploring");
    }
}
