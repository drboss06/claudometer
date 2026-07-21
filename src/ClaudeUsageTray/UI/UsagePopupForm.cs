using ClaudeUsageTray.Models;
using ClaudeUsageTray.Resources;

namespace ClaudeUsageTray.UI;

internal sealed class UsagePopupForm : Form
{
    private static readonly Font TitleFont = new("Segoe UI Semibold", 14f, FontStyle.Bold);
    private static readonly Font SmallFont = new("Segoe UI", 8.5f);
    private static readonly Font CloseFont = new("Segoe UI", 14f);

    private readonly Label _titleLabel;
    private readonly Label _planLabel;
    private readonly Label _expiryLabel;
    private readonly Label _statusLabel;
    private readonly Button _closeButton;
    private readonly Button _refreshButton;
    private readonly Button _actionButton;
    private readonly FlowLayoutPanel _metricsPanel;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private IReadOnlyDictionary<string, IReadOnlyList<double>>? _history;
    // Desired visibility, tracked explicitly because Control.Visible reflects the
    // parent's state and reads false until the form is actually shown.
    private bool _expiryShown;
    private bool _actionShown;

    public UsagePopupForm()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = nameof(UsagePopupForm);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = AppInfoText();
        TopMost = true;

        _titleLabel = new Label { AutoSize = true, Font = TitleFont, Text = Strings.PopupTitle };
        _planLabel = new Label { AutoEllipsis = true, Font = SmallFont, Text = Strings.WaitingFirstUpdate };
        _expiryLabel = new Label { AutoEllipsis = true, Font = SmallFont, Visible = false };

        _closeButton = MakeFlatButton("×");
        _closeButton.AccessibleName = Strings.CloseWindow;
        _closeButton.Font = CloseFont;
        _closeButton.Click += (_, _) => Hide();

        _metricsPanel = new FlowLayoutPanel
        {
            AutoScroll = false,
            FlowDirection = FlowDirection.TopDown,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            WrapContents = false
        };

        _statusLabel = new Label
        {
            AutoEllipsis = true,
            Font = SmallFont,
            Text = Strings.NotUpdatedYet,
            TextAlign = ContentAlignment.TopLeft
        };

        _actionButton = MakeFlatButton(Strings.SignIn);
        _actionButton.AccessibleName = Strings.SignIn;
        _actionButton.Visible = false;
        _actionButton.Click += (_, _) =>
            MessageBox.Show(Strings.HowToSignIn, AppInfoText(), MessageBoxButtons.OK, MessageBoxIcon.Information);

        _refreshButton = MakeFlatButton(Strings.Refresh);
        _refreshButton.AccessibleName = Strings.RefreshUsageNow;
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange([_titleLabel, _planLabel, _expiryLabel, _closeButton, _metricsPanel, _statusLabel, _actionButton, _refreshButton]);

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _countdownTimer.Tick += (_, _) =>
        {
            foreach (Control control in _metricsPanel.Controls)
            {
                control.Invalidate();
            }
        };
        _countdownTimer.Start();

        Paint += DrawBorder;
        ApplyTheme();
        RelayoutChrome();
    }

    public event EventHandler? RefreshRequested;

    public DateTimeOffset LastHiddenAt { get; private set; } = DateTimeOffset.MinValue;

    protected override CreateParams CreateParams
    {
        get
        {
            const int CsDropShadow = 0x00020000;
            var parameters = base.CreateParams;
            parameters.ClassStyle |= CsDropShadow;
            return parameters;
        }
    }

    private float DpiScale => DeviceDpi / 96f;

    private int S(float value) => (int)Math.Round(value * DpiScale);

    public void SetLoading()
    {
        _refreshButton.Enabled = false;
        _refreshButton.Text = Strings.Updating;
        _statusLabel.ForeColor = AppPalette.Muted;
        _statusLabel.Text = Strings.ContactingAnthropic;
    }

    public void SetSnapshot(UsageSnapshot snapshot, IReadOnlyDictionary<string, IReadOnlyList<double>>? history = null)
    {
        _history = history;
        _actionShown = false;
        _actionButton.Visible = false;

        _metricsPanel.SuspendLayout();
        // Dispose the previous rows before clearing so their native handles are not
        // orphaned to the finalizer on every refresh.
        foreach (Control control in _metricsPanel.Controls)
        {
            control.Dispose();
        }
        _metricsPanel.Controls.Clear();
        foreach (var metric in snapshot.Metrics)
        {
            _metricsPanel.Controls.Add(new UsageMetricControl(metric)
            {
                Width = _metricsPanel.ClientSize.Width,
                History = LookupHistory(metric.Label)
            });
        }
        _metricsPanel.ResumeLayout();

        _planLabel.Text = string.IsNullOrWhiteSpace(snapshot.PlanName)
            ? Strings.SubscriptionGeneric
            : HumanizePlan(snapshot.PlanName!);

        UpdateExpiry(snapshot.TokenExpiresAt);

        _statusLabel.ForeColor = AppPalette.Muted;
        _statusLabel.Text = Strings.UpdatedAt(snapshot.UpdatedAt.LocalDateTime.ToString("t"));
        _statusLabel.AccessibleDescription = Strings.CredentialsSource(snapshot.CredentialSource);
        FinishLoading();
        RelayoutChrome();
    }

    public void SetError(string message, bool authError)
    {
        _statusLabel.ForeColor = AppPalette.Red;
        _statusLabel.Text = message;
        _statusLabel.AccessibleDescription = message;
        _actionShown = authError;
        _actionButton.Visible = authError;
        _expiryShown = false;
        _expiryLabel.Visible = false;
        FinishLoading();
        RelayoutChrome();
    }

    public void ShowNearTray()
    {
        // Realise the handle/size first so positioning uses the final dimensions.
        if (!Visible)
        {
            Show();
        }

        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var x = Math.Clamp(Cursor.Position.X - Width / 2, workingArea.Left + 8, workingArea.Right - Width - 8);
        var y = workingArea.Bottom - Height - 8;
        Location = new Point(x, y);
        Activate();
    }

    /// <summary>Re-applies the active palette; call after the theme preference changes.</summary>
    public void ApplyTheme()
    {
        BackColor = AppPalette.Window;
        ForeColor = AppPalette.Text;
        _metricsPanel.BackColor = AppPalette.Window;
        _titleLabel.ForeColor = AppPalette.Text;
        _planLabel.ForeColor = AppPalette.Muted;
        _expiryLabel.ForeColor = AppPalette.Orange;
        _statusLabel.ForeColor = AppPalette.Muted;
        StyleButton(_closeButton);
        StyleButton(_refreshButton);
        StyleButton(_actionButton);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RelayoutChrome();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        LastHiddenAt = DateTimeOffset.UtcNow;
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countdownTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void UpdateExpiry(DateTimeOffset? expiresAt)
    {
        if (!expiresAt.HasValue)
        {
            _expiryShown = false;
            _expiryLabel.Visible = false;
            return;
        }

        var remaining = expiresAt.Value - DateTimeOffset.Now;
        // Only warn when the sign-in is close to lapsing, to avoid clutter.
        if (remaining <= TimeSpan.Zero || remaining > TimeSpan.FromHours(12))
        {
            _expiryShown = false;
            _expiryLabel.Visible = false;
            return;
        }

        _expiryLabel.Text = Strings.ExpiresIn(FormatShort(remaining));
        _expiryShown = true;
        _expiryLabel.Visible = true;
    }

    private static string FormatShort(TimeSpan remaining)
    {
        if (remaining.TotalHours >= 1)
        {
            return Strings.ShortHoursMinutes((int)remaining.TotalHours, remaining.Minutes);
        }

        return Strings.ShortMinutes(Math.Max(1, remaining.Minutes));
    }

    private IReadOnlyList<double>? LookupHistory(string label) =>
        _history is not null && _history.TryGetValue(label, out var series) ? series : null;

    private void RelayoutChrome()
    {
        var width = S(380);
        var padding = S(18);
        var inner = width - padding * 2;

        _titleLabel.Location = new Point(padding, S(16));
        _planLabel.Location = new Point(padding + S(2), S(46));
        _planLabel.Size = new Size(inner - S(40), S(20));

        _closeButton.Size = new Size(S(34), S(34));
        _closeButton.Location = new Point(width - padding - _closeButton.Width + S(3), S(13));

        var panelTop = S(76);
        _metricsPanel.Location = new Point(padding, panelTop);
        _metricsPanel.Width = inner;

        // Size the panel to its rows so extra model windows are never clipped.
        var content = 0;
        foreach (Control control in _metricsPanel.Controls)
        {
            control.Width = _metricsPanel.ClientSize.Width;
            content += control.Height + control.Margin.Top + control.Margin.Bottom;
        }
        if (content == 0)
        {
            content = S(80);
        }
        _metricsPanel.Height = content;

        var y = panelTop + content + S(4);
        // Position the expiry label from the tracked flag (not Control.Visible,
        // which is false until the form is shown); reserve space only when shown.
        _expiryLabel.Location = new Point(padding, y);
        _expiryLabel.Size = new Size(inner, S(18));
        if (_expiryShown)
        {
            y += S(20);
        }

        _statusLabel.Location = new Point(padding, y);
        _statusLabel.Size = new Size(inner, S(32));

        var buttonY = y + S(34);
        var buttonHeight = S(30);
        var refreshWidth = S(88);
        _refreshButton.Size = new Size(refreshWidth, buttonHeight);
        _refreshButton.Location = new Point(width - padding - refreshWidth, buttonY);

        if (_actionShown)
        {
            var actionWidth = S(80);
            _actionButton.Size = new Size(actionWidth, buttonHeight);
            _actionButton.Location = new Point(width - padding - refreshWidth - S(8) - actionWidth, buttonY);
        }

        ClientSize = new Size(width, buttonY + buttonHeight + padding);
    }

    private void FinishLoading()
    {
        _refreshButton.Enabled = true;
        _refreshButton.Text = Strings.Refresh;
    }

    private static Button MakeFlatButton(string text) => new()
    {
        Cursor = Cursors.Hand,
        FlatStyle = FlatStyle.Flat,
        Text = text,
        UseVisualStyleBackColor = false
    };

    private static void StyleButton(Button button)
    {
        button.BackColor = AppPalette.Surface;
        button.ForeColor = AppPalette.Text;
        button.FlatAppearance.BorderColor = AppPalette.Border;
        button.FlatAppearance.MouseOverBackColor = AppPalette.SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = AppPalette.Track;
    }

    private static string AppInfoText() => AppInfo.Name;

    private static string HumanizePlan(string plan)
    {
        var value = plan.Replace('_', ' ').Trim();
        return string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    private void DrawBorder(object? sender, PaintEventArgs eventArgs)
    {
        using var pen = new Pen(AppPalette.Border);
        eventArgs.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }
}
