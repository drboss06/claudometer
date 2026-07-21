namespace ClaudeUsageTray.UI;

/// <summary>
/// A resolved set of colours for one theme. <see cref="AppPalette.Current"/>
/// selects which palette the whole UI reads through.
/// </summary>
internal sealed class Palette
{
    public required Color Window { get; init; }
    public required Color Surface { get; init; }
    public required Color SurfaceHover { get; init; }
    public required Color Border { get; init; }
    public required Color Text { get; init; }
    public required Color Muted { get; init; }
    public required Color Track { get; init; }
    public required Color Green { get; init; }
    public required Color Orange { get; init; }
    public required Color Red { get; init; }

    public static readonly Palette Dark = new()
    {
        Window = Color.FromArgb(24, 26, 31),
        Surface = Color.FromArgb(33, 36, 42),
        SurfaceHover = Color.FromArgb(42, 46, 53),
        Border = Color.FromArgb(58, 62, 71),
        Text = Color.FromArgb(241, 243, 245),
        Muted = Color.FromArgb(155, 161, 171),
        Track = Color.FromArgb(54, 58, 66),
        Green = Color.FromArgb(66, 199, 118),
        Orange = Color.FromArgb(246, 166, 35),
        Red = Color.FromArgb(244, 91, 105)
    };

    public static readonly Palette Light = new()
    {
        Window = Color.FromArgb(248, 249, 251),
        Surface = Color.FromArgb(255, 255, 255),
        SurfaceHover = Color.FromArgb(238, 240, 244),
        Border = Color.FromArgb(210, 214, 221),
        Text = Color.FromArgb(28, 31, 36),
        Muted = Color.FromArgb(104, 110, 120),
        Track = Color.FromArgb(224, 227, 232),
        Green = Color.FromArgb(30, 158, 82),
        Orange = Color.FromArgb(200, 128, 10),
        Red = Color.FromArgb(208, 52, 68)
    };
}

/// <summary>
/// Static facade over the active <see cref="Palette"/>. Callers keep using
/// <c>AppPalette.Surface</c> etc.; assigning <see cref="Current"/> re-themes them.
/// The usage colour bands are driven by the same thresholds as notifications
/// (<see cref="WarnAt"/> / <see cref="CriticalAt"/>), so the two never drift.
/// </summary>
internal static class AppPalette
{
    public static Palette Current { get; set; } = Palette.Dark;

    public static int WarnAt { get; set; } = 80;
    public static int CriticalAt { get; set; } = 95;

    public static bool IsLight => ReferenceEquals(Current, Palette.Light);

    public static Color Window => Current.Window;
    public static Color Surface => Current.Surface;
    public static Color SurfaceHover => Current.SurfaceHover;
    public static Color Border => Current.Border;
    public static Color Text => Current.Text;
    public static Color Muted => Current.Muted;
    public static Color Track => Current.Track;
    public static Color Green => Current.Green;
    public static Color Orange => Current.Orange;
    public static Color Red => Current.Red;

    public static Color ForUsage(double utilization) => utilization switch
    {
        _ when utilization >= CriticalAt => Current.Red,
        _ when utilization >= WarnAt => Current.Orange,
        _ => Current.Green
    };
}
