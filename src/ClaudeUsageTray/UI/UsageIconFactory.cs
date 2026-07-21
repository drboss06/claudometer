using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ClaudeUsageTray.UI;

internal static class UsageIconFactory
{
    public static Icon Create(double utilization, bool hasError = false, bool lightBackground = false)
    {
        using var bitmap = CreateBitmap(32, utilization, hasError, lightBackground);
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    internal static Bitmap CreateBitmap(int size, double utilization, bool hasError = false, bool lightBackground = false)
    {
        // On a light taskbar draw the battery with a dark outline so it stays legible.
        var outline = lightBackground ? Color.FromArgb(45, 49, 56) : Color.FromArgb(235, 240, 243);

        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(size / 32f, size / 32f);

        var body = new RectangleF(3.5f, 7.5f, 23f, 17f);
        using var outlinePen = new Pen(outline, 2.2f);
        using var bodyPath = RoundedRectangle(body, 3.5f);
        graphics.DrawPath(outlinePen, bodyPath);
        using (var capBrush = new SolidBrush(outline))
        {
            graphics.FillRectangle(capBrush, 27f, 12f, 2.5f, 8f);
        }

        var clamped = Math.Clamp(utilization, 0, 100);
        var remaining = 100 - clamped;
        var fillColor = hasError ? AppPalette.Red : AppPalette.ForUsage(clamped);
        var innerWidth = 19f * (float)(remaining / 100d);
        if (innerWidth > 0.5f)
        {
            using var fillBrush = new SolidBrush(fillColor);
            var fillRect = new RectangleF(5.7f, 9.7f, Math.Max(2f, innerWidth), 12.6f);
            using var fillPath = RoundedRectangle(fillRect, 2f);
            graphics.FillPath(fillBrush, fillPath);
        }

        if (hasError)
        {
            using var errorPen = new Pen(outline, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var dotBrush = new SolidBrush(outline);
            graphics.DrawLine(errorPen, 14.8f, 11f, 14.8f, 17f);
            graphics.FillEllipse(dotBrush, 14f, 19f, 1.8f, 1.8f);
        }

        return bitmap;
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
