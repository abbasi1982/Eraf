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
    public void AgreesWithTheWriterSignatureRule()
    {
        // Anything the planner calls a JPEG must be accepted by the writer as an image payload.
        byte[] audio = { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70 };
        Assert.True(ErafMedia.IsJpeg(Jpeg));
        Assert.Null(ErafReader.Validate(ErafWriter.Write(Jpeg, audio)));
    }
}
