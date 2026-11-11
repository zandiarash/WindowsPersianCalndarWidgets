using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using PersianCalendar.Core;

namespace PersianCalendar.WidgetProvider.Rendering;

internal enum CardTheme { Dark, Light }

/// <summary>A rendered piece of the card: the bitmap plus its size in device-independent pixels.</summary>
internal sealed record CardImage(BitmapSource Bitmap, double Width, double Height)
{
    public string ToDataUri()
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    }
}

internal sealed record Palette(Color Text, Color Secondary, Color Tertiary, Color Holiday, Color Surface, Color Stroke)
{
    public static Palette For(CardTheme theme) => theme == CardTheme.Dark ? Dark : Light;

    private static readonly Palette Dark = new(C("#F4F4FA"), C("#BDBDCC"), C("#8B8BA0"), C("#FF6B7A"), C("#16FFFFFF"), C("#14FFFFFF"));
    private static readonly Palette Light = new(C("#1B1B26"), C("#55556E"), C("#9A9AB0"), C("#E11D48"), C("#0C000000"), C("#10000000"));

    public static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);
}

/// <summary>
/// Draws the widget's visuals with WPF (Vazirmatn font, gradients, glow) exactly like the desktop widget.
/// The Widgets Board can only use its system font, so the card shows these crisp images instead of text.
/// WPF needs an STA thread; every render is marshalled onto a dedicated one.
/// </summary>
internal static class CardRenderer
{
    private const double Scale = 2.5;      // pixels per DIP → sharp at 100–250 % display scaling
    public const double CardWidth = 272;   // content width of a Widgets Board card (DIP)

    private static readonly Lazy<Dispatcher> UiThread = new(static () =>
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        { IsBackground = true, Name = "CardRenderer" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    });

    private static readonly FontFamily Vazir = new(
        new Uri(System.IO.Path.Combine(AppContext.BaseDirectory, "Fonts") + System.IO.Path.DirectorySeparatorChar), "./#Vazirmatn");
    private static readonly FontFamily Latin = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private static readonly SolidColorBrush White = Frozen(new SolidColorBrush(Colors.White));

    /// <summary>Runs rendering work on the STA render thread.</summary>
    public static T Run<T>(Func<T> work) => UiThread.Value.Invoke(work);

    // =====================================================================
    //  Hero: today's date + clock on the seasonal gradient (theme independent)
    // =====================================================================

    public static CardImage Hero(JalaliDate today, DateTimeOffset now, bool compact, EventCatalog catalog)
    {
        double w = CardWidth, h = compact ? 116 : 144;
        var card = GradientCard(today.Season, w, h, 16);

        var content = new Grid { Margin = new Thickness(16, 12, 16, 12), FlowDirection = FlowDirection.RightToLeft };
        content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.ColumnDefinitions.Add(new ColumnDefinition());
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Right: weekday + big day + month/year
        var date = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        date.Children.Add(Text(today.DayOfWeek.PersianName, 13.5, FontWeights.Bold, White, opacity: 0.92));
        var dayRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, -2, 0, 0) };
        var day = Text(today.Day.ToPersianDigits(), compact ? 46 : 54, FontWeights.Black, White);
        day.Margin = new Thickness(0, compact ? -10 : -12, 0, compact ? -12 : -14);
        day.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Direction = 270, Opacity = 0.22 };
        dayRow.Children.Add(day);
        var monthYear = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        monthYear.Children.Add(Text(today.MonthName, compact ? 18 : 20, FontWeights.Bold, White));
        monthYear.Children.Add(Text(today.Year.ToPersianDigits(), 12.5, FontWeights.Normal, White, opacity: 0.85, margin: new Thickness(0, -4, 0, 0)));
        dayRow.Children.Add(monthYear);
        date.Children.Add(dayRow);
        content.Children.Add(date);

        // Left: clock, greeting, other calendars (aligned to the visual left edge)
        var side = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(side, 1);
        var clock = Text(now.ToString("HH:mm", CultureInfo.InvariantCulture).ToPersianDigits(), compact ? 24 : 27, FontWeights.Bold, White);
        clock.HorizontalAlignment = HorizontalAlignment.Right;
        clock.Margin = new Thickness(0, -4, 0, -2);
        side.Children.Add(clock);
        side.Children.Add(Text(PersianText.Greeting(now.Hour), 10.5, FontWeights.Medium, White, opacity: 0.85, align: HorizontalAlignment.Right));
        var greg = Text(today.ToDateOnly().ToString("ddd, d MMM yyyy", CultureInfo.GetCultureInfo("en-US")), 10.5, FontWeights.Normal, White,
            font: Latin, opacity: 0.9, align: HorizontalAlignment.Right, margin: new Thickness(0, 4, 0, 0));
        greg.FlowDirection = FlowDirection.LeftToRight;
        side.Children.Add(greg);
        side.Children.Add(Text(HijriDate.FromDateOnly(today.ToDateOnly(), catalog.HijriAdjustment).ToString(), 10.5, FontWeights.Normal, White,
            opacity: 0.9, align: HorizontalAlignment.Right));
        content.Children.Add(side);

        if (!compact)
        {
            // Year progress (fills from the right, like reading direction)
            var progress = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            Grid.SetRow(progress, 1);
            Grid.SetColumnSpan(progress, 2);
            var bar = new Grid { Height = 4 };
            bar.Children.Add(new Border { CornerRadius = new CornerRadius(2), Background = Frozen(new SolidColorBrush(Color.FromArgb(0x40, 255, 255, 255))) });
            bar.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = White,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = (w - 32) * today.DayOfYear / today.DaysInYear,
            });
            progress.Children.Add(bar);
            var toNowruz = today.DaysUntil(new JalaliDate(today.Year + 1, 1, 1));
            progress.Children.Add(Text($"روز {today.DayOfYear.ToPersianDigits()} از {today.DaysInYear.ToPersianDigits()}  ·  {toNowruz.ToPersianDigits()} روز تا نوروز",
                10, FontWeights.Normal, White, opacity: 0.85, margin: new Thickness(0, 4, 0, 0)));
            content.Children.Add(progress);
        }

        card.Children.Add(content);
        return Render(card, w, h);
    }

    /// <summary>Square tile for the small widget.</summary>
    public static CardImage Tile(JalaliDate today, DateTimeOffset now)
    {
        const double size = 128;
        var card = GradientCard(today.Season, size, size, 18);
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FlowDirection = FlowDirection.RightToLeft };
        stack.Children.Add(Text(today.DayOfWeek.PersianName, 12, FontWeights.Bold, White, opacity: 0.92, align: HorizontalAlignment.Center));
        var day = Text(today.Day.ToPersianDigits(), 46, FontWeights.Black, White, align: HorizontalAlignment.Center, margin: new Thickness(0, -10, 0, -12));
        day.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Direction = 270, Opacity = 0.22 };
        stack.Children.Add(day);
        stack.Children.Add(Text($"{today.MonthName} {today.Year.ToPersianDigits()}", 13, FontWeights.Bold, White, align: HorizontalAlignment.Center));
        stack.Children.Add(Text(now.ToString("HH:mm", CultureInfo.InvariantCulture).ToPersianDigits(), 11.5, FontWeights.Medium, White,
            opacity: 0.85, align: HorizontalAlignment.Center, margin: new Thickness(0, 2, 0, 0)));
        card.Children.Add(stack);
        return Render(card, size, size);
    }

    // =====================================================================
    //  Themed parts (rendered once for dark, once for light)
    // =====================================================================

    /// <summary>Month title, year and season chip.</summary>
    public static CardImage MonthTitle(JalaliDate month, CardTheme theme)
    {
        var p = Palette.For(theme);
        var (accent, _) = Accent(month.Season);
        const double w = 190, h = 40;
        var root = new StackPanel { Width = w, Height = h, FlowDirection = FlowDirection.RightToLeft, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Text(month.MonthName, 17, FontWeights.Bold, Brush(p.Text)));
        row.Children.Add(Text(month.Year.ToPersianDigits(), 17, FontWeights.Light, Brush(p.Secondary), margin: new Thickness(5, 0, 0, 0)));
        row.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Brush(Color.FromArgb(0x30, accent.R, accent.G, accent.B)),
            Padding = new Thickness(7, 0, 7, 1),
            Margin = new Thickness(8, 3, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = Text(month.SeasonName, 10, FontWeights.Bold, Brush(theme == CardTheme.Dark ? Lighten(accent) : accent)),
        });
        root.Children.Add(row);

        var first = month.ToDateOnly();
        var last = month.AddDays(month.DaysInMonth - 1).ToDateOnly();
        var en = CultureInfo.GetCultureInfo("en-US");
        var sub = first.Month == last.Month ? first.ToString("MMMM yyyy", en) : $"{first.ToString("MMM", en)} – {last.ToString("MMM yyyy", en)}";
        var subtitle = Text(sub, 10.5, FontWeights.Normal, Brush(p.Tertiary), font: Latin, align: HorizontalAlignment.Left, margin: new Thickness(0, -3, 0, 0));
        subtitle.FlowDirection = FlowDirection.LeftToRight;
        root.Children.Add(subtitle);
        return Render(root, w, h);
    }

    /// <summary>Round chevron button. <paramref name="pointsLeft"/> = ‹ .</summary>
    public static CardImage Arrow(bool pointsLeft, CardTheme theme)
    {
        var p = Palette.For(theme);
        const double s = 32;
        var root = new Grid { Width = s, Height = s };
        root.Children.Add(new Ellipse { Fill = Brush(p.Surface), Stroke = Brush(p.Stroke), StrokeThickness = 1 });
        root.Children.Add(new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(pointsLeft ? "M 18.5,10.5 L 13,16 L 18.5,21.5" : "M 13.5,10.5 L 19,16 L 13.5,21.5"),
            Stroke = Brush(p.Text),
            StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        });
        return Render(root, s, s);
    }

    /// <summary>The «امروز» pill shown when browsing another month.</summary>
    public static CardImage TodayPill(Season season, CardTheme theme)
    {
        var (accent, _) = Accent(season);
        const double w = 56, h = 28;
        var root = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(14),
            Background = Brush(Color.FromArgb(0x30, accent.R, accent.G, accent.B)),
            Child = Text("امروز", 11.5, FontWeights.Bold, Brush(theme == CardTheme.Dark ? Lighten(accent) : accent),
                align: HorizontalAlignment.Center, valign: VerticalAlignment.Center),
        };
        return Render(root, w, h);
    }

    /// <summary>Saturday-first month grid with week-day header.</summary>
    public static CardImage MonthGrid(JalaliDate month, JalaliDate today, CardTheme theme, EventCatalog catalog)
    {
        var p = Palette.For(theme);
        var (accent, accentTo) = Accent(month.Season);
        var cells = MonthLayout.Build(month.Year, month.Month, today, catalog, fixedSixWeeks: false);
        const double w = CardWidth, headerH = 22, rowH = 27;
        var h = headerH + rowH * (cells.Count / 7) + 5;

        var root = new StackPanel { Width = w, FlowDirection = FlowDirection.RightToLeft };
        var header = new UniformGrid { Columns = 7, Height = headerH };
        for (var i = 0; i < 7; i++)
            header.Children.Add(Text(PersianText.WeekDaysShort[i], 11.5, FontWeights.Bold, Brush(i == 6 ? p.Holiday : p.Tertiary),
                align: HorizontalAlignment.Center, valign: VerticalAlignment.Center));
        root.Children.Add(header);

        var grid = new UniformGrid { Columns = 7 };
        foreach (var c in cells)
        {
            var cell = new Grid { Height = rowH, Opacity = c.IsInMonth ? 1 : 0.32 };
            var fg = c.IsToday ? White : Brush(c.IsHoliday ? p.Holiday : p.Text);
            if (c.IsToday)
            {
                cell.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Margin = new Thickness(5, 1, 5, 1),
                    Background = Gradient(accent, accentTo),
                    Effect = new DropShadowEffect { Color = accent, BlurRadius = 12, ShadowDepth = 2, Direction = 270, Opacity = 0.5 },
                });
            }
            cell.Children.Add(Text(c.Date.Day.ToPersianDigits(), 14, c.IsToday ? FontWeights.Bold : FontWeights.SemiBold, fg,
                align: HorizontalAlignment.Center, valign: VerticalAlignment.Center, margin: new Thickness(0, 0, 0, 1)));
            if (c.HasEvents && c.IsInMonth)
            {
                cell.Children.Add(new Ellipse
                {
                    Width = 4, Height = 4,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 3),
                    Fill = c.IsToday ? White : Brush(c.IsHoliday ? p.Holiday : accent),
                });
            }
            grid.Children.Add(cell);
        }
        root.Children.Add(grid);
        return Render(root, w, h);
    }

    /// <summary>The current week (Saturday → Friday) for the medium widget.</summary>
    public static CardImage WeekStrip(JalaliDate today, CardTheme theme, EventCatalog catalog)
    {
        var p = Palette.For(theme);
        var (accent, accentTo) = Accent(today.Season);
        const double w = CardWidth, h = 70;
        var saturday = today.AddDays(-today.WeekDayIndex);
        var root = new UniformGrid { Columns = 7, Width = w, Height = h, FlowDirection = FlowDirection.RightToLeft };
        for (var i = 0; i < 7; i++)
        {
            var d = saturday.AddDays(i);
            var isToday = d == today;
            var holiday = catalog.IsHoliday(d);
            var cell = new Grid { Margin = new Thickness(3, 2, 3, 8) };
            if (isToday)
            {
                cell.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Gradient(accent, accentTo),
                    Effect = new DropShadowEffect { Color = accent, BlurRadius = 12, ShadowDepth = 2, Direction = 270, Opacity = 0.5 },
                });
            }
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(Text(PersianText.WeekDaysShort[i], 10.5, FontWeights.Bold,
                isToday ? White : Brush(holiday ? p.Holiday : p.Tertiary), opacity: isToday ? 0.85 : 1, align: HorizontalAlignment.Center));
            stack.Children.Add(Text(d.Day.ToPersianDigits(), 16, FontWeights.Bold,
                isToday ? White : Brush(holiday ? p.Holiday : p.Text), align: HorizontalAlignment.Center, margin: new Thickness(0, -2, 0, 0)));
            stack.Children.Add(new Ellipse
            {
                Width = 4, Height = 4, Margin = new Thickness(0, 1, 0, 0),
                Fill = catalog.GetEvents(d).Count == 0 ? Brushes.Transparent : isToday ? White : Brush(holiday ? p.Holiday : accent),
            });
            cell.Children.Add(stack);
            root.Children.Add(cell);
        }
        return Render(root, w, h);
    }

    /// <summary>"Coming up" strip: next occasion with countdown.</summary>
    public static CardImage NextEvent(JalaliDate today, JalaliDate date, CalendarEvent e, CardTheme theme)
    {
        var p = Palette.For(theme);
        var (accent, _) = Accent(today.Season);
        const double w = CardWidth, h = 32;
        var root = new Border
        {
            Width = w,
            Height = h,
            CornerRadius = new CornerRadius(11),
            Background = Brush(p.Surface),
            BorderBrush = Brush(p.Stroke),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(11, 0, 11, 0),
            FlowDirection = FlowDirection.RightToLeft,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(Text("", 12.5, FontWeights.Normal, Brush(e.IsHoliday ? p.Holiday : accent), font: Icons, valign: VerticalAlignment.Center));

        var title = new TextBlock
        {
            FontFamily = Vazir,
            Foreground = Brush(p.Text), // also colours the trimming ellipsis
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 8, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Inlines.Add(new System.Windows.Documents.Run("پیش رو: ") { Foreground = Brush(p.Tertiary), FontSize = 10.5 });
        title.Inlines.Add(new System.Windows.Documents.Run(e.Title) { Foreground = Brush(p.Text), FontSize = 11.5, FontWeight = FontWeights.Bold });
        Grid.SetColumn(title, 1);
        grid.Children.Add(title);

        var when = Text(PersianText.Relative(today.DaysUntil(date)), 10.5, FontWeights.Medium,
            Brush(e.IsHoliday ? p.Holiday : p.Secondary), valign: VerticalAlignment.Center);
        Grid.SetColumn(when, 2);
        grid.Children.Add(when);
        root.Child = grid;
        return Render(root, w, h);
    }

    // =====================================================================
    //  Preview: approximate how the card looks on the Widgets Board
    // =====================================================================

    public static CardImage BoardMock(CardTheme theme, CardImage?[] parts, double spacing = 8)
    {
        var p = Palette.For(theme);
        var card = new Border
        {
            Width = CardWidth + 28,
            CornerRadius = new CornerRadius(8),
            Background = Brush(theme == CardTheme.Dark ? Palette.C("#2B2B33") : Palette.C("#FBF7F4")),
            Padding = new Thickness(14, 10, 14, 14),
        };
        var stack = new StackPanel();
        stack.Children.Add(Text("تقویم شمسی", 12, FontWeights.Normal, Brush(p.Text), align: HorizontalAlignment.Right, margin: new Thickness(0, 0, 4, 8)));
        foreach (var part in parts)
        {
            if (part is null) continue;
            stack.Children.Add(new Image { Source = part.Bitmap, Width = part.Width, Height = part.Height, Margin = new Thickness(0, 0, 0, spacing) });
        }
        card.Child = stack;
        card.Measure(new Size(card.Width, double.PositiveInfinity));
        return Render(card, card.Width, card.DesiredSize.Height);
    }

    // =====================================================================
    //  Helpers
    // =====================================================================

    private static Grid GradientCard(Season season, double w, double h, double radius)
    {
        var (from, to) = Accent(season);
        var card = new Grid { Width = w, Height = h, Clip = new RectangleGeometry(new Rect(0, 0, w, h), radius, radius) };
        card.Children.Add(new Rectangle { Fill = Gradient(from, to) });
        var orbs = new Canvas();
        AddOrb(orbs, w * 0.62, -h * 0.55, h * 1.15, 0x26);
        AddOrb(orbs, -h * 0.25, h * 0.45, h * 0.9, 0x18);
        AddOrb(orbs, w * 0.30, -h * 0.35, h * 0.5, 0x12);
        card.Children.Add(orbs);
        return card;

        static void AddOrb(Canvas canvas, double x, double y, double d, byte alpha)
        {
            var orb = new Ellipse { Width = d, Height = d, Fill = Frozen(new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255))) };
            Canvas.SetLeft(orb, x);
            Canvas.SetTop(orb, y);
            canvas.Children.Add(orb);
        }
    }

    private static (Color From, Color To) Accent(Season season)
    {
        var (from, to) = SeasonPalette.Hex(season);
        return (Palette.C(from), Palette.C(to));
    }

    private static Color Lighten(Color c) => Color.FromRgb((byte)(c.R + (255 - c.R) * 0.25), (byte)(c.G + (255 - c.G) * 0.25), (byte)(c.B + (255 - c.B) * 0.25));

    private static LinearGradientBrush Gradient(Color from, Color to) =>
        Frozen(new LinearGradientBrush(from, to, new Point(1, 0), new Point(0, 1)));

    private static SolidColorBrush Brush(Color c) => Frozen(new SolidColorBrush(c));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static TextBlock Text(string text, double size, FontWeight weight, Brush brush, FontFamily? font = null, double opacity = 1,
        HorizontalAlignment align = HorizontalAlignment.Stretch, VerticalAlignment valign = VerticalAlignment.Top, Thickness margin = default) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = brush,
        FontFamily = font ?? Vazir,
        Opacity = opacity,
        HorizontalAlignment = align,
        VerticalAlignment = valign,
        Margin = margin,
    };

    private static CardImage Render(FrameworkElement element, double w, double h)
    {
        // Host in a left-to-right root so right-to-left content is laid out (not mirrored) correctly.
        var host = new Border { Width = w, Height = h, FlowDirection = FlowDirection.LeftToRight, Child = element };
        TextOptions.SetTextFormattingMode(host, TextFormattingMode.Ideal);
        TextOptions.SetTextRenderingMode(host, TextRenderingMode.Grayscale);
        host.Measure(new Size(w, h));
        host.Arrange(new Rect(0, 0, w, h));
        host.UpdateLayout();

        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(w * Scale), (int)Math.Ceiling(h * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bitmap.Render(host);
        bitmap.Freeze();
        return new CardImage(bitmap, w, h);
    }
}
