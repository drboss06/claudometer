using System.Text.Json;
using ClaudeUsageTray.Infrastructure;
using ClaudeUsageTray.Models;

namespace ClaudeUsageTray.Testing;

internal static class SelfTestRunner
{
    public static int Run()
    {
        try
        {
            CredentialsParsingWorks();
            UsageParsingWorks();
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static void CredentialsParsingWorks()
    {
        const string json = """
            {
              "claudeAiOauth": {
                "accessToken": "test-token-not-a-secret",
                "expiresAt": 1893456000000,
                "subscriptionType": "max"
              }
            }
            """;

        var credentials = CredentialsProvider.TryParse(json, "test")
            ?? throw new InvalidOperationException("Credentials parser returned null.");

        Assert(credentials.AccessToken == "test-token-not-a-secret", "Access token mismatch.");
        Assert(credentials.ExpiresAt == DateTimeOffset.FromUnixTimeMilliseconds(1893456000000), "Expiration mismatch.");
        Assert(credentials.SubscriptionType == "max", "Subscription mismatch.");
    }

    private static void UsageParsingWorks()
    {
        const string json = """
            {
              "five_hour": { "utilization": 12.4, "resets_at": "2030-01-01T05:00:00Z" },
              "seven_day": { "utilization": 54.6, "resets_at": "2030-01-07T00:00:00Z" },
              "seven_day_sonnet": { "utilization": 8.0, "resets_at": "2030-01-07T00:00:00Z" }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var credentials = new ClaudeCredentials("token", null, "test", "max");
        var snapshot = AnthropicUsageClient.ParseUsage(document.RootElement, credentials);

        Assert(snapshot.Metrics.Count == 3, "Expected three metrics.");
        Assert(Math.Abs(snapshot.MaximumUtilization - 54.6) < 0.001, "Maximum utilization mismatch.");
        Assert(snapshot.Metrics[2].Label == "Weekly Sonnet", "Model metric mismatch.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
