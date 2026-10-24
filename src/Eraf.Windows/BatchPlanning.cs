namespace Eraf.Windows;

// Pure System.IO and logic: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>What to do when the ERAF file a photo would produce already exists.</summary>
public enum ExistingFilePolicy
{
    /// <summary>Leave the existing file alone and do not make a new one for that photo.</summary>
    Skip,

    /// <summary>Replace the existing file (atomically, so a failure leaves the old one).</summary>
    Replace,

    /// <summary>Keep both: the new file gets a number, "photo (2).eraf".</summary>
    KeepBoth,
}

/// <summary>Where batch results are written.</summary>
public enum OutputMode
{
    /// <summary>Beside each photo, named after it.</summary>
    NextToPhoto,

    /// <summary>All in one chosen folder.</summary>
    OneFolder,
}

public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>The user's options. Anything unreadable or unknown falls back to the default, so a damaged setting never breaks the app.</summary>
public sealed record AppOptions(
    ThemeChoice Theme,
    bool ScanSubfolders,
    ExistingFilePolicy ExistingFiles,
    OutputMode Output,
    string? OutputFolder)
{
    public static AppOptions Default { get; } = new(ThemeChoice.System, false, ExistingFilePolicy.Skip, OutputMode.NextToPhoto, null);

    public static AppOptions Load(Func<string, string?> get)
    {
        AppOptions d = Default;
        return new AppOptions(
            ParseEnum(get("Theme"), d.Theme),
            get("ScanSubfolders") is { } s ? s == "1" : d.ScanSubfolders,
            ParseEnum(get("ExistingFiles"), d.ExistingFiles),
            ParseEnum(get("Output"), d.Output),
            string.IsNullOrWhiteSpace(get("OutputFolder")) ? null : get("OutputFolder"));
    }

    public void Save(Action<string, string?> set)
    {
        set("Theme", Theme.ToString());
        set("ScanSubfolders", ScanSubfolders ? "1" : "0");
        set("ExistingFiles", ExistingFiles.ToString());
        set("Output", Output.ToString());
        set("OutputFolder", string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder);
    }

    private static T ParseEnum<T>(string? text, T fallback) where T : struct, Enum =>
        Enum.TryParse(text, ignoreCase: false, out T value) && Enum.IsDefined(value) ? value : fallback;
}

/// <summary>The photos found for a batch.</summary>
public sealed record ScanResult(IReadOnlyList<string> Photos, bool Truncated, int FoldersScanned);

/// <summary>Collects photo files from a mix of files and folders.</summary>
public static class PhotoScanner
{
    /// <summary>The most photos one batch will take, so a huge drive cannot be selected by accident.</summary>
    public const int DefaultMaxPhotos = 2000;

    /// <summary>
    /// Files that are photos are taken as they are; folders are searched for photos (and, when asked, their subfolders).
    /// Hidden and system items and folder links are skipped, unreadable folders are ignored, duplicates are removed,
    /// and the result is sorted by path. Stops at <paramref name="maxPhotos"/> and says so.
    /// </summary>
    public static ScanResult Scan(IEnumerable<string> paths, bool includeSubfolders, int maxPhotos, CancellationToken cancel = default)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int folders = 0;
        bool truncated = false;

        foreach (string path in paths)
        {
            cancel.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                if (FileKinds.ClassifyFile(path) == EntryKind.Photo && !Add(found, Path.GetFullPath(path), maxPhotos, ref truncated))
                {
                    break;
                }

                continue;
            }

            if (!Directory.Exists(path))
            {
                continue;
            }

            var pending = new Stack<string>();
            pending.Push(Path.GetFullPath(path));
            while (pending.Count > 0 && !truncated)
            {
                cancel.ThrowIfCancellationRequested();
                string folder = pending.Pop();
                folders++;
                try
                {
                    foreach (FileSystemInfo item in new DirectoryInfo(folder).EnumerateFileSystemInfos())
                    {
                        if ((item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                        {
                            continue;
                        }

                        if (item is DirectoryInfo sub)
                        {
                            if (includeSubfolders && (sub.Attributes & FileAttributes.ReparsePoint) == 0)
                            {
                                pending.Push(sub.FullName);
                            }
                        }
                        else if (FileKinds.ClassifyFile(item.Name) == EntryKind.Photo && !Add(found, item.FullName, maxPhotos, ref truncated))
                        {
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    // An unreadable folder is skipped; the rest of the scan carries on.
                }
            }

            if (truncated)
            {
                break;
            }
        }

        return new ScanResult(found.ToList(), truncated, folders);
    }

    private static bool Add(SortedSet<string> found, string path, int max, ref bool truncated)
    {
        if (found.Count >= max && !found.Contains(path))
        {
            truncated = true;
            return false;
        }

        found.Add(path);
        return true;
    }
}

public enum BatchAction
{
    Create,
    Replace,
    Skip,
}

/// <summary>One planned output: which photo, where its ERAF file goes, and whether it is made, replaced or skipped.</summary>
public sealed record BatchTarget(string Source, string Target, BatchAction Action);

/// <summary>Works out the output name for every photo in a batch.</summary>
public static class BatchTargets
{
    /// <param name="exists">Whether a file already exists (injected so the rules can be tested without a disk).</param>
    public static List<BatchTarget> Plan(
        IReadOnlyList<string> photos,
        OutputMode mode,
        string? outputFolder,
        ExistingFilePolicy policy,
        Func<string, bool> exists)
    {
        bool oneFolder = mode == OutputMode.OneFolder && !string.IsNullOrWhiteSpace(outputFolder);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<BatchTarget>(photos.Count);

        foreach (string photo in photos)
        {
            string folder = oneFolder ? outputFolder! : Path.GetDirectoryName(photo) ?? string.Empty;
            string stem = Path.GetFileNameWithoutExtension(photo);
            string target = Path.Combine(folder, stem + FileKinds.ErafExtension);

            if (claimed.Contains(target))
            {
                // Two photos in this batch want the same name: the later one always gets a number, whatever the policy.
                plan.Add(new BatchTarget(photo, Numbered(folder, stem, claimed, exists), BatchAction.Create));
            }
            else if (!exists(target))
            {
                plan.Add(new BatchTarget(photo, target, BatchAction.Create));
            }
            else
            {
                plan.Add(policy switch
                {
                    ExistingFilePolicy.Skip => new BatchTarget(photo, target, BatchAction.Skip),
                    ExistingFilePolicy.Replace => new BatchTarget(photo, target, BatchAction.Replace),
                    _ => new BatchTarget(photo, Numbered(folder, stem, claimed, exists), BatchAction.Create),
                });
            }

            claimed.Add(plan[^1].Target);
        }

        return plan;
    }

    private static string Numbered(string folder, string stem, HashSet<string> claimed, Func<string, bool> exists)
    {
        for (int n = 2; ; n++)
        {
            string candidate = Path.Combine(folder, $"{stem} ({n}){FileKinds.ErafExtension}");
            if (!claimed.Contains(candidate) && !exists(candidate))
            {
                return candidate;
            }
        }
    }
}

/// <summary>What a batch did, in words the user can read.</summary>
public sealed record BatchSummary(int Created, int Replaced, int Skipped, IReadOnlyList<(string Name, string Reason)> Failed, bool Cancelled)
{
    public int Failures => Failed.Count;

    public string Headline
    {
        get
        {
            var parts = new List<string>();
            int made = Created + Replaced;
            parts.Add(made == 1 ? "1 ERAF file made" : $"{made} ERAF files made");
            if (Replaced > 0)
            {
                parts.Add($"{Replaced} replaced");
            }

            if (Skipped > 0)
            {
                parts.Add($"{Skipped} skipped because the file already existed");
            }

            if (Failures > 0)
            {
                parts.Add($"{Failures} could not be made");
            }

            string text = string.Join(", ", parts) + ".";
            return Cancelled ? "Stopped early: " + text : text;
        }
    }
}
