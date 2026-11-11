#!/usr/bin/env dotnet
// File-based app (.NET 10+): `dotnet run tools/generate-assets.cs -- <repoRoot>`
// Regenerates every icon / logo / background image used by the solution.
#:package System.Drawing.Common@10.0.0
#:property PublishAot=false
#:property ManagePackageVersionsCentrally=false
#pragma warning disable CA1416 // Windows only tool

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;

var root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
var fontsDir = Path.Combine(root, "src", "PersianCalendar.Desktop", "Assets", "Fonts");
using var fonts = new PrivateFontCollection();
fonts.AddFontFile(Path.Combine(fontsDir, "Vazirmatn-Black.ttf"));
fonts.AddFontFile(Path.Combine(fontsDir, "Vazirmatn-Bold.ttf"));
var vazir = fonts.Families[0];

(Color A, Color B)[] seasons =
[
    (Color.FromArgb(0x10, 0xB9, 0x81), Color.FromArgb(0x06, 0x8F, 0xC7)), // spring
    (Color.FromArgb(0xF5, 0x9E, 0x0B), Color.FromArgb(0xE1, 0x1D, 0x48)), // summer
    (Color.FromArgb(0xF9, 0x73, 0x16), Color.FromArgb(0xBE, 0x18, 0x5D)), // autumn
    (Color.FromArgb(0x63, 0x66, 0xF1), Color.FromArgb(0x0E, 0x74, 0xC9)), // winter
];
var brand = (A: Color.FromArgb(0x7C, 0x3A, 0xED), B: Color.FromArgb(0xEC, 0x48, 0x99));

// ---------- Desktop app icon (.ico with PNG frames) ----------
var desktopAssets = Path.Combine(root, "src", "PersianCalendar.Desktop", "Assets");
WriteIco(Path.Combine(desktopAssets, "app.ico"), [16, 20, 24, 32, 40, 48, 64, 256]);

// ---------- Widget provider package assets ----------
var widgetAssets = Path.Combine(root, "src", "PersianCalendar.WidgetProvider", "Assets");
Directory.CreateDirectory(widgetAssets);
SavePng(Icon(44), Path.Combine(widgetAssets, "Square44x44Logo.png"));
SavePng(Icon(150, padding: 0.18f), Path.Combine(widgetAssets, "Square150x150Logo.png"));
SavePng(Icon(50), Path.Combine(widgetAssets, "StoreLogo.png"));
SavePng(Icon(256), Path.Combine(widgetAssets, "WidgetIcon.png"));
SavePng(Banner(310, 150), Path.Combine(widgetAssets, "Wide310x150Logo.png"));
SavePng(Banner(620, 300), Path.Combine(widgetAssets, "SplashScreen.png"));
// Widget-picker screenshots (Assets/Screenshot*.png) come from the real card renderer:
//   PersianCalendar.WidgetProvider.exe --preview <folder>   → large-dark.png / large-light.png
_ = seasons;

Console.WriteLine("Assets generated under " + root);

// =====================================================================

Bitmap Icon(int size, float padding = 0f)
{
    var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    Hq(g);
    var pad = size * padding;
    var r = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
    var s = r.Width;

    using (var path = Rounded(r, s * 0.24f))
    using (var bg = new LinearGradientBrush(r, brand.A, brand.B, 45f))
        g.FillPath(bg, path);

    // Calendar page
    var page = new RectangleF(r.X + s * 0.18f, r.Y + s * 0.22f, s * 0.64f, s * 0.60f);
    using (var path = Rounded(page, s * 0.10f))
    using (var white = new SolidBrush(Color.White))
        g.FillPath(white, path);
    var header = new RectangleF(page.X, page.Y, page.Width, page.Height * 0.26f);
    using (var path = RoundedTop(header, s * 0.10f))
    using (var hb = new SolidBrush(Color.FromArgb(255, 0xF9, 0x73, 0x16)))
        g.FillPath(hb, path);
    // Binding rings
    using (var ring = new SolidBrush(Color.White))
    {
        var rw = s * 0.07f; var rh = s * 0.14f;
        g.FillEllipse(ring, page.X + page.Width * 0.27f - rw / 2, page.Y - rh * 0.55f, rw, rh);
        g.FillEllipse(ring, page.X + page.Width * 0.73f - rw / 2, page.Y - rh * 0.55f, rw, rh);
    }
    if (size >= 20)
    {
        using var font = new Font(vazir, s * 0.36f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var tb = new SolidBrush(Color.FromArgb(0x4C, 0x1D, 0x95));
        var body = new RectangleF(page.X, header.Bottom - s * 0.02f, page.Width, page.Bottom - header.Bottom + s * 0.04f);
        using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("۱", font, tb, body, sf);
    }
    return bmp;
}

Bitmap Banner(int w, int h)
{
    var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    Hq(g);
    using var icon = Icon((int)(h * 0.62f));
    g.DrawImage(icon, (w - icon.Width) / 2f, (h - icon.Height) / 2f);
    return bmp;
}

void WriteIco(string path, int[] sizes)
{
    var frames = sizes.Select(s => { using var b = Icon(s); using var ms = new MemoryStream(); b.Save(ms, ImageFormat.Png); return ms.ToArray(); }).ToArray();
    using var fs = File.Create(path);
    using var w = new BinaryWriter(fs);
    w.Write((short)0); w.Write((short)1); w.Write((short)frames.Length);
    var offset = 6 + 16 * frames.Length;
    for (var i = 0; i < frames.Length; i++)
    {
        var s = sizes[i];
        w.Write((byte)(s >= 256 ? 0 : s)); w.Write((byte)(s >= 256 ? 0 : s));
        w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
        w.Write(frames[i].Length); w.Write(offset);
        offset += frames[i].Length;
    }
    foreach (var f in frames) w.Write(f);
}

static void SavePng(Bitmap bmp, string path) { using (bmp) bmp.Save(path, ImageFormat.Png); }

static void Hq(Graphics g)
{
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.TextRenderingHint = TextRenderingHint.AntiAlias;
}

static GraphicsPath Rounded(RectangleF r, float radius)
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

static GraphicsPath RoundedTop(RectangleF r, float radius)
{
    var d = radius * 2;
    var p = new GraphicsPath();
    p.AddArc(r.X, r.Y, d, d, 180, 90);
    p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
    p.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
    p.CloseFigure();
    return p;
}
