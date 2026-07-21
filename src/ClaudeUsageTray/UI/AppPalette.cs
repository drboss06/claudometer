namespace ClaudeUsageTray.UI;

internal static class AppPalette
{
    public static readonly Color Window = Color.FromArgb(24, 26, 31);
    public static readonly Color Surface = Color.FromArgb(33, 36, 42);
    public static readonly Color SurfaceHover = Color.FromArgb(42, 46, 53);
    public static readonly Color Border = Color.FromArgb(58, 62, 71);
    public static readonly Color Text = Color.FromArgb(241, 243, 245);
    public static readonly Color Muted = Color.FromArgb(155, 161, 171);
    public static readonly Color Track = Color.FromArgb(54, 58, 66);
    public static readonly Color Green = Color.FromArgb(66, 199, 118);
    public static readonly Color Orange = Color.FromArgb(246, 166, 35);
    public static readonly Color Red = Color.FromArgb(244, 91, 105);

    public static Color ForUsage(double utilization) => utilization switch
    {
        >= 90 => Red,
        >= 70 => Orange,
        _ => Green
    };
}
