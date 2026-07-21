using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeUsageTray.Infrastructure;

internal enum AppThemePreference
{
    System,
    Dark,
    Light
}

/// <summary>
/// User-configurable behaviour, persisted as JSON under
/// <c>%APPDATA%\ClaudeUsageTray\settings.json</c>. Loading never throws: a
/// missing or corrupt file falls back to defaults.
/// </summary>
internal sealed class AppSettings
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public int RefreshIntervalMinutes { get; set; } = 1;

    public int WarnThresholdPercent { get; set; } = 80;

    public int CriticalThresholdPercent { get; set; } = 95;

    public bool NotificationsEnabled { get; set; } = true;

    public AppThemePreference Theme { get; set; } = AppThemePreference.System;

    public bool CheckForUpdates { get; set; }

    [JsonIgnore]
    public TimeSpan RefreshInterval => TimeSpan.FromMinutes(Math.Clamp(RefreshIntervalMinutes, 1, 240));

    [JsonIgnore]
    public int WarnAt => Math.Clamp(WarnThresholdPercent, 1, 100);

    [JsonIgnore]
    public int CriticalAt => Math.Clamp(Math.Max(CriticalThresholdPercent, WarnAt), 1, 100);

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsageTray",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // Fall through to defaults on any read/parse failure.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, SerializerOptions);

            // Write atomically so a crash mid-write cannot corrupt the settings file.
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Settings are best-effort; ignore persistence failures.
        }
    }
}
