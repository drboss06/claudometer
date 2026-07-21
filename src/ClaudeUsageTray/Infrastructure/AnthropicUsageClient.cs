using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.Resources;

namespace ClaudeUsageTray.Infrastructure;

internal sealed class AnthropicUsageClient : IDisposable
{
    private const string AllowedHost = "api.anthropic.com";
    private static readonly Uri UsageEndpoint = new("https://api.anthropic.com/api/oauth/usage");
    private readonly HttpClient _httpClient;
    private readonly CredentialsProvider _credentialsProvider;

    public AnthropicUsageClient(CredentialsProvider credentialsProvider)
    {
        _credentialsProvider = credentialsProvider;

        // Explicit handler so pooled connections (and their DNS) are refreshed
        // periodically on a process that stays alive for days, and so the token
        // is never followed across a redirect.
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(AppInfo.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        var credentials = await _credentialsProvider.GetAsync(cancellationToken);

        // Defence-in-depth: never attach the bearer token to anything but the
        // hard-coded Anthropic HTTPS endpoint.
        if (UsageEndpoint.Scheme != Uri.UriSchemeHttps
            || !UsageEndpoint.Host.Equals(AllowedHost, StringComparison.OrdinalIgnoreCase))
        {
            throw new UsageApiException(Strings.GenericUpdateFailed);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new SessionExpiredException(Strings.SessionExpired, response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UsageApiException(Strings.HttpError((int)response.StatusCode), response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseUsage(document.RootElement, credentials);
    }

    internal static UsageSnapshot ParseUsage(JsonElement root, ClaudeCredentials credentials)
    {
        var metrics = new List<UsageMetric>();
        AddMetric(metrics, root, "five_hour", "Current session");
        AddMetric(metrics, root, "seven_day", "Weekly limit");

        // Show every model-specific weekly window the endpoint reports, not just one.
        AddMetric(metrics, root, "seven_day_sonnet", "Weekly Sonnet");
        AddMetric(metrics, root, "seven_day_opus", "Weekly Opus");

        if (metrics.Count == 0)
        {
            throw new UsageApiException(Strings.UnknownFormat);
        }

        return new UsageSnapshot(
            metrics,
            DateTimeOffset.Now,
            credentials.Source,
            credentials.SubscriptionType,
            credentials.ExpiresAt);
    }

    private static bool AddMetric(List<UsageMetric> target, JsonElement root, string property, string label)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(property, out var metric)
            || metric.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        double? utilization = null;
        if (metric.TryGetProperty("utilization", out var utilizationNode)
            && utilizationNode.ValueKind == JsonValueKind.Number
            && utilizationNode.TryGetDouble(out var numericUtilization))
        {
            utilization = Math.Clamp(numericUtilization, 0, 100);
        }

        DateTimeOffset? resetsAt = null;
        if (metric.TryGetProperty("resets_at", out var resetNode)
            && resetNode.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(resetNode.GetString(), out var parsedReset))
        {
            resetsAt = parsedReset;
        }

        var used = ReadLong(metric, "used");
        var limit = ReadLong(metric, "limit");

        target.Add(new UsageMetric(label, utilization, resetsAt, used, limit));
        return true;
    }

    private static long? ReadLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt64(out var number)
                ? number
                : null;
    }

    public void Dispose() => _httpClient.Dispose();
}

internal class UsageApiException : Exception
{
    public UsageApiException(string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}

/// <summary>The saved Claude Code session was rejected (expired or revoked).</summary>
internal sealed class SessionExpiredException : UsageApiException
{
    public SessionExpiredException(string message, HttpStatusCode? statusCode = null)
        : base(message, statusCode)
    {
    }
}
