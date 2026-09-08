using System.IO;
using System.Text.Json;

namespace memoNOW;

public sealed class SettingsStore
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _settingsPath = Path.Combine(appData, "memoNOW", "settings.json");
    }

    public HotkeySettings LoadHotkey()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return HotkeySettings.Default;
            }

            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<HotkeySettings>(json, _jsonOptions);
            if (settings is not null && settings.IsValid(out _))
            {
                return settings;
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return HotkeySettings.Default;
    }

    public void SaveHotkey(HotkeySettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath)!;
        Directory.CreateDirectory(directory);

        var tempPath = _settingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _settingsPath, overwrite: true);
    }
}
