using Windows.Devices.Enumeration;
using Windows.Storage;

namespace Eraf.Windows;

/// <summary>Lists the available microphones and remembers which one the user picked.</summary>
public static class MicrophoneService
{
    private const string SettingKey = "MicrophoneId";

    public static async Task<IReadOnlyList<MicrophoneOption>> ListAsync()
    {
        try
        {
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
            return MicrophoneChoice.Build(devices.Select(d => (d.Id, d.Name)));
        }
        catch (Exception)
        {
            // Enumeration is a convenience; recording still works with the default microphone.
            return MicrophoneChoice.Build(Array.Empty<(string, string)>());
        }
    }

    public static string? LoadPreferredId()
    {
        try
        {
            return ApplicationData.Current.LocalSettings.Values[SettingKey] as string;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static void SavePreferredId(string? id)
    {
        try
        {
            if (id is null)
            {
                ApplicationData.Current.LocalSettings.Values.Remove(SettingKey);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values[SettingKey] = id;
            }
        }
        catch (Exception)
        {
            // Not remembered; the choice still applies for this run.
        }
    }
}
