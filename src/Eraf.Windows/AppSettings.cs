using Windows.Storage;

namespace Eraf.Windows;

/// <summary>Small per-user settings (last folder). Failures are ignored: a setting that cannot be stored is just not remembered.</summary>
public static class AppSettings
{
    private static AppOptions? _options;

    /// <summary>The user's options (theme, folder scanning, batch rules). Loaded once, saved when changed.</summary>
    public static AppOptions Options
    {
        get => _options ??= AppOptions.Load(Get);
        set
        {
            _options = value;
            value.Save(Set);
            OptionsChanged?.Invoke(value);
        }
    }

    public static event Action<AppOptions>? OptionsChanged;

    public static string? Get(string key)
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[key] as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void Set(string key, string? value)
    {
        try
        {
            if (value is null)
            {
                ApplicationData.Current.LocalSettings.Values.Remove(key);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[key] = value;
            }
        }
        catch (Exception)
        {
            // Not remembered.
        }
    }
}
