using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace WSMMonitor;

/// <summary>Tray / splash: blue shield + red pulse line.</summary>
public static class AgentIconFactory
{
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    /// <param name="pixelSize">Icon size in pixels (e.g. 32 or 64).</param>
    public static Icon CreateShieldPulseIcon(int pixelSize = 32)
    {
        pixelSize = Math.Clamp(pixelSize, 16, 256);
        using var bmp = new Bitmap(pixelSize, pixelSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        bmp.MakeTransparent();
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);
            DrawShieldPulseFrame(g, new RectangleF(0, 0, pixelSize, pixelSize), 1f, 1f);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hIcon);
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>Draw animated shield + ECG. <paramref name="shieldProgress01"/> 0..1 grows shield; <paramref name="linePulse01"/> scales line alpha/width (heartbeat).</summary>
    public static void DrawShieldPulseFrame(Graphics g, RectangleF bounds, float shieldProgress01, float linePulse01)
    {
        var pixelSize = (int)Math.Clamp(Math.Min(bounds.Width, bounds.Height), 16f, 256f);
        var ox = bounds.X + (bounds.Width - pixelSize) / 2f;
        var oy = bounds.Y + (bounds.Height - pixelSize) / 2f;

        var state = g.Save();
        try
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TranslateTransform(ox, oy);

            var t = Math.Clamp(shieldProgress01, 0f, 1f);
            var scale = EaseOutBack(t);
            if (scale < 0.08f)
                scale = 0.08f;

            g.TranslateTransform(pixelSize / 2f, pixelSize / 2f);
            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-pixelSize / 2f, -pixelSize / 2f);

            var pad = pixelSize * 0.06f;
            var w = pixelSize - 2 * pad;
            var h = pixelSize - 2 * pad;

            using var shieldPath = BuildShieldPath(pad, pad, w, h);
            var rectTop = new RectangleF(pad, pad, w, h * 0.55f);
            using (var br = new LinearGradientBrush(
                       rectTop,
                       Color.FromArgb(255, 100, 170, 255),
                       Color.FromArgb(255, 30, 90, 200),
                       LinearGradientMode.Vertical))
            {
                g.FillPath(br, shieldPath);
            }

            var outlineW = Math.Max(1f, pixelSize / 28f);
            using var outlinePen = new Pen(Color.FromArgb(255, 15, 55, 130), outlineW);
            g.DrawPath(outlinePen, shieldPath);

            DrawHospitalPulse(g, pixelSize, linePulse01);
        }
        finally
        {
            g.Restore(state);
        }
    }

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        x = Math.Clamp(x, 0f, 1f);
        return 1f + c3 * MathF.Pow(x - 1f, 3) + c1 * MathF.Pow(x - 1f, 2);
    }

    private static GraphicsPath BuildShieldPath(float x, float y, float w, float h)
    {
        var cx = x + w / 2f;
        var top = y + h * 0.06f;
        var bottom = y + h * 0.94f;
        var sideY = y + h * 0.68f;

        var path = new GraphicsPath();
        path.StartFigure();
        path.AddBezier(
            new PointF(x + w * 0.18f, top + h * 0.06f),
            new PointF(x + w * 0.02f, top + h * 0.22f),
            new PointF(x + w * 0.02f, y + h * 0.42f),
            new PointF(x + w * 0.06f, sideY));
        path.AddLine(x + w * 0.06f, sideY, cx, bottom);
        path.AddLine(cx, bottom, x + w * 0.94f, sideY);
        path.AddBezier(
            new PointF(x + w * 0.94f, sideY),
            new PointF(x + w * 0.98f, y + h * 0.42f),
            new PointF(x + w * 0.98f, top + h * 0.22f),
            new PointF(x + w * 0.82f, top + h * 0.06f));
        path.AddBezier(
            new PointF(x + w * 0.82f, top + h * 0.06f),
            new PointF(cx, top),
            new PointF(cx, top),
            new PointF(x + w * 0.18f, top + h * 0.06f));
        path.CloseFigure();
        return path;
    }

    private static void DrawHospitalPulse(Graphics g, int pixelSize, float linePulse01)
    {
        var lp = Math.Clamp(linePulse01, 0.2f, 1.4f);
        var a = (int)Math.Clamp(255 * (0.35f + 0.65f * lp), 40f, 255f);
        var red = Color.FromArgb(a, 230, 45, 55);
        var stroke = Math.Max(1.2f, pixelSize * 0.07f * (0.85f + 0.35f * lp));
        using var pen = new Pen(red, stroke)
        {
            LineJoin = LineJoin.Round,
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        var yb = pixelSize * 0.54f;
        var x0 = pixelSize * 0.12f;
        var pts = new[]
        {
            new PointF(x0, yb),
            new PointF(pixelSize * 0.26f, yb),
            new PointF(pixelSize * 0.32f, yb - pixelSize * 0.14f),
            new PointF(pixelSize * 0.38f, yb + pixelSize * 0.05f),
            new PointF(pixelSize * 0.44f, yb - pixelSize * 0.09f),
            new PointF(pixelSize * 0.54f, yb),
            new PointF(pixelSize * 0.64f, yb),
            new PointF(pixelSize * 0.70f, yb - pixelSize * 0.18f),
            new PointF(pixelSize * 0.76f, yb + pixelSize * 0.07f),
            new PointF(pixelSize * 0.88f, yb),
        };
        g.DrawLines(pen, pts);
    }
}
