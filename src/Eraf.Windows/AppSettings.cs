using Windows.Storage;

namespace Eraf.Windows;

/// <summary>Small per-user settings (last folder). Failures are ignored: a setting that cannot be stored is just not remembered.</summary>
public static class AppSettings
{
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
