using System.Drawing.Drawing2D;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.Resources;

namespace ClaudeUsageTray.UI;

internal sealed class UsageMetricControl : Control
{
    // Point-sized fonts render correctly at any DPI, so a single shared set is
    // reused across every row instead of allocating three fonts per paint.
    private static readonly Font LabelFont = new("Segoe UI", 9.5f, FontStyle.Regular);
    private static readonly Font PercentFont = new("Segoe UI Semibold", 10f, FontStyle.Bold);
    private static readonly Font DetailFont = new("Segoe UI", 8.5f, FontStyle.Regular);

    // Design metrics at 96 DPI; everything is multiplied by the runtime scale.
    private const int BaseHeight = 72;

    private readonly UsageMetric _metric;

    public UsageMetricControl(UsageMetric metric)
    {
        _metric = metric;
        DoubleBuffered = true;
        Margin = new Padding(0, 0, 0, 5);
        BackColor = AppPalette.Window;
        ForeColor = AppPalette.Text;
        AccessibleName = Strings.PercentAccessible(metric.Label, metric.RoundedPercent);
        Height = ScaledHeight();
    }

    /// <summary>Recent utilisation samples (oldest first) for the trend sparkline.</summary>
    public IReadOnlyList<double>? History { get; set; }

    public int ScaledHeight() => (int)Math.Round(BaseHeight * (DeviceDpi / 96f));

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var scale = DeviceDpi / 96f;
        int S(float value) => (int)Math.Round(value * scale);

        var percent = _metric.RoundedPercent;
        var accent = AppPalette.ForUsage(percent);
        var percentColumn = S(68);

        TextRenderer.DrawText(
            graphics,
            _metric.Label,
            LabelFont,
            new Rectangle(0, 0, Width - percentColumn - S(2), S(24)),
            AppPalette.Text,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        TextRenderer.DrawText(
            graphics,
            _metric.Utilization.HasValue ? $"{percent}%" : "—",
            PercentFont,
            new Rectangle(Width - percentColumn, 0, percentColumn, S(24)),
            accent,
            TextFormatFlags.Right | TextFormatFlags.VerticalCenter);

        var trackTop = S(31);
        var trackHeight = S(8);
        var track = new RectangleF(0, trackTop, Width, trackHeight);
        using (var trackPath = RoundedRectangle(track, trackHeight / 2f))
        using (var trackBrush = new SolidBrush(AppPalette.Track))
        {
            graphics.FillPath(trackBrush, trackPath);
        }

        if (_metric.Utilization.HasValue && percent > 0)
        {
            var fillWidth = Math.Max(trackHeight, Width * percent / 100f);
            var fill = new RectangleF(0, trackTop, fillWidth, trackHeight);
            using var fillPath = RoundedRectangle(fill, trackHeight / 2f);
            using var fillBrush = new SolidBrush(accent);
            graphics.FillPath(fillBrush, fillPath);
        }

        var detailTop = S(45);
        var detailHeight = Height - detailTop;
        var sparkWidth = S(56);
        var resetText = FormatReset(_metric.ResetsAt);
        if (_metric.HasAbsolute)
        {
            resetText = $"{resetText}  ·  {_metric.Used}/{_metric.Limit}";
        }

        TextRenderer.DrawText(
            graphics,
            resetText,
            DetailFont,
            new Rectangle(0, detailTop, Width - sparkWidth - S(6), detailHeight),
            AppPalette.Muted,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        DrawSparkline(graphics, new Rectangle(Width - sparkWidth, detailTop, sparkWidth, detailHeight), accent);
    }

    private void DrawSparkline(Graphics graphics, Rectangle area, Color accent)
    {
        var history = History;
        if (history is null || history.Count < 2)
        {
            return;
        }

        var count = Math.Min(history.Count, 40);
        var start = history.Count - count;
        var points = new PointF[count];
        var stepX = area.Width / (float)(count - 1);
        for (var i = 0; i < count; i++)
        {
            var value = (float)Math.Clamp(history[start + i], 0, 100);
            var x = area.Left + stepX * i;
            var y = area.Bottom - 1 - (value / 100f) * (area.Height - 2);
            points[i] = new PointF(x, y);
        }

        using var pen = new Pen(Color.FromArgb(150, accent), Math.Max(1f, DeviceDpi / 96f));
        graphics.DrawLines(pen, points);
    }

    internal static string FormatReset(DateTimeOffset? resetsAt)
    {
        if (!resetsAt.HasValue)
        {
            return Strings.ResetUnavailable;
        }

        var remaining = resetsAt.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return Strings.ResettingNow;
        }

        if (remaining.TotalDays >= 1)
        {
            return Strings.ResetsInDaysHours((int)remaining.TotalDays, remaining.Hours);
        }

        if (remaining.TotalHours >= 1)
        {
            return Strings.ResetsInHoursMinutes((int)remaining.TotalHours, remaining.Minutes);
        }

        return Strings.ResetsInMinutes(Math.Max(1, remaining.Minutes));
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
