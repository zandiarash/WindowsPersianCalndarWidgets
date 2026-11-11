using System.Globalization;

namespace PersianCalendar.Core;

/// <summary>Persian names and human friendly phrases.</summary>
public static class PersianText
{
    public static IReadOnlyList<string> Months { get; } =
        ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];

    public static IReadOnlyList<string> WeekDays { get; } =
        ["شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه"];

    public static IReadOnlyList<string> WeekDaysShort { get; } = ["ش", "ی", "د", "س", "چ", "پ", "ج"];

    public static IReadOnlyList<string> Seasons { get; } = ["بهار", "تابستان", "پاییز", "زمستان"];

    public static IReadOnlyList<string> HijriMonths { get; } =
    [
        "محرم", "صفر", "ربیع‌الاول", "ربیع‌الثانی", "جمادی‌الاول", "جمادی‌الثانی",
        "رجب", "شعبان", "رمضان", "شوال", "ذی‌القعده", "ذی‌الحجه",
    ];

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    /// <summary>e.g. «Wednesday, 11 November 2026».</summary>
    public static string Gregorian(DateOnly date) => date.ToString("dddd, d MMMM yyyy", English);

    /// <summary>e.g. «Wed, 11 Nov 2026».</summary>
    public static string GregorianShort(DateOnly date) => date.ToString("ddd, d MMM yyyy", English);

    /// <summary>A friendly greeting for the time of day.</summary>
    public static string Greeting(int hour) => hour switch
    {
        >= 5 and < 12 => "صبح بخیر",
        >= 12 and < 15 => "ظهر بخیر",
        >= 15 and < 19 => "عصر بخیر",
        _ => "شب بخیر",
    };

    /// <summary>«امروز»، «فردا»، «۳ روز دیگر»، «۲ روز پیش» …</summary>
    public static string Relative(int days) => days switch
    {
        0 => "امروز",
        1 => "فردا",
        -1 => "دیروز",
        2 => "پس‌فردا",
        -2 => "پریروز",
        > 0 => $"{days.ToPersianDigits()} روز دیگر",
        _ => $"{(-days).ToPersianDigits()} روز پیش",
    };
}
