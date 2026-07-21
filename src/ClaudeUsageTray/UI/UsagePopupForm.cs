using ClaudeUsageTray.Models;

namespace ClaudeUsageTray.UI;

internal sealed class UsagePopupForm : Form
{
    private readonly Label _planLabel;
    private readonly Label _statusLabel;
    private readonly FlowLayoutPanel _metricsPanel;
    private readonly Button _refreshButton;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private UsageSnapshot? _snapshot;

    public UsagePopupForm()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = AppPalette.Window;
        ClientSize = new Size(380, 344);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = nameof(UsagePopupForm);
        Padding = new Padding(18);
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "Claude Usage Tray";
        TopMost = true;

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
            ForeColor = AppPalette.Text,
            Location = new Point(18, 17),
            Text = "Claude usage"
        };

        _planLabel = new Label
        {
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = AppPalette.Muted,
            Location = new Point(20, 47),
            Size = new Size(260, 20),
            Text = "Waiting for the first update…"
        };

        var closeButton = MakeFlatButton("×", new Size(34, 34));
        closeButton.AccessibleName = "Close usage window";
        closeButton.Font = new Font("Segoe UI", 14f);
        closeButton.Location = new Point(ClientSize.Width - 51, 13);
        closeButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        closeButton.Click += (_, _) => Hide();

        _metricsPanel = new FlowLayoutPanel
        {
            AutoScroll = false,
            BackColor = AppPalette.Window,
            FlowDirection = FlowDirection.TopDown,
            Location = new Point(18, 76),
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = new Size(ClientSize.Width - 36, 226),
            WrapContents = false
        };

        _statusLabel = new Label
        {
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = AppPalette.Muted,
            Location = new Point(18, ClientSize.Height - 31),
            Size = new Size(245, 20),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = "Not updated yet"
        };

        _refreshButton = MakeFlatButton("Refresh", new Size(88, 30));
        _refreshButton.AccessibleName = "Refresh usage now";
        _refreshButton.Location = new Point(ClientSize.Width - 106, ClientSize.Height - 39);
        _refreshButton.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
        _refreshButton.Click += (_, _) => RefreshRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange([titleLabel, _planLabel, closeButton, _metricsPanel, _statusLabel, _refreshButton]);

        _countdownTimer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _countdownTimer.Tick += (_, _) =>
        {
            foreach (Control control in _metricsPanel.Controls)
            {
                control.Invalidate();
            }
        };
        _countdownTimer.Start();

        Deactivate += (_, _) => Hide();
        Paint += DrawBorder;
    }

    public event EventHandler? RefreshRequested;

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

    public void SetLoading()
    {
        _refreshButton.Enabled = false;
        _refreshButton.Text = "Updating…";
        _statusLabel.ForeColor = AppPalette.Muted;
        _statusLabel.Text = "Contacting Anthropic…";
    }

    public void SetSnapshot(UsageSnapshot snapshot)
    {
        _snapshot = snapshot;
        _metricsPanel.SuspendLayout();
        _metricsPanel.Controls.Clear();
        foreach (var metric in snapshot.Metrics)
        {
            _metricsPanel.Controls.Add(new UsageMetricControl(metric)
            {
                Width = _metricsPanel.ClientSize.Width
            });
        }
        _metricsPanel.ResumeLayout();

        _planLabel.Text = string.IsNullOrWhiteSpace(snapshot.PlanName)
            ? "Claude Code subscription"
            : HumanizePlan(snapshot.PlanName);
        _statusLabel.ForeColor = AppPalette.Muted;
        _statusLabel.Text = $"Updated {snapshot.UpdatedAt.LocalDateTime:t}";
        _statusLabel.AccessibleDescription = $"Credentials: {snapshot.CredentialSource}";
        FinishLoading();
    }

    public void SetError(string message)
    {
        _statusLabel.ForeColor = AppPalette.Red;
        _statusLabel.Text = message;
        _statusLabel.AccessibleDescription = message;
        FinishLoading();
    }

    public void ShowNearTray()
    {
        var workingArea = Screen.FromPoint(Cursor.Position).WorkingArea;
        var x = Math.Clamp(Cursor.Position.X - Width / 2, workingArea.Left + 8, workingArea.Right - Width - 8);
        var y = workingArea.Bottom - Height - 8;
        Location = new Point(x, y);

        Show();
        Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _countdownTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void FinishLoading()
    {
        _refreshButton.Enabled = true;
        _refreshButton.Text = "Refresh";
    }

    private static Button MakeFlatButton(string text, Size size)
    {
        var button = new Button
        {
            BackColor = AppPalette.Surface,
            Cursor = Cursors.Hand,
            FlatStyle = FlatStyle.Flat,
            ForeColor = AppPalette.Text,
            Size = size,
            Text = text,
            UseVisualStyleBackColor = false
        };
        button.FlatAppearance.BorderColor = AppPalette.Border;
        button.FlatAppearance.MouseOverBackColor = AppPalette.SurfaceHover;
        button.FlatAppearance.MouseDownBackColor = AppPalette.Track;
        return button;
    }

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
