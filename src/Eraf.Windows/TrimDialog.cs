using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Eraf.Windows;

/// <summary>
/// Asks which part of a voice note to keep: start and end sliders in tenths of a second, a live summary,
/// and a Play selection button that previews exactly that part. Returns null when cancelled.
/// </summary>
public static class TrimDialog
{
    public static async Task<TrimRange?> ShowAsync(XamlRoot xamlRoot, AudioClip clip)
    {
        double totalSeconds = clip.DurationMs / 1000.0;
        var start = new Slider
        {
            Header = "Start",
            Minimum = 0,
            Maximum = totalSeconds,
            StepFrequency = 0.1,
            SmallChange = 0.1,
            LargeChange = 1,
            Value = 0,
            ThumbToolTipValueConverter = new SecondsConverter(),
        };
        var end = new Slider
        {
            Header = "End",
            Minimum = 0,
            Maximum = totalSeconds,
            StepFrequency = 0.1,
            SmallChange = 0.1,
            LargeChange = 1,
            Value = totalSeconds,
            ThumbToolTipValueConverter = new SecondsConverter(),
        };
        AutomationProperties.SetName(start, "Trim start");
        AutomationProperties.SetHelpText(start, "Where the kept part begins, in seconds. Arrow keys move by a tenth of a second.");
        AutomationProperties.SetAutomationId(start, "TrimStartSlider");
        AutomationProperties.SetName(end, "Trim end");
        AutomationProperties.SetHelpText(end, "Where the kept part ends, in seconds. Arrow keys move by a tenth of a second.");
        AutomationProperties.SetAutomationId(end, "TrimEndSlider");

        var summary = new TextBlock { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetLiveSetting(summary, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        var problem = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.8 };
        var preview = new Button { Content = "Play selection" };
        AutomationProperties.SetName(preview, "Play selection");
        AutomationProperties.SetHelpText(preview, "Listen to the part that would be kept.");
        AutomationProperties.SetAutomationId(preview, "TrimPreviewButton");

        var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
        panel.Children.Add(new TextBlock
        {
            Text = $"This voice note is {TrimRange.Clock(clip.DurationMs)} long. Choose the part to keep.",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(start);
        panel.Children.Add(end);
        panel.Children.Add(summary);
        panel.Children.Add(problem);
        panel.Children.Add(preview);

        ContentDialog dialog = BatchDialogs.Themed(new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Trim voice note",
            Content = panel,
            PrimaryButtonText = "Trim",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        ulong StartMs() => Math.Min(clip.DurationMs, (ulong)Math.Round(start.Value * 10) * 100);
        ulong EndMs() => Math.Min(clip.DurationMs, (ulong)Math.Round(end.Value * 10) * 100);

        void Refresh()
        {
            ulong s = StartMs();
            ulong e = EndMs();
            // The very end of the clip is not always a whole tenth: keep the real length when the slider is at the maximum.
            if (end.Value >= totalSeconds)
            {
                e = clip.DurationMs;
            }

            TrimCheck check = TrimRange.Check(s, e, clip.DurationMs);
            summary.Text = TrimRange.Describe(s, e);
            problem.Text = TrimRange.Explain(check);
            dialog.IsPrimaryButtonEnabled = check == TrimCheck.Ok;
            preview.IsEnabled = check is TrimCheck.Ok or TrimCheck.WholeClip;
        }

        start.ValueChanged += (_, _) => Refresh();
        end.ValueChanged += (_, _) => Refresh();
        Refresh();

        ClipPlayer? player = null;
        var stopTimer = new DispatcherTimerLite();
        ulong playEnd = 0;
        void StopPreview()
        {
            stopTimer.Stop();
            player?.Stop();
            preview.Content = "Play selection";
            AutomationProperties.SetName(preview, "Play selection");
        }

        stopTimer.Tick += () =>
        {
            if (player is not null && player.PositionMs >= playEnd)
            {
                StopPreview();
            }
        };
        preview.Click += async (_, _) =>
        {
            if (stopTimer.IsRunning)
            {
                StopPreview();
                return;
            }

            player ??= await ClipPlayer.CreateAsync(clip);
            playEnd = end.Value >= totalSeconds ? clip.DurationMs : EndMs();
            player.PlayFrom(StartMs());
            preview.Content = "Stop";
            AutomationProperties.SetName(preview, "Stop playing selection");
            stopTimer.Start();
        };

        try
        {
            ContentDialogResult result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
            {
                return null;
            }

            ulong s = StartMs();
            ulong e = end.Value >= totalSeconds ? clip.DurationMs : EndMs();
            return TrimRange.Check(s, e, clip.DurationMs) == TrimCheck.Ok ? new TrimRange(s, e) : null;
        }
        finally
        {
            stopTimer.Stop();
            player?.Dispose();
        }
    }

    /// <summary>Shows slider values as seconds with one decimal (the default would show 0 to 100 style numbers).</summary>
    private sealed class SecondsConverter : Microsoft.UI.Xaml.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language) =>
            $"{(double)value:0.0} s";

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }

    /// <summary>A 100 ms UI timer with a plain callback.</summary>
    private sealed class DispatcherTimerLite
    {
        private readonly Microsoft.UI.Xaml.DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };

        public DispatcherTimerLite() => _timer.Tick += (_, _) => Tick?.Invoke();

        public event Action? Tick;

        public bool IsRunning => _timer.IsEnabled;

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();
    }
}
