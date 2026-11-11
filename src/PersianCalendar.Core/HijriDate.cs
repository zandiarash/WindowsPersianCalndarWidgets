using System.Globalization;

namespace PersianCalendar.Core;

/// <summary>
/// A lunar Hijri date. Computed with the Umm al-Qura tables and shifted by an
/// adjustment (in days) because Iran's official lunar months usually start one day later.
/// </summary>
public readonly record struct HijriDate(int Year, int Month, int Day, int DaysInMonth)
{
    /// <summary>Default shift that matches Iran's official calendar most of the time.</summary>
    public const int IranAdjustment = -1;

    private static readonly UmAlQuraCalendar UmAlQura = new();
    private static readonly HijriCalendar Fallback = new();

    public static HijriDate FromDateOnly(DateOnly date, int adjustment = IranAdjustment)
    {
        var dt = date.AddDays(adjustment).ToDateTime(TimeOnly.MinValue);
        Calendar calendar = dt >= UmAlQura.MinSupportedDateTime && dt <= UmAlQura.MaxSupportedDateTime ? UmAlQura : Fallback;
        var (y, m) = (calendar.GetYear(dt), calendar.GetMonth(dt));
        return new(y, m, calendar.GetDayOfMonth(dt), calendar.GetDaysInMonth(y, m));
    }

    public string MonthName => PersianText.HijriMonths[Month - 1];

    /// <summary>e.g. «۲۰ جمادی‌الاول ۱۴۴۸».</summary>
    public override string ToString() => $"{Day.ToPersianDigits()} {MonthName} {Year.ToPersianDigits()}";
}
