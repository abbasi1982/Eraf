using Eraf.Windows;

namespace Eraf.Format.Tests;

/// <summary>The microphone dropdown: list building and which entry is selected.</summary>
public sealed class MicrophoneChoiceTests
{
    [Fact]
    public void ListStartsWithTheDefaultMicrophoneEvenWithNoDevices()
    {
        var list = MicrophoneChoice.Build(Array.Empty<(string, string)>());

        Assert.Single(list);
        Assert.Null(list[0].Id);
        Assert.Equal(MicrophoneChoice.DefaultName, list[0].Name);
    }

    [Fact]
    public void DevicesFollowTheDefaultInOrderAndDuplicatesAndBlankIdsAreDropped()
    {
        var list = MicrophoneChoice.Build(new[] { ("a", "USB Mic"), ("b", "Headset"), ("a", "USB Mic again"), ("", "No id"), (" ", "Blank id") });

        Assert.Equal(new[] { null, "a", "b" }, list.Select(o => o.Id));
        Assert.Equal(new[] { MicrophoneChoice.DefaultName, "USB Mic", "Headset" }, list.Select(o => o.Name));
    }

    [Fact]
    public void UnnamedDevicesGetANumberedLabel()
    {
        var list = MicrophoneChoice.Build(new[] { ("a", ""), ("b", "  "), ("c", "Real name") });

        Assert.Equal(new[] { MicrophoneChoice.DefaultName, "Microphone 1", "Microphone 2", "Real name" }, list.Select(o => o.Name));
    }

    [Fact]
    public void ResolveKeepsARememberedDeviceThatIsStillPresent()
    {
        var list = MicrophoneChoice.Build(new[] { ("a", "A"), ("b", "B") });

        Assert.Equal("b", MicrophoneChoice.Resolve(list, "b").Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("gone")]
    [InlineData("")]
    public void ResolveFallsBackToTheDefaultWhenNothingOrAMissingDeviceIsRemembered(string? remembered)
    {
        var list = MicrophoneChoice.Build(new[] { ("a", "A") });

        Assert.Null(MicrophoneChoice.Resolve(list, remembered).Id);
    }

    [Fact]
    public void SameListComparesDevicesAndOrder()
    {
        var one = MicrophoneChoice.Build(new[] { ("a", "A"), ("b", "B") });
        var same = MicrophoneChoice.Build(new[] { ("a", "A"), ("b", "B") });
        var renamed = MicrophoneChoice.Build(new[] { ("a", "A2"), ("b", "B") });
        var reordered = MicrophoneChoice.Build(new[] { ("b", "B"), ("a", "A") });
        var shorter = MicrophoneChoice.Build(new[] { ("a", "A") });

        Assert.True(MicrophoneChoice.SameList(one, same));
        Assert.False(MicrophoneChoice.SameList(one, renamed));
        Assert.False(MicrophoneChoice.SameList(one, reordered));
        Assert.False(MicrophoneChoice.SameList(one, shorter));
    }
}
