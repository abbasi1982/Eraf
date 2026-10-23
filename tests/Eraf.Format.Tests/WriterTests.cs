namespace Eraf.Format.Tests;

public class WriterTests
{
    private static readonly byte[] SmallImage = { 0xFF, 0xD8, 0xFF };
    private static readonly byte[] SmallAudio = { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x70 }; // ... "ftyp"

    [Fact]
    public void WriterReproducesRecipeBytesWithoutHashes() =>
        Assert.Equal(Recipes.ValidMinimal, ErafWriter.Write(Recipes.Image, Recipes.Audio));

    [Fact]
    public void WriterReproducesRecipeBytesWithHashes()
    {
        var options = new ErafWriteOptions
        {
            AudioDurationMs = 1500,
            CreatedUtcUnixMs = 1_700_000_000_000,
            IncludeImageSha256 = true,
            IncludeAudioSha256 = true,
        };

        Assert.Equal(Recipes.ValidHashes, ErafWriter.Write(Recipes.Image, Recipes.Audio, options));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RoundTripKeepsPayloadsByteIdentical(bool imageHash, bool audioHash)
    {
        byte[] image = new byte[5000];
        byte[] audio = new byte[3000];
        new Random(1234).NextBytes(image);
        new Random(5678).NextBytes(audio);
        SmallImage.CopyTo(image, 0);
        SmallAudio.CopyTo(audio, 0);
        var options = new ErafWriteOptions
        {
            AudioDurationMs = 4321,
            CreatedUtcUnixMs = 1_234_567_890_123,
            IncludeImageSha256 = imageHash,
            IncludeAudioSha256 = audioHash,
        };

        byte[] file = ErafWriter.Write(image, audio, options);
        ErafDocument fromMemory = ErafReader.Read(file);
        ErafDocument fromStream = ErafReader.Read(new MemoryStream(file));

        foreach (ErafDocument d in new[] { fromMemory, fromStream })
        {
            Assert.Equal(image, d.Image.ToArray());
            Assert.Equal(audio, d.Audio.ToArray());
            Assert.Equal(4321UL, d.AudioDurationMs);
            Assert.Equal(1_234_567_890_123UL, d.CreatedUtcUnixMs);
            Assert.Equal(imageHash, d.ImageSha256.HasValue);
            Assert.Equal(audioHash, d.AudioSha256.HasValue);
        }
    }

    [Fact]
    public void StreamOverloadWritesSameBytes()
    {
        using var ms = new MemoryStream();
        ErafWriter.Write(ms, Recipes.Image, Recipes.Audio);
        Assert.Equal(Recipes.ValidMinimal, ms.ToArray());
    }

    [Fact]
    public void SmallestAcceptedPayloadsRoundTrip()
    {
        byte[] file = ErafWriter.Write(SmallImage, SmallAudio);
        Assert.Null(ErafReader.Validate(file));
    }

    [Fact]
    public void MaximumDurationIsAccepted()
    {
        byte[] file = ErafWriter.Write(SmallImage, SmallAudio, new ErafWriteOptions { AudioDurationMs = 600_000 });
        Assert.Equal(600_000UL, ErafReader.Read(file).AudioDurationMs);
    }

    [Fact]
    public void MaximumImageSizeRoundTripsAndOneMoreIsRefused()
    {
        byte[] image = new byte[ErafLimits.MaxImageBytes];
        SmallImage.CopyTo(image, 0);
        byte[] file = ErafWriter.Write(image, SmallAudio);
        Assert.Equal(ErafLimits.HeaderLength + ErafLimits.MaxImageBytes + SmallAudio.Length, file.LongLength);
        Assert.Equal((int)ErafLimits.MaxImageBytes, ErafReader.Read(file).Image.Length);

        byte[] tooBig = new byte[ErafLimits.MaxImageBytes + 1];
        SmallImage.CopyTo(tooBig, 0);
        Assert.Equal(ErafError.LimitExceeded, Refusal(tooBig, SmallAudio));
    }

    [Fact]
    public void RefusesInputsThatCouldNotProduceAValidFile()
    {
        Assert.Equal(ErafError.EmptyPayload, Refusal(Array.Empty<byte>(), SmallAudio));
        Assert.Equal(ErafError.EmptyPayload, Refusal(SmallImage, Array.Empty<byte>()));
        Assert.Equal(ErafError.InvalidPayloadSignature, Refusal(new byte[] { 0xFF, 0xD8 }, SmallAudio)); // image < 3 bytes
        Assert.Equal(ErafError.InvalidPayloadSignature, Refusal(SmallImage, SmallAudio[..7]));           // audio < 8 bytes
        Assert.Equal(ErafError.InvalidPayloadSignature, Refusal(new byte[] { 0, 0xD8, 0xFF }, SmallAudio));
        Assert.Equal(ErafError.InvalidPayloadSignature, Refusal(SmallImage, new byte[] { 0, 0, 0, 0, 0x66, 0x74, 0x79, 0x78 })); // "ftyx"
    }

    [Fact]
    public void RefusesDurationAboveLimit()
    {
        var ex = Assert.Throws<ErafFormatException>(() =>
            ErafWriter.Write(SmallImage, SmallAudio, new ErafWriteOptions { AudioDurationMs = 600_001 }));
        Assert.Equal(ErafError.LimitExceeded, ex.Error);
    }

    private static ErafError Refusal(byte[] image, byte[] audio) =>
        Assert.Throws<ErafFormatException>(() => ErafWriter.Write(image, audio)).Error;
}
