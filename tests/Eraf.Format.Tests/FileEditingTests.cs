using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>Renaming, new folders and the words used before deleting.</summary>
public sealed class FileEditingTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-edit-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Make(string name)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[3]);
        return path;
    }

    // ---------------------------------------------------------------- names

    [Theory]
    [InlineData("holiday.jpg")]
    [InlineData("My photo 2")]
    [InlineData("a")]
    [InlineData("naïve café.png")]
    [InlineData("name.with.dots.eraf")]
    public void OrdinaryNamesAreFine(string name) => Assert.Null(FileNames.Check(name));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("what?")]
    [InlineData("star*")]
    [InlineData("a:b")]
    [InlineData("q\"q")]
    [InlineData("<x>")]
    [InlineData("a|b")]
    [InlineData("tab\there")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("ends.")]
    [InlineData("ends ")]
    [InlineData(" starts")]
    [InlineData("CON")]
    [InlineData("nul.txt")]
    [InlineData("com1")]
    [InlineData("LPT9.jpg")]
    public void NamesWindowsWouldRefuseAreExplained(string name)
    {
        string? problem = FileNames.Check(name);

        Assert.NotNull(problem);
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void ATooLongNameIsRefused() => Assert.Contains("too long", FileNames.Check(new string('x', 256)));

    [Fact]
    public void ANameThatIsTakenIsRefusedButChangingOnlyTheCaseOfTheSameItemIsAllowed()
    {
        string a = Make("a.jpg");
        Make("b.jpg");

        Assert.Contains("already here", FileNames.CheckNew("b.jpg", _dir, a));
        Assert.Contains("already here", FileNames.CheckNew("B.JPG", _dir, a));
        Assert.Null(FileNames.CheckNew("A.JPG", _dir, a));
        Assert.Null(FileNames.CheckNew("c.jpg", _dir, a));
        Assert.Contains("already here", FileNames.CheckNew("a.jpg", _dir, renaming: null));
    }

    [Theory]
    [InlineData("photo.jpg", false, "photo", ".jpg")]
    [InlineData("archive.tar.eraf", false, "archive.tar", ".eraf")]
    [InlineData("noext", false, "noext", "")]
    [InlineData("My.Folder", true, "My.Folder", "")]
    public void OnlyTheNameBeforeTheExtensionIsEditedForFiles(string name, bool folder, string editable, string fixedPart) =>
        Assert.Equal((editable, fixedPart), FileNames.SplitForRename(name, folder));

    [Fact]
    public void NewFolderNamesCountUp()
    {
        Assert.Equal("New folder", FileNames.UniqueFolderName(_dir));
        Directory.CreateDirectory(Path.Combine(_dir, "New folder"));
        Assert.Equal("New folder (2)", FileNames.UniqueFolderName(_dir));
        Directory.CreateDirectory(Path.Combine(_dir, "New folder (2)"));
        File.WriteAllBytes(Path.Combine(_dir, "New folder (3)"), new byte[1]);
        Assert.Equal("New folder (4)", FileNames.UniqueFolderName(_dir));
    }

    // ---------------------------------------------------------------- renaming and folders

    [Fact]
    public void RenamingAFileMovesItAndKeepsTheBytes()
    {
        string path = Make("old.jpg");

        EditResult result = FileEditing.Rename(path, "new.jpg");

        Assert.True(result.Ok);
        Assert.Equal(Path.Combine(_dir, "new.jpg"), result.NewPath);
        Assert.False(File.Exists(path));
        Assert.Equal(new byte[3], File.ReadAllBytes(result.NewPath!));
    }

    [Fact]
    public void RenamingAFolderKeepsItsContents()
    {
        string folder = Directory.CreateDirectory(Path.Combine(_dir, "old")).FullName;
        File.WriteAllBytes(Path.Combine(folder, "inside.jpg"), new byte[2]);

        EditResult result = FileEditing.Rename(folder, "renamed");

        Assert.True(result.Ok);
        Assert.True(File.Exists(Path.Combine(_dir, "renamed", "inside.jpg")));
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public void ARenameOntoAnExistingNameChangesNothing()
    {
        string a = Make("a.jpg");
        string b = Make("b.jpg");
        File.WriteAllBytes(b, new byte[] { 9 });

        EditResult result = FileEditing.Rename(a, "b.jpg");

        Assert.False(result.Ok);
        Assert.Contains("already here", result.Error);
        Assert.True(File.Exists(a));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(b));
    }

    [Fact]
    public void ChangingOnlyTheCaseWorks()
    {
        string path = Make("photo.jpg");

        EditResult result = FileEditing.Rename(path, "PHOTO.jpg");

        Assert.True(result.Ok);
        Assert.Contains("PHOTO.jpg", Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void RenamingToTheSameNameIsHarmless()
    {
        string path = Make("same.jpg");

        EditResult result = FileEditing.Rename(path, "same.jpg");

        Assert.True(result.Ok);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void RenamingSomethingThatVanishedGivesAPlainMessage()
    {
        EditResult result = FileEditing.Rename(Path.Combine(_dir, "gone.jpg"), "x.jpg");

        Assert.False(result.Ok);
        Assert.DoesNotContain(_dir, result.Error);
    }

    [Fact]
    public void AFileOpenElsewhereCannotBeRenamedAndSaysWhy()
    {
        string path = Make("busy.jpg");
        using var hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        EditResult result = FileEditing.Rename(path, "free.jpg");

        Assert.False(result.Ok);
        Assert.Contains("open in another app", result.Error);
        Assert.DoesNotContain(_dir, result.Error);
    }

    [Fact]
    public void CreatingAFolderWorksAndRefusesBadOrTakenNames()
    {
        EditResult ok = FileEditing.CreateFolder(_dir, "Holiday");
        Assert.True(ok.Ok);
        Assert.True(Directory.Exists(Path.Combine(_dir, "Holiday")));

        Assert.False(FileEditing.CreateFolder(_dir, "Holiday").Ok);
        Assert.False(FileEditing.CreateFolder(_dir, "bad:name").Ok);
        Assert.False(FileEditing.CreateFolder(_dir, "").Ok);
    }

    // ---------------------------------------------------------------- delete wording

    private static BrowserEntry Entry(string name, EntryKind kind) => new(name, @"C:\x\" + name, kind, 0, DateTime.MinValue);

    [Fact]
    public void DeletingOneItemNamesIt()
    {
        var one = new[] { Entry("a.jpg", EntryKind.Photo) };

        Assert.Equal("Move \"a.jpg\" to the Recycle Bin?", DeleteWording.Title(one));
        Assert.Contains("1 file to the Recycle Bin", DeleteWording.Detail(one));
        Assert.Contains("put it back", DeleteWording.Detail(one));
    }

    [Theory]
    [InlineData(@"C:\Users\me\Pictures\a.jpg", DriveType.Fixed, true)]
    [InlineData(@"E:\DCIM\a.jpg", DriveType.Removable, false)]
    [InlineData(@"Y:\backup\a.jpg", DriveType.Network, false)]
    [InlineData(@"C:\Users\me\AppData\Local\Temp\a.jpg", DriveType.Fixed, false)]
    [InlineData(@"C:\Users\me\AppData\Local\TempFiles\a.jpg", DriveType.Fixed, true)]
    public void OnlyLocalFixedDrivesOutsideTempUseTheRecycleBin(string path, DriveType drive, bool expected) =>
        Assert.Equal(expected, RecycleSupport.IsRecoverable(path, _ => drive, @"C:\Users\me\AppData\Local\Temp\"));

    [Fact]
    public void AnUnknownDriveIsTreatedAsNotRecoverable() =>
        Assert.False(RecycleSupport.IsRecoverable(@"Q:\x.jpg", _ => null, @"C:\Temp"));

    [Fact]
    public void TheDeleteQuestionOnlyPromisesTheRecycleBinWhenItIsTrue()
    {
        var one = new[] { Entry("a.jpg", EntryKind.Photo) };
        var three = new[] { Entry("a", EntryKind.Photo), Entry("b", EntryKind.Photo), Entry("c", EntryKind.Photo) };

        Assert.Contains("Recycle Bin", DeleteWording.Title(one, 0));
        Assert.Equal("Move to Recycle Bin", DeleteWording.PrimaryButton(1, 0));

        Assert.Equal("Delete \"a.jpg\" for good?", DeleteWording.Title(one, 1));
        Assert.DoesNotContain("Recycle Bin?", DeleteWording.Title(one, 1));
        Assert.Contains("cannot be brought back", DeleteWording.Detail(one, 1));
        Assert.Equal("Delete for good", DeleteWording.PrimaryButton(1, 1));
        Assert.Equal("Deleted \"a.jpg\".", DeleteWording.Done(one, 1, 1));

        Assert.Equal("Delete 3 items?", DeleteWording.Title(three, 2));
        Assert.Contains("2 of them", DeleteWording.Detail(three, 2));
        Assert.Contains("The others go to the Recycle Bin", DeleteWording.Detail(three, 2));
        Assert.Equal("Delete", DeleteWording.PrimaryButton(3, 2));
        Assert.Equal("Moved 3 items to the Recycle Bin.", DeleteWording.Done(three, 3, 0));
        Assert.Equal("Deleted 3 items (2 for good).", DeleteWording.Done(three, 3, 2));
    }

    [Fact]
    public void DeletingSeveralItemsCountsFoldersAndFilesAndWarnsAboutContents()
    {
        var mixed = new[] { Entry("f", EntryKind.Folder), Entry("g", EntryKind.Folder), Entry("a.jpg", EntryKind.Photo), Entry("b.eraf", EntryKind.Eraf) };

        Assert.Equal("Move 4 items to the Recycle Bin?", DeleteWording.Title(mixed));
        Assert.Contains("2 folders (with everything inside) and 2 files", DeleteWording.Detail(mixed));
        Assert.Contains("everything inside it", DeleteWording.Detail(new[] { Entry("f", EntryKind.Folder) }));
        Assert.Contains("3 files", DeleteWording.Detail(new[] { Entry("a", EntryKind.Photo), Entry("b", EntryKind.Photo), Entry("c", EntryKind.Eraf) }));
    }
}
