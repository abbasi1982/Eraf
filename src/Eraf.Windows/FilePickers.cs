using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage.Pickers;

namespace Eraf.Windows;

/// <summary>
/// Windows App SDK file pickers (ADR-007). Each returns the chosen path, or null if the user cancelled.
/// Picker failures are returned as an exception message through <paramref name="error"/> instead of throwing.
/// </summary>
public static class FilePickers
{
    public static async Task<(string? Path, string? Error)> PickOpenAsync(Window window, string[] extensions)
    {
        try
        {
            var picker = new FileOpenPicker(window.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            foreach (string extension in extensions)
            {
                picker.FileTypeFilter.Add(extension);
            }

            PickFileResult? picked = await picker.PickSingleFileAsync();
            return (picked?.Path, null);
        }
        catch (Exception ex)
        {
            return (null, $"The file picker could not be opened ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    /// <summary>
    /// Asks where to save. The system dialog itself asks before overwriting an existing file
    /// (<c>ShowOverwritePrompt</c>); the caller then replaces atomically.
    /// </summary>
    public static async Task<(string? Path, string? Error)> PickSaveAsync(
        Window window, string suggestedName, string typeLabel, string extension, string? suggestedFolder = null)
    {
        try
        {
            var picker = new FileSavePicker(window.AppWindow.Id)
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = suggestedName,
                DefaultFileExtension = extension,
                ShowOverwritePrompt = true,
            };
            if (suggestedFolder is not null && Directory.Exists(suggestedFolder))
            {
                picker.SuggestedFolder = suggestedFolder;
            }

            picker.FileTypeChoices.Add(typeLabel, new List<string> { extension });

            PickFileResult? picked = await picker.PickSaveFileAsync();
            return (picked?.Path, null);
        }
        catch (Exception ex)
        {
            return (null, $"The save dialog could not be opened ({ex.GetType().Name}: {ex.Message}).");
        }
    }
}
