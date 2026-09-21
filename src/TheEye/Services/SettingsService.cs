using System.IO;
using TheEye.Core;

namespace TheEye.Services;

public sealed class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TheEye");
        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        try
        {
            return SettingsSerializer.Deserialize(await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var tempPath = _settingsPath + ".tmp";
        await File.WriteAllTextAsync(tempPath, SettingsSerializer.Serialize(settings)).ConfigureAwait(false);
        File.Move(tempPath, _settingsPath, true);
    }
}
