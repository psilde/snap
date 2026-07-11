using System;
using System.IO;
using System.Text.Json;
using Snap.Models;

namespace Snap.Services;

public class SettingsService
{
    private readonly string _configPath;

    public SettingsService(string configPath)
    {
        _configPath = configPath;
    }

    public static string GetDefaultConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "Snap", "settings.json");
    }

    public AppSettings LoadOrCreateDefault()
    {
        if (!File.Exists(_configPath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        var json = File.ReadAllText(_configPath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json);
        return settings ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_configPath, json);
    }
}
