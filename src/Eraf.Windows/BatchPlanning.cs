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

/// <summary>The look of the app. <see cref="System"/> follows Windows; the others are colour themes (see <see cref="ThemePalettes"/>).</summary>
public enum ThemeChoice
{
    System,
    TealConsole,
    OfficeBlue,
    Slate,
    ForestGreen,
    Violet,
    Amber,
    Midnight,
}

/// <summary>
/// How a theme looks: the light or dark base, the accent colour of buttons and ticks, and an optional window background
/// (colours are 0xRRGGBB). <see cref="ThemeChoice.System"/> has none of these: Windows decides.
/// </summary>
public sealed record ThemePalette(ThemeChoice Choice, string Label, bool Dark, uint? Accent, uint? Background)
{
    /// <summary>Text on an accent-coloured button: white on the darker accents of light themes, black on the lighter accents of dark ones.</summary>
    public uint TextOnAccent => Dark ? 0x000000u : 0xFFFFFFu;
}

/// <summary>The theme list, in the order it is shown.</summary>
public static class ThemePalettes
{
    public static IReadOnlyList<ThemePalette> All { get; } = new ThemePalette[]
    {
        new(ThemeChoice.System, "System (follow Windows)", false, null, null),
        new(ThemeChoice.TealConsole, "Teal console", true, 0x2DD4BF, 0x0B1D1F),
        new(ThemeChoice.OfficeBlue, "Office blue", false, 0x2B579A, 0xEEF3FA),
        new(ThemeChoice.Slate, "Slate", true, 0x8FA8C8, 0x232B36),
        new(ThemeChoice.ForestGreen, "Forest green", false, 0x2E7D32, 0xEEF5EE),
        new(ThemeChoice.Violet, "Violet", false, 0x6B46C1, 0xF3EFFA),
        new(ThemeChoice.Amber, "Amber", false, 0xB45309, 0xFFF6E5),
        new(ThemeChoice.Midnight, "Midnight", true, 0x6C8CFF, 0x0A1024),
    };

    public static ThemePalette For(ThemeChoice choice) => All.First(p => p.Choice == choice);

    public static int IndexOf(ThemeChoice choice) => Math.Max(0, All.ToList().FindIndex(p => p.Choice == choice));

    /// <summary>
    /// The accent in the six shades Windows keeps for it (three lighter, three darker). Light themes colour buttons with the
    /// first darker shade and dark themes with the second lighter one, so those are the shades the readability checks use.
    /// </summary>
    public static (uint Light1, uint Light2, uint Light3, uint Dark1, uint Dark2, uint Dark3) AccentShades(uint accent) => (
        Mix(accent, 0xFFFFFF, 0.15), Mix(accent, 0xFFFFFF, 0.30), Mix(accent, 0xFFFFFF, 0.45),
        Mix(accent, 0x000000, 0.15), Mix(accent, 0x000000, 0.30), Mix(accent, 0x000000, 0.45));

    /// <summary>The colour buttons are really filled with in this theme.</summary>
    public static uint ButtonFill(ThemePalette palette)
    {
        var shades = AccentShades(palette.Accent ?? 0);
        return palette.Dark ? shades.Light2 : shades.Dark1;
    }

    private static uint Mix(uint from, uint to, double amount)
    {
        static uint Channel(uint a, uint b, double t, int shift) =>
            (uint)Math.Round(((a >> shift) & 0xFF) * (1 - t) + ((b >> shift) & 0xFF) * t);

        return (Channel(from, to, amount, 16) << 16) | (Channel(from, to, amount, 8) << 8) | Channel(from, to, amount, 0);
    }

    /// <summary>WCAG contrast ratio between two colours (1 to 21); 4.5 or more is comfortable for text.</summary>
    public static double Contrast(uint a, uint b)
    {
        double la = Luminance(a), lb = Luminance(b);
        return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
    }

    private static double Luminance(uint rgb)
    {
        static double Channel(uint v)
        {
            double c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return 0.2126 * Channel((rgb >> 16) & 0xFF) + 0.7152 * Channel((rgb >> 8) & 0xFF) + 0.0722 * Channel(rgb & 0xFF);
    }
}

/// <summary>The user's options. Anything unreadable or unknown falls back to the default, so a damaged setting never breaks the app.</summary>
public sealed record AppOptions(
    ThemeChoice Theme,
    bool ScanSubfolders,
    ExistingFilePolicy ExistingFiles,
    OutputMode Output,
    string? OutputFolder,
    bool ShowCheckboxes = true,
    ViewMode ListMode = ViewMode.Details,
    bool OfferScreenshots = true,
    bool GlobalHotkey = true)
{
    public static AppOptions Default { get; } = new(ThemeChoice.System, false, ExistingFilePolicy.Skip, OutputMode.NextToPhoto, null, true, ViewMode.Details, true, true);

    public static AppOptions Load(Func<string, string?> get)
    {
        AppOptions d = Default;
        return new AppOptions(
            ParseTheme(get("Theme"), d.Theme),
            get("ScanSubfolders") is { } s ? s == "1" : d.ScanSubfolders,
            ParseEnum(get("ExistingFiles"), d.ExistingFiles),
            ParseEnum(get("Output"), d.Output),
            string.IsNullOrWhiteSpace(get("OutputFolder")) ? null : get("OutputFolder"),
            get("ShowCheckboxes") is { } c ? c != "0" : d.ShowCheckboxes,
            ParseEnum(get("ListMode"), d.ListMode),
            get("OfferScreenshots") is { } o ? o != "0" : d.OfferScreenshots,
            get("GlobalHotkey") is { } g ? g != "0" : d.GlobalHotkey);
    }

    public void Save(Action<string, string?> set)
    {
        set("Theme", Theme.ToString());
        set("ScanSubfolders", ScanSubfolders ? "1" : "0");
        set("ExistingFiles", ExistingFiles.ToString());
        set("Output", Output.ToString());
        set("OutputFolder", string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder);
        set("ShowCheckboxes", ShowCheckboxes ? "1" : "0");
        set("ListMode", ListMode.ToString());
        set("OfferScreenshots", OfferScreenshots ? "1" : "0");
        set("GlobalHotkey", GlobalHotkey ? "1" : "0");
    }

    /// <summary>Earlier versions stored "Light" and "Dark"; they become the nearest colour themes.</summary>
    private static ThemeChoice ParseTheme(string? text, ThemeChoice fallback) => text switch
    {
        "Light" => ThemeChoice.OfficeBlue,
        "Dark" => ThemeChoice.Midnight,
        _ => ParseEnum(text, fallback),
    };

    private static T ParseEnum<T>(string? text, T fallback) where T : struct, Enum =>
        Enum.TryParse(text, ignoreCase: false, out T value) && Enum.IsDefined(value) ? value : fallback;
}

/// <summary>The photos found for a batch.</summary>
public sealed record ScanResult(IReadOnlyList<string> Photos, bool Truncated, int FoldersScanned);

/// <summary>How far a scan has got: shown to the user while a large folder is searched.</summary>
public sealed record ScanProgress(int FoldersScanned, int PhotosFound, int EntriesSeen);

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
    public static ScanResult Scan(
        IEnumerable<string> paths, bool includeSubfolders, int maxPhotos,
        CancellationToken cancel = default, IProgress<ScanProgress>? progress = null)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        int folders = 0;
        int entries = 0;
        bool truncated = false;
        void Report() => progress?.Report(new ScanProgress(folders, found.Count, entries));

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
                Report();
                try
                {
                    foreach (FileSystemInfo item in new DirectoryInfo(folder).EnumerateFileSystemInfos())
                    {
                        // A single huge folder must stay stoppable and visible too, so check in and report as entries go by.
                        if (++entries % ReportEvery == 0)
                        {
                            cancel.ThrowIfCancellationRequested();
                            Report();
                        }

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
                catch (OperationCanceledException)
                {
                    throw; // Stop must stop, not be mistaken for an unreadable folder.
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

        Report();
        return new ScanResult(found.ToList(), truncated, folders);
    }

    /// <summary>Progress is reported at every folder and after this many entries inside one.</summary>
    public const int ReportEvery = 200;

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

public enum DropKind
{
    /// <summary>Nothing usable was dropped; <see cref="DropPlan.Message"/> says why.</summary>
    Nothing,

    /// <summary>One ERAF file: open it.</summary>
    OpenEraf,

    /// <summary>One photo: start an ERAF with it (the normal editor).</summary>
    SinglePhoto,

    /// <summary>Several photos and/or folders: the batch flow with one shared voice note.</summary>
    Batch,
}

/// <summary>What dropping these items on the window should do.</summary>
public sealed record DropPlan(DropKind Kind, IReadOnlyList<string> Paths, int Ignored, bool AnyFolder, string? Message);

/// <summary>Decides what a drop means. It is the same as selecting the items in the list and pressing Make ERAF.</summary>
public static class DropPlanner
{
    public static DropPlan Plan(IEnumerable<string> dropped)
    {
        var usable = new List<string>();      // photos and folders
        var erafs = new List<string>();
        int ignored = 0;
        bool anyFolder = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string raw in dropped)
        {
            if (string.IsNullOrWhiteSpace(raw) || !seen.Add(raw))
            {
                continue;
            }

            if (Directory.Exists(raw))
            {
                usable.Add(raw);
                anyFolder = true;
            }
            else if (File.Exists(raw) && FileKinds.ClassifyFile(raw) is { } kind)
            {
                (kind == EntryKind.Eraf ? erafs : usable).Add(raw);
            }
            else
            {
                ignored++;
            }
        }

        if (usable.Count == 0)
        {
            return erafs.Count switch
            {
                1 when ignored == 0 => new DropPlan(DropKind.OpenEraf, erafs, 0, false, null),
                0 => new DropPlan(DropKind.Nothing, usable, ignored, false, "That is not a photo, a folder or an ERAF file."),
                _ => new DropPlan(DropKind.Nothing, usable, ignored + erafs.Count, false, "Drop one ERAF file at a time to open it."),
            };
        }

        int skipped = ignored + erafs.Count;
        return usable.Count == 1 && !anyFolder
            ? new DropPlan(DropKind.SinglePhoto, usable, skipped, false, null)
            : new DropPlan(DropKind.Batch, usable, skipped, anyFolder, null);
    }
}
