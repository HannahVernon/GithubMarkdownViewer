using System;
using System.IO;
using System.Text.Json;
using GithubMarkdownViewer.Models;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Loads and saves application settings to a JSON file beside the executable.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    AppLogger.Info($"Settings loaded from {SettingsPath}");
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to load settings, using defaults", ex);
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
            AppLogger.Info($"Settings saved to {SettingsPath}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to save settings", ex);
        }
    }
}
