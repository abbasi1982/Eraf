using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>The file manager's pure parts: which files are listed, sorting, filtering, history and folder reading.</summary>
public sealed class FileBrowsingTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-browse-" + Guid.NewGuid().ToString("N"))).FullName;

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

    private static BrowserEntry Entry(string name, EntryKind kind, long size = 0, int day = 1) =>
        new(name, @"C:\x\" + name, kind, size, new DateTime(2026, 1, day));

    // ---------------------------------------------------------------- classification

    [Theory]
    [InlineData("a.eraf", EntryKind.Eraf)]
    [InlineData("A.ERAF", EntryKind.Eraf)]
    [InlineData("p.jpg", EntryKind.Photo)]
    [InlineData("p.JPEG", EntryKind.Photo)]
    [InlineData("p.png", EntryKind.Photo)]
    [InlineData("p.bmp", EntryKind.Photo)]
    [InlineData("p.gif", EntryKind.Photo)]
    [InlineData("p.tif", EntryKind.Photo)]
    [InlineData("p.TIFF", EntryKind.Photo)]
    public void SupportedFilesAreClassifiedByExtensionIgnoringCase(string name, EntryKind expected) =>
        Assert.Equal(expected, FileKinds.ClassifyFile(name));

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("movie.mp4")]
    [InlineData("noextension")]
    [InlineData("archive.eraf.zip")]
    [InlineData("photo.jpg.exe")]
    public void OtherFilesAreNotListed(string name) =>
        Assert.Null(FileKinds.ClassifyFile(name));

    [Fact]
    public void EveryKindHasALabel()
    {
        foreach (EntryKind kind in Enum.GetValues<EntryKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(FileKinds.TypeLabel(kind)));
        }
    }

    // ---------------------------------------------------------------- sorting and filtering

    [Fact]
    public void FoldersAlwaysComeFirstInEveryOrder()
    {
        var entries = new[]
        {
            Entry("b.jpg", EntryKind.Photo, 5), Entry("Zeta", EntryKind.Folder), Entry("a.eraf", EntryKind.Eraf, 9), Entry("alpha", EntryKind.Folder),
        };

        foreach (SortKey key in Enum.GetValues<SortKey>())
        {
            foreach (bool ascending in new[] { true, false })
            {
                var sorted = BrowserList.Sort(entries, key, ascending);
                Assert.Equal(EntryKind.Folder, sorted[0].Kind);
                Assert.Equal(EntryKind.Folder, sorted[1].Kind);
                Assert.NotEqual(EntryKind.Folder, sorted[2].Kind);
            }
        }
    }

    [Fact]
    public void NameSortIgnoresCaseAndCanBeReversed()
    {
        var entries = new[] { Entry("b.jpg", EntryKind.Photo), Entry("A.jpg", EntryKind.Photo), Entry("c.jpg", EntryKind.Photo) };

        Assert.Equal(new[] { "A.jpg", "b.jpg", "c.jpg" }, BrowserList.Sort(entries, SortKey.Name, true).Select(e => e.Name));
        Assert.Equal(new[] { "c.jpg", "b.jpg", "A.jpg" }, BrowserList.Sort(entries, SortKey.Name, false).Select(e => e.Name));
    }

    [Fact]
    public void SizeAndModifiedSortsUseTheirOwnKeyWithNameAsTieBreaker()
    {
        var entries = new[]
        {
            Entry("b.jpg", EntryKind.Photo, size: 10, day: 3),
            Entry("a.jpg", EntryKind.Photo, size: 10, day: 1),
            Entry("c.jpg", EntryKind.Photo, size: 5, day: 2),
        };

        Assert.Equal(new[] { "c.jpg", "a.jpg", "b.jpg" }, BrowserList.Sort(entries, SortKey.Size, true).Select(e => e.Name));
        Assert.Equal(new[] { "a.jpg", "b.jpg", "c.jpg" }, BrowserList.Sort(entries, SortKey.Size, false).Select(e => e.Name).OrderBy(n => n));
        Assert.Equal(new[] { "a.jpg", "c.jpg", "b.jpg" }, BrowserList.Sort(entries, SortKey.Modified, true).Select(e => e.Name));
        Assert.Equal(new[] { "b.jpg", "c.jpg", "a.jpg" }, BrowserList.Sort(entries, SortKey.Modified, false).Select(e => e.Name));
    }

    [Fact]
    public void TypeSortGroupsErafBeforePhotos()
    {
        var entries = new[] { Entry("z.jpg", EntryKind.Photo), Entry("a.jpg", EntryKind.Photo), Entry("m.eraf", EntryKind.Eraf) };

        Assert.Equal(new[] { "m.eraf", "a.jpg", "z.jpg" }, BrowserList.Sort(entries, SortKey.Type, true).Select(e => e.Name));
    }

    [Fact]
    public void SortDoesNotChangeTheInput()
    {
        var entries = new[] { Entry("b.jpg", EntryKind.Photo), Entry("a.jpg", EntryKind.Photo) };
        BrowserList.Sort(entries, SortKey.Name, true);

        Assert.Equal("b.jpg", entries[0].Name);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("", 3)]
    [InlineData("   ", 3)]
    [InlineData("holiday", 2)]
    [InlineData("HOLIDAY", 2)]
    [InlineData(".eraf", 1)]
    [InlineData("nothing", 0)]
    public void FilterMatchesAnyPartOfTheNameIgnoringCase(string? text, int expected)
    {
        var entries = new[]
        {
            Entry("Holiday 1.jpg", EntryKind.Photo), Entry("holiday.eraf", EntryKind.Eraf), Entry("Work", EntryKind.Folder),
        };

        Assert.Equal(expected, BrowserList.Filter(entries, text).Count);
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(512L, "512 B")]
    [InlineData(1024L, "1.0 KB")]
    [InlineData(1536L, "1.5 KB")]
    [InlineData(1048576L, "1.0 MB")]
    [InlineData(3355443L, "3.2 MB")]
    [InlineData(1073741824L, "1.0 GB")]
    [InlineData(-5L, "0 B")]
    public void SizesAreWrittenInPlainUnits(long bytes, string expected) =>
        Assert.Equal(expected, BrowserList.FormatSize(bytes));

    // ---------------------------------------------------------------- history

    [Fact]
    public void HistoryGoesBackAndForwardAndForgetsForwardAfterANewFolder()
    {
        var history = new FolderHistory();
        Assert.False(history.CanBack);
        Assert.Null(history.Back());

        history.Navigate(@"C:\a");
        history.Navigate(@"C:\b");
        history.Navigate(@"C:\c");
        Assert.Equal(@"C:\c", history.Current);

        Assert.Equal(@"C:\b", history.Back());
        Assert.Equal(@"C:\a", history.Back());
        Assert.False(history.CanBack);
        Assert.True(history.CanForward);
        Assert.Equal(@"C:\b", history.Forward());

        history.Navigate(@"C:\d");
        Assert.False(history.CanForward);
        Assert.Equal(@"C:\b", history.Back());
    }

    [Fact]
    public void NavigatingToTheCurrentFolderAgainChangesNothing()
    {
        var history = new FolderHistory();
        history.Navigate(@"C:\a");
        history.Navigate(@"c:\A");

        Assert.False(history.CanBack);
        Assert.Equal(@"C:\a", history.Current);
    }

    // ---------------------------------------------------------------- reading folders

    [Fact]
    public void ListingShowsFoldersAndSupportedFilesOnly()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Sub"));
        File.WriteAllBytes(Path.Combine(_dir, "a.jpg"), new byte[10]);
        File.WriteAllBytes(Path.Combine(_dir, "b.ERAF"), new byte[20]);
        File.WriteAllBytes(Path.Combine(_dir, "notes.txt"), new byte[5]);
        File.WriteAllBytes(Path.Combine(_dir, "song.mp3"), new byte[5]);

        FolderListing listing = FolderLister.List(_dir);

        Assert.Null(listing.Error);
        Assert.Equal(new[] { "Sub", "a.jpg", "b.ERAF" }, BrowserList.Sort(listing.Entries, SortKey.Name, true).Select(e => e.Name));
        Assert.Equal(20, listing.Entries.Single(e => e.Name == "b.ERAF").SizeBytes);
        Assert.Equal(EntryKind.Eraf, listing.Entries.Single(e => e.Name == "b.ERAF").Kind);
        Assert.All(listing.Entries, e => Assert.StartsWith(_dir, e.FullPath));
    }

    [Fact]
    public void HiddenAndSystemItemsAreSkipped()
    {
        string hidden = Path.Combine(_dir, "hidden.jpg");
        File.WriteAllBytes(hidden, new byte[1]);
        File.SetAttributes(hidden, FileAttributes.Hidden);
        string hiddenFolder = Directory.CreateDirectory(Path.Combine(_dir, "secret")).FullName;
        File.SetAttributes(hiddenFolder, FileAttributes.Hidden | FileAttributes.Directory);
        File.WriteAllBytes(Path.Combine(_dir, "seen.jpg"), new byte[1]);

        Assert.Equal(new[] { "seen.jpg" }, FolderLister.List(_dir).Entries.Select(e => e.Name));
        Assert.Empty(FolderLister.ListSubfolders(_dir));
    }

    [Fact]
    public void AMissingFolderGivesAPlainMessageNotAnException()
    {
        FolderListing listing = FolderLister.List(Path.Combine(_dir, "gone"));

        Assert.Empty(listing.Entries);
        Assert.Contains("does not exist", listing.Error);
        Assert.DoesNotContain("gone", listing.Error);
    }

    [Fact]
    public void SubfoldersAreSortedByNameAndUnreadableFoldersGiveNone()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "beta"));
        Directory.CreateDirectory(Path.Combine(_dir, "Alpha"));

        Assert.Equal(new[] { "Alpha", "beta" }, FolderLister.ListSubfolders(_dir).Select(f => f.Name));
        Assert.Empty(FolderLister.ListSubfolders(Path.Combine(_dir, "gone")));
    }

    [Theory]
    [InlineData(@"C:\a\b", @"C:\a")]
    [InlineData(@"C:\a\b\", @"C:\a")]
    [InlineData(@"C:\a", @"C:\")]
    [InlineData(@"C:\", null)]
    public void ParentGoesUpOneFolderAndStopsAtTheDriveRoot(string path, string? expected) =>
        Assert.Equal(expected, FolderLister.Parent(path));

    [Fact]
    public void PlacesListOnlyFoldersThatExistAndDrivesLookLikeDrives()
    {
        Assert.All(Places.QuickAccess(), p => Assert.True(Directory.Exists(p.Path)));
        Assert.All(Places.Drives(), d =>
        {
            Assert.Matches(@"\([A-Za-z]:\)$", d.Name);
            Assert.EndsWith(@":\", d.Path);
        });
    }
}
