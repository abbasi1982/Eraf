using Eraf.Windows;

namespace Eraf.Format.Tests;

public sealed class ScreenshotRulesTests
{
    [Fact]
    public void NamesCarryTheTimeAndAreSafeFileNames()
    {
        var when = new DateTime(2026, 10, 24, 9, 5, 7);

        Assert.Equal("Screenshot 2026-10-24 090507", ScreenshotRules.ScreenshotName(when));
        Assert.Equal("Clipboard 2026-10-24 090507", ScreenshotRules.ClipboardName(when));
        Assert.Null(FileNames.Check(ScreenshotRules.ScreenshotName(when) + ".eraf"));
        Assert.NotEqual(ScreenshotRules.ScreenshotName(when), ScreenshotRules.ScreenshotName(when.AddSeconds(1)));
    }

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, true, false)]
    public void OffersOnlyWhenTheOptionIsOnAndNothingElseIsGoingOn(bool option, bool atList, bool busy, bool open, bool expected) =>
        Assert.Equal(expected, ScreenshotRules.CanOffer(option, atList, busy, open));

    [Theory]
    [InlineData("Notepad", 800, 600, false, true)]
    [InlineData("", 800, 600, false, false)]
    [InlineData(null, 800, 600, false, false)]
    [InlineData("   ", 800, 600, false, false)]
    [InlineData("Tiny", 10, 600, false, false)]
    [InlineData("Tiny", 800, 20, false, false)]
    [InlineData("Minimized", 800, 600, true, false)]
    public void OnlyRealVisibleWindowsAreListed(string? title, int w, int h, bool minimized, bool expected) =>
        Assert.Equal(expected, ScreenshotRules.IsCapturableWindow(title, w, h, minimized));

    [Theory]
    [InlineData(1920, 1080, true)]
    [InlineData(11520, 2160, true)]
    [InlineData(16384, 16384, true)]
    [InlineData(16385, 16384, false)]
    [InlineData(0, 100, false)]
    [InlineData(100, -1, false)]
    public void AbsurdSizesAreRefused(int w, int h, bool expected) => Assert.Equal(expected, ScreenshotRules.IsSaneSize(w, h));

    [Fact]
    public void TheHotkeyIsNamedForTheUser() => Assert.Equal("Ctrl+Shift+F9", ScreenshotRules.HotkeyLabel);
}
