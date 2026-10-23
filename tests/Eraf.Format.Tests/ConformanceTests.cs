using System.Text.Json;

namespace Eraf.Format.Tests;

/// <summary>Reader results must equal the expected-result sidecars of every checked-in fixture.</summary>
public class ConformanceTests
{
    public static IEnumerable<object[]> ValidFixtures() => FixtureFiles.All("valid");

    public static IEnumerable<object[]> InvalidFixtures() => FixtureFiles.All("invalid");

    [Fact]
    public void FixtureSetIsComplete()
    {
        Assert.Equal(2, FixtureFiles.All("valid").Count());
        Assert.Equal(21, FixtureFiles.All("invalid").Count());
        for (int n = 1; n <= 21; n++)
        {
            string id = $"/invalid-N{n:00}-";
            Assert.Single(FixtureFiles.All("invalid"), f => ((string)f[0]).Contains(id));
        }
    }

    [Theory]
    [MemberData(nameof(ValidFixtures))]
    public void ValidFixtureMatchesSidecar(string relative)
    {
        JsonElement expected = FixtureFiles.ReadSidecar(relative);
        Assert.True(expected.GetProperty("valid").GetBoolean());
        byte[] file = FixtureFiles.ReadBytes(relative);

        ErafDocument fromMemory = ErafReader.Read(file);
        ErafDocument fromStream = ErafReader.Read(new MemoryStream(file));

        foreach (ErafDocument doc in new[] { fromMemory, fromStream })
        {
            Assert.Equal(expected.GetProperty("imageLength").GetInt32(), doc.Image.Length);
            Assert.Equal(expected.GetProperty("audioLength").GetInt32(), doc.Audio.Length);
            Assert.Equal(expected.GetProperty("audioDurationMs").GetUInt64(), doc.AudioDurationMs);
            Assert.Equal(expected.GetProperty("createdUtcUnixMs").GetUInt64(), doc.CreatedUtcUnixMs);
            Assert.Equal(Recipes.Image, doc.Image.ToArray());
            Assert.Equal(Recipes.Audio, doc.Audio.ToArray());

            Assert.Equal(expected.GetProperty("hasImageSha256").GetBoolean(), doc.ImageSha256.HasValue);
            Assert.Equal(expected.GetProperty("hasAudioSha256").GetBoolean(), doc.AudioSha256.HasValue);
            if (doc.ImageSha256 is { } imageSha)
            {
                Assert.Equal(expected.GetProperty("imageSha256").GetString(), Convert.ToHexString(imageSha.Span).ToLowerInvariant());
                Assert.Equal(Recipes.ImageSha256, Convert.ToHexString(imageSha.Span).ToLowerInvariant());
            }

            if (doc.AudioSha256 is { } audioSha)
            {
                Assert.Equal(expected.GetProperty("audioSha256").GetString(), Convert.ToHexString(audioSha.Span).ToLowerInvariant());
                Assert.Equal(Recipes.AudioSha256, Convert.ToHexString(audioSha.Span).ToLowerInvariant());
            }
        }
    }

    [Theory]
    [MemberData(nameof(InvalidFixtures))]
    public void InvalidFixtureFailsWithExactError(string relative)
    {
        JsonElement expected = FixtureFiles.ReadSidecar(relative);
        Assert.False(expected.GetProperty("valid").GetBoolean());
        string expectedError = expected.GetProperty("error").GetString()!;
        byte[] file = FixtureFiles.ReadBytes(relative);

        Assert.False(ErafReader.TryRead(file, out ErafDocument? doc, out ErafError memoryError));
        Assert.Null(doc);
        Assert.Equal(expectedError, memoryError.ToString());

        Assert.False(ErafReader.TryRead(new MemoryStream(file), out doc, out ErafError streamError));
        Assert.Null(doc);
        Assert.Equal(expectedError, streamError.ToString());

        Assert.Equal(memoryError, ErafReader.Validate(file));
        Assert.Equal(memoryError, Assert.Throws<ErafFormatException>(() => ErafReader.Read(file)).Error);
    }

    [Fact]
    public void CheckedInValidFixturesEqualRecipeHex()
    {
        Assert.Equal(Recipes.ValidMinimal, FixtureFiles.ReadBytes("valid/valid-minimal.eraf"));
        Assert.Equal(Recipes.ValidHashes, FixtureFiles.ReadBytes("valid/valid-hashes.eraf"));
    }
}
