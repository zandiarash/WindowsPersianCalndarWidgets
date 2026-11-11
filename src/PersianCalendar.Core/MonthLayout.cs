namespace PersianCalendar.Core;

/// <summary>A single cell in a month view.</summary>
public sealed record DayInfo(JalaliDate Date, bool IsInMonth, bool IsToday, bool IsHoliday, IReadOnlyList<CalendarEvent> Events)
{
    public DateOnly Gregorian => Date.ToDateOnly();
    public bool HasEvents => Events.Count > 0;
}

/// <summary>Builds the Saturday-first grid for a Persian month.</summary>
public static class MonthLayout
{
    /// <param name="fixedSixWeeks">Always return 42 cells (stable height) instead of only the weeks needed.</param>
    public static IReadOnlyList<DayInfo> Build(int year, int month, JalaliDate today, EventCatalog? catalog = null, bool fixedSixWeeks = true)
    {
        catalog ??= EventCatalog.Default;
        var first = new JalaliDate(year, month, 1);
        var start = first.AddDays(-first.WeekDayIndex);
        var weeks = fixedSixWeeks ? 6 : (first.WeekDayIndex + first.DaysInMonth + 6) / 7;

        var cells = new DayInfo[weeks * 7];
        for (var i = 0; i < cells.Length; i++)
        {
            var date = start.AddDays(i);
            var events = catalog.GetEvents(date);
            cells[i] = new DayInfo(
                date,
                IsInMonth: date.Month == month,
                IsToday: date == today,
                IsHoliday: date.IsFriday || events.Any(static e => e.IsHoliday),
                events);
        }
        return cells;
    }
}
