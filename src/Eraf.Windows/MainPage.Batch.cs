using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace Eraf.Windows;

/// <summary>Many photos (files and folders), one shared voice note: finding the photos, previewing the first, and making the files.</summary>
public sealed partial class MainPage
{
    /// <summary>The photos a batch will use. Only the first is ever shown; the rest are read one at a time when the files are made.</summary>
    private sealed record BatchContext(IReadOnlyList<string> Photos, bool Truncated);

    private BatchContext? _batch;

    /// <summary>True when Make ERAF has something to work on: a photo, a folder, or exactly one ERAF file to open.</summary>
    private static bool CanMake(IReadOnlyList<BrowserEntry> selection) =>
        selection.Any(e => e.Kind is EntryKind.Photo or EntryKind.Folder)
        || (selection.Count == 1 && selection[0].Kind == EntryKind.Eraf);

    private async void OnMakeErafClick(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<BrowserEntry> selection = _selection;
        if (_workflow.State != SessionState.Idle || _importBusy)
        {
            return;
        }

        if (selection.Count == 1 && selection[0].Kind != EntryKind.Folder)
        {
            await OpenEntryAsync(selection[0]); // one photo, or one ERAF file to open: the same as double-clicking it
        }
        else if (CanMake(selection))
        {
            await StartBatchAsync(
                selection.Where(s => s.Kind != EntryKind.Eraf).Select(s => s.FullPath).ToList(),
                selection.Any(s => s.Kind == EntryKind.Folder),
                note: null);
        }
        else
        {
            ShowInfo("Select one or more photos or folders in the list first.");
        }
    }

    /// <summary>Looks for photos in the selection, then shows the first one so a single voice note can be added for all of them.</summary>
    private async Task StartBatchAsync(IReadOnlyList<string> paths, bool anyFolder, string? note)
    {
        if (!_workflow.CanApply(SessionEvent.PhotoReady))
        {
            return;
        }

        AppOptions options = AppSettings.Options;

        ClearMessage();
        ShowInfo(anyFolder ? "Looking for photos..." : "Getting ready...");
        SetBusy(true);
        try
        {
            ScanResult scan = await Task.Run(() => PhotoScanner.Scan(paths, options.ScanSubfolders, PhotoScanner.DefaultMaxPhotos));
            if (scan.Photos.Count == 0)
            {
                ShowInfo(anyFolder && !options.ScanSubfolders
                    ? "No photos were found directly inside the selected folders. To look in their subfolders too, turn on \"Search subfolders\" in Settings."
                    : "No photos were found in the selection.");
                return;
            }

            // The first photo that can be read is shown, so a damaged first file does not block the rest.
            ImageSession? preview = null;
            foreach (string path in scan.Photos.Take(10))
            {
                try
                {
                    ImportResult result = await ImageImportService.ImportFileAsync(await StorageFile.GetFileFromPathAsync(path));
                    if (result.Session is not null && await TryPreviewAsync(result.Session))
                    {
                        preview = result.Session;
                        break;
                    }
                }
                catch (Exception)
                {
                    // Try the next photo.
                }
            }

            if (preview is null)
            {
                ShowError("None of the first photos could be opened. They may be damaged or in use.");
                return;
            }

            _sessionFolder = Path.GetDirectoryName(scan.Photos[0]);
            _batch = new BatchContext(scan.Photos, scan.Truncated);
            await ShowPhotoAsync(preview);
            if (_workflow.State == SessionState.Idle)
            {
                _batch = null;
                return;
            }

            int n = scan.Photos.Count;
            PhotoInfo.Text = $"{n} photo{(n == 1 ? string.Empty : "s")} selected. Showing {preview.SourceName}.";
            ShowInfo(
                $"{n} photo{(n == 1 ? " was" : "s were")} found"
                + (scan.Truncated ? $" (the first {PhotoScanner.DefaultMaxPhotos}; narrow the selection to include the rest)" : string.Empty)
                + ". Record or attach one voice note: it will be used for all of them."
                + (note is null ? string.Empty : " " + note));
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ---------------------------------------------------------------- drag and drop

    /// <summary>Dropping is only for starting something new: from the file list, with nothing else going on.</summary>
    private bool CanAcceptDrop =>
        _workflow.State == SessionState.Idle && !_importBusy && !_showingSettings && !_dialogOpen;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (CanAcceptDrop && e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Make ERAF";
            e.DragUIOverride.IsGlyphVisible = false;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }

        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!CanAcceptDrop || !e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        // Only the reading of the dropped items belongs inside the deferral; the work that follows can take a while.
        var paths = new List<string>();
        DragOperationDeferral deferral = e.GetDeferral();
        try
        {
            foreach (IStorageItem item in await e.DataView.GetStorageItemsAsync())
            {
                if (!string.IsNullOrEmpty(item.Path))
                {
                    paths.Add(item.Path);
                }
            }
        }
        catch (Exception)
        {
            // Treated as nothing usable below.
        }
        finally
        {
            deferral.Complete();
        }

        await HandleDropAsync(paths);
    }

    private async Task HandleDropAsync(IReadOnlyList<string> paths)
    {
        DropPlan plan = DropPlanner.Plan(paths);
        string? note = plan.Ignored > 0
            ? $"{plan.Ignored} dropped item{(plan.Ignored == 1 ? " was" : "s were")} left out because {(plan.Ignored == 1 ? "it is" : "they are")} not a photo or folder."
            : null;

        switch (plan.Kind)
        {
            case DropKind.OpenEraf:
                _sessionFolder = Path.GetDirectoryName(plan.Paths[0]);
                await OpenErafPathAsync(plan.Paths[0]);
                break;
            case DropKind.SinglePhoto:
                _sessionFolder = Path.GetDirectoryName(plan.Paths[0]);
                await ImportPhotoPathAsync(plan.Paths[0]);
                break;
            case DropKind.Batch:
                await StartBatchAsync(plan.Paths, plan.AnyFolder, note);
                break;
            default:
                ShowInfo(plan.Message ?? "Nothing was opened.");
                break;
        }
    }

    /// <summary>Makes an ERAF file for every photo in the batch with the voice note now held.</summary>
    private async Task SaveBatchAsync()
    {
        if (_batch is not { } batch || _session?.Audio is not { } clip || !_workflow.CanSave || _dialogOpen)
        {
            return;
        }

        ClearMessage();
        AppOptions options = AppSettings.Options;
        if (options.Output == OutputMode.OneFolder && (options.OutputFolder is null || !Directory.Exists(options.OutputFolder)))
        {
            ShowError("The folder for ERAF files is not set or no longer exists. Choose it in Settings, or save next to each photo.");
            return;
        }

        ExistingFilePolicy? policy;
        _dialogOpen = true;
        try
        {
            policy = await BatchDialogs.ChoosePolicyAsync(XamlRoot, batch.Photos.Count, batch.Truncated, options, clip.DurationMs);
        }
        finally
        {
            _dialogOpen = false;
        }

        if (policy is null || !_workflow.CanSave)
        {
            return;
        }

        List<BatchTarget> targets = await Task.Run(() =>
            BatchTargets.Plan(batch.Photos, options.Output, options.OutputFolder, policy.Value, File.Exists));
        if (!_workflow.TryApply(SessionEvent.BeginSave))
        {
            return;
        }

        using var cancel = new CancellationTokenSource();
        var box = new BatchDialogs.ProgressBox(XamlRoot, cancel);
        var progress = new Progress<(int Done, int Total, string Name)>(p => box.Update(p.Done, p.Total, p.Name));
        BatchSummary summary;
        _dialogOpen = true;
        Task shown = box.ShowAsync();
        try
        {
            summary = await BatchRunner.RunAsync(targets, clip, progress, cancel.Token);
        }
        finally
        {
            box.Close();
            await shown;
            _dialogOpen = false;
        }

        int made = summary.Created + summary.Replaced;
        bool nothingToDo = made == 0 && summary.Skipped > 0 && summary.Failures == 0 && !summary.Cancelled;
        if (made > 0 || nothingToDo)
        {
            _savedLabel = made > 0
                ? $"Saved: {made} ERAF file{(made == 1 ? string.Empty : "s")}"
                : "Nothing new to save: every file already exists";
            _workflow.TryApply(SessionEvent.SaveSucceeded);
        }
        else
        {
            _workflow.TryApply(SessionEvent.SaveFailed);
            ShowError("No ERAF files were made. Your photos and voice note are still here.");
        }

        await BatchDialogs.ShowSummaryAsync(XamlRoot, summary);
    }
}
