using System.Buffers.Binary;
using System.IO.Compression;

namespace Eraf.Format.Tests;

public class HostileInputTests
{
    /// <summary>
    /// Counts bytes handed out so tests can prove payloads were never read. Only the array overload is
    /// counted: for a MemoryStream subclass, Read(Span) routes through it, so counting both would double up.
    /// </summary>
    private sealed class CountingStream(byte[] data) : MemoryStream(data)
    {
        public long BytesRead { get; private set; }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = base.Read(buffer, offset, count);
            BytesRead += n;
            return n;
        }
    }

    [Fact]
    public void HugeClaimedLengthReadsOnlyTheHeaderAndAllocatesNothingLarge()
    {
        byte[] file = FixtureFiles.ReadBytes("invalid/invalid-N17-limit-exceeded.eraf");

        // Warm up so one-time costs (CRC table, JIT) are not counted.
        Assert.False(ErafReader.TryRead(new MemoryStream(file), out _, out _));

        var stream = new CountingStream(file);
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool ok = ErafReader.TryRead(stream, out ErafDocument? doc, out ErafError error);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.False(ok);
        Assert.Null(doc);
        Assert.Equal(ErafError.LimitExceeded, error);
        Assert.Equal(ErafLimits.HeaderLength, stream.BytesRead);
        Assert.True(allocated < 4096, $"allocated {allocated} bytes");
    }

    [Fact]
    public void ClaimedLengthLargerThanStreamIsRejectedBeforeAllocation()
    {
        // Within limits (64 MiB image) but the stream holds only 92 bytes: LengthMismatch, no payload read.
        byte[] file = (byte[])Recipes.ValidMinimal.Clone();
        BinaryPrimitives.WriteUInt64LittleEndian(file.AsSpan(20), (ulong)ErafLimits.MaxImageBytes);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(52), Crc32.Compute(file.AsSpan(0, 52)));

        var stream = new CountingStream(file);
        Assert.False(ErafReader.TryRead(stream, out _, out ErafError error));
        Assert.Equal(ErafError.LengthMismatch, error);
        Assert.Equal(ErafLimits.HeaderLength, stream.BytesRead);
    }

    [Fact]
    public void EveryTruncationOfTheHeaderIsTooShort()
    {
        for (int length = 0; length < ErafLimits.HeaderLength; length++)
        {
            Assert.Equal(ErafError.TooShort, ErafReader.Validate(Recipes.ValidMinimal.AsMemory(0, length)));
        }
    }

    [Fact]
    public void FlippingAnySingleHeaderByteIsDetected()
    {
        for (int i = 0; i < ErafLimits.HeaderLength; i++)
        {
            byte[] file = (byte[])Recipes.ValidMinimal.Clone();
            file[i] ^= 0xFF;
            Assert.NotNull(ErafReader.Validate(file));
        }
    }

    [Fact]
    public void FlippingAnyPayloadByteWithHashesIsDetected()
    {
        for (int i = ErafLimits.HeaderLength + 64; i < Recipes.ValidHashes.Length; i++)
        {
            byte[] file = (byte[])Recipes.ValidHashes.Clone();
            file[i] ^= 0xFF;
            Assert.NotNull(ErafReader.Validate(file));
        }
    }

    [Fact]
    public void RandomGarbageNeverThrowsOrPasses()
    {
        var rng = new Random(42);
        foreach (int length in new[] { 0, 1, 4, 63, 64, 65, 100, 4096 })
        {
            byte[] junk = new byte[length];
            rng.NextBytes(junk);
            Assert.NotNull(ErafReader.Validate(junk));
        }
    }

    [Fact]
    public void NonSeekableStreamIsRejectedAsAnArgumentError()
    {
        using var compressor = new DeflateStream(new MemoryStream(), CompressionMode.Compress);
        Assert.Throws<ArgumentException>(() => ErafReader.TryRead(compressor, out _, out _));
    }

    [Fact]
    public void StreamReadStartsAtCurrentPosition()
    {
        byte[] padded = new byte[10].Concat(Recipes.ValidMinimal).ToArray();
        var stream = new MemoryStream(padded) { Position = 10 };
        Assert.Equal(Recipes.Image, ErafReader.Read(stream).Image.ToArray());
    }
}
