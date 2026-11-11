using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using PersianCalendar.Core;

namespace PersianCalendar.Desktop.Services;

/// <summary>
/// Notification-area icon that always shows <b>today's Persian day number</b> on a seasonal tile.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon = new() { Visible = false };
    private nint _hIcon;

    public event Action? LeftClick;
    public event Action? RightClick;

    public TrayIconService()
    {
        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) LeftClick?.Invoke();
            else if (e.Button == MouseButtons.Right) RightClick?.Invoke();
        };
    }

    public void Show() => _notifyIcon.Visible = true;

    public void Update(JalaliDate today, System.Windows.Media.Color accent)
    {
        const int size = 64;
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using var path = RoundedRect(new RectangleF(1, 1, size - 2, size - 2), 16);
            using var bg = new LinearGradientBrush(new Rectangle(0, 0, size, size),
                Color.FromArgb(accent.R, accent.G, accent.B),
                Color.FromArgb(Math.Max(0, accent.R - 60), Math.Max(0, accent.G - 60), Math.Max(0, accent.B - 30)), 45f);
            g.FillPath(bg, path);

            using var font = new Font("Segoe UI", today.Day >= 10 ? 34 : 40, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(today.Day.ToPersianDigits(), font, Brushes.White, new RectangleF(0, 3, size, size), sf);
        }

        var old = _hIcon;
        _hIcon = bmp.GetHicon();
        _notifyIcon.Icon = Icon.FromHandle(_hIcon);
        _notifyIcon.Text = $"{today.ToLongPersianString()}\n{PersianText.Gregorian(today.ToDateOnly())}";
        if (old != 0) NativeMethods.DestroyIcon(old);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var d = radius * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        if (_hIcon != 0) NativeMethods.DestroyIcon(_hIcon);
    }
}
