using PersianCalendar.Core;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace PersianCalendar.Core.Tests;

[TestClass]
public sealed class CalendarTests
{
    [TestMethod]
    [DataRow(2025, 3, 21, 1404, 1, 1)]
    [DataRow(2026, 11, 11, 1405, 8, 20)]
    [DataRow(2025, 2, 6, 1403, 11, 18)]
    public void Converts_gregorian_to_jalali(int gy, int gm, int gd, int y, int m, int d)
    {
        var date = JalaliDate.FromDateOnly(new DateOnly(gy, gm, gd));
        Assert.AreEqual(new JalaliDate(y, m, d), date);
        Assert.AreEqual(new DateOnly(gy, gm, gd), date.ToDateOnly());
    }

    [TestMethod]
    public void Week_starts_on_saturday()
    {
        Assert.AreEqual(0, DayOfWeek.Saturday.PersianIndex);
        Assert.AreEqual(6, DayOfWeek.Friday.PersianIndex);
        Assert.AreEqual("چهارشنبه", new JalaliDate(1405, 8, 20).DayOfWeek.PersianName);
    }

    [TestMethod]
    public void Formats_persian_digits()
    {
        Assert.AreEqual("۱۴۰۵", 1405.ToPersianDigits());
        Assert.AreEqual("۰۹:۰۵", "09:05".ToPersianDigits());
        Assert.AreEqual("چهارشنبه ۲۰ آبان ۱۴۰۵", new JalaliDate(1405, 8, 20).ToLongPersianString());
    }

    [TestMethod]
    public void Adds_months_and_clamps_day()
    {
        Assert.AreEqual(new JalaliDate(1405, 7, 30), new JalaliDate(1405, 6, 31).AddMonths(1));
        Assert.AreEqual(new JalaliDate(1406, 1, 15), new JalaliDate(1405, 12, 15).AddMonths(1));
        Assert.AreEqual(new JalaliDate(1404, 12, 15), new JalaliDate(1405, 1, 15).AddMonths(-1));
    }

    [TestMethod]
    public void Day_of_year_matches_framework()
    {
        var calendar = new System.Globalization.PersianCalendar();
        for (var d = new JalaliDate(1405, 1, 1); d.Year == 1405; d = d.AddDays(1))
            Assert.AreEqual(calendar.GetDayOfYear(d.ToDateOnly().ToDateTime(TimeOnly.MinValue)), d.DayOfYear);
    }

    [TestMethod]
    public void Month_grid_has_42_cells_starting_on_saturday()
    {
        var cells = MonthLayout.Build(1405, 8, new JalaliDate(1405, 8, 20));
        Assert.HasCount(42, cells);
        Assert.AreEqual(DayOfWeek.Saturday, cells[0].Date.DayOfWeek);
        Assert.AreEqual(new JalaliDate(1405, 8, 20), cells.Single(c => c.IsToday).Date);
        Assert.AreEqual(30, cells.Count(c => c.IsInMonth));
        Assert.IsTrue(cells.Where(c => c.Date.IsFriday).All(c => c.IsHoliday));
    }

    [TestMethod]
    public void Solar_holidays_are_detected()
    {
        Assert.IsTrue(EventCatalog.Default.IsHoliday(new JalaliDate(1405, 1, 1)));
        Assert.IsTrue(EventCatalog.Default.IsHoliday(new JalaliDate(1405, 11, 22)));
        Assert.IsFalse(EventCatalog.Default.GetEvents(new JalaliDate(1405, 9, 30)).Single().IsHoliday); // Yalda
    }

    [TestMethod]
    public void Lunar_holidays_match_iran_official_calendar()
    {
        // Ashura 1447 → 15 Tir 1404, Eid al-Fitr 1446 → 11 Farvardin 1404 (official Iranian calendar).
        Assert.IsTrue(EventCatalog.Default.GetEvents(new JalaliDate(1404, 4, 15)).Any(e => e.Title.Contains("عاشورا")));
        Assert.IsTrue(EventCatalog.Default.GetEvents(new JalaliDate(1404, 1, 11)).Any(e => e.Title.Contains("فطر")));
    }

    [TestMethod]
    public void Chaharshanbe_suri_is_on_the_last_tuesday()
    {
        var day = new JalaliDate(1404, 12, 26); // Tue 17 March 2026
        Assert.AreEqual(DayOfWeek.Tuesday, day.DayOfWeek);
        Assert.IsTrue(EventCatalog.Default.GetEvents(day).Any(e => e.Title == "چهارشنبه‌سوری"));
    }
}
