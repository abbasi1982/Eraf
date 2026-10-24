using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;

namespace Eraf.Windows;

/// <summary>The questions and reports around a batch: what to do about existing files, progress with Stop, the result, and About.</summary>
public static class BatchDialogs
{
    /// <summary>Dialogs live in their own layer, so they must be told which theme the window uses.</summary>
    public static ContentDialog Themed(ContentDialog dialog)
    {
        if (App.MainWindow is MainWindow window)
        {
            dialog.RequestedTheme = window.CurrentTheme;
        }

        return dialog;
    }

    /// <summary>Confirms a batch and asks what to do when a file with the same name exists. Null when cancelled.</summary>
    public static async Task<ExistingFilePolicy?> ChoosePolicyAsync(
        XamlRoot xamlRoot, int count, bool truncated, AppOptions options, ulong durationMs)
    {
        var panel = new StackPanel { Spacing = 12, MinWidth = 400 };
        panel.Children.Add(new TextBlock
        {
            Text = $"This makes {count} ERAF file{(count == 1 ? string.Empty : "s")}, each with its own photo and the same "
                + $"{TrimRange.Clock(durationMs)} voice note."
                + (truncated ? $" Only the first {PhotoScanner.DefaultMaxPhotos} photos found are included." : string.Empty),
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = options.Output == OutputMode.OneFolder && options.OutputFolder is { } folder
                ? $"Saved in: {folder}"
                : "Each file is saved next to its photo, with the photo's name.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.8,
        });

        var choices = new RadioButtons { Header = "If an ERAF file with that name already exists" };
        choices.Items.Add("Skip that photo");
        choices.Items.Add("Replace the existing file");
        choices.Items.Add("Keep both (add a number to the new name)");
        choices.SelectedIndex = (int)options.ExistingFiles;
        AutomationProperties.SetName(choices, "What to do when the file already exists");
        AutomationProperties.SetAutomationId(choices, "BatchPolicyChoices");
        panel.Children.Add(choices);

        var dialog = Themed(new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Make ERAF files",
            Content = panel,
            PrimaryButtonText = "Make ERAF files",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary ? (ExistingFilePolicy)Math.Max(0, choices.SelectedIndex) : null;
    }

    /// <summary>A progress box with a Stop button. Call <see cref="ProgressBox.Close"/> when the work ends.</summary>
    public sealed class ProgressBox
    {
        private readonly ContentDialog _dialog;
        private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 1 };
        private readonly TextBlock _text = new() { TextWrapping = TextWrapping.Wrap };
        private bool _closed;
        private bool _opened;

        public ProgressBox(XamlRoot xamlRoot, CancellationTokenSource cancel, string title = "Making ERAF files", bool indeterminate = false)
        {
            _bar.IsIndeterminate = indeterminate;
            var panel = new StackPanel { Spacing = 12, MinWidth = 380 };
            panel.Children.Add(_text);
            panel.Children.Add(_bar);
            AutomationProperties.SetName(_bar, "Progress");
            AutomationProperties.SetAutomationId(_bar, "BatchProgress");
            AutomationProperties.SetLiveSetting(_text, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            _dialog = Themed(new ContentDialog
            {
                XamlRoot = xamlRoot,
                Title = title,
                Content = panel,
                CloseButtonText = "Stop",
            });
            _dialog.Opened += (_, _) =>
            {
                _opened = true;
                if (_closed)
                {
                    _dialog.Hide();
                }
            };
            _dialog.CloseButtonClick += (_, args) =>
            {
                if (!_closed)
                {
                    cancel.Cancel();
                    _text.Text = "Stopping...";
                    args.Cancel = true; // stay open until the running photo has finished
                }
            };
        }

        /// <summary>Opens the box. Completes when it is closed.</summary>
        public Task ShowAsync() => _dialog.ShowAsync().AsTask();

        /// <summary>Replaces the message (used by the scan, which has no total to count towards).</summary>
        public void SetText(string text) => _text.Text = text;

        public void Update(int done, int total, string name)
        {
            _bar.Maximum = Math.Max(1, total);
            _bar.Value = done;
            if (name.Length > 0)
            {
                _text.Text = $"Photo {Math.Min(done + 1, total)} of {total}: {name}";
            }
        }

        public void Close()
        {
            _closed = true;
            if (_opened)
            {
                _dialog.Hide();
            }
        }
    }

    public static async Task ShowSummaryAsync(XamlRoot xamlRoot, BatchSummary summary)
    {
        var panel = new StackPanel { Spacing = 10, MinWidth = 420 };
        panel.Children.Add(new TextBlock { Text = summary.Headline, TextWrapping = TextWrapping.Wrap });
        if (summary.Failures > 0)
        {
            const int shown = 30;
            string lines = string.Join(
                "\n", summary.Failed.Take(shown).Select(f => $"{f.Name}: {f.Reason}"));
            if (summary.Failures > shown)
            {
                lines += $"\n...and {summary.Failures - shown} more.";
            }

            panel.Children.Add(new ScrollViewer
            {
                MaxHeight = 260,
                Content = new TextBlock { Text = lines, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, Opacity = 0.85 },
            });
        }

        await Themed(new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = summary.Failures > 0 ? "Finished with some problems" : "Finished",
            Content = panel,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        }).ShowAsync();
    }

    /// <summary>
    /// Asks for a name (rename, new folder). The box is filled and selected; the button stays off while the name is not usable and the
    /// reason is shown. <paramref name="fixedSuffix"/> (a file's extension) is shown after the box and added by the caller.
    /// Null when cancelled.
    /// </summary>
    public static async Task<string?> AskNameAsync(
        XamlRoot xamlRoot, string title, string prompt, string initial, string fixedSuffix, string primaryText, Func<string, string?> problemWith)
    {
        var box = new TextBox { Text = initial, MinWidth = 280 };
        AutomationProperties.SetName(box, prompt);
        AutomationProperties.SetAutomationId(box, "NameBox");
        var problem = new TextBlock { TextWrapping = TextWrapping.Wrap, Opacity = 0.85 };
        AutomationProperties.SetLiveSetting(problem, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        row.Children.Add(box);
        if (fixedSuffix.Length > 0)
        {
            row.Children.Add(new TextBlock { Text = fixedSuffix, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.8 });
        }

        var panel = new StackPanel { Spacing = 8, MinWidth = 340 };
        panel.Children.Add(new TextBlock { Text = prompt });
        panel.Children.Add(row);
        panel.Children.Add(problem);

        ContentDialog dialog = Themed(new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = panel,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });

        void Refresh()
        {
            string? why = problemWith(box.Text + fixedSuffix);
            problem.Text = why ?? string.Empty;
            dialog.IsPrimaryButtonEnabled = why is null;
        }

        box.TextChanged += (_, _) => Refresh();
        dialog.Opened += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
        Refresh();
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text + fixedSuffix : null;
    }

    /// <summary>Lists the open windows (front to back) and returns the one chosen, or null.</summary>
    public static async Task<CapturableWindow?> PickWindowAsync(XamlRoot xamlRoot, IReadOnlyList<CapturableWindow> windows)
    {
        var list = new ListView
        {
            ItemsSource = windows.Select(w => w.Title).ToList(),
            SelectionMode = ListViewSelectionMode.Single,
            MaxHeight = 320,
            MinWidth = 420,
            SelectedIndex = 0,
        };
        AutomationProperties.SetName(list, "Open windows");
        AutomationProperties.SetAutomationId(list, "WindowList");
        ContentDialog dialog = Themed(new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "Capture which window?",
            Content = list,
            PrimaryButtonText = "Capture",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        });
        list.DoubleTapped += (_, _) => dialog.Hide();
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary && list.SelectedIndex >= 0 ? windows[list.SelectedIndex] : null;
    }

    /// <summary>A yes/no question. True when the primary button was chosen; the safe button is the default.</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot xamlRoot, string title, string detail, string primaryText) =>
        await Themed(new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap, MinWidth = 340 },
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        }).ShowAsync() == ContentDialogResult.Primary;

    public static async Task ShowAboutAsync(XamlRoot xamlRoot)
    {
        await Themed(new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = "About ERAF",
            Content = new TextBlock
            {
                Text = "ERAF keeps one photo and one voice note together in a single .eraf file.\n\n"
                    + "Everything stays on this computer: the app does not use the internet.",
                TextWrapping = TextWrapping.Wrap,
                MinWidth = 380,
            },
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
        }).ShowAsync();
    }
}
