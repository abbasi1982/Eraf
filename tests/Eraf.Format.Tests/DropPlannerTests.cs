using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>What dropping files and folders on the window means.</summary>
public sealed class DropPlannerTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-drop-" + Guid.NewGuid().ToString("N"))).FullName;

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

    private string File_(string name)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[2]);
        return path;
    }

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_dir, name)).FullName;

    [Fact]
    public void OneErafFileIsOpened()
    {
        string eraf = File_("a.eraf");

        DropPlan plan = DropPlanner.Plan(new[] { eraf });

        Assert.Equal(DropKind.OpenEraf, plan.Kind);
        Assert.Equal(new[] { eraf }, plan.Paths);
    }

    [Fact]
    public void OnePhotoStartsTheNormalEditor()
    {
        string photo = File_("a.JPG");

        DropPlan plan = DropPlanner.Plan(new[] { photo });

        Assert.Equal(DropKind.SinglePhoto, plan.Kind);
        Assert.Equal(0, plan.Ignored);
    }

    [Fact]
    public void OneFolderIsABatchEvenForASinglePath()
    {
        string folder = Folder("pics");

        DropPlan plan = DropPlanner.Plan(new[] { folder });

        Assert.Equal(DropKind.Batch, plan.Kind);
        Assert.True(plan.AnyFolder);
    }

    [Fact]
    public void SeveralPhotosAreABatchWithoutFolders()
    {
        DropPlan plan = DropPlanner.Plan(new[] { File_("a.jpg"), File_("b.png") });

        Assert.Equal(DropKind.Batch, plan.Kind);
        Assert.False(plan.AnyFolder);
        Assert.Equal(2, plan.Paths.Count);
    }

    [Fact]
    public void ErafFilesAndOtherFilesAreLeftOutOfABatchAndCounted()
    {
        DropPlan plan = DropPlanner.Plan(new[]
        {
            File_("a.jpg"), Folder("pics"), File_("old.eraf"), File_("notes.txt"), Path.Combine(_dir, "missing.png"),
        });

        Assert.Equal(DropKind.Batch, plan.Kind);
        Assert.Equal(2, plan.Paths.Count);
        Assert.Equal(3, plan.Ignored);
    }

    [Fact]
    public void ASinglePhotoDroppedWithJunkStillStartsTheEditor()
    {
        DropPlan plan = DropPlanner.Plan(new[] { File_("a.jpg"), File_("notes.txt") });

        Assert.Equal(DropKind.SinglePhoto, plan.Kind);
        Assert.Equal(1, plan.Ignored);
    }

    [Fact]
    public void SeveralErafFilesAreRefusedWithAReason()
    {
        DropPlan plan = DropPlanner.Plan(new[] { File_("a.eraf"), File_("b.eraf") });

        Assert.Equal(DropKind.Nothing, plan.Kind);
        Assert.Contains("one ERAF file at a time", plan.Message);
    }

    [Fact]
    public void AnErafFileWithOtherJunkIsNotOpenedByAccident()
    {
        DropPlan plan = DropPlanner.Plan(new[] { File_("a.eraf"), File_("notes.txt") });

        Assert.Equal(DropKind.Nothing, plan.Kind);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("song.mp3")]
    public void UnsupportedFilesGiveAPlainMessage(string name)
    {
        DropPlan plan = DropPlanner.Plan(new[] { File_(name) });

        Assert.Equal(DropKind.Nothing, plan.Kind);
        Assert.Contains("not a photo", plan.Message);
    }

    [Fact]
    public void EmptyBlankMissingAndDuplicateItemsAreHarmless()
    {
        string photo = File_("a.jpg");

        Assert.Equal(DropKind.Nothing, DropPlanner.Plan(Array.Empty<string>()).Kind);
        Assert.Equal(DropKind.Nothing, DropPlanner.Plan(new[] { "", "  ", Path.Combine(_dir, "gone.jpg") }).Kind);
        DropPlan twice = DropPlanner.Plan(new[] { photo, photo.ToUpperInvariant() });
        Assert.Equal(DropKind.SinglePhoto, twice.Kind);
    }
}
