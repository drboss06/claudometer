using System.Text.Json;
using ClaudeUsageTray.Infrastructure;
using ClaudeUsageTray.Models;
using Xunit;

namespace ClaudeUsageTray.Tests;

public class UsageParsingTests
{
    private static UsageSnapshot Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var credentials = new ClaudeCredentials("token", null, "test", "max");
        return AnthropicUsageClient.ParseUsage(document.RootElement, credentials);
    }

    [Fact]
    public void IncludesBothModelWindowsWhenPresent()
    {
        var snapshot = Parse("""
            {
              "five_hour": { "utilization": 12.4, "resets_at": "2030-01-01T05:00:00Z" },
              "seven_day": { "utilization": 54.6, "resets_at": "2030-01-07T00:00:00Z" },
              "seven_day_sonnet": { "utilization": 8.0 },
              "seven_day_opus": { "utilization": 41.0 }
            }
            """);

        Assert.Equal(4, snapshot.Metrics.Count);
        Assert.Equal("Weekly Sonnet", snapshot.Metrics[2].Label);
        Assert.Equal("Weekly Opus", snapshot.Metrics[3].Label);
        Assert.Equal(54.6, snapshot.MaximumUtilization, 3);
    }

    [Fact]
    public void ClampsUtilizationAndReadsAbsoluteCounts()
    {
        var snapshot = Parse("""
            {
              "five_hour": { "utilization": 130.0, "used": 480, "limit": 500 }
            }
            """);

        var metric = snapshot.Metrics[0];
        Assert.Equal(100, metric.RoundedPercent);
        Assert.True(metric.HasAbsolute);
        Assert.Equal(480, metric.Used);
        Assert.Equal(500, metric.Limit);
    }

    [Fact]
    public void ThrowsOnUnknownFormat()
    {
        Assert.Throws<UsageApiException>(() => Parse("""{ "unexpected": true }"""));
    }
}
