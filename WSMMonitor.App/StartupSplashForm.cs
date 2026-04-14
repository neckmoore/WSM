using System.Drawing.Drawing2D;

namespace WSMMonitor;

/// <summary>Startup window with shield + pulse animation (same visual language as the tray icon).</summary>
public sealed class StartupSplashForm : Form
{
    /// <summary>Minimum time the host should keep this form visible so the animation can be seen.</summary>
    public const int MinimumHostDisplayMs = 4200;

    private readonly System.Windows.Forms.Timer _anim = new() { Interval = 40 };
    private readonly AnimPanel _canvas = new() { Size = new Size(200, 200), BackColor = Color.White };
    private readonly Label _title;
    private readonly Label _subtitle;
    private long _shownTick;
    private bool _clockStarted;

    public StartupSplashForm()
    {
        Text = WsmLocalization.T("AppTitle");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        ControlBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("Segoe UI", 9f);
        BackColor = SystemColors.Window;
        ClientSize = new Size(480, 300);

        try
        {
            using var ic = AgentIconFactory.CreateShieldPulseIcon(32);
            Icon = (Icon)ic.Clone();
        }
        catch
        {
            /* ignore */
        }

        _title = new Label
        {
            Text = WsmLocalization.T("AppTitle"),
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(22, 18)
        };
        _subtitle = new Label
        {
            Text = WsmLocalization.T("SplashStarting"),
            ForeColor = SystemColors.GrayText,
            AutoSize = true,
            Location = new Point(22, ClientSize.Height - 42)
        };

        _canvas.Location = new Point((ClientSize.Width - _canvas.Width) / 2, 54);
        _canvas.Paint += OnCanvasPaint;

        Controls.Add(_title);
        Controls.Add(_canvas);
        Controls.Add(_subtitle);

        Shown += OnShown;

        _anim.Tick += (_, _) =>
        {
            if (!_clockStarted)
                return;
            _canvas.Invalidate();
        };

        FormClosed += (_, _) =>
        {
            _anim.Stop();
            _anim.Dispose();
        };
    }

    private void OnShown(object? sender, EventArgs e)
    {
        _shownTick = Environment.TickCount64;
        _clockStarted = true;
        _anim.Start();
        _canvas.Invalidate();
    }

    private void OnCanvasPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.Clear(Color.White);
        using var border = new Pen(Color.FromArgb(220, 200, 210, 230), 1f);
        g.DrawRectangle(border, 0, 0, _canvas.Width - 1, _canvas.Height - 1);

        float shieldT;
        float linePulse;
        if (_clockStarted)
        {
            var ms = (float)Math.Max(0, Environment.TickCount64 - _shownTick);
            // ~2.8s linear 0..1; EaseOutBack is applied inside DrawShieldPulseFrame.
            shieldT = Math.Min(1f, ms / 2800f);
            // ~1.1 visible pulse cycles per second on the ECG line.
            var pulsePhase = ms * 0.007f;
            linePulse = 0.52f + 0.48f * (0.5f + 0.5f * MathF.Sin(pulsePhase));
        }
        else
        {
            shieldT = 0.04f;
            linePulse = 0.55f;
        }

        AgentIconFactory.DrawShieldPulseFrame(g, new RectangleF(4, 4, _canvas.Width - 8, _canvas.Height - 8), shieldT, linePulse);
    }

    private sealed class AnimPanel : Panel
    {
        public AnimPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}
