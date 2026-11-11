using System.Globalization;

namespace PersianCalendar.Core;

/// <summary>The four seasons of the solar Hijri year (each is exactly three months).</summary>
public enum Season { Spring, Summer, Autumn, Winter }

/// <summary>
/// An immutable date in the Solar Hijri (Jalali / Persian) calendar.
/// Backed by <see cref="DateOnly"/> so arithmetic is exact and allocation free.
/// </summary>
public readonly record struct JalaliDate : IComparable<JalaliDate>
{
    private static readonly System.Globalization.PersianCalendar Calendar = new();

    public int Year { get; }
    public int Month { get; }
    public int Day { get; }

    public JalaliDate(int year, int month, int day)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9378);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);
        ArgumentOutOfRangeException.ThrowIfLessThan(day, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(day, Calendar.GetDaysInMonth(year, month));

        (Year, Month, Day) = (year, month, day);
    }

    public static JalaliDate FromDateTime(DateTime value) =>
        new(Calendar.GetYear(value), Calendar.GetMonth(value), Calendar.GetDayOfMonth(value));

    public static JalaliDate FromDateOnly(DateOnly value) => FromDateTime(value.ToDateTime(TimeOnly.MinValue));

    /// <summary>Today in local time, according to the supplied (or system) <see cref="TimeProvider"/>.</summary>
    public static JalaliDate Today(TimeProvider? time = null) =>
        FromDateTime((time ?? TimeProvider.System).GetLocalNow().DateTime);

    public DateOnly ToDateOnly() => DateOnly.FromDateTime(Calendar.ToDateTime(Year, Month, Day, 0, 0, 0, 0));

    public DayOfWeek DayOfWeek => ToDateOnly().DayOfWeek;

    /// <summary>Index of the day inside a Persian week: 0 = Saturday (شنبه) … 6 = Friday (جمعه).</summary>
    public int WeekDayIndex => DayOfWeek.PersianIndex;

    public bool IsFriday => DayOfWeek == DayOfWeek.Friday;

    public int DaysInMonth => Calendar.GetDaysInMonth(Year, Month);

    public int DaysInYear => Calendar.GetDaysInYear(Year);

    public bool IsLeapYear => Calendar.IsLeapYear(Year);

    public int DayOfYear => Month <= 6 ? (Month - 1) * 31 + Day : 186 + (Month - 7) * 30 + Day;

    public Season Season => (Season)((Month - 1) / 3);

    public JalaliDate FirstDayOfMonth => new(Year, Month, 1);

    public JalaliDate AddDays(int days) => FromDateOnly(ToDateOnly().AddDays(days));

    public JalaliDate AddMonths(int months)
    {
        var total = Year * 12 + (Month - 1) + months;
        var (year, month) = (total / 12, total % 12 + 1);
        return new(year, month, Math.Min(Day, Calendar.GetDaysInMonth(year, month)));
    }

    public JalaliDate AddYears(int years) => AddMonths(years * 12);

    /// <summary>Signed number of days from this date to <paramref name="other"/>.</summary>
    public int DaysUntil(JalaliDate other) => other.ToDateOnly().DayNumber - ToDateOnly().DayNumber;

    public int CompareTo(JalaliDate other) => (Year, Month, Day).CompareTo((other.Year, other.Month, other.Day));

    public static bool operator <(JalaliDate left, JalaliDate right) => left.CompareTo(right) < 0;
    public static bool operator >(JalaliDate left, JalaliDate right) => left.CompareTo(right) > 0;
    public static bool operator <=(JalaliDate left, JalaliDate right) => left.CompareTo(right) <= 0;
    public static bool operator >=(JalaliDate left, JalaliDate right) => left.CompareTo(right) >= 0;

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Year:0000}/{Month:00}/{Day:00}");
}
