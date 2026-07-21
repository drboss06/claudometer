using ClaudeUsageTray.Infrastructure;
using Microsoft.Win32;

namespace ClaudeUsageTray.UI;

/// <summary>
/// Resolves the effective UI palette from the user's preference and, when set to
/// <see cref="AppThemePreference.System"/>, the current Windows theme.
/// </summary>
internal static class ThemeManager
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>Applies the palette for the given preference to <see cref="AppPalette"/>.</summary>
    public static void Apply(AppThemePreference preference)
    {
        var light = preference switch
        {
            AppThemePreference.Light => true,
            AppThemePreference.Dark => false,
            _ => AppsUseLightTheme()
        };

        AppPalette.Current = light ? Palette.Light : Palette.Dark;
    }

    /// <summary>
    /// True when the taskbar (where the tray icon lives) uses a light background,
    /// so the icon can be drawn with a dark outline for contrast.
    /// </summary>
    public static bool TaskbarIsLight() => ReadDword("SystemUsesLightTheme") == 1;

    private static bool AppsUseLightTheme() => ReadDword("AppsUseLightTheme") == 1;

    private static int ReadDword(string valueName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
            return key?.GetValue(valueName) is int value ? value : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return 0;
        }
    }
}
