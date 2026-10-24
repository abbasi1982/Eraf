namespace Eraf.Windows;

// Pure logic: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>Whether a chosen start and end make a usable trim of a voice note.</summary>
public enum TrimCheck
{
    Ok,

    /// <summary>The start is not before the end.</summary>
    NotInOrder,

    /// <summary>The end is past the end of the clip.</summary>
    OutOfRange,

    /// <summary>The part kept would be shorter than <see cref="AudioClip.MinDurationMs"/>.</summary>
    TooShort,

    /// <summary>The whole clip is selected, so there is nothing to trim.</summary>
    WholeClip,
}

/// <summary>The part of a voice note to keep, in milliseconds from its start.</summary>
public readonly record struct TrimRange(ulong StartMs, ulong EndMs)
{
    public ulong LengthMs => EndMs > StartMs ? EndMs - StartMs : 0;

    public static TrimCheck Check(ulong startMs, ulong endMs, ulong clipDurationMs)
    {
        if (startMs >= endMs)
        {
            return TrimCheck.NotInOrder;
        }

        if (endMs > clipDurationMs)
        {
            return TrimCheck.OutOfRange;
        }

        if (endMs - startMs < AudioClip.MinDurationMs)
        {
            return TrimCheck.TooShort;
        }

        return startMs == 0 && endMs == clipDurationMs ? TrimCheck.WholeClip : TrimCheck.Ok;
    }

    /// <summary>One line for the dialog, for example "Keeps 0:02.0 to 0:08.0 (6.0 s)".</summary>
    public static string Describe(ulong startMs, ulong endMs)
    {
        ulong length = endMs > startMs ? endMs - startMs : 0;
        return $"Keeps {Clock(startMs)} to {Clock(endMs)} ({length / 1000.0:0.0} s)";
    }

    /// <summary>m:ss with tenths: "0:02.5".</summary>
    public static string Clock(ulong ms)
    {
        ulong tenths = ms / 100;
        return $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}";
    }

    /// <summary>The message shown for a check that failed (empty for <see cref="TrimCheck.Ok"/>).</summary>
    public static string Explain(TrimCheck check) => check switch
    {
        TrimCheck.NotInOrder => "The start must come before the end.",
        TrimCheck.OutOfRange => "The end is past the end of the voice note.",
        TrimCheck.TooShort => "Keep at least a third of a second.",
        TrimCheck.WholeClip => "Move the start or end to cut something off.",
        _ => string.Empty,
    };
}
