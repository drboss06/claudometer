using System.Text.Json;

namespace ClaudeUsageTray.Infrastructure;

/// <summary>
/// Opt-in check against the GitHub Releases API for a newer tagged version.
/// Disabled unless <see cref="RepositorySlug"/> is set to a real "owner/repo"
/// and the user enables it in settings. Never sends any credentials.
/// </summary>
internal sealed class UpdateChecker : IDisposable
{

    public const string RepositorySlug = "drboss06";

    private readonly HttpClient _httpClient;

    public UpdateChecker()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public static bool IsConfigured =>
        !RepositorySlug.Contains("OWNER", StringComparison.OrdinalIgnoreCase)
        && RepositorySlug.Contains('/');

    /// <summary>Returns the newer release tag (e.g. "v0.2.0") or null when up to date.</summary>
    public async Task<string?> GetNewerReleaseAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var url = $"https://api.github.com/repos/{RepositorySlug}/releases/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!document.RootElement.TryGetProperty("tag_name", out var tagNode)
            || tagNode.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var tag = tagNode.GetString();
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return IsNewer(tag, AppInfo.Version) ? tag : null;
    }

    internal static bool IsNewer(string latestTag, string current)
    {
        return TryParse(latestTag, out var latest)
            && TryParse(current, out var currentVersion)
            && latest > currentVersion;
    }

    private static bool TryParse(string value, out Version version)
    {
        var trimmed = value.TrimStart('v', 'V').Trim();
        return Version.TryParse(trimmed, out version!);
    }

    public void Dispose() => _httpClient.Dispose();
}
