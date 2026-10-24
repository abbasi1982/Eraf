namespace Eraf.Format.Tests;

public class ErafMediaTests
{
    private static readonly byte[] Jpeg = { 0xFF, 0xD8, 0xFF, 0xE0 };
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47 };

    [Fact]
    public void IsJpegMatchesTheSpecSignature()
    {
        Assert.True(ErafMedia.IsJpeg(Jpeg));
        Assert.True(ErafMedia.IsJpeg(new byte[] { 0xFF, 0xD8, 0xFF }));
        Assert.False(ErafMedia.IsJpeg(new byte[] { 0xFF, 0xD8 }));
        Assert.False(ErafMedia.IsJpeg(Png));
        Assert.False(ErafMedia.IsJpeg(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void JpegIsKeptUntouched() =>
        Assert.Equal(ImageImportAction.KeepAsJpeg, ErafMedia.PlanImageImport(1000, Jpeg));

    [Fact]
    public void NonJpegIsConverted() =>
        Assert.Equal(ImageImportAction.ConvertToJpeg, ErafMedia.PlanImageImport(1000, Png));

    [Fact]
    public void ShortHeadThatIsNotAJpegIsConverted() =>
        Assert.Equal(ImageImportAction.ConvertToJpeg, ErafMedia.PlanImageImport(2, new byte[] { 0xFF, 0xD8 }));

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void EmptyIsRejected(long length) =>
        Assert.Equal(ImageImportAction.RejectEmpty, ErafMedia.PlanImageImport(length, Jpeg));

    [Fact]
    public void LimitIsInclusive()
    {
        Assert.Equal(ImageImportAction.KeepAsJpeg, ErafMedia.PlanImageImport(ErafLimits.MaxImageBytes, Jpeg));
        Assert.Equal(ImageImportAction.RejectTooLarge, ErafMedia.PlanImageImport(ErafLimits.MaxImageBytes + 1, Jpeg));
        Assert.Equal(ImageImportAction.RejectTooLarge, ErafMedia.PlanImageImport(ErafLimits.MaxImageBytes + 1, Png));
    }

    [Fact]
    public void JpegEndMarkerIsFoundAtTheEndOrInTheTrailingWindow()
    {
        Assert.True(ErafMedia.JpegHasEndMarker(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 0xFF, 0xD9 }));

        // Some encoders pad after the marker.
        byte[] padded = new byte[200];
        padded[0] = 0xFF; padded[1] = 0xD8; padded[2] = 0xFF;
        padded[200 - 20] = 0xFF; padded[200 - 19] = 0xD9;
        Assert.True(ErafMedia.JpegHasEndMarker(padded));
    }

    [Fact]
    public void JpegWithoutAnEndMarkerNearTheEndLooksTruncated()
    {
        Assert.False(ErafMedia.JpegHasEndMarker(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4 }));
        Assert.False(ErafMedia.JpegHasEndMarker(ReadOnlySpan<byte>.Empty));
        Assert.False(ErafMedia.JpegHasEndMarker(new byte[] { 0xFF }));

        // A marker earlier than the trailing window does not count: the data after it is not just padding.
        byte[] early = new byte[500];
        early[0] = 0xFF; early[1] = 0xD8; early[2] = 0xFF;
        early[100] = 0xFF; early[101] = 0xD9;
        Assert.False(ErafMedia.JpegHasEndMarker(early));
    }

    [Fact]
    public void ARealJpegProducedByTheSystemHasAnEndMarker()
    {
        // The ERAF test image written by System.Drawing during manual testing ends in FF D9; here a minimal stand-in.
        Assert.True(ErafMedia.JpegHasEndMarker(Recipes.Image));
    }

    private static readonly byte[] M4a = { 0, 0, 0, 0x14, 0x66, 0x74, 0x79, 0x70, 0x4D, 0x34, 0x41, 0x20 };

    [Fact]
    public void IsM4aMatchesTheSpecSignature()
    {
        Assert.True(ErafMedia.IsM4a(M4a));
        Assert.False(ErafMedia.IsM4a(new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79 })); // 7 bytes
        Assert.False(ErafMedia.IsM4a(new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x78 })); // "ftyx"
        Assert.False(ErafMedia.IsM4a(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void GoodRecordingIsOk() =>
        Assert.Equal(AudioCheck.Ok, ErafMedia.CheckAudio(5000, 12_000, M4a));

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void EmptyRecordingIsNeverOk(long length) =>
        Assert.Equal(AudioCheck.Empty, ErafMedia.CheckAudio(length, 1000, M4a));

    [Fact]
    public void DurationLimitIsInclusive()
    {
        Assert.Equal(AudioCheck.Ok, ErafMedia.CheckAudio(5000, ErafLimits.MaxAudioDurationMs, M4a));
        Assert.Equal(AudioCheck.TooLong, ErafMedia.CheckAudio(5000, ErafLimits.MaxAudioDurationMs + 1, M4a));
    }

    [Fact]
    public void SizeLimitIsInclusive()
    {
        Assert.Equal(AudioCheck.Ok, ErafMedia.CheckAudio(ErafLimits.MaxAudioBytes, 1000, M4a));
        Assert.Equal(AudioCheck.TooLarge, ErafMedia.CheckAudio(ErafLimits.MaxAudioBytes + 1, 1000, M4a));
    }

    [Fact]
    public void NonM4aIsRejected() =>
        Assert.Equal(AudioCheck.NotM4a, ErafMedia.CheckAudio(12, 1000, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

    [Fact]
    public void CheckAudioAgreesWithTheWriter()
    {
        // A recording CheckAudio accepts must be acceptable to the writer as an audio payload.
        Assert.Equal(AudioCheck.Ok, ErafMedia.CheckAudio(M4a.Length, 600_000, M4a));
        byte[] file = ErafWriter.Write(Jpeg, M4a, new ErafWriteOptions { AudioDurationMs = 600_000 });
        Assert.Null(ErafReader.Validate(file));
    }

    [Fact]
    public void AgreesWithTheWriterSignatureRule()
    {
        // Anything the planner calls a JPEG must be accepted by the writer as an image payload.
        byte[] audio = { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70 };
        Assert.True(ErafMedia.IsJpeg(Jpeg));
        Assert.Null(ErafReader.Validate(ErafWriter.Write(Jpeg, audio)));
    }
}
