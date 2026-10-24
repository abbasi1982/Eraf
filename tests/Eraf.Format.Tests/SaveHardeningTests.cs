using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>M7: orphaned temp-file cleanup, access-denied saves, and plain failure wording.</summary>
public sealed class SaveHardeningTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-harden-" + Guid.NewGuid().ToString("N"))).FullName;

    public void Dispose()
    {
        try
        {
            foreach (string f in Directory.GetFiles(_dir))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Target(string name = "out.eraf") => Path.Combine(_dir, name);

    private string[] Files() => Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n).ToArray()!;

    private string Orphan(string target, string hex, TimeSpan age)
    {
        string path = Path.Combine(_dir, $"{target}.{hex}.partial");
        File.WriteAllBytes(path, new byte[] { 1 });
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - age);
        return Path.GetFileName(path);
    }

    [Fact]
    public void RemovesOldOrphanedPartialsOfTheSameTargetOnly()
    {
        string old = Orphan("out.eraf", new string('a', 32), TimeSpan.FromHours(2));
        string fresh = Orphan("out.eraf", new string('b', 32), TimeSpan.FromSeconds(10));
        string otherTarget = Orphan("other.eraf", new string('a', 32), TimeSpan.FromHours(2));
        string notHex = Orphan("out.eraf", new string('z', 32), TimeSpan.FromHours(2));
        string wrongLength = Orphan("out.eraf", "abc", TimeSpan.FromHours(2));
        File.WriteAllBytes(Target("out.eraf.notes.txt"), new byte[] { 1 });
        File.WriteAllBytes(Target("keep.partial"), new byte[] { 1 });

        AtomicFile.WriteAllBytes(Target(), new byte[] { 7 }, replaceExisting: false);

        string[] left = Files();
        Assert.DoesNotContain(old, left);
        Assert.Contains(fresh, left);
        Assert.Contains(otherTarget, left);
        Assert.Contains(notHex, left);
        Assert.Contains(wrongLength, left);
        Assert.Contains("out.eraf.notes.txt", left);
        Assert.Contains("keep.partial", left);
        Assert.Contains("out.eraf", left);
    }

    [Fact]
    public void AnOpenOrphanIsNeverRemovedAndDoesNotStopTheSave()
    {
        string name = Orphan("out.eraf", new string('c', 32), TimeSpan.FromHours(3));
        using (new FileStream(Target(name), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            AtomicFile.WriteAllBytes(Target(), new byte[] { 7 }, replaceExisting: false);
        }

        Assert.Contains(name, Files());
        Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(Target()));
    }

    [Fact]
    public void AReadOnlyTargetFailsAndLeavesTheOriginalAndNoTempFile()
    {
        File.WriteAllBytes(Target(), new byte[] { 9, 9 });
        File.SetAttributes(Target(), FileAttributes.ReadOnly);

        Exception? thrown = Record.Exception(() =>
            AtomicFile.WriteAllBytes(Target(), new byte[] { 1 }, replaceExisting: true));

        File.SetAttributes(Target(), FileAttributes.Normal);
        Assert.NotNull(thrown);
        Assert.True(thrown is IOException or UnauthorizedAccessException, thrown?.GetType().Name);
        Assert.Equal(new byte[] { 9, 9 }, File.ReadAllBytes(Target()));
        Assert.Equal(new[] { "out.eraf" }, Files());
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), "did not allow")]
    [InlineData(typeof(DirectoryNotFoundException), "no longer exists")]
    [InlineData(typeof(PathTooLongException), "too long")]
    [InlineData(typeof(InvalidDataException), "final check")]
    [InlineData(typeof(IOException), "open in another app")]
    [InlineData(typeof(InvalidOperationException), "unexpected")]
    public void SaveErrorTextIsPlainAndNeverLeaksPathsOrSystemMessages(Type type, string expected)
    {
        var ex = (Exception)Activator.CreateInstance(type, "C:\\Users\\someone\\secret\\x.eraf")!;
        string text = SaveErrorText.Describe(ex);
        Assert.Contains(expected, text);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("C:\\", text);
    }

    [Theory]
    [InlineData(unchecked((int)0x80070070))]
    [InlineData(unchecked((int)0x80070027))]
    public void DiskFullIsReportedAsNotEnoughSpace(int hresult)
    {
        Assert.Contains("not enough free space", SaveErrorText.Describe(new IOException("x", hresult)));
    }
}
