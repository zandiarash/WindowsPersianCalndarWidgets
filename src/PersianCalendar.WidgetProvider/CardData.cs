using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Windows.Widgets;
using PersianCalendar.Core;
using PersianCalendar.WidgetProvider.Rendering;

namespace PersianCalendar.WidgetProvider;

/// <summary>Loads the Adaptive Card templates embedded in the assembly.</summary>
internal static class CardTemplates
{
    private static readonly ConcurrentDictionary<string, string> Cache = new();

    public static string For(WidgetSize size) => Load(size switch
    {
        WidgetSize.Small => "small.json",
        WidgetSize.Medium => "medium.json",
        _ => "large.json",
    });

    private static string Load(string name) => Cache.GetOrAdd(name, static n =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Templates." + n)
            ?? throw new InvalidOperationException($"Missing template {n}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
}

/// <summary>
/// Builds the data bound to the Adaptive Card templates. All visible text is pre-rendered with the
/// Vazirmatn font (see <see cref="CardRenderer"/>); themed parts are produced for both dark and light,
/// and the template picks one with <c>$host.hostTheme</c>.
/// </summary>
internal static class CardData
{
    // Themed images only change when the day or the shown month changes, so cache them.
    private static readonly ConcurrentDictionary<string, string> ImageCache = new();

    public static string Build(WidgetSize size, int monthOffset, TimeProvider time) =>
        CardRenderer.Run(() => BuildCore(size, monthOffset, time.GetLocalNow()));

    private static string BuildCore(WidgetSize size, int monthOffset, DateTimeOffset now)
    {
        var catalog = EventCatalog.Default;
        var today = JalaliDate.FromDateTime(now.DateTime);
        var shown = today.FirstDayOfMonth.AddMonths(monthOffset);
        var next = catalog.Upcoming(today).FirstOrDefault();

        var data = new JsonObject
        {
            ["alt"] = today.ToLongPersianString(),
            ["isAway"] = monthOffset != 0,
        };

        if (ImageCache.Count > 200) ImageCache.Clear();

        switch (size)
        {
            case WidgetSize.Small:
                data["tile"] = CardRenderer.SmallBanner(today, now, catalog).ToDataUri();
                break;

            case WidgetSize.Medium:
                data["hero"] = CardRenderer.Hero(today, now, compact: true, catalog).ToDataUri();
                foreach (var theme in Enum.GetValues<CardTheme>())
                {
                    data[Key(theme)] = new JsonObject
                    {
                        ["week"] = Cached($"week|{today}|{theme}", () => CardRenderer.WeekStrip(today, theme, catalog)),
                    };
                }
                break;

            default:
                data["hero"] = CardRenderer.Hero(today, now, compact: false, catalog).ToDataUri();
                foreach (var theme in Enum.GetValues<CardTheme>())
                {
                    var themed = new JsonObject
                    {
                        ["title"] = Cached($"title|{shown}|{theme}", () => CardRenderer.MonthTitle(shown, theme)),
                        ["grid"] = Cached($"grid|{shown}|{today}|{theme}", () => CardRenderer.MonthGrid(shown, today, theme, catalog)),
                        ["prev"] = Cached($"prev|{theme}", () => CardRenderer.Arrow(pointsLeft: true, theme)),
                        ["next"] = Cached($"next|{theme}", () => CardRenderer.Arrow(pointsLeft: false, theme)),
                        ["today"] = Cached($"today|{shown.Season}|{theme}", () => CardRenderer.TodayPill(shown.Season, theme)),
                    };
                    if (next.Event is { } e)
                        themed["next_event"] = Cached($"event|{today}|{theme}", () => CardRenderer.NextEvent(today, next.Date, e, theme));
                    data[Key(theme)] = themed;
                }
                break;
        }

        return data.ToJsonString();
    }

    private static string Key(CardTheme theme) => theme == CardTheme.Dark ? "dark" : "light";

    private static string Cached(string key, Func<CardImage> render) =>
        ImageCache.GetOrAdd(key, _ => render().ToDataUri());

    /// <summary>Writes every rendered part plus a board mock-up per size and theme (for docs / design checks).</summary>
    public static void WritePreview(string folder, DateTimeOffset now) => CardRenderer.Run(() =>
    {
        Directory.CreateDirectory(folder);
        var catalog = EventCatalog.Default;
        var today = JalaliDate.FromDateTime(now.DateTime);
        var next = catalog.Upcoming(today).First();

        foreach (var theme in Enum.GetValues<CardTheme>())
        {
            var name = Key(theme);
            Save(CardRenderer.BoardMock(theme, [CardRenderer.SmallBanner(today, now, catalog)]), $"small-{name}.png");
            Save(CardRenderer.BoardMock(theme, [CardRenderer.Hero(today, now, true, catalog), CardRenderer.WeekStrip(today, theme, catalog)]), $"medium-{name}.png");

            var nav = NavMock(today, theme);
            Save(CardRenderer.BoardMock(theme,
            [
                CardRenderer.Hero(today, now, false, catalog), nav,
                CardRenderer.MonthGrid(today.FirstDayOfMonth, today, theme, catalog),
                CardRenderer.NextEvent(today, next.Date, next.Event, theme),
            ]), $"large-{name}.png");
        }

        foreach (var size in (ReadOnlySpan<WidgetSize>)[WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large])
        {
            var json = BuildCore(size, 0, now);
            File.WriteAllText(Path.Combine(folder, $"{size.ToString().ToLowerInvariant()}.data.json"), json);
            Console.WriteLine($"{size,-7} data: {json.Length / 1024.0:N0} KB");
        }
        return 0;

        void Save(CardImage image, string file)
        {
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image.Bitmap));
            using var fs = File.Create(Path.Combine(folder, file));
            encoder.Save(fs);
        }
    });

    /// <summary>Mock-up of the navigation row (title on the right, arrows on the left) as the card lays it out.</summary>
    private static CardImage NavMock(JalaliDate today, CardTheme theme)
    {
        var title = CardRenderer.MonthTitle(today.FirstDayOfMonth, theme);
        var prev = CardRenderer.Arrow(true, theme);
        var next = CardRenderer.Arrow(false, theme);
        var row = new System.Windows.Controls.Grid { Width = CardRenderer.CardWidth, Height = 40 };
        var right = new System.Windows.Controls.Image { Source = title.Bitmap, Width = title.Width, Height = title.Height, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var arrows = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Left, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        arrows.Children.Add(new System.Windows.Controls.Image { Source = prev.Bitmap, Width = 32, Height = 32, Margin = new System.Windows.Thickness(0, 0, 6, 0) });
        arrows.Children.Add(new System.Windows.Controls.Image { Source = next.Bitmap, Width = 32, Height = 32 });
        row.Children.Add(right);
        row.Children.Add(arrows);
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap((int)(row.Width * 2.5), (int)(40 * 2.5), 240, 240, System.Windows.Media.PixelFormats.Pbgra32);
        row.Measure(new System.Windows.Size(row.Width, 40));
        row.Arrange(new System.Windows.Rect(0, 0, row.Width, 40));
        bmp.Render(row);
        return new CardImage(bmp, row.Width, 40);
    }
}
