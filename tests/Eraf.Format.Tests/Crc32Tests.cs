using System.Text;

namespace Eraf.Format.Tests;

public class Crc32Tests
{
    [Fact]
    public void StandardCheckValue() =>
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));

    [Fact]
    public void EmptyInputIsZero() => Assert.Equal(0u, Crc32.Compute(ReadOnlySpan<byte>.Empty));

    [Fact]
    public void RecipeHeaderCrcs()
    {
        // CRC values are the ones printed in RECIPES.md; the input is bytes 0-51 of each recipe file.
        Assert.Equal(0x8C2AD17Fu, Crc32.Compute(Recipes.ValidMinimal.AsSpan(0, 52)));
        Assert.Equal(0x5C5C7DBEu, Crc32.Compute(Recipes.ValidHashes.AsSpan(0, 52)));
    }
}
