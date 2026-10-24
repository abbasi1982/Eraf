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
