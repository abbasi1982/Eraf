using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>Options, folder scanning and output naming for "one voice note for many photos".</summary>
public sealed class BatchPlanningTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-batch-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try
        {
            foreach (string f in Directory.GetFiles(_dir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Make(string relative, int size = 3)
    {
        string path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    private string[] Names(IEnumerable<string> paths) =>
        paths.Select(p => Path.GetRelativePath(_dir, p).Replace('\\', '/')).ToArray();

    // ---------------------------------------------------------------- options

    [Fact]
    public void DefaultsAreSafe()
    {
        AppOptions d = AppOptions.Default;

        Assert.Equal(ThemeChoice.System, d.Theme);
        Assert.False(d.ScanSubfolders);
        Assert.Equal(ExistingFilePolicy.Skip, d.ExistingFiles);
        Assert.Equal(OutputMode.NextToPhoto, d.Output);
        Assert.Null(d.OutputFolder);
        Assert.True(d.ShowCheckboxes);
        Assert.Equal(ViewMode.Details, d.ListMode);
    }

    [Fact]
    public void OptionsSurviveASaveAndLoad()
    {
        var store = new Dictionary<string, string?>();
        var chosen = new AppOptions(ThemeChoice.Midnight, true, ExistingFilePolicy.KeepBoth, OutputMode.OneFolder, @"D:\Out", ShowCheckboxes: false, ListMode: ViewMode.Tiles);

        chosen.Save((k, v) => store[k] = v);
        AppOptions loaded = AppOptions.Load(k => store.GetValueOrDefault(k));

        Assert.Equal(chosen, loaded);
    }

    [Fact]
    public void MissingOrDamagedSettingsFallBackToDefaults()
    {
        var store = new Dictionary<string, string?>
        {
            ["Theme"] = "Purple",
            ["ExistingFiles"] = "99",
            ["Output"] = "",
            ["ScanSubfolders"] = "maybe",
            ["OutputFolder"] = "  ",
            ["ShowCheckboxes"] = "0",
            ["ListMode"] = "Huge",
        };

        AppOptions loaded = AppOptions.Load(k => store.GetValueOrDefault(k));

        Assert.Equal(ThemeChoice.System, loaded.Theme);
        Assert.Equal(ExistingFilePolicy.Skip, loaded.ExistingFiles);
        Assert.Equal(OutputMode.NextToPhoto, loaded.Output);
        Assert.False(loaded.ScanSubfolders);
        Assert.Null(loaded.OutputFolder);
        Assert.False(loaded.ShowCheckboxes);
        Assert.Equal(ViewMode.Details, loaded.ListMode);
        Assert.Equal(AppOptions.Default, AppOptions.Load(_ => null));
    }

    [Theory]
    [InlineData("Light", ThemeChoice.OfficeBlue)]
    [InlineData("Dark", ThemeChoice.Midnight)]
    [InlineData("Amber", ThemeChoice.Amber)]
    [InlineData("Purple", ThemeChoice.System)]
    public void OldAndNewThemeNamesAreUnderstood(string stored, ThemeChoice expected)
    {
        var store = new Dictionary<string, string?> { ["Theme"] = stored };

        Assert.Equal(expected, AppOptions.Load(k => store.GetValueOrDefault(k)).Theme);
    }

    [Fact]
    public void TheThemeListIsTheAgreedOneInTheAgreedOrder()
    {
        Assert.Equal(
            new[] { "System (follow Windows)", "Teal console", "Office blue", "Slate", "Forest green", "Violet", "Amber", "Midnight" },
            ThemePalettes.All.Select(p => p.Label));
        Assert.Equal(Enum.GetValues<ThemeChoice>().Length, ThemePalettes.All.Count);
        Assert.Equal(ThemePalettes.All.Count, ThemePalettes.All.Select(p => p.Choice).Distinct().Count());
        Assert.All(Enum.GetValues<ThemeChoice>(), c => Assert.Equal(c, ThemePalettes.For(c).Choice));
        Assert.All(ThemePalettes.All, p => Assert.Equal(p.Choice, ThemePalettes.All[ThemePalettes.IndexOf(p.Choice)].Choice));
    }

    [Fact]
    public void SystemFollowsWindowsAndAddsNoColours()
    {
        ThemePalette system = ThemePalettes.For(ThemeChoice.System);

        Assert.Null(system.Accent);
        Assert.Null(system.Background);
    }

    [Fact]
    public void EveryColourThemeIsComfortablyReadable()
    {
        foreach (ThemePalette p in ThemePalettes.All.Where(p => p.Choice != ThemeChoice.System))
        {
            Assert.NotNull(p.Accent);
            Assert.NotNull(p.Background);
            // The colour buttons really get (a shade of the accent), the text on it, that colour against the window, and
            // the normal text of the light or dark base against the window.
            uint fill = ThemePalettes.ButtonFill(p);
            Assert.True(ThemePalettes.Contrast(fill, p.TextOnAccent) >= 4.5, p.Label + " text on buttons");
            Assert.True(ThemePalettes.Contrast(fill, p.Background!.Value) >= 3.0, p.Label + " buttons against the window");
            uint text = p.Dark ? 0xFFFFFFu : 0x1B1B1Bu;
            Assert.True(ThemePalettes.Contrast(text, p.Background!.Value) >= 7.0, p.Label + " text");
        }
    }

    [Fact]
    public void AccentShadesGetLighterAndDarkerInSteps()
    {
        var s = ThemePalettes.AccentShades(0x336699);

        Assert.True(ThemePalettes.Contrast(s.Light1, 0xFFFFFF) < ThemePalettes.Contrast(0x336699, 0xFFFFFF));
        Assert.True(ThemePalettes.Contrast(s.Light3, 0xFFFFFF) < ThemePalettes.Contrast(s.Light2, 0xFFFFFF));
        Assert.True(ThemePalettes.Contrast(s.Dark1, 0xFFFFFF) > ThemePalettes.Contrast(0x336699, 0xFFFFFF));
        Assert.True(ThemePalettes.Contrast(s.Dark3, 0xFFFFFF) > ThemePalettes.Contrast(s.Dark2, 0xFFFFFF));
        Assert.Equal(0xFFFFFFu, ThemePalettes.AccentShades(0xFFFFFF).Light3);
        Assert.Equal(0x000000u, ThemePalettes.AccentShades(0x000000).Dark3);
    }

    [Fact]
    public void ContrastMatchesKnownValues()
    {
        Assert.Equal(21.0, ThemePalettes.Contrast(0x000000, 0xFFFFFF), 1);
        Assert.Equal(1.0, ThemePalettes.Contrast(0x777777, 0x777777), 3);
    }

    [Fact]
    public void AClearedOutputFolderIsRemoved()
    {
        var store = new Dictionary<string, string?> { ["OutputFolder"] = @"D:\Old" };
        AppOptions.Default.Save((k, v) => store[k] = v);

        Assert.Null(store["OutputFolder"]);
    }

    // ---------------------------------------------------------------- scanning

    [Fact]
    public void AFolderGivesItsPhotosOnlyAndNotItsSubfoldersByDefault()
    {
        Make("a.jpg"); Make("b.PNG"); Make("notes.txt"); Make("made.eraf"); Make(@"sub\c.jpg");

        ScanResult result = PhotoScanner.Scan(new[] { _dir }, includeSubfolders: false, PhotoScanner.DefaultMaxPhotos);

        Assert.Equal(new[] { "a.jpg", "b.PNG" }, Names(result.Photos));
        Assert.False(result.Truncated);
    }

    [Fact]
    public void SubfoldersAreIncludedWhenAsked()
    {
        Make("a.jpg"); Make(@"sub\c.jpg"); Make(@"sub\deeper\d.tif");

        ScanResult result = PhotoScanner.Scan(new[] { _dir }, includeSubfolders: true, PhotoScanner.DefaultMaxPhotos);

        Assert.Equal(new[] { "a.jpg", "sub/c.jpg", "sub/deeper/d.tif" }, Names(result.Photos));
        Assert.Equal(3, result.FoldersScanned);
    }

    [Fact]
    public void FilesAndFoldersMixAndDuplicatesAreRemoved()
    {
        string a = Make("one" + Path.DirectorySeparatorChar + "a.jpg");
        Make(@"one\b.jpg");
        string loose = Make(@"two\z.png");
        Make(@"two\notaphoto.txt");

        ScanResult result = PhotoScanner.Scan(
            new[] { Path.Combine(_dir, "one"), a, loose, Path.Combine(_dir, "two", "notaphoto.txt") },
            includeSubfolders: false, PhotoScanner.DefaultMaxPhotos);

        Assert.Equal(new[] { "one/a.jpg", "one/b.jpg", "two/z.png" }, Names(result.Photos));
    }

    [Fact]
    public void HiddenItemsMissingPathsAndUnreadableFoldersAreIgnored()
    {
        string hidden = Make("hidden.jpg");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        Make("seen.jpg");
        string hiddenFolder = Directory.CreateDirectory(Path.Combine(_dir, "secret")).FullName;
        File.WriteAllBytes(Path.Combine(hiddenFolder, "inside.jpg"), new byte[1]);
        File.SetAttributes(hiddenFolder, FileAttributes.Hidden | FileAttributes.Directory);

        ScanResult result = PhotoScanner.Scan(
            new[] { _dir, Path.Combine(_dir, "does-not-exist") }, includeSubfolders: true, PhotoScanner.DefaultMaxPhotos);

        Assert.Equal(new[] { "seen.jpg" }, Names(result.Photos));
    }

    [Fact]
    public void TheCountIsCappedAndSaidSo()
    {
        for (int i = 0; i < 10; i++)
        {
            Make($"p{i:00}.jpg");
        }

        ScanResult result = PhotoScanner.Scan(new[] { _dir }, includeSubfolders: false, maxPhotos: 4);

        Assert.Equal(4, result.Photos.Count);
        Assert.True(result.Truncated);
        Assert.False(PhotoScanner.Scan(new[] { _dir }, false, 10).Truncated);
    }

    [Fact]
    public void ScanReportsProgressAndTheLastReportMatchesTheResult()
    {
        for (int i = 0; i < 3; i++)
        {
            Make($"f{i}\\p.jpg");
        }

        var reports = new List<ScanProgress>();
        ScanResult result = PhotoScanner.Scan(new[] { _dir }, true, 100, default, new SyncProgress(reports.Add));

        Assert.NotEmpty(reports);
        Assert.Equal(result.Photos.Count, reports[^1].PhotosFound);
        Assert.Equal(result.FoldersScanned, reports[^1].FoldersScanned);
        for (int i = 1; i < reports.Count; i++)
        {
            Assert.True(reports[i].FoldersScanned >= reports[i - 1].FoldersScanned);
            Assert.True(reports[i].PhotosFound >= reports[i - 1].PhotosFound);
            Assert.True(reports[i].EntriesSeen >= reports[i - 1].EntriesSeen);
        }
    }

    [Fact]
    public void AHugeFolderIsReportedAndStoppableWhileItIsBeingRead()
    {
        for (int i = 0; i < PhotoScanner.ReportEvery * 3; i++)
        {
            Make($"big\\n{i:0000}.txt", 1);
        }

        var reports = new List<ScanProgress>();
        ScanResult result = PhotoScanner.Scan(new[] { Path.Combine(_dir, "big") }, false, 100, default, new SyncProgress(reports.Add));

        Assert.Empty(result.Photos);
        Assert.True(reports.Count >= 4, "reports during one folder: " + reports.Count);
        Assert.Equal(PhotoScanner.ReportEvery * 3, reports[^1].EntriesSeen);

        using var cts = new CancellationTokenSource();
        var stopAtFirst = new SyncProgress(p => { if (p.EntriesSeen > 0) { cts.Cancel(); } });
        Assert.Throws<OperationCanceledException>(
            () => PhotoScanner.Scan(new[] { Path.Combine(_dir, "big") }, false, 100, cts.Token, stopAtFirst));
    }

    /// <summary>Reports on the calling thread at once (Progress&lt;T&gt; would post them later, which a test cannot wait for).</summary>
    private sealed class SyncProgress : IProgress<ScanProgress>
    {
        private readonly Action<ScanProgress> _onReport;

        public SyncProgress(Action<ScanProgress> onReport) => _onReport = onReport;

        public void Report(ScanProgress value) => _onReport(value);
    }

    [Fact]
    public void ACancelledScanThrows()
    {
        Make("a.jpg");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => PhotoScanner.Scan(new[] { _dir }, true, 10, cts.Token));
    }

    // ---------------------------------------------------------------- output names

    private static readonly Func<string, bool> Nothing = _ => false;

    [Fact]
    public void ErafFilesGoBesideEachPhotoNamedAfterIt()
    {
        var plan = BatchTargets.Plan(
            new[] { @"C:\p\a.jpg", @"C:\q\b.png" }, OutputMode.NextToPhoto, null, ExistingFilePolicy.Skip, Nothing);

        Assert.Equal(new[] { @"C:\p\a.eraf", @"C:\q\b.eraf" }, plan.Select(t => t.Target));
        Assert.All(plan, t => Assert.Equal(BatchAction.Create, t.Action));
    }

    [Fact]
    public void OneFolderModePutsEverythingInThatFolder()
    {
        var plan = BatchTargets.Plan(
            new[] { @"C:\p\a.jpg", @"C:\q\b.png" }, OutputMode.OneFolder, @"D:\out", ExistingFilePolicy.Skip, Nothing);

        Assert.Equal(new[] { @"D:\out\a.eraf", @"D:\out\b.eraf" }, plan.Select(t => t.Target));
    }

    [Fact]
    public void OneFolderModeWithoutAFolderFallsBackToBesideThePhoto()
    {
        var plan = BatchTargets.Plan(new[] { @"C:\p\a.jpg" }, OutputMode.OneFolder, "  ", ExistingFilePolicy.Skip, Nothing);

        Assert.Equal(@"C:\p\a.eraf", plan[0].Target);
    }

    [Theory]
    [InlineData(ExistingFilePolicy.Skip, BatchAction.Skip, @"C:\p\a.eraf")]
    [InlineData(ExistingFilePolicy.Replace, BatchAction.Replace, @"C:\p\a.eraf")]
    [InlineData(ExistingFilePolicy.KeepBoth, BatchAction.Create, @"C:\p\a (2).eraf")]
    public void AnExistingFileIsHandledByThePolicy(ExistingFilePolicy policy, BatchAction action, string target)
    {
        var plan = BatchTargets.Plan(
            new[] { @"C:\p\a.jpg" }, OutputMode.NextToPhoto, null, policy, p => p == @"C:\p\a.eraf");

        Assert.Equal(action, plan[0].Action);
        Assert.Equal(target, plan[0].Target);
    }

    [Fact]
    public void KeepBothSkipsNumbersThatAreAlreadyTaken()
    {
        var taken = new HashSet<string> { @"C:\p\a.eraf", @"C:\p\a (2).eraf", @"C:\p\a (3).eraf" };

        var plan = BatchTargets.Plan(new[] { @"C:\p\a.jpg" }, OutputMode.NextToPhoto, null, ExistingFilePolicy.KeepBoth, taken.Contains);

        Assert.Equal(@"C:\p\a (4).eraf", plan[0].Target);
    }

    [Theory]
    [InlineData(ExistingFilePolicy.Skip)]
    [InlineData(ExistingFilePolicy.Replace)]
    [InlineData(ExistingFilePolicy.KeepBoth)]
    public void TwoPhotosWantingTheSameNameInOneBatchNeverOverwriteEachOther(ExistingFilePolicy policy)
    {
        var plan = BatchTargets.Plan(
            new[] { @"C:\p\a.jpg", @"C:\p\a.png", @"C:\q\a.jpg" }, OutputMode.OneFolder, @"D:\out", policy, Nothing);

        Assert.Equal(new[] { @"D:\out\a.eraf", @"D:\out\a (2).eraf", @"D:\out\a (3).eraf" }, plan.Select(t => t.Target));
        Assert.Equal(3, plan.Select(t => t.Target).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    // ---------------------------------------------------------------- summary

    [Fact]
    public void TheHeadlineSaysWhatHappened()
    {
        Assert.Equal("1 ERAF file made.", new BatchSummary(1, 0, 0, Array.Empty<(string, string)>(), false).Headline);
        Assert.Equal(
            "5 ERAF files made, 2 replaced, 3 skipped because the file already existed, 1 could not be made.",
            new BatchSummary(3, 2, 3, new[] { ("x.jpg", "unreadable") }, false).Headline);
        Assert.StartsWith("Stopped early: ", new BatchSummary(1, 0, 0, Array.Empty<(string, string)>(), true).Headline);
    }
}
