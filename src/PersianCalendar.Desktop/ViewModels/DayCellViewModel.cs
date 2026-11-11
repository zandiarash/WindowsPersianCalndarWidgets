using PersianCalendar.Core;

namespace PersianCalendar.Desktop.ViewModels;

public sealed class DayCellViewModel(DayInfo info) : ObservableObject
{
    public DayInfo Info { get; } = info;
    public JalaliDate Date => Info.Date;

    public string Number { get; } = info.Date.Day.ToPersianDigits();

    /// <summary>The Gregorian day number, shown small in the corner of each cell.</summary>
    public string GregorianNumber { get; } = info.Gregorian.Day.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public bool IsInMonth => Info.IsInMonth;
    public bool IsToday => Info.IsToday;
    public bool IsHoliday => Info.IsHoliday;
    public bool HasEvents => Info.HasEvents;

    public string ToolTip { get; } = string.Join(Environment.NewLine,
        [info.Date.ToLongPersianString(), PersianText.Gregorian(info.Gregorian), .. info.Events.Select(static e => "• " + e.Title)]);

    public bool IsSelected { get; set => SetProperty(ref field, value); }
}
