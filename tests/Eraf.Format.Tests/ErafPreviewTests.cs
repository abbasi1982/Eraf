using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>The photo shown as a saved ERAF file's icon: only ever taken from a fully valid file.</summary>
public sealed class ErafPreviewTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-preview-" + Guid.NewGuid().ToString("N"))).FullName;

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

    private static (byte[] File, byte[] Jpeg) Sample()
    {
        byte[] jpeg = new byte[3000];
        byte[] m4a = new byte[1200];
        new Random(5).NextBytes(jpeg);
        new Random(6).NextBytes(m4a);
        jpeg[0] = 0xFF; jpeg[1] = 0xD8; jpeg[2] = 0xFF;
        m4a[4] = 0x66; m4a[5] = 0x74; m4a[6] = 0x79; m4a[7] = 0x70;
        var session = new CaptureSession(new ImageSession(jpeg, ImageOrigin.ImportedJpeg, "x.jpg"));
        session.SetAudio(new AudioClip(m4a, 2500));
        return (SessionFile.Build(session, DateTimeOffset.UtcNow), jpeg);
    }

    private string Save(byte[] bytes, string name = "a.eraf")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact]
    public void AValidFileGivesBackItsPhotoUnchanged()
    {
        (byte[] file, byte[] jpeg) = Sample();

        Assert.Equal(jpeg, ErafPreview.ReadPhoto(Save(file)));
    }

    [Fact]
    public void EveryGoldenValidFixtureGivesItsPhoto()
    {
        foreach (string name in new[] { "valid/valid-minimal.eraf", "valid/valid-hashes.eraf" })
        {
            byte[]? photo = ErafPreview.ReadPhoto(Path.Combine(FixtureFiles.Root, name));

            Assert.NotNull(photo);
            Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF }, photo![..3]);
        }
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void EveryInvalidGoldenFixtureGivesNoPhoto(string relative) =>
        Assert.Null(ErafPreview.ReadPhoto(Path.Combine(FixtureFiles.Root, relative)));

    public static IEnumerable<object[]> InvalidFixtures() => FixtureFiles.All("invalid");

    [Fact]
    public void ACorruptedPhotoByteMeansNoPhotoBecauseTheHashNoLongerMatches()
    {
        (byte[] file, _) = Sample();
        file[64 + 64 + 100] ^= 0x01; // inside the photo payload, after the 64-byte header and the two 32-byte digests

        Assert.Null(ErafPreview.ReadPhoto(Save(file)));
    }

    [Fact]
    public void ACorruptedVoiceNoteByteAlsoMeansNoPhotoBecauseTheWholeFileMustPass()
    {
        (byte[] file, _) = Sample();
        file[^10] ^= 0x01;

        Assert.Null(ErafPreview.ReadPhoto(Save(file)));
    }

    [Fact]
    public void ATruncatedFileGivesNoPhoto()
    {
        (byte[] file, _) = Sample();

        Assert.Null(ErafPreview.ReadPhoto(Save(file[..(file.Length - 100)])));
    }

    [Fact]
    public void AFileOfAnotherKindAnEmptyFileAndAMissingFileGiveNoPhoto()
    {
        Assert.Null(ErafPreview.ReadPhoto(Save(new byte[] { 1, 2, 3 }, "tiny.eraf")));
        Assert.Null(ErafPreview.ReadPhoto(Save(Array.Empty<byte>(), "empty.eraf")));
        Assert.Null(ErafPreview.ReadPhoto(Save(new byte[500], "zeros.eraf")));
        Assert.Null(ErafPreview.ReadPhoto(Path.Combine(_dir, "gone.eraf")));
    }

    [Fact]
    public void AFileOverTheSizeCapGivesNoPhotoWithoutBeingReadWhole()
    {
        string path = Path.Combine(_dir, "huge.eraf");
        using (var stream = new FileStream(path, FileMode.Create))
        {
            stream.SetLength(ErafPreview.MaxFileBytes + 1);
        }

        Assert.Null(ErafPreview.ReadPhoto(path));
    }

    [Fact]
    public void AFileHeldOpenWithoutSharingGivesNoPhotoAndDoesNotThrow()
    {
        (byte[] file, _) = Sample();
        string path = Save(file);
        using var hold = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        Assert.Null(ErafPreview.ReadPhoto(path));
    }
}
