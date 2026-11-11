using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using PersianCalendar.Core;

namespace PersianCalendar.Desktop.Services;

/// <summary>
/// Swaps the light / dark palette (following Windows when set to System) and
/// paints the seasonal accent colours: spring green, summer sun, autumn ember, winter ice.
/// </summary>
public sealed class ThemeService
{
    private static readonly (Color From, Color To)[] SeasonColors =
    [.. Enum.GetValues<Season>().Select(static s =>
    {
        var (from, to) = SeasonPalette.Hex(s);
        return ((Color)ColorConverter.ConvertFromString(from), (Color)ColorConverter.ConvertFromString(to));
    })];

    private AppTheme _theme;

    public event Action? ThemeChanged;

    public bool IsDark { get; private set; }

    public ThemeService() => SystemEvents.UserPreferenceChanged += (_, e) =>
    {
        if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color && _theme == AppTheme.System)
            Application.Current.Dispatcher.BeginInvoke(() => Apply(_theme));
    };

    public void Apply(AppTheme theme)
    {
        _theme = theme;
        IsDark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDark(),
        };

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var palette = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(IsDark ? "Dark" : "Light")}.xaml", UriKind.Absolute),
        };
        dictionaries[0] = palette;
        ThemeChanged?.Invoke();
    }

    /// <summary>Hero gradient follows today's season.</summary>
    public static void ApplyHeroSeason(Season season)
    {
        var (from, to) = SeasonColors[(int)season];
        Application.Current.Resources["HeroBrush"] = Gradient(from, to);
    }

    /// <summary>Accent colours follow the season of the month on screen.</summary>
    public static void ApplyAccentSeason(Season season)
    {
        var (from, to) = SeasonColors[(int)season];
        var res = Application.Current.Resources;
        res["AccentBrush"] = Freeze(new SolidColorBrush(from));
        res["AccentGradientBrush"] = Gradient(from, to);
        res["AccentSoftBrush"] = Freeze(new SolidColorBrush(Color.FromArgb(0x2E, from.R, from.G, from.B)));
        res["AccentGlowColor"] = from;
    }

    public static Color SeasonColor(Season season) => SeasonColors[(int)season].From;

    private static LinearGradientBrush Gradient(Color from, Color to) =>
        Freeze(new LinearGradientBrush(from, to, new Point(0, 0), new Point(1, 1)));

    private static T Freeze<T>(T brush) where T : Freezable
    {
        brush.Freeze();
        return brush;
    }

    private static bool IsSystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int and 0;
    }
}
