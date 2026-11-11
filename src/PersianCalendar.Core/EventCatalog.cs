using System.Collections.Frozen;

namespace PersianCalendar.Core;

public enum EventKind { National, Religious, Cultural }

/// <summary>An occasion shown on the calendar. Holidays are painted red.</summary>
public sealed record CalendarEvent(string Title, bool IsHoliday, EventKind Kind);

/// <summary>
/// Official Iranian holidays and a few well-loved cultural occasions.
/// Solar dates are exact; lunar dates follow <see cref="HijriDate"/> and may differ by ±1 day
/// from the final official announcement.
/// </summary>
public sealed class EventCatalog(int hijriAdjustment = HijriDate.IranAdjustment)
{
    public static EventCatalog Default { get; } = new();

    public int HijriAdjustment { get; } = hijriAdjustment;

    private static readonly FrozenDictionary<(int Month, int Day), CalendarEvent[]> Solar = new Dictionary<(int, int), CalendarEvent[]>
    {
        [(1, 1)] = [new("جشن نوروز / آغاز سال نو", true, EventKind.National)],
        [(1, 2)] = [new("عید نوروز", true, EventKind.National)],
        [(1, 3)] = [new("عید نوروز", true, EventKind.National)],
        [(1, 4)] = [new("عید نوروز", true, EventKind.National)],
        [(1, 12)] = [new("روز جمهوری اسلامی", true, EventKind.National)],
        [(1, 13)] = [new("روز طبیعت (سیزده‌بدر)", true, EventKind.National)],
        [(2, 1)] = [new("روز بزرگداشت سعدی", false, EventKind.Cultural)],
        [(2, 25)] = [new("روز بزرگداشت فردوسی", false, EventKind.Cultural)],
        [(3, 14)] = [new("رحلت امام خمینی", true, EventKind.National)],
        [(3, 15)] = [new("قیام ۱۵ خرداد", true, EventKind.National)],
        [(4, 13)] = [new("جشن تیرگان", false, EventKind.Cultural)],
        [(5, 8)] = [new("روز بزرگداشت سهروردی", false, EventKind.Cultural)],
        [(6, 31)] = [new("آخرین روز تابستان", false, EventKind.Cultural)],
        [(7, 1)] = [new("آغاز پاییز / بازگشایی مدارس", false, EventKind.Cultural)],
        [(7, 8)] = [new("روز بزرگداشت مولوی", false, EventKind.Cultural)],
        [(7, 16)] = [new("جشن مهرگان", false, EventKind.Cultural)],
        [(7, 20)] = [new("روز بزرگداشت حافظ", false, EventKind.Cultural)],
        [(9, 30)] = [new("شب یلدا", false, EventKind.Cultural)],
        [(10, 1)] = [new("آغاز زمستان", false, EventKind.Cultural)],
        [(11, 10)] = [new("جشن سده", false, EventKind.Cultural)],
        [(11, 22)] = [new("پیروزی انقلاب اسلامی", true, EventKind.National)],
        [(12, 29)] = [new("ملی شدن صنعت نفت", true, EventKind.National)],
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<(int Month, int Day), CalendarEvent[]> Lunar = new Dictionary<(int, int), CalendarEvent[]>
    {
        [(1, 9)] = [new("تاسوعای حسینی", true, EventKind.Religious)],
        [(1, 10)] = [new("عاشورای حسینی", true, EventKind.Religious)],
        [(2, 20)] = [new("اربعین حسینی", true, EventKind.Religious)],
        [(2, 28)] = [new("رحلت پیامبر اکرم و شهادت امام حسن مجتبی", true, EventKind.Religious)],
        [(3, 8)] = [new("شهادت امام حسن عسکری", true, EventKind.Religious)],
        [(3, 17)] = [new("میلاد پیامبر اکرم و امام جعفر صادق", true, EventKind.Religious)],
        [(6, 3)] = [new("شهادت حضرت فاطمه زهرا", true, EventKind.Religious)],
        [(7, 13)] = [new("ولادت امام علی / روز پدر", true, EventKind.Religious)],
        [(7, 27)] = [new("مبعث رسول اکرم", true, EventKind.Religious)],
        [(8, 15)] = [new("ولادت حضرت قائم", true, EventKind.Religious)],
        [(9, 21)] = [new("شهادت امام علی", true, EventKind.Religious)],
        [(10, 1)] = [new("عید سعید فطر", true, EventKind.Religious)],
        [(10, 2)] = [new("تعطیل به مناسبت عید فطر", true, EventKind.Religious)],
        [(10, 25)] = [new("شهادت امام جعفر صادق", true, EventKind.Religious)],
        [(12, 10)] = [new("عید سعید قربان", true, EventKind.Religious)],
        [(12, 18)] = [new("عید سعید غدیر خم", true, EventKind.Religious)],
    }.ToFrozenDictionary();

    private static readonly CalendarEvent ImamReza = new("شهادت امام رضا", true, EventKind.Religious);
    private static readonly CalendarEvent ChaharshanbeSuri = new("چهارشنبه‌سوری", false, EventKind.Cultural);

    public IReadOnlyList<CalendarEvent> GetEvents(JalaliDate date)
    {
        List<CalendarEvent> events = [];

        if (Solar.TryGetValue((date.Month, date.Day), out var solar))
            events.AddRange(solar);

        var hijri = HijriDate.FromDateOnly(date.ToDateOnly(), HijriAdjustment);
        if (Lunar.TryGetValue((hijri.Month, hijri.Day), out var lunar))
            events.AddRange(lunar);

        // Imam Reza's martyrdom is observed on the last day of Safar (29th or 30th).
        if (hijri is { Month: 2 } && hijri.Day == hijri.DaysInMonth)
            events.Add(ImamReza);

        // Chaharshanbe Suri: the eve (Tuesday) of the last Wednesday of the year.
        if (date is { Month: 12 } && date.DayOfWeek == DayOfWeek.Tuesday && date.AddDays(8).Year > date.Year)
            events.Add(ChaharshanbeSuri);

        return events;
    }

    public bool IsHoliday(JalaliDate date) => date.IsFriday || GetEvents(date).Any(static e => e.IsHoliday);

    /// <summary>Upcoming occasions after <paramref name="from"/> (exclusive), nearest first.</summary>
    public IEnumerable<(JalaliDate Date, CalendarEvent Event)> Upcoming(JalaliDate from, int horizonDays = 400)
    {
        for (var i = 1; i <= horizonDays; i++)
        {
            var day = from.AddDays(i);
            foreach (var e in GetEvents(day))
                yield return (day, e);
        }
    }
}
