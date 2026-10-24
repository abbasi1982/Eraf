using Eraf.Format;

namespace Eraf.Format.Tests;

/// <summary>Choosing how to turn a chosen audio file into the voice-note payload.</summary>
public sealed class AudioImportPlanTests
{
    private static readonly byte[] M4aHead = { 0, 0, 0, 0x20, (byte)'f', (byte)'t', (byte)'y', (byte)'p', (byte)'M', (byte)'4', (byte)'A', (byte)' ' };
    private static readonly byte[] Mp3Head = { (byte)'I', (byte)'D', (byte)'3', 4, 0, 0, 0, 0, 0, 0, 0, 0 };

    [Fact]
    public void AnAacMpeg4FileIsKeptUntouched() =>
        Assert.Equal(AudioImportAction.KeepAsM4a, ErafMedia.PlanAudioImport(5000, M4aHead, platformSaysAac: true));

    [Fact]
    public void AnMpeg4FileWithoutAacAudioIsConverted() =>
        Assert.Equal(AudioImportAction.ConvertToM4a, ErafMedia.PlanAudioImport(5000, M4aHead, platformSaysAac: false));

    [Fact]
    public void AacInAnotherContainerIsConverted() =>
        Assert.Equal(AudioImportAction.ConvertToM4a, ErafMedia.PlanAudioImport(5000, Mp3Head, platformSaysAac: true));

    [Fact]
    public void Mp3IsConverted() =>
        Assert.Equal(AudioImportAction.ConvertToM4a, ErafMedia.PlanAudioImport(5000, Mp3Head, platformSaysAac: false));

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void EmptyIsRejected(long length) =>
        Assert.Equal(AudioImportAction.RejectEmpty, ErafMedia.PlanAudioImport(length, M4aHead, true));

    [Fact]
    public void TheSourceLimitIsInclusiveAndIsLargerThanThePayloadLimit()
    {
        Assert.True(ErafMedia.MaxAudioImportSourceBytes > ErafLimits.MaxAudioBytes);
        Assert.Equal(AudioImportAction.ConvertToM4a, ErafMedia.PlanAudioImport(ErafMedia.MaxAudioImportSourceBytes, Mp3Head, false));
        Assert.Equal(AudioImportAction.RejectTooLarge, ErafMedia.PlanAudioImport(ErafMedia.MaxAudioImportSourceBytes + 1, Mp3Head, false));
        Assert.Equal(AudioImportAction.RejectTooLarge, ErafMedia.PlanAudioImport(ErafMedia.MaxAudioImportSourceBytes + 1, M4aHead, true));
    }

    [Fact]
    public void AShortOrEmptyHeadNeverCountsAsM4a() =>
        Assert.Equal(AudioImportAction.ConvertToM4a, ErafMedia.PlanAudioImport(5000, new byte[] { 0, 0 }, true));
}
