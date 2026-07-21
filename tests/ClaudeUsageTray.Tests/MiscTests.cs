using System.Net;
using ClaudeUsageTray.Infrastructure;
using ClaudeUsageTray.UI;
using Xunit;

namespace ClaudeUsageTray.Tests;

public class VersionComparisonTests
{
    [Theory]
    [InlineData("v0.2.0", "0.1.0", true)]
    [InlineData("0.2.0", "0.1.0", true)]
    [InlineData("v0.1.0", "0.1.0", false)]
    [InlineData("v0.0.9", "0.1.0", false)]
    [InlineData("garbage", "0.1.0", false)]
    public void IsNewerComparesSemanticVersions(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateChecker.IsNewer(latest, current));
    }

    [Fact]
    public void UpdateCheckerDisabledForPlaceholderSlug()
    {
        Assert.False(UpdateChecker.IsConfigured);
    }
}

public class SettingsTests
{
    [Fact]
    public void ClampsOutOfRangeValues()
    {
        var settings = new AppSettings
        {
            RefreshIntervalMinutes = 0,
            WarnThresholdPercent = 250,
            CriticalThresholdPercent = -10
        };

        Assert.True(settings.RefreshInterval >= TimeSpan.FromMinutes(1));
        Assert.InRange(settings.WarnAt, 1, 100);
        Assert.True(settings.CriticalAt >= settings.WarnAt);
    }
}

public class ExceptionHierarchyTests
{
    [Fact]
    public void SessionExpiredIsUsageApiException()
    {
        var exception = new SessionExpiredException("expired", HttpStatusCode.Unauthorized);
        Assert.IsAssignableFrom<UsageApiException>(exception);
        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
    }
}

public class ResetFormattingTests
{
    [Fact]
    public void HandlesMissingAndPastResets()
    {
        Assert.False(string.IsNullOrEmpty(UsageMetricControl.FormatReset(null)));
        Assert.False(string.IsNullOrEmpty(UsageMetricControl.FormatReset(DateTimeOffset.Now.AddMinutes(-5))));
    }

    [Fact]
    public void IncludesDayCountForMultiDayReset()
    {
        var text = UsageMetricControl.FormatReset(DateTimeOffset.Now.AddDays(3).AddHours(5));
        Assert.Contains("3", text);
    }
}
