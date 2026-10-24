using Eraf.Windows;

namespace Eraf.Format.Tests;

public sealed class TrimRangeTests
{
    [Theory]
    [InlineData(1000UL, 5000UL, 10000UL, TrimCheck.Ok)]
    [InlineData(0UL, 5000UL, 10000UL, TrimCheck.Ok)]
    [InlineData(5000UL, 10000UL, 10000UL, TrimCheck.Ok)]
    [InlineData(0UL, 10000UL, 10000UL, TrimCheck.WholeClip)]
    [InlineData(5000UL, 5000UL, 10000UL, TrimCheck.NotInOrder)]
    [InlineData(6000UL, 5000UL, 10000UL, TrimCheck.NotInOrder)]
    [InlineData(1000UL, 10001UL, 10000UL, TrimCheck.OutOfRange)]
    [InlineData(1000UL, 1299UL, 10000UL, TrimCheck.TooShort)]
    [InlineData(1000UL, 1300UL, 10000UL, TrimCheck.Ok)]
    public void CheckClassifiesEveryCase(ulong start, ulong end, ulong duration, TrimCheck expected) =>
        Assert.Equal(expected, TrimRange.Check(start, end, duration));

    [Fact]
    public void LengthIsTheKeptPartAndNeverNegative()
    {
        Assert.Equal(4000UL, new TrimRange(1000, 5000).LengthMs);
        Assert.Equal(0UL, new TrimRange(5000, 1000).LengthMs);
    }

    [Theory]
    [InlineData(0UL, "0:00.0")]
    [InlineData(2500UL, "0:02.5")]
    [InlineData(65400UL, "1:05.4")]
    [InlineData(599900UL, "9:59.9")]
    public void ClockShowsMinutesSecondsAndTenths(ulong ms, string expected) =>
        Assert.Equal(expected, TrimRange.Clock(ms));

    [Fact]
    public void DescribeSaysWhatIsKept() =>
        Assert.Equal("Keeps 0:02.0 to 0:08.0 (6.0 s)", TrimRange.Describe(2000, 8000));

    [Fact]
    public void EveryFailedCheckHasAMessageAndOkHasNone()
    {
        foreach (TrimCheck check in Enum.GetValues<TrimCheck>())
        {
            Assert.Equal(check == TrimCheck.Ok, TrimRange.Explain(check).Length == 0);
        }
    }
}
