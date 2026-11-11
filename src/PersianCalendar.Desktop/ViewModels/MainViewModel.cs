using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using PersianCalendar.Core;

namespace PersianCalendar.Desktop.ViewModels;

public sealed record EventItem(string Title, bool IsHoliday);

/// <summary>State for the whole widget: live clock, today's hero card and the month grid.</summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly TimeProvider _time;
    private readonly DispatcherTimer _timer;
    private EventCatalog _catalog;
    private JalaliDate _displayedMonth;

    public MainViewModel(TimeProvider time, int hijriAdjustment)
    {
        _time = time;
        _catalog = new EventCatalog(hijriAdjustment);
        Today = JalaliDate.Today(time);
        _displayedMonth = Today.FirstDayOfMonth;

        PreviousMonthCommand = new RelayCommand(() => ShowMonth(_displayedMonth.AddMonths(-1), -1));
        NextMonthCommand = new RelayCommand(() => ShowMonth(_displayedMonth.AddMonths(1), +1));
        PreviousYearCommand = new RelayCommand(() => ShowMonth(_displayedMonth.AddYears(-1), -1));
        NextYearCommand = new RelayCommand(() => ShowMonth(_displayedMonth.AddYears(1), +1));
        GoToTodayCommand = new RelayCommand(GoToToday);
        SelectDayCommand = new RelayCommand(p => { if (p is DayCellViewModel cell) Select(cell); });

        RefreshToday();
        BuildMonth();
        Tick();

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    /// <summary>Raised after the displayed month changes; the argument is the direction (-1 / +1 / 0).</summary>
    public event Action<int>? MonthChanged;

    public ICommand PreviousMonthCommand { get; }
    public ICommand NextMonthCommand { get; }
    public ICommand PreviousYearCommand { get; }
    public ICommand NextYearCommand { get; }
    public ICommand GoToTodayCommand { get; }
    public ICommand SelectDayCommand { get; }

    // ---------------- Clock ----------------
    public string HourMinute { get; private set => SetProperty(ref field, value); } = "";
    public string Seconds { get; private set => SetProperty(ref field, value); } = "";
    public string Greeting { get; private set => SetProperty(ref field, value); } = "";
    public bool ShowSeconds { get; set => SetProperty(ref field, value); } = true;
    public bool ShowGregorianNumbers { get; set => SetProperty(ref field, value); } = true;

    // ---------------- Today (hero) ----------------
    public JalaliDate Today { get; private set; }
    public string TodayWeekDay { get; private set => SetProperty(ref field, value); } = "";
    public string TodayDay { get; private set => SetProperty(ref field, value); } = "";
    public string TodayMonth { get; private set => SetProperty(ref field, value); } = "";
    public string TodayYear { get; private set => SetProperty(ref field, value); } = "";
    public string TodayGregorian { get; private set => SetProperty(ref field, value); } = "";
    public string TodayHijri { get; private set => SetProperty(ref field, value); } = "";
    public double YearProgress { get; private set => SetProperty(ref field, value); }
    public string YearProgressText { get; private set => SetProperty(ref field, value); } = "";
    public Season TodaySeason { get; private set => SetProperty(ref field, value); }

    // ---------------- Displayed month ----------------
    public string MonthName { get; private set => SetProperty(ref field, value); } = "";
    public string MonthYear { get; private set => SetProperty(ref field, value); } = "";
    public string MonthSubtitle { get; private set => SetProperty(ref field, value); } = "";
    public string SeasonName { get; private set => SetProperty(ref field, value); } = "";
    public Season DisplayedSeason { get; private set => SetProperty(ref field, value); }
    public bool IsAwayFromToday { get; private set => SetProperty(ref field, value); }
    public IReadOnlyList<DayCellViewModel> Days { get; private set => SetProperty(ref field, value); } = [];
    public IReadOnlyList<string> WeekDayHeaders { get; } = PersianText.WeekDaysShort;

    // ---------------- Selected day (footer) ----------------
    public DayCellViewModel? SelectedDay { get; private set => SetProperty(ref field, value); }
    public string SelectedTitle { get; private set => SetProperty(ref field, value); } = "";
    public string SelectedRelative { get; private set => SetProperty(ref field, value); } = "";
    public string SelectedGregorian { get; private set => SetProperty(ref field, value); } = "";
    public string SelectedHijri { get; private set => SetProperty(ref field, value); } = "";
    public IReadOnlyList<EventItem> SelectedEvents { get; private set => SetProperty(ref field, value); } = [];
    public string NextEventTitle { get; private set => SetProperty(ref field, value); } = "";
    public string NextEventWhen { get; private set => SetProperty(ref field, value); } = "";

    public void SetHijriAdjustment(int adjustment)
    {
        _catalog = new EventCatalog(adjustment);
        RefreshToday();
        BuildMonth();
    }

    public void ShowMonth(JalaliDate anyDayInMonth, int direction)
    {
        _displayedMonth = anyDayInMonth.FirstDayOfMonth;
        BuildMonth();
        MonthChanged?.Invoke(direction);
    }

    private void GoToToday()
    {
        var direction = Today.FirstDayOfMonth.CompareTo(_displayedMonth);
        _displayedMonth = Today.FirstDayOfMonth;
        BuildMonth();
        MonthChanged?.Invoke(direction);
    }

    private void Tick()
    {
        var now = _time.GetLocalNow();
        HourMinute = now.ToString("HH:mm", CultureInfo.InvariantCulture).ToPersianDigits();
        Seconds = now.Second.ToPersianDigits("00");
        Greeting = PersianText.Greeting(now.Hour);

        // Midnight rollover: refresh everything that depends on "today".
        if (JalaliDate.FromDateTime(now.DateTime) != Today)
        {
            var wasOnToday = _displayedMonth == Today.FirstDayOfMonth;
            Today = JalaliDate.FromDateTime(now.DateTime);
            if (wasOnToday) _displayedMonth = Today.FirstDayOfMonth;
            RefreshToday();
            BuildMonth();
        }

        // Re-align the timer to the start of the next second so the clock never drifts.
        if (_timer is not null)
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, 1000 - now.Millisecond));
    }

    private void RefreshToday()
    {
        var t = Today;
        TodayWeekDay = t.DayOfWeek.PersianName;
        TodayDay = t.Day.ToPersianDigits();
        TodayMonth = t.MonthName;
        TodayYear = t.Year.ToPersianDigits();
        TodaySeason = t.Season;
        TodayGregorian = PersianText.GregorianShort(t.ToDateOnly());
        TodayHijri = HijriDate.FromDateOnly(t.ToDateOnly(), _catalog.HijriAdjustment).ToString();

        YearProgress = (double)t.DayOfYear / t.DaysInYear;
        var toNowruz = t.DaysUntil(new JalaliDate(t.Year + 1, 1, 1));
        YearProgressText = $"روز {t.DayOfYear.ToPersianDigits()} از {t.DaysInYear.ToPersianDigits()}  ·  {toNowruz.ToPersianDigits()} روز تا نوروز";

        var next = _catalog.Upcoming(t).FirstOrDefault();
        (NextEventTitle, NextEventWhen) = next.Event is null
            ? ("", "")
            : (next.Event.Title, $"{PersianText.Relative(t.DaysUntil(next.Date))}  ·  {next.Date.ToShortPersianString()}");
    }

    private void BuildMonth()
    {
        var m = _displayedMonth;
        var previouslySelected = SelectedDay?.Date;

        Days = [.. MonthLayout.Build(m.Year, m.Month, Today, _catalog).Select(static d => new DayCellViewModel(d))];

        MonthName = m.MonthName;
        MonthYear = m.Year.ToPersianDigits();
        DisplayedSeason = m.Season;
        SeasonName = m.SeasonName;
        IsAwayFromToday = m != Today.FirstDayOfMonth;

        var firstG = m.ToDateOnly();
        var lastG = m.AddDays(m.DaysInMonth - 1).ToDateOnly();
        var en = CultureInfo.GetCultureInfo("en-US");
        MonthSubtitle = firstG.Month == lastG.Month
            ? firstG.ToString("MMMM yyyy", en)
            : $"{firstG.ToString(firstG.Year == lastG.Year ? "MMM" : "MMM yyyy", en)} – {lastG.ToString("MMM yyyy", en)}";

        var toSelect = Days.FirstOrDefault(d => d.IsInMonth && d.Date == previouslySelected)
                       ?? Days.FirstOrDefault(static d => d.IsToday && d.IsInMonth)
                       ?? Days.First(static d => d.IsInMonth);
        Select(toSelect);
    }

    private void Select(DayCellViewModel cell)
    {
        if (!cell.IsInMonth)
        {
            // Clicking a faded day from a neighbouring month navigates there.
            var direction = cell.Date < _displayedMonth ? -1 : 1;
            _displayedMonth = cell.Date.FirstDayOfMonth;
            SelectedDay = null;
            BuildMonth();
            Select(Days.First(d => d.IsInMonth && d.Date == cell.Date));
            MonthChanged?.Invoke(direction);
            return;
        }

        SelectedDay?.IsSelected = false; // C# 14 null-conditional assignment
        cell.IsSelected = true;
        SelectedDay = cell;

        var d = cell.Date;
        SelectedTitle = d.ToLongPersianString();
        SelectedRelative = PersianText.Relative(Today.DaysUntil(d));
        SelectedGregorian = PersianText.Gregorian(d.ToDateOnly());
        SelectedHijri = HijriDate.FromDateOnly(d.ToDateOnly(), _catalog.HijriAdjustment).ToString();
        SelectedEvents = [.. cell.Info.Events.Select(static e => new EventItem(e.Title, e.IsHoliday))];
    }

    public void Dispose() => _timer.Stop();
}
