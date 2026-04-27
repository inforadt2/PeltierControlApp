using System.Text.Json;
using PeltierControlApp.Models;

namespace PeltierControlApp.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "peltiersettings.json");
        Load();
        if (!File.Exists(_settingsPath))
            Save();
    }

    public void Load()
    {
        if (!File.Exists(_settingsPath)) return;
        try
        {
            var json = File.ReadAllText(_settingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { Settings = new AppSettings(); }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}
