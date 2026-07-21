using System.Drawing.Drawing2D;
using ClaudeUsageTray.Models;

namespace ClaudeUsageTray.UI;

internal sealed class UsageMetricControl : Control
{
    private readonly UsageMetric _metric;

    public UsageMetricControl(UsageMetric metric)
    {
        _metric = metric;
        DoubleBuffered = true;
        Height = 72;
        Margin = new Padding(0, 0, 0, 5);
        BackColor = AppPalette.Window;
        ForeColor = AppPalette.Text;
        AccessibleName = $"{metric.Label}: {metric.RoundedPercent} percent";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        using var labelFont = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        using var percentageFont = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        using var detailFont = new Font("Segoe UI", 8.5f, FontStyle.Regular);

        var percent = _metric.RoundedPercent;
        var accent = AppPalette.ForUsage(percent);

        TextRenderer.DrawText(
            graphics,
            _metric.Label,
            labelFont,
            new Rectangle(0, 0, Width - 70, 24),
            AppPalette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(
            graphics,
            _metric.Utilization.HasValue ? $"{percent}%" : "—",
            percentageFont,
            new Rectangle(Width - 68, 0, 68, 24),
            accent,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        var track = new RectangleF(0, 31, Width, 8);
        using (var trackPath = RoundedRectangle(track, 4))
        using (var trackBrush = new SolidBrush(AppPalette.Track))
        {
            graphics.FillPath(trackBrush, trackPath);
        }

        if (_metric.Utilization.HasValue && percent > 0)
        {
            var fillWidth = Math.Max(8, Width * percent / 100f);
            var fill = new RectangleF(0, 31, fillWidth, 8);
            using var fillPath = RoundedRectangle(fill, 4);
            using var fillBrush = new SolidBrush(accent);
            graphics.FillPath(fillBrush, fillPath);
        }

        var resetText = FormatReset(_metric.ResetsAt);
        TextRenderer.DrawText(
            graphics,
            resetText,
            detailFont,
            new Rectangle(0, 46, Width, 22),
            AppPalette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }

    internal static string FormatReset(DateTimeOffset? resetsAt)
    {
        if (!resetsAt.HasValue)
        {
            return "Reset time unavailable";
        }

        var remaining = resetsAt.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return "Resetting now";
        }

        if (remaining.TotalDays >= 1)
        {
            return $"Resets in {(int)remaining.TotalDays}d {remaining.Hours}h";
        }

        if (remaining.TotalHours >= 1)
        {
            return $"Resets in {(int)remaining.TotalHours}h {remaining.Minutes}m";
        }

        return $"Resets in {Math.Max(1, remaining.Minutes)}m";
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
