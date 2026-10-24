using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>Session payloads through the .eraf format and back, and opening files safely (including every golden fixture).</summary>
public sealed class SessionFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateDirectory(
        Path.Combine(Path.GetTempPath(), "eraf-session-" + Guid.NewGuid().ToString("N"))).FullName;

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

    private static CaptureSession SampleSession(ulong durationMs = 4321)
    {
        byte[] jpeg = new byte[2000];
        byte[] m4a = new byte[900];
        new Random(11).NextBytes(jpeg);
        new Random(12).NextBytes(m4a);
        jpeg[0] = 0xFF; jpeg[1] = 0xD8; jpeg[2] = 0xFF;
        m4a[4] = 0x66; m4a[5] = 0x74; m4a[6] = 0x79; m4a[7] = 0x70;
        var session = new CaptureSession(new ImageSession(jpeg, ImageOrigin.ImportedJpeg, "x.jpg"));
        session.SetAudio(new AudioClip(m4a, durationMs));
        return session;
    }

    [Fact]
    public void BuildKeepsPayloadsDurationAndTimeAndAddsBothHashes()
    {
        CaptureSession session = SampleSession();
        var now = new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

        byte[] file = SessionFile.Build(session, now);
        ErafDocument doc = ErafReader.Read(file);

        Assert.Equal(session.Photo.JpegBytes, doc.Image.ToArray());
        Assert.Equal(session.Audio!.M4aBytes, doc.Audio.ToArray());
        Assert.Equal(4321UL, doc.AudioDurationMs);
        Assert.Equal((ulong)now.ToUnixTimeMilliseconds(), doc.CreatedUtcUnixMs);
        Assert.True(doc.ImageSha256.HasValue);
        Assert.True(doc.AudioSha256.HasValue);
    }

    [Fact]
    public void BuildRequiresBothPhotoAndVoiceNote()
    {
        var photoOnly = new CaptureSession(new ImageSession(new byte[] { 0xFF, 0xD8, 0xFF }, ImageOrigin.Captured, null));
        Assert.Throws<InvalidOperationException>(() => SessionFile.Build(photoOnly, DateTimeOffset.UtcNow));

        CaptureSession cleared = SampleSession();
        cleared.Clear();
        Assert.Throws<InvalidOperationException>(() => SessionFile.Build(cleared, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void BuildRefusesPayloadsTheFormatWouldNotAccept()
    {
        var notJpeg = new CaptureSession(new ImageSession(new byte[] { 1, 2, 3, 4 }, ImageOrigin.ImportedJpeg, "x"));
        notJpeg.SetAudio(new AudioClip(new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70 }, 1000));
        var ex = Assert.Throws<ErafFormatException>(() => SessionFile.Build(notJpeg, DateTimeOffset.UtcNow));
        Assert.Equal(ErafError.InvalidPayloadSignature, ex.Error);
    }

    [Fact]
    public void SaveThenOpenReturnsTheSamePayloads()
    {
        CaptureSession session = SampleSession(durationMs: 9000);
        string path = Path.Combine(_dir, "roundtrip.eraf");

        byte[] file = SessionFile.Build(session, DateTimeOffset.UtcNow);
        AtomicFile.WriteAllBytes(path, file, replaceExisting: false,
            verify: temp => ErafReader.Validate(File.ReadAllBytes(temp)) is null);

        OpenResult opened = SessionFile.Open(path);

        Assert.Null(opened.Error);
        Assert.NotNull(opened.Session);
        Assert.Equal(session.Photo.JpegBytes, opened.Session!.Photo.JpegBytes);
        Assert.Equal(session.Audio!.M4aBytes, opened.Session.Audio!.M4aBytes);
        Assert.Equal(9000UL, opened.Session.Audio.DurationMs);
        Assert.Equal(ImageOrigin.OpenedFile, opened.Session.Photo.Origin);
        Assert.Equal("roundtrip.eraf", opened.Session.Photo.SourceName);
        Assert.True(opened.Session.IsComplete);
    }

    public static IEnumerable<object[]> ValidFixtures() => FixtureFiles.All("valid");

    public static IEnumerable<object[]> InvalidFixtures() => FixtureFiles.All("invalid");

    [Theory]
    [MemberData(nameof(ValidFixtures))]
    public void OpeningAValidGoldenFixtureYieldsItsPayloads(string relative)
    {
        OpenResult opened = SessionFile.Open(Path.Combine(FixtureFiles.Root, relative));

        Assert.Null(opened.Error);
        Assert.Equal(Recipes.Image, opened.Session!.Photo.JpegBytes);
        Assert.Equal(Recipes.Audio, opened.Session.Audio!.M4aBytes);
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void OpeningAnInvalidGoldenFixtureFailsWithTheExactErrorAndAMessage(string relative)
    {
        string expected = FixtureFiles.ReadSidecar(relative).GetProperty("error").GetString()!;

        OpenResult opened = SessionFile.Open(Path.Combine(FixtureFiles.Root, relative));

        Assert.Null(opened.Session);
        Assert.Equal(expected, opened.Code.ToString());
        Assert.False(string.IsNullOrWhiteSpace(opened.Error));
        Assert.Contains(Path.GetFileName(relative), opened.Error); // names the file the user chose
    }

    [Fact]
    public void ARandomFileRenamedToEraFIsRejectedWithoutThrowing()
    {
        string path = Path.Combine(_dir, "junk.eraf");
        byte[] junk = new byte[5000];
        new Random(5).NextBytes(junk);
        File.WriteAllBytes(path, junk);

        OpenResult opened = SessionFile.Open(path);

        Assert.Null(opened.Session);
        Assert.Equal(ErafError.BadMagic, opened.Code);
    }

    [Fact]
    public void MissingFilesAndFoldersAreReportedNotThrown()
    {
        OpenResult missing = SessionFile.Open(Path.Combine(_dir, "nope.eraf"));
        Assert.Null(missing.Session);
        Assert.False(string.IsNullOrWhiteSpace(missing.Error));

        OpenResult folder = SessionFile.Open(_dir);
        Assert.Null(folder.Session);
        Assert.False(string.IsNullOrWhiteSpace(folder.Error));
    }

    [Fact]
    public void ALockedFileIsReportedNotThrown()
    {
        string path = Path.Combine(_dir, "locked.eraf");
        File.WriteAllBytes(path, Recipes.ValidMinimal);
        using var lockStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        OpenResult opened = SessionFile.Open(path);

        Assert.Null(opened.Session);
        Assert.False(string.IsNullOrWhiteSpace(opened.Error));
    }

    [Fact]
    public void EveryErrorHasUserFacingTextThatNamesTheFile()
    {
        foreach (ErafError error in Enum.GetValues<ErafError>())
        {
            string text = ErafErrorText.Describe("my file.eraf", error);
            Assert.Contains("my file.eraf", text);
            Assert.False(text.Contains(error.ToString()), $"{error} leaks an internal error name to the user");
        }
    }
}
