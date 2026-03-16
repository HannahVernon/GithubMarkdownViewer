using System;
using System.IO;
using System.Text.Json;
using GithubMarkdownViewer.Models;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Loads and saves application settings to a JSON file in the user's AppData directory.
/// </summary>
public static class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GithubMarkdownViewer");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static AppSettings Load()
    {
        try
        {
            MigrateFromLegacyLocation();

            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings != null)
                {
                    settings.Sanitize();
                    AppLogger.Info("Settings loaded");
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
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
            AppLogger.Info("Settings saved");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to save settings", ex);
        }
    }

    /// <summary>
    /// One-time migration: move settings.json from the old app-directory location.
    /// </summary>
    private static void MigrateFromLegacyLocation()
    {
        try
        {
            var legacyPath = Path.Combine(AppContext.BaseDirectory, "settings.json");
            if (File.Exists(legacyPath) && !File.Exists(SettingsPath))
            {
                Directory.CreateDirectory(SettingsDir);
                File.Move(legacyPath, SettingsPath);
                AppLogger.Info("Migrated settings from legacy location");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Settings migration failed: {ex.Message}");
        }
    }
}
