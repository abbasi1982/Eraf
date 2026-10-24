namespace Eraf.Windows;

// Pure System.IO: no WinUI / WinRT types. tests/Eraf.Format.Tests compiles this file directly (linked source).

/// <summary>
/// Writes a file so that a failure at any point leaves any existing file at the target untouched
/// (ERAF_FORMAT_SPEC.md §11): build the whole file in a uniquely named temp file in the same directory,
/// flush it to disk, optionally verify it, then swap it into place. The temp file is removed on every failure path.
/// </summary>
public static class AtomicFile
{
    /// <summary>
    /// Writes <paramref name="bytes"/> to <paramref name="targetPath"/>.
    /// </summary>
    /// <param name="replaceExisting">
    /// When false, an existing target is an error. When true, it is replaced atomically with
    /// <see cref="File.Replace(string, string, string?)"/>; the caller is expected to have asked the user first.
    /// </param>
    /// <param name="verify">
    /// Optional check run on the finished temp file before it is swapped in (for example re-reading it with
    /// the ERAF validator). Return false to abort; the target is then left alone.
    /// </param>
    /// <exception cref="IOException">The target exists and replacing is not allowed, or the swap failed.</exception>
    /// <exception cref="InvalidDataException"><paramref name="verify"/> rejected the temp file.</exception>
    public static void WriteAllBytes(string targetPath, ReadOnlySpan<byte> bytes, bool replaceExisting, Func<string, bool>? verify = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetPath);
        string fullTarget = Path.GetFullPath(targetPath);
        string directory = Path.GetDirectoryName(fullTarget)
            ?? throw new ArgumentException("The path has no directory.", nameof(targetPath));
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The folder \"{directory}\" does not exist.");
        }

        if (!replaceExisting && File.Exists(fullTarget))
        {
            throw new IOException($"\"{Path.GetFileName(fullTarget)}\" already exists.");
        }

        RemoveOrphanedPartials(directory, Path.GetFileName(fullTarget));

        // Same directory (so the swap is on one volume and atomic), unique name (so nothing is clobbered).
        string temp = Path.Combine(directory, $"{Path.GetFileName(fullTarget)}.{Guid.NewGuid():N}.partial");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            if (verify is not null && !verify(temp))
            {
                throw new InvalidDataException("The file that was just written did not pass verification.");
            }

            if (File.Exists(fullTarget))
            {
                if (!replaceExisting)
                {
                    throw new IOException($"\"{Path.GetFileName(fullTarget)}\" already exists.");
                }

                File.Replace(temp, fullTarget, destinationBackupFileName: null);
            }
            else
            {
                // overwrite: false, so a file that appeared since the check above is never silently replaced.
                File.Move(temp, fullTarget, overwrite: false);
            }
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>A temp file is only deleted as an orphan once it has been untouched this long (a live save rewrites it constantly).</summary>
    internal static readonly TimeSpan OrphanAge = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Deletes leftovers of an earlier crashed save of this same target: files named exactly
    /// <c>{target name}.{32 hex digits}.partial</c> that are old enough and not open elsewhere. Nothing else is touched.
    /// </summary>
    internal static void RemoveOrphanedPartials(string directory, string targetName)
    {
        try
        {
            foreach (string path in Directory.EnumerateFiles(directory, targetName + ".*.partial"))
            {
                string name = Path.GetFileName(path);
                string middle = name.Substring(targetName.Length + 1, name.Length - targetName.Length - 1 - ".partial".Length);
                if (middle.Length != 32 || !middle.All(Uri.IsHexDigit))
                {
                    continue;
                }

                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) >= OrphanAge)
                {
                    TryDelete(path);
                }
            }
        }
        catch (Exception)
        {
            // Cleanup is a courtesy; it must never stop a save.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Best effort: a leftover .partial is harmless and fails ERAF validation if opened.
        }
    }
}

/// <summary>User-facing wording for a failed save or export. Never includes a path or a raw system message.</summary>
public static class SaveErrorText
{
    private const int ErrorHandleDiskFull = unchecked((int)0x80070027);
    private const int ErrorDiskFull = unchecked((int)0x80070070);

    public static string Describe(Exception ex) => ex switch
    {
        UnauthorizedAccessException =>
            "Windows did not allow writing to that file or folder. Choose a different name or folder (for example Documents), or check that it is not read-only.",
        DirectoryNotFoundException => "That folder no longer exists. Choose another folder.",
        PathTooLongException => "That file path is too long. Choose a shorter name or a folder closer to the top.",
        InvalidDataException => "The file was written but did not pass the final check, so it was not kept.",
        IOException io when io.HResult is ErrorDiskFull or ErrorHandleDiskFull => "There is not enough free space on that drive.",
        IOException => "The file could not be written. It may be open in another app, or the drive may be unavailable.",
        _ => "An unexpected problem stopped the save.",
    };
}
