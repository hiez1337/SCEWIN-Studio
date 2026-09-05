using System;
using System.IO;
using System.Text.Json;
using SCEWIN_Studio.Models;

namespace SCEWIN_Studio.Services;

public class SettingsService : ISettingsService
{
    private static readonly string SettingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SCEWIN_Studio");

    private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                    return;
                }
            }
        }
        catch { }

        Settings = new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch { }
    }
}
