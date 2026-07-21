using System.Text.Json;
using ClaudeUsageTray.Infrastructure;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.UI;

namespace ClaudeUsageTray.Testing;

/// <summary>
/// Fast, dependency-free checks over the fragile parsing/formatting logic. Run in
/// CI via <c>--self-test</c>; the richer assertions live in the xUnit project.
/// </summary>
internal static class SelfTestRunner
{
    public static int Run()
    {
        try
        {
            CredentialsParsingWorks();
            ExpirationSecondsAndMilliseconds();
            UsageParsingShowsBothModels();
            UnknownFormatThrows();
            ResetFormattingCoversRanges();
            VersionComparisonWorks();
            SettingsClampOutOfRangeValues();
            SessionExpiredIsUsageApiException();
            Console.WriteLine("Self-test passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Self-test failed: {exception.Message}");
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

    private static void ExpirationSecondsAndMilliseconds()
    {
        var seconds = CredentialsProvider.TryParse(
            """{ "oauth": { "accessToken": "t", "expiresAt": 1893456000 } }""", "t");
        Assert(seconds?.ExpiresAt == DateTimeOffset.FromUnixTimeSeconds(1893456000), "Seconds expiry mismatch.");
    }

    private static void UsageParsingShowsBothModels()
    {
        const string json = """
            {
              "five_hour": { "utilization": 12.4, "resets_at": "2030-01-01T05:00:00Z" },
              "seven_day": { "utilization": 54.6, "resets_at": "2030-01-07T00:00:00Z" },
              "seven_day_sonnet": { "utilization": 8.0, "resets_at": "2030-01-07T00:00:00Z" },
              "seven_day_opus": { "utilization": 41.0, "resets_at": "2030-01-07T00:00:00Z" }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var credentials = new ClaudeCredentials("token", null, "test", "max");
        var snapshot = AnthropicUsageClient.ParseUsage(document.RootElement, credentials);

        Assert(snapshot.Metrics.Count == 4, "Expected four metrics (both models).");
        Assert(Math.Abs(snapshot.MaximumUtilization - 54.6) < 0.001, "Maximum utilization mismatch.");
        Assert(snapshot.Metrics[2].Label == "Weekly Sonnet", "Sonnet metric mismatch.");
        Assert(snapshot.Metrics[3].Label == "Weekly Opus", "Opus metric mismatch.");
    }

    private static void UnknownFormatThrows()
    {
        using var document = JsonDocument.Parse("""{ "unexpected": true }""");
        var credentials = new ClaudeCredentials("token", null, "test", null);
        var threw = false;
        try
        {
            AnthropicUsageClient.ParseUsage(document.RootElement, credentials);
        }
        catch (UsageApiException)
        {
            threw = true;
        }

        Assert(threw, "Unknown usage format should throw UsageApiException.");
    }

    private static void ResetFormattingCoversRanges()
    {
        var now = DateTimeOffset.Now;
        Assert(UsageMetricControl.FormatReset(null).Length > 0, "Null reset should format.");
        Assert(UsageMetricControl.FormatReset(now.AddMinutes(-1)).Length > 0, "Past reset should format.");

        var days = UsageMetricControl.FormatReset(now.AddDays(3).AddHours(5));
        Assert(days.Contains('3'), "Day reset should include the day count.");

        var hours = UsageMetricControl.FormatReset(now.AddHours(2).AddMinutes(10));
        Assert(hours.Contains('2'), "Hour reset should include the hour count.");
    }

    private static void VersionComparisonWorks()
    {
        Assert(UpdateChecker.IsNewer("v0.2.0", "0.1.0"), "0.2.0 should be newer than 0.1.0.");
        Assert(!UpdateChecker.IsNewer("v0.1.0", "0.1.0"), "Equal versions are not newer.");
        Assert(!UpdateChecker.IsNewer("0.0.9", "0.1.0"), "Older version is not newer.");
        Assert(!UpdateChecker.IsNewer("not-a-version", "0.1.0"), "Garbage tag is not newer.");
    }

    private static void SettingsClampOutOfRangeValues()
    {
        var settings = new AppSettings
        {
            RefreshIntervalMinutes = 0,
            WarnThresholdPercent = 200,
            CriticalThresholdPercent = -5
        };

        Assert(settings.RefreshInterval >= TimeSpan.FromMinutes(1), "Interval should clamp to at least 1 minute.");
        Assert(settings.WarnAt is >= 1 and <= 100, "Warn threshold should clamp into range.");
        Assert(settings.CriticalAt >= settings.WarnAt, "Critical threshold should not fall below warn.");
    }

    private static void SessionExpiredIsUsageApiException()
    {
        Assert(new SessionExpiredException("x") is UsageApiException, "SessionExpiredException should derive from UsageApiException.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
