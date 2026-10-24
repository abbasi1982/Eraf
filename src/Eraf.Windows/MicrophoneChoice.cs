namespace Eraf.Windows;

// Pure logic: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>One entry in the microphone list. A null <see cref="Id"/> means "use the Windows default microphone".</summary>
public sealed record MicrophoneOption(string? Id, string Name);

/// <summary>Builds the dropdown list and decides which entry is selected.</summary>
public static class MicrophoneChoice
{
    public const string DefaultName = "Default microphone";

    /// <summary>The Windows default first, then each distinct device in the order given. Unnamed devices get a number.</summary>
    public static IReadOnlyList<MicrophoneOption> Build(IEnumerable<(string Id, string Name)> devices)
    {
        var list = new List<MicrophoneOption> { new(null, DefaultName) };
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int unnamed = 0;
        foreach ((string id, string name) in devices)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                continue;
            }

            string label = string.IsNullOrWhiteSpace(name) ? $"Microphone {++unnamed}" : name.Trim();
            list.Add(new MicrophoneOption(id, label));
        }

        return list;
    }

    /// <summary>
    /// The remembered device if it is still present, otherwise the default entry (index 0).
    /// <paramref name="options"/> must come from <see cref="Build"/>.
    /// </summary>
    public static MicrophoneOption Resolve(IReadOnlyList<MicrophoneOption> options, string? preferredId)
    {
        if (preferredId is not null)
        {
            foreach (MicrophoneOption option in options)
            {
                if (option.Id == preferredId)
                {
                    return option;
                }
            }
        }

        return options[0];
    }

    /// <summary>True when both lists show the same devices in the same order (so the UI need not be rebuilt).</summary>
    public static bool SameList(IReadOnlyList<MicrophoneOption> a, IReadOnlyList<MicrophoneOption> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }
}
