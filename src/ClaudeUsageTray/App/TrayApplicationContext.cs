using System.Diagnostics;
using ClaudeUsageTray.Infrastructure;
using ClaudeUsageTray.Models;
using ClaudeUsageTray.Resources;
using ClaudeUsageTray.UI;

namespace ClaudeUsageTray.App;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly int[] IntervalChoicesMinutes = [1, 5, 15, 30];
    private const int HistoryCap = 40;
    private static readonly TimeSpan TrayReopenGuard = TimeSpan.FromMilliseconds(300);

    private readonly bool _demoMode;
    private readonly AppSettings _settings;
    private readonly AnthropicUsageClient _usageClient;
    private readonly UpdateChecker _updateChecker;
    private readonly UsagePopupForm _popup;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _updateItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<string, List<double>> _history = new(StringComparer.Ordinal);

    private Icon? _currentIcon;
    private int _lastNotificationLevel;
    private double _lastUtilization;
    private bool _lastHadError;
    private bool _appliedLight;
    private string? _updateTag;

    public TrayApplicationContext(bool demoMode)
    {
        _demoMode = demoMode;
        _settings = AppSettings.Load();
        ApplyThresholds();
        ThemeManager.Apply(_settings.Theme);
        _appliedLight = AppPalette.IsLight;

        _usageClient = new AnthropicUsageClient(new CredentialsProvider());
        _updateChecker = new UpdateChecker();
        _popup = new UsagePopupForm();
        _popup.RefreshRequested += async (_, _) => await RefreshAsync(showPopup: true);

        _menu = BuildMenu(out _updateItem);

        _currentIcon = UsageIconFactory.Create(0, hasError: false, lightBackground: ThemeManager.TaskbarIsLight());
        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _currentIcon,
            Text = Strings.TrayUpdating,
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
            Interval = (int)_settings.RefreshInterval.TotalMilliseconds
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync(showPopup: false);
        _refreshTimer.Start();

        _ = RefreshAsync(showPopup: false);
        _ = MaybeCheckForUpdatesAsync();
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
            _menu.Dispose();
            _currentIcon?.Dispose();
            _popup.Dispose();
            _usageClient.Dispose();
            _updateChecker.Dispose();
        }
        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu(out ToolStripMenuItem updateItem)
    {
        var menu = new ContextMenuStrip { Renderer = new DarkMenuRenderer() };

        updateItem = new ToolStripMenuItem { Visible = false };
        updateItem.Click += (_, _) => OpenReleasesPage();
        menu.Items.Add(updateItem);

        menu.Items.Add(Strings.OpenUsage, null, (_, _) => TogglePopup());
        menu.Items.Add(Strings.RefreshNow, null, async (_, _) => await RefreshAsync(showPopup: false));
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(BuildThemeMenu());
        menu.Items.Add(BuildIntervalMenu());

        var notificationsItem = new ToolStripMenuItem(Strings.Notifications)
        {
            Checked = _settings.NotificationsEnabled,
            CheckOnClick = true
        };
        notificationsItem.CheckedChanged += (_, _) =>
        {
            _settings.NotificationsEnabled = notificationsItem.Checked;
            _settings.Save();
        };
        menu.Items.Add(notificationsItem);

        var startupItem = new ToolStripMenuItem(Strings.RunAtStartup)
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
                MessageBox.Show(exception.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        menu.Items.Add(startupItem);

        var updatesItem = new ToolStripMenuItem(Strings.CheckForUpdates)
        {
            Checked = _settings.CheckForUpdates,
            CheckOnClick = true,
            Available = UpdateChecker.IsConfigured
        };
        updatesItem.CheckedChanged += (_, _) =>
        {
            _settings.CheckForUpdates = updatesItem.Checked;
            _settings.Save();
            if (updatesItem.Checked)
            {
                _ = MaybeCheckForUpdatesAsync();
            }
        };
        menu.Items.Add(updatesItem);

        menu.Items.Add(Strings.About, null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.Exit, null, (_, _) => ExitThread());

        ApplyMenuTheme(menu);
        return menu;
    }

    private ToolStripMenuItem BuildThemeMenu()
    {
        var root = new ToolStripMenuItem(Strings.Theme);
        AddThemeChoice(root, Strings.ThemeSystem, AppThemePreference.System);
        AddThemeChoice(root, Strings.ThemeDark, AppThemePreference.Dark);
        AddThemeChoice(root, Strings.ThemeLight, AppThemePreference.Light);
        return root;
    }

    private void AddThemeChoice(ToolStripMenuItem root, string text, AppThemePreference preference)
    {
        var item = new ToolStripMenuItem(text) { Checked = _settings.Theme == preference };
        item.Click += (_, _) =>
        {
            _settings.Theme = preference;
            _settings.Save();
            foreach (ToolStripMenuItem sibling in root.DropDownItems.OfType<ToolStripMenuItem>())
            {
                sibling.Checked = ReferenceEquals(sibling, item);
            }
            ApplyThemeEverywhere();
        };
        root.DropDownItems.Add(item);
    }

    private ToolStripMenuItem BuildIntervalMenu()
    {
        var root = new ToolStripMenuItem(Strings.RefreshInterval);
        foreach (var minutes in IntervalChoicesMinutes)
        {
            var item = new ToolStripMenuItem(Strings.Minutes(minutes))
            {
                Checked = _settings.RefreshIntervalMinutes == minutes,
                Tag = minutes
            };
            item.Click += (_, _) =>
            {
                _settings.RefreshIntervalMinutes = minutes;
                _settings.Save();
                _refreshTimer.Interval = (int)_settings.RefreshInterval.TotalMilliseconds;
                foreach (ToolStripMenuItem sibling in root.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    sibling.Checked = sibling.Tag is int value && value == minutes;
                }
            };
            root.DropDownItems.Add(item);
        }

        return root;
    }

    private async Task RefreshAsync(bool showPopup)
    {
        if (!await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            MaybeReapplySystemTheme();

            if (showPopup || _popup.Visible)
            {
                _popup.SetLoading();
            }

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
        catch (SessionExpiredException exception)
        {
            ApplyError(exception.Message, authError: true);
        }
        catch (CredentialsNotFoundException exception)
        {
            ApplyError(exception.Message, authError: true);
        }
        catch (Exception exception) when (exception is UsageApiException or HttpRequestException or TaskCanceledException)
        {
            ApplyError(exception.Message, authError: false);
        }
        catch (Exception)
        {
            // Never surface an arbitrary exception message (it could contain sensitive text).
            ApplyError(Strings.GenericUpdateFailed, authError: false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        RecordHistory(snapshot);
        _popup.SetSnapshot(snapshot, HistoryView());
        _lastUtilization = snapshot.MaximumUtilization;
        _lastHadError = false;
        SetTrayIcon(snapshot.MaximumUtilization, hasError: false);
        var maximum = (int)Math.Round(snapshot.MaximumUtilization);
        _notifyIcon.Text = TruncateTooltip(Strings.TrayMax(maximum));
        MaybeNotify(maximum);
    }

    private void ApplyError(string message, bool authError)
    {
        _popup.SetError(message, authError);
        _lastUtilization = 100;
        _lastHadError = true;
        SetTrayIcon(100, hasError: true);
        _notifyIcon.Text = Strings.TrayFailed;
    }

    private void RecordHistory(UsageSnapshot snapshot)
    {
        foreach (var metric in snapshot.Metrics)
        {
            if (!metric.Utilization.HasValue)
            {
                continue;
            }

            if (!_history.TryGetValue(metric.Label, out var series))
            {
                series = new List<double>();
                _history[metric.Label] = series;
            }

            series.Add(metric.Utilization.Value);
            if (series.Count > HistoryCap)
            {
                series.RemoveRange(0, series.Count - HistoryCap);
            }
        }
    }

    private IReadOnlyDictionary<string, IReadOnlyList<double>> HistoryView() =>
        _history.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<double>)pair.Value.ToArray(), StringComparer.Ordinal);

    private void SetTrayIcon(double utilization, bool hasError)
    {
        var nextIcon = UsageIconFactory.Create(utilization, hasError, ThemeManager.TaskbarIsLight());
        _notifyIcon.Icon = nextIcon;
        var previous = _currentIcon;
        _currentIcon = nextIcon;
        previous?.Dispose();
    }

    private void MaybeNotify(int maximum)
    {
        var level = maximum switch
        {
            _ when maximum >= _settings.CriticalAt => 2,
            _ when maximum >= _settings.WarnAt => 1,
            _ => 0
        };

        if (_settings.NotificationsEnabled && level > _lastNotificationLevel)
        {
            _notifyIcon.ShowBalloonTip(
                5_000,
                Strings.BalloonTitle,
                Strings.BalloonBody(maximum),
                level == 2 ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }

        _lastNotificationLevel = level;
    }

    private void TogglePopup()
    {
        if (_popup.Visible)
        {
            _popup.Hide();
            return;
        }

        // A click on the tray icon first deactivates (and hides) an open popup;
        // don't immediately reopen it in that case.
        if (DateTimeOffset.UtcNow - _popup.LastHiddenAt < TrayReopenGuard)
        {
            return;
        }

        _popup.ShowNearTray();
    }

    private void ApplyThresholds()
    {
        AppPalette.WarnAt = _settings.WarnAt;
        AppPalette.CriticalAt = _settings.CriticalAt;
    }

    private void MaybeReapplySystemTheme()
    {
        if (_settings.Theme != AppThemePreference.System)
        {
            return;
        }

        ThemeManager.Apply(_settings.Theme);
        if (AppPalette.IsLight != _appliedLight)
        {
            _appliedLight = AppPalette.IsLight;
            _popup.ApplyTheme();
            ApplyMenuTheme(_menu);
            SetTrayIcon(_lastUtilization, _lastHadError);
        }
    }

    private void ApplyThemeEverywhere()
    {
        ThemeManager.Apply(_settings.Theme);
        _appliedLight = AppPalette.IsLight;
        _popup.ApplyTheme();
        ApplyMenuTheme(_menu);
        SetTrayIcon(_lastUtilization, _lastHadError);
    }

    private static void ApplyMenuTheme(ContextMenuStrip menu)
    {
        menu.BackColor = AppPalette.Surface;
        menu.ForeColor = AppPalette.Text;
    }

    private async Task MaybeCheckForUpdatesAsync()
    {
        if (_demoMode || !_settings.CheckForUpdates || !UpdateChecker.IsConfigured)
        {
            return;
        }

        try
        {
            var tag = await _updateChecker.GetNewerReleaseAsync(_shutdown.Token);
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _updateTag = tag;
                _updateItem.Text = Strings.UpdateAvailable(tag);
                _updateItem.Visible = true;
                _notifyIcon.ShowBalloonTip(5_000, AppInfo.Name, Strings.UpdateAvailable(tag), ToolTipIcon.Info);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Update checks are best-effort.
        }
    }

    private void OpenReleasesPage()
    {
        if (!UpdateChecker.IsConfigured)
        {
            return;
        }

        try
        {
            var url = $"https://github.com/{UpdateChecker.RepositorySlug}/releases/latest";
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Ignore inability to open the browser.
        }
    }

    private static string TruncateTooltip(string text) => text.Length <= 63 ? text : text[..63];

    private static void ShowAbout()
    {
        MessageBox.Show(
            Strings.AboutText(AppInfo.Version),
            Strings.About,
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
