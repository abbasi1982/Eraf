using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>Atomic replace (spec §11): a failure at any point leaves an existing file untouched and no temp file behind.</summary>
public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-atomic-" + Guid.NewGuid().ToString("N"))).FullName;

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

    private string Target(string name = "out.eraf") => Path.Combine(_dir, name);

    private string[] Files() => Directory.GetFiles(_dir).Select(Path.GetFileName).OrderBy(n => n).ToArray()!;

    [Fact]
    public void WritesANewFileExactlyAndLeavesNothingElse()
    {
        byte[] data = { 1, 2, 3, 4, 5 };
        AtomicFile.WriteAllBytes(Target(), data, replaceExisting: false);

        Assert.Equal(data, File.ReadAllBytes(Target()));
        Assert.Equal(new[] { "out.eraf" }, Files());
    }

    [Fact]
    public void ReplacesAnExistingFileWhenAllowed()
    {
        File.WriteAllBytes(Target(), new byte[] { 9, 9, 9 });
        byte[] data = { 1, 2 };

        AtomicFile.WriteAllBytes(Target(), data, replaceExisting: true);

        Assert.Equal(data, File.ReadAllBytes(Target()));
        Assert.Equal(new[] { "out.eraf" }, Files());
    }

    [Fact]
    public void RefusesToReplaceWhenNotAllowedAndLeavesTheOriginal()
    {
        byte[] original = { 9, 9, 9 };
        File.WriteAllBytes(Target(), original);

        Assert.Throws<IOException>(() => AtomicFile.WriteAllBytes(Target(), new byte[] { 1 }, replaceExisting: false));

        Assert.Equal(original, File.ReadAllBytes(Target()));
        Assert.Equal(new[] { "out.eraf" }, Files());
    }

    [Fact]
    public void TheTempFileIsBesideTheTargetWhileItIsBeingVerified()
    {
        string? seenDirectory = null;
        string? seenName = null;
        AtomicFile.WriteAllBytes(Target(), new byte[] { 1, 2, 3 }, replaceExisting: false, verify: temp =>
        {
            seenDirectory = Path.GetDirectoryName(temp);
            seenName = Path.GetFileName(temp);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(temp)); // fully written before the swap
            Assert.False(File.Exists(Target()));                            // target not touched yet
            return true;
        });

        Assert.Equal(_dir, seenDirectory);
        Assert.StartsWith("out.eraf.", seenName);
        Assert.EndsWith(".partial", seenName);
    }

    [Fact]
    public void FailedVerificationLeavesTheOriginalAndNoTempFile()
    {
        byte[] original = { 7, 7, 7, 7 };
        File.WriteAllBytes(Target(), original);

        Assert.Throws<InvalidDataException>(() =>
            AtomicFile.WriteAllBytes(Target(), new byte[] { 1 }, replaceExisting: true, verify: _ => false));

        Assert.Equal(original, File.ReadAllBytes(Target()));
        Assert.Equal(new[] { "out.eraf" }, Files());
    }

    [Fact]
    public void FailedVerificationOfANewFileCreatesNothing()
    {
        Assert.Throws<InvalidDataException>(() =>
            AtomicFile.WriteAllBytes(Target(), new byte[] { 1 }, replaceExisting: false, verify: _ => false));

        Assert.Empty(Files());
    }

    [Fact]
    public void ALockedTargetFailsTheSwapButKeepsTheOriginalAndLeavesNoTempFile()
    {
        Assert.True(OperatingSystem.IsWindows());
        byte[] original = { 5, 5, 5, 5, 5 };
        File.WriteAllBytes(Target(), original);

        using (new FileStream(Target(), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() =>
                AtomicFile.WriteAllBytes(Target(), new byte[] { 1, 2, 3 }, replaceExisting: true));
        }

        Assert.Equal(original, File.ReadAllBytes(Target()));
        Assert.Equal(new[] { "out.eraf" }, Files());
    }

    [Fact]
    public void AMissingFolderIsReportedAndNothingIsCreated()
    {
        string target = Path.Combine(_dir, "no-such-folder", "out.eraf");
        Assert.Throws<DirectoryNotFoundException>(() =>
            AtomicFile.WriteAllBytes(target, new byte[] { 1 }, replaceExisting: true));
        Assert.Empty(Files());
    }

    [Fact]
    public void ExportStyleVerificationCanCompareBytesExactly()
    {
        byte[] data = Enumerable.Range(0, 1000).Select(i => (byte)(i * 7)).ToArray();
        AtomicFile.WriteAllBytes(Target("photo.jpg"), data, replaceExisting: true,
            verify: temp => File.ReadAllBytes(temp).AsSpan().SequenceEqual(data));

        Assert.Equal(data, File.ReadAllBytes(Target("photo.jpg")));
    }
}
