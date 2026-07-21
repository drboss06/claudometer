using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using ClaudeUsageTray.Models;

namespace ClaudeUsageTray.Infrastructure;

internal sealed class AnthropicUsageClient : IDisposable
{
    private static readonly Uri UsageEndpoint = new("https://api.anthropic.com/api/oauth/usage");
    private readonly HttpClient _httpClient;
    private readonly CredentialsProvider _credentialsProvider;

    public AnthropicUsageClient(CredentialsProvider credentialsProvider)
    {
        _credentialsProvider = credentialsProvider;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeUsageTray/0.1.0");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken cancellationToken)
    {
        var credentials = await _credentialsProvider.GetAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new UsageApiException(
                "Claude rejected the saved session. Run `claude auth login`, then refresh.",
                response.StatusCode);
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new UsageApiException(
                $"Anthropic usage service returned HTTP {(int)response.StatusCode}.",
                response.StatusCode);
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

        if (!AddMetric(metrics, root, "seven_day_sonnet", "Weekly Sonnet"))
        {
            AddMetric(metrics, root, "seven_day_opus", "Weekly Opus");
        }

        if (metrics.Count == 0)
        {
            throw new UsageApiException("Anthropic returned an unknown usage format.");
        }

        var planName = credentials.SubscriptionType;
        return new UsageSnapshot(metrics, DateTimeOffset.Now, credentials.Source, planName);
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

        target.Add(new UsageMetric(label, utilization, resetsAt));
        return true;
    }

    public void Dispose() => _httpClient.Dispose();
}

internal sealed class UsageApiException : Exception
{
    public UsageApiException(string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }
}
