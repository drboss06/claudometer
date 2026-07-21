using System.Diagnostics;
using ClaudeUsageTray.Infrastructure;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.UI;

namespace ClaudeUsageTray.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);
    private readonly bool _demoMode;
    private readonly AnthropicUsageClient _usageClient;
    private readonly UsagePopupForm _popup;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private Icon? _currentIcon;
    private int _lastNotificationLevel;

    public TrayApplicationContext(bool demoMode)
    {
        _demoMode = demoMode;
        _usageClient = new AnthropicUsageClient(new CredentialsProvider());
        _popup = new UsagePopupForm();
        _popup.RefreshRequested += async (_, _) => await RefreshAsync(showPopup: true);

        var menu = new ContextMenuStrip
        {
            BackColor = AppPalette.Surface,
            ForeColor = AppPalette.Text,
            Renderer = new DarkMenuRenderer()
        };
        menu.Items.Add("Open usage", null, (_, _) => TogglePopup());
        menu.Items.Add("Refresh now", null, async (_, _) => await RefreshAsync(showPopup: false));
        menu.Items.Add(new ToolStripSeparator());

        var startupItem = new ToolStripMenuItem("Run at Windows startup")
        {
            Checked = StartupManager.IsEnabled,
            CheckOnClick = true
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            try
            {
                StartupManager.SetEnabled(startupItem.Checked);
            }
            catch (Exception exception)
            {
                startupItem.Checked = StartupManager.IsEnabled;
                MessageBox.Show(exception.Message, "Claude Usage Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        menu.Items.Add(startupItem);
        menu.Items.Add("About", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _currentIcon = UsageIconFactory.Create(0);
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = _currentIcon,
            Text = "Claude usage — updating…",
            Visible = true
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                TogglePopup();
            }
        };

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)RefreshInterval.TotalMilliseconds
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync(showPopup: false);
        _refreshTimer.Start();

        _ = RefreshAsync(showPopup: false);
    }

    protected override void ExitThreadCore()
    {
        _shutdown.Cancel();
        _refreshTimer.Stop();
        _notifyIcon.Visible = false;
        _popup.Close();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _shutdown.Dispose();
            _refreshGate.Dispose();
            _refreshTimer.Dispose();
            _notifyIcon.Dispose();
            _currentIcon?.Dispose();
            _popup.Dispose();
            _usageClient.Dispose();
        }
        base.Dispose(disposing);
    }

    private async Task RefreshAsync(bool showPopup)
    {
        if (!await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            _popup.SetLoading();
            if (showPopup && !_popup.Visible)
            {
                _popup.ShowNearTray();
            }

            var snapshot = _demoMode
                ? UsageSnapshot.CreateDemo()
                : await _usageClient.GetUsageAsync(_shutdown.Token);
            ApplySnapshot(snapshot);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception) when (exception is CredentialsNotFoundException or UsageApiException or HttpRequestException or TaskCanceledException)
        {
            ApplyError(exception.Message);
        }
        catch (Exception exception)
        {
            ApplyError($"Update failed: {exception.Message}");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        _popup.SetSnapshot(snapshot);
        SetTrayIcon(snapshot.MaximumUtilization, hasError: false);
        var maximum = (int)Math.Round(snapshot.MaximumUtilization);
        _notifyIcon.Text = TruncateTooltip($"Claude usage — {maximum}% max");
        MaybeNotify(maximum);
    }

    private void ApplyError(string message)
    {
        _popup.SetError(message);
        SetTrayIcon(100, hasError: true);
        _notifyIcon.Text = "Claude usage — update failed";
    }

    private void SetTrayIcon(double utilization, bool hasError)
    {
        var nextIcon = UsageIconFactory.Create(utilization, hasError);
        _notifyIcon.Icon = nextIcon;
        var previous = _currentIcon;
        _currentIcon = nextIcon;
        previous?.Dispose();
    }

    private void MaybeNotify(int maximum)
    {
        var level = maximum switch
        {
            >= 95 => 2,
            >= 80 => 1,
            _ => 0
        };

        if (level > _lastNotificationLevel)
        {
            _notifyIcon.ShowBalloonTip(
                5_000,
                "Claude usage is running low",
                $"Your highest usage window is at {maximum}%.",
                level == 2 ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }

        _lastNotificationLevel = level;
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
        }
        else
        {
            _popup.ShowNearTray();
        }
    }

    private static string TruncateTooltip(string text) => text.Length <= 63 ? text : text[..63];

    private static void ShowAbout()
    {
        MessageBox.Show(
            "Claude Usage Tray 0.1.0\n\nAn unofficial open-source Windows monitor for Claude Code subscription usage.\n\nCredentials stay on this computer and are sent only to Anthropic.",
            "About Claude Usage Tray",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable())
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs eventArgs)
        {
            eventArgs.TextColor = eventArgs.Item.Enabled ? AppPalette.Text : AppPalette.Muted;
            base.OnRenderItemText(eventArgs);
        }
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => AppPalette.Surface;
        public override Color ImageMarginGradientBegin => AppPalette.Surface;
        public override Color ImageMarginGradientMiddle => AppPalette.Surface;
        public override Color ImageMarginGradientEnd => AppPalette.Surface;
        public override Color MenuItemSelected => AppPalette.SurfaceHover;
        public override Color MenuItemBorder => AppPalette.Border;
        public override Color MenuBorder => AppPalette.Border;
        public override Color SeparatorDark => AppPalette.Border;
        public override Color SeparatorLight => AppPalette.Surface;
    }
}
