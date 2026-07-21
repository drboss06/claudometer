namespace ClaudeUsageTray.Models;

internal sealed record UsageMetric(
    string Label,
    double? Utilization,
    DateTimeOffset? ResetsAt)
{
    public int RoundedPercent => (int)Math.Clamp(Math.Round(Utilization ?? 0), 0, 100);
}

internal sealed record UsageSnapshot(
    IReadOnlyList<UsageMetric> Metrics,
    DateTimeOffset UpdatedAt,
    string CredentialSource,
    string? PlanName = null)
{
    public double MaximumUtilization => Metrics
        .Where(metric => metric.Utilization.HasValue)
        .Select(metric => metric.Utilization!.Value)
        .DefaultIfEmpty(0)
        .Max();

    public static UsageSnapshot CreateDemo() => new(
        [
            new("Current session", 34, DateTimeOffset.Now.AddHours(2).AddMinutes(18)),
            new("Weekly limit", 62, DateTimeOffset.Now.AddDays(3).AddHours(7)),
            new("Weekly Sonnet", 18, DateTimeOffset.Now.AddDays(5).AddHours(11))
        ],
        DateTimeOffset.Now,
        "Demo data",
        "Claude Max");
}

internal sealed record ClaudeCredentials(
    string AccessToken,
    DateTimeOffset? ExpiresAt,
    string Source,
    string? SubscriptionType);
