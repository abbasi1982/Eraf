namespace Eraf.Windows;

// Pure System.IO and logic: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>The outcome of renaming or creating something: the new full path, or a message that is safe to show.</summary>
public sealed record EditResult(string? NewPath, string? Error)
{
    public bool Ok => NewPath is not null;
}

/// <summary>Rules for names the user types when renaming or creating a folder, in plain words.</summary>
public static class FileNames
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Whether <paramref name="name"/> could be a file or folder name on Windows. Null when fine, otherwise why not.</summary>
    public static string? Check(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Type a name.";
        }

        if (name.Length > 255)
        {
            return "That name is too long.";
        }

        if (name.Any(c => c < ' ' || "\\/:*?\"<>|".Contains(c)))
        {
            return "A name cannot contain any of these: \\ / : * ? \" < > |";
        }

        if (name is "." or ".." || name.EndsWith('.') || name.EndsWith(' ') || name.StartsWith(' '))
        {
            return "A name cannot start with a space or end with a space or a dot.";
        }

        string stem = Path.GetFileNameWithoutExtension(name);
        if (Reserved.Contains(stem) || Reserved.Contains(name))
        {
            return "Windows keeps that name for itself. Choose another.";
        }

        return null;
    }

    /// <summary>
    /// <see cref="Check"/> plus: nothing else in <paramref name="folder"/> already has that name (letters of different case count as
    /// the same name, except for the item being renamed itself, so changing only the case is allowed).
    /// </summary>
    public static string? CheckNew(string name, string folder, string? renaming)
    {
        string? problem = Check(name);
        if (problem is not null)
        {
            return problem;
        }

        string target = Path.Combine(folder, name);
        bool taken = File.Exists(target) || Directory.Exists(target);
        bool sameItem = renaming is not null && string.Equals(Path.GetFullPath(renaming), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase);
        return taken && !sameItem ? "Something with that name is already here." : null;
    }

    /// <summary>"New folder", or "New folder (2)" and so on when that is taken.</summary>
    public static string UniqueFolderName(string parent, string baseName = "New folder")
    {
        string name = baseName;
        for (int n = 2; Directory.Exists(Path.Combine(parent, name)) || File.Exists(Path.Combine(parent, name)); n++)
        {
            name = $"{baseName} ({n})";
        }

        return name;
    }

    /// <summary>For a file the extension stays as it is and only the part before it is edited; a folder is edited whole.</summary>
    public static (string Editable, string Fixed) SplitForRename(string name, bool isFolder) =>
        isFolder ? (name, string.Empty) : (Path.GetFileNameWithoutExtension(name), Path.GetExtension(name));
}

/// <summary>Renaming and creating folders on disk, with every failure turned into a plain message.</summary>
public static class FileEditing
{
    public static EditResult Rename(string path, string newName)
    {
        string? folder = Path.GetDirectoryName(path);
        if (folder is null)
        {
            return new EditResult(null, "That item cannot be renamed.");
        }

        string? problem = FileNames.CheckNew(newName, folder, path);
        if (problem is not null)
        {
            return new EditResult(null, problem);
        }

        string target = Path.Combine(folder, newName);
        try
        {
            if (string.Equals(path, target, StringComparison.Ordinal))
            {
                return new EditResult(path, null); // nothing to do
            }

            if (Directory.Exists(path))
            {
                Directory.Move(path, target);
            }
            else
            {
                File.Move(path, target, overwrite: false);
            }

            return new EditResult(target, null);
        }
        catch (Exception ex)
        {
            return new EditResult(null, Explain(ex, "renamed"));
        }
    }

    public static EditResult CreateFolder(string parent, string name)
    {
        string? problem = FileNames.CheckNew(name, parent, renaming: null);
        if (problem is not null)
        {
            return new EditResult(null, problem);
        }

        try
        {
            return new EditResult(Directory.CreateDirectory(Path.Combine(parent, name)).FullName, null);
        }
        catch (Exception ex)
        {
            return new EditResult(null, Explain(ex, "created"));
        }
    }

    private static string Explain(Exception ex, string verb) => ex switch
    {
        UnauthorizedAccessException => $"Windows did not allow it, so nothing was {verb}. The item may be read-only or protected.",
        PathTooLongException => "The new path would be too long.",
        DirectoryNotFoundException or FileNotFoundException => "The item is no longer there. Refresh the list and try again.",
        IOException => $"It could not be {verb}. It may be open in another app.",
        _ => $"It could not be {verb}.",
    };
}

/// <summary>Whether Windows will put a deleted item in the Recycle Bin (so it can be brought back) or delete it for good.</summary>
public static class RecycleSupport
{
    /// <summary>
    /// Items on an ordinary local drive are recycled. Items on removable or network drives, and in the Temp folder, are not: Windows
    /// deletes those at once. <paramref name="driveTypeOf"/> and <paramref name="tempPath"/> are injected so this can be tested.
    /// </summary>
    public static bool IsRecoverable(string path, Func<string, DriveType?> driveTypeOf, string tempPath)
    {
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }

        string temp = Path.GetFullPath(tempPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (full.StartsWith(temp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return driveTypeOf(full) == DriveType.Fixed;
    }

    /// <summary>The real thing: asks the drive its type.</summary>
    public static bool IsRecoverable(string path) => IsRecoverable(
        path,
        full =>
        {
            try
            {
                return new DriveInfo(Path.GetPathRoot(full) ?? string.Empty).DriveType;
            }
            catch (Exception)
            {
                return null;
            }
        },
        Path.GetTempPath());
}

/// <summary>The words used to ask before deleting. They say "Recycle Bin" only for items that really go there.</summary>
public static class DeleteWording
{
    /// <param name="notRecoverable">How many of <paramref name="items"/> Windows will delete for good (see <see cref="RecycleSupport"/>).</param>
    public static string Title(IReadOnlyList<BrowserEntry> items, int notRecoverable = 0)
    {
        if (notRecoverable == 0)
        {
            return items.Count == 1 ? $"Move \"{items[0].Name}\" to the Recycle Bin?" : $"Move {items.Count} items to the Recycle Bin?";
        }

        return items.Count == 1 ? $"Delete \"{items[0].Name}\" for good?" : $"Delete {items.Count} items?";
    }

    public static string Detail(IReadOnlyList<BrowserEntry> items, int notRecoverable = 0)
    {
        int folders = items.Count(i => i.Kind == EntryKind.Folder);
        int files = items.Count - folders;
        string parts = (folders, files) switch
        {
            (0, _) => $"{files} file{(files == 1 ? string.Empty : "s")}",
            (_, 0) => $"{folders} folder{(folders == 1 ? string.Empty : "s")} and everything inside {(folders == 1 ? "it" : "them")}",
            _ => $"{folders} folder{(folders == 1 ? string.Empty : "s")} (with everything inside) and {files} file{(files == 1 ? string.Empty : "s")}",
        };
        if (notRecoverable == 0)
        {
            return $"This moves {parts} to the Recycle Bin. You can put {(items.Count == 1 ? "it" : "them")} back from there.";
        }

        string gone = notRecoverable == items.Count
            ? $"Windows cannot use the Recycle Bin here (a removable or network drive, or the Temp folder), so {(items.Count == 1 ? "it is" : "they are")} deleted for good and cannot be brought back."
            : $"{notRecoverable} of them are on a removable or network drive (or in the Temp folder), where Windows cannot use the Recycle Bin, so those are deleted for good. The others go to the Recycle Bin.";
        return $"This deletes {parts}. {gone}";
    }

    public static string PrimaryButton(int count, int notRecoverable) =>
        notRecoverable == 0 ? "Move to Recycle Bin" : notRecoverable == count ? "Delete for good" : "Delete";

    /// <summary>What to say afterwards.</summary>
    public static string Done(IReadOnlyList<BrowserEntry> items, int moved, int notRecoverable)
    {
        if (notRecoverable == 0)
        {
            return moved == 1 ? $"Moved \"{items[0].Name}\" to the Recycle Bin." : $"Moved {moved} items to the Recycle Bin.";
        }

        return moved == 1 ? $"Deleted \"{items[0].Name}\"." : $"Deleted {moved} items ({notRecoverable} for good).";
    }
}
