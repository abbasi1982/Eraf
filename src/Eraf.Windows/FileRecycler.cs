using Windows.Storage;

namespace Eraf.Windows;

/// <summary>Moves files and folders to the Recycle Bin (never deletes for good: if Windows cannot recycle something, it stays and is reported).</summary>
public static class FileRecycler
{
    public sealed record Outcome(int Moved, IReadOnlyList<string> Failed);

    public static async Task<Outcome> RecycleAsync(IEnumerable<BrowserEntry> items)
    {
        int moved = 0;
        var failed = new List<string>();
        foreach (BrowserEntry item in items)
        {
            try
            {
                IStorageItem target = item.Kind == EntryKind.Folder
                    ? await StorageFolder.GetFolderFromPathAsync(item.FullPath)
                    : await StorageFile.GetFileFromPathAsync(item.FullPath);
                // "Default" sends it to the Recycle Bin where Windows can; it is not "PermanentDelete".
                await target.DeleteAsync(StorageDeleteOption.Default);
                moved++;
            }
            catch (Exception)
            {
                failed.Add(item.Name);
            }
        }

        return new Outcome(moved, failed);
    }
}
