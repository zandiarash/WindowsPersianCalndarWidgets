using System.Globalization;

namespace PersianCalendar.Core;

/// <summary>C# 14 extension members used across the solution.</summary>
public static class PersianExtensions
{
    extension(DayOfWeek day)
    {
        /// <summary>0 = Saturday … 6 = Friday (Persian week order).</summary>
        public int PersianIndex => ((int)day + 1) % 7;

        public string PersianName => PersianText.WeekDays[day.PersianIndex];
    }

    extension(int value)
    {
        /// <summary>Formats the number with Persian (Eastern Arabic) digits: 1405 → ۱۴۰۵.</summary>
        public string ToPersianDigits(string format = "0") =>
            value.ToString(format, CultureInfo.InvariantCulture).ToPersianDigits();
    }

    extension(string text)
    {
        public string ToPersianDigits() => string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                var c = source[i];
                span[i] = c is >= '0' and <= '9' ? (char)('۰' + (c - '0')) : c;
            }
        });
    }

    extension(JalaliDate date)
    {
        public string MonthName => PersianText.Months[date.Month - 1];

        public string SeasonName => PersianText.Seasons[(int)date.Season];

        /// <summary>e.g. «چهارشنبه ۲۰ آبان ۱۴۰۵».</summary>
        public string ToLongPersianString() =>
            $"{date.DayOfWeek.PersianName} {date.Day.ToPersianDigits()} {date.MonthName} {date.Year.ToPersianDigits()}";

        /// <summary>e.g. «۲۰ آبان».</summary>
        public string ToShortPersianString() => $"{date.Day.ToPersianDigits()} {date.MonthName}";
    }
}
