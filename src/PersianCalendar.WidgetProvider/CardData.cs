using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;
using PersianCalendar.Core;

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

    public static string Load(string name) => Cache.GetOrAdd(name, static n =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Templates." + n)
            ?? throw new InvalidOperationException($"Missing template {n}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    /// <summary>Seasonal gradient as a data URI (the Widgets host renders it as the hero background).</summary>
    public static string Hero(Season season) => Cache.GetOrAdd("hero-" + season, static key =>
    {
        var file = $"Templates.{key.ToLowerInvariant()}.png";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(file)
            ?? throw new InvalidOperationException($"Missing image {file}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
    });
}

/// <summary>Builds the JSON data bound to the Adaptive Card templates (Adaptive Cards templating).</summary>
internal static class CardData
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static string Build(WidgetSize size, int monthOffset, TimeProvider time)
    {
        var catalog = EventCatalog.Default;
        var now = time.GetLocalNow();
        var today = JalaliDate.FromDateTime(now.DateTime);
        var shown = today.FirstDayOfMonth.AddMonths(monthOffset);

        var data = new JsonObject
        {
            ["hero"] = CardTemplates.Hero(today.Season),
            ["weekday"] = today.DayOfWeek.PersianName,
            ["day"] = today.Day.ToPersianDigits(),
            ["month"] = today.MonthName,
            ["year"] = today.Year.ToPersianDigits(),
            ["monthYear"] = $"{today.MonthName} {today.Year.ToPersianDigits()}",
            ["time"] = now.ToString("HH:mm", CultureInfo.InvariantCulture).ToPersianDigits(),
            ["greeting"] = PersianText.Greeting(now.Hour),
            ["gregorian"] = today.ToDateOnly().ToString("ddd, d MMM yyyy", English),
            ["hijri"] = HijriDate.FromDateOnly(today.ToDateOnly(), catalog.HijriAdjustment).ToString(),
            ["progress"] = $"روز {today.DayOfYear.ToPersianDigits()} از {today.DaysInYear.ToPersianDigits()} · {today.DaysUntil(new JalaliDate(today.Year + 1, 1, 1)).ToPersianDigits()} روز تا نوروز",
            ["season"] = today.SeasonName,
            ["todayEvents"] = string.Join(" · ", catalog.GetEvents(today).Select(static e => e.Title)),
            ["isHoliday"] = catalog.IsHoliday(today),
        };

        if (catalog.Upcoming(today).FirstOrDefault() is { Event: { } next } upcoming)
        {
            data["nextEvent"] = next.Title;
            data["nextWhen"] = $"{PersianText.Relative(today.DaysUntil(upcoming.Date))} · {upcoming.Date.ToShortPersianString()}";
            data["nextColor"] = next.IsHoliday ? "attention" : "accent";
        }

        if (size == WidgetSize.Medium)
            data["week"] = Week(today, catalog);

        if (size == WidgetSize.Large)
        {
            data["title"] = $"{shown.MonthName} {shown.Year.ToPersianDigits()}";
            data["subtitle"] = Subtitle(shown);
            data["isAway"] = monthOffset != 0;
            data["headers"] = new JsonArray([.. PersianText.WeekDaysShort.Select((h, i) =>
                (JsonNode)new JsonObject { ["t"] = h, ["color"] = i == 6 ? "attention" : "default" })]);
            data["weeks"] = Weeks(shown, today, catalog);
        }

        return data.ToJsonString();
    }

    /// <summary>The current Saturday→Friday week strip for the medium widget.</summary>
    private static JsonArray Week(JalaliDate today, EventCatalog catalog)
    {
        var saturday = today.AddDays(-today.WeekDayIndex);
        return [.. Enumerable.Range(0, 7).Select(i =>
        {
            var d = saturday.AddDays(i);
            var isToday = d == today;
            return (JsonNode)new JsonObject
            {
                ["wd"] = PersianText.WeekDaysShort[i],
                ["n"] = d.Day.ToPersianDigits(),
                ["style"] = isToday ? "accent" : "default",
                ["color"] = catalog.IsHoliday(d) ? "attention" : "default",
                ["weight"] = isToday ? "bolder" : "default",
                ["dot"] = catalog.GetEvents(d).Count > 0 ? "•" : " ",
            };
        })];
    }

    private static JsonArray Weeks(JalaliDate month, JalaliDate today, EventCatalog catalog)
    {
        var cells = MonthLayout.Build(month.Year, month.Month, today, catalog, fixedSixWeeks: false);
        return [.. cells.Chunk(7).Select(week => (JsonNode)new JsonObject
        {
            ["days"] = new JsonArray([.. week.Select(c => (JsonNode)new JsonObject
            {
                ["n"] = c.Date.Day.ToPersianDigits(),
                ["style"] = c.IsToday ? "accent" : "default",
                ["color"] = c.IsHoliday ? "attention" : "default",
                ["subtle"] = !c.IsInMonth,
                ["weight"] = c.IsToday ? "bolder" : "default",
            })]),
        })];
    }

    private static string Subtitle(JalaliDate month)
    {
        var first = month.ToDateOnly();
        var last = month.AddDays(month.DaysInMonth - 1).ToDateOnly();
        return first.Month == last.Month
            ? first.ToString("MMMM yyyy", English)
            : $"{first.ToString("MMM", English)} – {last.ToString("MMM yyyy", English)}";
    }
}
