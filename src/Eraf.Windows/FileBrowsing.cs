namespace Eraf.Windows;

// Pure System.IO and logic: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>What a row in the file list is. Only folders and the file types the app understands are ever listed.</summary>
public enum EntryKind
{
    Folder,
    Eraf,
    Photo,
}

/// <summary>One row of the file list.</summary>
public sealed record BrowserEntry(string Name, string FullPath, EntryKind Kind, long SizeBytes, DateTime ModifiedLocal);

public enum SortKey
{
    Name,
    Modified,
    Size,
    Type,
}

/// <summary>Which files the browser shows, and how they are named.</summary>
public static class FileKinds
{
    public static readonly string[] PhotoExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };

    public const string ErafExtension = ".eraf";

    /// <summary>The kind of a file by its name, or null when the app does not handle it (so it is not listed).</summary>
    public static EntryKind? ClassifyFile(string fileName)
    {
        string extension = Path.GetExtension(fileName);
        if (extension.Equals(ErafExtension, StringComparison.OrdinalIgnoreCase))
        {
            return EntryKind.Eraf;
        }

        foreach (string photo in PhotoExtensions)
        {
            if (extension.Equals(photo, StringComparison.OrdinalIgnoreCase))
            {
                return EntryKind.Photo;
            }
        }

        return null;
    }

    public static string TypeLabel(EntryKind kind) => kind switch
    {
        EntryKind.Folder => "File folder",
        EntryKind.Eraf => "ERAF file",
        _ => "Photo",
    };
}

/// <summary>Sorting, filtering and size wording for the file list.</summary>
public static class BrowserList
{
    /// <summary>Folders first (always), then the chosen key; ties fall back to the name.</summary>
    public static List<BrowserEntry> Sort(IEnumerable<BrowserEntry> entries, SortKey key, bool ascending)
    {
        var folders = entries.Where(e => e.Kind == EntryKind.Folder).ToList();
        var files = entries.Where(e => e.Kind != EntryKind.Folder).ToList();
        Order(folders, key == SortKey.Modified ? SortKey.Modified : SortKey.Name, ascending);
        Order(files, key, ascending);
        folders.AddRange(files);
        return folders;
    }

    private static void Order(List<BrowserEntry> list, SortKey key, bool ascending)
    {
        Comparison<BrowserEntry> by = key switch
        {
            SortKey.Modified => (a, b) => a.ModifiedLocal.CompareTo(b.ModifiedLocal),
            SortKey.Size => (a, b) => a.SizeBytes.CompareTo(b.SizeBytes),
            SortKey.Type => (a, b) => StringComparer.OrdinalIgnoreCase.Compare(FileKinds.TypeLabel(a.Kind), FileKinds.TypeLabel(b.Kind)),
            _ => (a, b) => NameComparer.Compare(a.Name, b.Name),
        };
        list.Sort((a, b) =>
        {
            int result = by(a, b);
            if (!ascending)
            {
                result = -result;
            }

            // Equal on the key: the name decides, always A to Z, so the order is stable and predictable.
            return result != 0 ? result : NameComparer.Compare(a.Name, b.Name);
        });
    }

    private static readonly StringComparer NameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>Entries whose name contains the text (any case). Blank text keeps everything.</summary>
    public static List<BrowserEntry> Filter(IEnumerable<BrowserEntry> entries, string? text)
    {
        string trimmed = text?.Trim() ?? string.Empty;
        return trimmed.Length == 0
            ? entries.ToList()
            : entries.Where(e => e.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>"512 B", "1.5 KB", "3.2 MB", "1.1 GB". Folders have no size, so callers show nothing for them.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{Math.Max(bytes, 0)} B";
        }

        double value = bytes;
        string[] units = { "KB", "MB", "GB", "TB" };
        int unit = -1;
        do
        {
            value /= 1024;
            unit++;
        }
        while (value >= 1024 && unit < units.Length - 1);

        return $"{value:0.0} {units[unit]}";
    }
}

/// <summary>The result of reading a folder: its entries, or a plain message when it could not be read.</summary>
public sealed record FolderListing(IReadOnlyList<BrowserEntry> Entries, string? Error);

/// <summary>Reads folders from disk for the browser. Never throws for an unreadable folder.</summary>
public static class FolderLister
{
    /// <summary>Visible subfolders plus the .eraf and photo files in <paramref name="path"/>. Hidden and system items are skipped.</summary>
    public static FolderListing List(string path)
    {
        var entries = new List<BrowserEntry>();
        try
        {
            var directory = new DirectoryInfo(path);
            foreach (FileSystemInfo item in directory.EnumerateFileSystemInfos())
            {
                if ((item.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    continue;
                }

                if (item is DirectoryInfo folder)
                {
                    entries.Add(new BrowserEntry(folder.Name, folder.FullName, EntryKind.Folder, 0, SafeTime(folder)));
                }
                else if (item is FileInfo file && FileKinds.ClassifyFile(file.Name) is { } kind)
                {
                    entries.Add(new BrowserEntry(file.Name, file.FullName, kind, SafeLength(file), SafeTime(file)));
                }
            }

            return new FolderListing(entries, null);
        }
        catch (UnauthorizedAccessException)
        {
            return new FolderListing(entries, "You do not have permission to open this folder.");
        }
        catch (DirectoryNotFoundException)
        {
            return new FolderListing(entries, "This folder does not exist any more.");
        }
        catch (IOException)
        {
            return new FolderListing(entries, "This location is not available. A drive may be disconnected or not ready.");
        }
        catch (Exception)
        {
            return new FolderListing(entries, "This folder could not be read.");
        }
    }

    /// <summary>Names and paths of the visible subfolders, sorted by name (for the folder tree). Empty when unreadable.</summary>
    public static List<(string Name, string Path)> ListSubfolders(string path)
    {
        var result = new List<(string, string)>();
        try
        {
            foreach (DirectoryInfo folder in new DirectoryInfo(path).EnumerateDirectories())
            {
                if ((folder.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
                {
                    result.Add((folder.Name, folder.FullName));
                }
            }
        }
        catch (Exception)
        {
            // Unreadable folders simply show no children.
        }

        result.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Item1, b.Item1));
        return result;
    }

    /// <summary>The folder above <paramref name="path"/>, or null at a drive root.</summary>
    public static string? Parent(string path)
    {
        string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (trimmed.Length == 0)
        {
            return null;
        }

        string? parent = Path.GetDirectoryName(trimmed);
        return string.IsNullOrEmpty(parent) ? null : parent;
    }

    private static DateTime SafeTime(FileSystemInfo info)
    {
        try
        {
            return info.LastWriteTime;
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }

    private static long SafeLength(FileInfo file)
    {
        try
        {
            return file.Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }
}

/// <summary>Back / forward / up bookkeeping for the address bar. Pure: it only remembers paths.</summary>
public sealed class FolderHistory
{
    private readonly List<string> _back = new();
    private readonly List<string> _forward = new();

    public string? Current { get; private set; }

    public bool CanBack => _back.Count > 0;

    public bool CanForward => _forward.Count > 0;

    /// <summary>Goes to a new folder (clears forward history). Going to the folder already shown changes nothing.</summary>
    public void Navigate(string path)
    {
        if (string.Equals(Current, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Current is not null)
        {
            _back.Add(Current);
        }

        _forward.Clear();
        Current = path;
    }

    public string? Back()
    {
        if (_back.Count == 0 || Current is null)
        {
            return null;
        }

        _forward.Add(Current);
        Current = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        return Current;
    }

    public string? Forward()
    {
        if (_forward.Count == 0 || Current is null)
        {
            return null;
        }

        _back.Add(Current);
        Current = _forward[^1];
        _forward.RemoveAt(_forward.Count - 1);
        return Current;
    }
}

/// <summary>The fixed places shown at the top of the folder tree.</summary>
public static class Places
{
    public static List<(string Name, string Path)> QuickAccess()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new (string Name, string Path)[]
        {
            ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
            ("Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
            ("Downloads", string.IsNullOrEmpty(profile) ? string.Empty : Path.Combine(profile, "Downloads")),
        };
        return candidates.Where(c => c.Path.Length > 0 && Directory.Exists(c.Path)).ToList();
    }

    /// <summary>Ready drives as ("Local Disk (C:)", "C:\").</summary>
    public static List<(string Name, string Path)> Drives()
    {
        var result = new List<(string, string)>();
        try
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                string letter = drive.Name.TrimEnd('\\');
                string label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
                    ? drive.DriveType switch
                    {
                        DriveType.Removable => "Removable Disk",
                        DriveType.Network => "Network Drive",
                        DriveType.CDRom => "Disc Drive",
                        _ => "Local Disk",
                    }
                    : drive.VolumeLabel;
                result.Add(($"{label} ({letter})", drive.Name));
            }
        }
        catch (Exception)
        {
            // A drive that vanishes while listing is just left out.
        }

        return result;
    }
}

/// <summary>How the file list is laid out, like the View choices in File Explorer.</summary>
public enum ViewMode
{
    /// <summary>One row per item with Name, Date modified, Size and Type columns (sortable).</summary>
    Details,

    /// <summary>One compact row per item: a small icon and the name.</summary>
    List,

    /// <summary>Items flow across the window as a small icon and a name.</summary>
    SmallIcons,

    /// <summary>Items flow across the window with a medium thumbnail above the name.</summary>
    MediumIcons,

    /// <summary>Items flow across the window with a large thumbnail above the name.</summary>
    LargeIcons,

    /// <summary>Items flow across the window as a thumbnail with the name and a line about the item beside it.</summary>
    Tiles,
}

/// <summary>What each <see cref="ViewMode"/> is called, whether it flows like a grid, and how big its thumbnails are.</summary>
public sealed record ViewModeInfo(ViewMode Mode, string Label, bool IsGrid, int ThumbnailPixels);

public static class ViewModes
{
    /// <summary>In the order they are offered.</summary>
    public static IReadOnlyList<ViewModeInfo> All { get; } = new ViewModeInfo[]
    {
        new(ViewMode.Details, "Details", false, 72),
        new(ViewMode.Tiles, "Tiles", true, 112),
        new(ViewMode.SmallIcons, "Small icons", true, 48),
        new(ViewMode.MediumIcons, "Medium icons", true, 160),
        new(ViewMode.LargeIcons, "Large icons", true, 256),
        new(ViewMode.List, "List", false, 48),
    };

    public static ViewModeInfo For(ViewMode mode) => All.First(m => m.Mode == mode);

    public static int IndexOf(ViewMode mode) => Math.Max(0, All.ToList().FindIndex(m => m.Mode == mode));
}
