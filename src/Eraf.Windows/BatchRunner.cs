using Eraf.Format;
using Windows.Storage;

namespace Eraf.Windows;

/// <summary>
/// Makes one ERAF file per photo, all with the same voice note. One photo at a time is read, converted if it is not a JPEG,
/// wrapped, written atomically and re-validated, so memory stays small and one bad photo never stops the rest.
/// </summary>
public static class BatchRunner
{
    public static async Task<BatchSummary> RunAsync(
        IReadOnlyList<BatchTarget> targets,
        AudioClip clip,
        IProgress<(int Done, int Total, string Name)> progress,
        CancellationToken cancel)
    {
        int created = 0, replaced = 0, skipped = 0;
        var failed = new List<(string Name, string Reason)>();
        bool cancelled = false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (cancel.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            BatchTarget target = targets[i];
            string name = Path.GetFileName(target.Source);
            progress.Report((i, targets.Count, name));

            if (target.Action == BatchAction.Skip)
            {
                skipped++;
                continue;
            }

            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(target.Source);
                ImportResult imported = await ImageImportService.ImportFileAsync(file);
                if (imported.Session is null)
                {
                    failed.Add((name, imported.Error ?? "The photo could not be read."));
                    continue;
                }

                var session = new CaptureSession(imported.Session);
                session.SetAudio(clip);
                byte[] bytes = SessionFile.Build(session, DateTimeOffset.UtcNow);
                bool replace = target.Action == BatchAction.Replace;
                string path = target.Target;
                await Task.Run(() => AtomicFile.WriteAllBytes(
                    path, bytes, replaceExisting: replace,
                    verify: temp => ErafReader.Validate(File.ReadAllBytes(temp)) is null));
                if (replace)
                {
                    replaced++;
                }
                else
                {
                    created++;
                }
            }
            catch (Exception ex)
            {
                failed.Add((name, SaveErrorText.Describe(ex)));
            }
        }

        progress.Report((targets.Count, targets.Count, string.Empty));
        return new BatchSummary(created, replaced, skipped, failed, cancelled);
    }
}
