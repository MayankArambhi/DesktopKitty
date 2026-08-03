using System.IO;
using System.Text.Json;
using TinyBongo.Models;

namespace TinyBongo.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON in the user's AppData folder.
/// </summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public SettingsService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopKitty");

        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            AppSettings settings;

            if (!File.Exists(_settingsPath))
            {
                settings = new AppSettings();
            }
            else
            {
                var json = File.ReadAllText(_settingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }

            if (EnsureDefaults(settings))
            {
                Save(settings);
            }
            else if (MigrateLegacyCounters(settings))
            {
                Save(settings);
            }

            if (StatisticsCalculator.EnsureTodayCurrent(settings))
            {
                Save(settings);
            }

            return settings;
        }
        catch
        {
            var settings = new AppSettings();
            EnsureDefaults(settings);
            Save(settings);
            return settings;
        }
    }

    private static bool EnsureDefaults(AppSettings settings)
    {
        if (settings.InstallDate != default)
        {
            return false;
        }

        settings.InstallDate = DateTime.UtcNow;
        return true;
    }

    private static bool MigrateLegacyCounters(AppSettings settings)
    {
        var changed = false;

        if (settings.TodayDate == default)
        {
            settings.TodayDate = DateTime.UtcNow.Date;
            changed = true;
        }

        var tracked = settings.KeyboardClickCount + settings.MouseClickCount;
        if (settings.ClickCount > tracked)
        {
            settings.KeyboardClickCount += settings.ClickCount - tracked;
            changed = true;
        }

        return changed;
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Settings persistence failure should not crash the mascot.
        }
    }
}
