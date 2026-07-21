using ClaudeUsageTray.App;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.Testing;
using ClaudeUsageTray.UI;

namespace ClaudeUsageTray;

internal static class Program
{
    private const string MutexName = "Local\\ClaudeUsageTray-8D9F4CBF-1240-4F89-B046-13981970C31D";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = SelfTestRunner.Run();
            return;
        }

        if (args.Length >= 2 && args[0].Equals("--export-icon", StringComparison.OrdinalIgnoreCase))
        {
            ExportIcon(args[1]);
            return;
        }

        if (args.Length >= 2 && args[0].Equals("--render-preview", StringComparison.OrdinalIgnoreCase))
        {
            RenderPreview(args[1]);
            return;
        }

        using var mutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                Resources.Strings.AlreadyRunning,
                AppInfo.Name,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.Run(new TrayApplicationContext(args.Contains("--demo", StringComparer.OrdinalIgnoreCase)));
    }

    private static void RenderPreview(string outputPath)
    {
        var commandLine = Environment.GetCommandLineArgs();

        var culture = commandLine.FirstOrDefault(arg => arg is "en" or "ru");
        if (culture is not null)
        {
            var info = new System.Globalization.CultureInfo(culture);
            System.Globalization.CultureInfo.CurrentUICulture = info;
            System.Globalization.CultureInfo.CurrentCulture = info;
        }

        if (commandLine.Contains("light", StringComparer.OrdinalIgnoreCase))
        {
            UI.AppPalette.Current = UI.Palette.Light;
        }

        using var form = new UsagePopupForm();
        form.SetSnapshot(UsageSnapshot.CreateDemo());
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-10_000, -10_000);
        form.Show();
        Application.DoEvents();

        using var bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.ClientSize));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        form.Hide();
    }

    private static void ExportIcon(string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var sizes = new[] { 16, 20, 24, 32, 48, 64, 128, 256 };
        var images = new List<byte[]>();
        foreach (var size in sizes)
        {
            using var bitmap = UsageIconFactory.CreateBitmap(size, 42);
            using var memory = new MemoryStream();
            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
            images.Add(memory.ToArray());
        }

        using var stream = File.Create(fullPath);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); // Reserved.
        writer.Write((ushort)1); // Icon resource.
        writer.Write((ushort)sizes.Length);

        var dataOffset = 6 + (16 * sizes.Length);
        for (var index = 0; index < sizes.Length; index++)
        {
            var size = sizes[index];
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write((uint)images[index].Length);
            writer.Write((uint)dataOffset);
            dataOffset += images[index].Length;
        }

        foreach (var image in images)
        {
            writer.Write(image);
        }
    }
}
