using System.Text.Json;

namespace Eraf.Format.Tests;

/// <summary>Locates fixtures/eraf by walking up to Eraf.sln, and reads the expected-result sidecars.</summary>
internal static class FixtureFiles
{
    public static readonly string Root = FindRoot();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Eraf.sln")))
            {
                return Path.Combine(dir.FullName, "fixtures", "eraf");
            }
        }

        throw new InvalidOperationException("Eraf.sln not found above " + AppContext.BaseDirectory);
    }

    /// <summary>Fixture paths relative to the fixtures root, e.g. "valid/valid-minimal.eraf".</summary>
    public static IEnumerable<object[]> All(string folder) =>
        Directory.GetFiles(Path.Combine(Root, folder), "*.eraf")
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => new object[] { folder + "/" + Path.GetFileName(p) });

    public static byte[] ReadBytes(string relative) => File.ReadAllBytes(Path.Combine(Root, relative));

    public static JsonElement ReadSidecar(string relative)
    {
        string name = Path.GetFileNameWithoutExtension(relative);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "expected", name + ".json")));
        return doc.RootElement.Clone();
    }
}
