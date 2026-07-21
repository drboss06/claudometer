using System.Reflection;

namespace ClaudeUsageTray;

/// <summary>
/// Single source of truth for the application name and version. The version is
/// read from the assembly (fed by the csproj <c>Version</c> / release tag) so it
/// never has to be duplicated in code.
/// </summary>
internal static class AppInfo
{
    public const string Name = "Claude Usage Tray";

    public static string Version { get; } = ResolveVersion();

    public static string UserAgent { get; } = $"ClaudeUsageTray/{Version}";

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip the source-revision suffix that the SDK appends (e.g. "1.2.3+abc123").
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
