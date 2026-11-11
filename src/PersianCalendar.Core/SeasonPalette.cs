namespace PersianCalendar.Core;

/// <summary>Seasonal gradient colours shared by the desktop widget and the Widgets Board cards.</summary>
public static class SeasonPalette
{
    /// <summary>Gradient start / end as #RRGGBB.</summary>
    public static (string From, string To) Hex(Season season) => season switch
    {
        Season.Spring => ("#10B981", "#068FC7"), // بهار: sprout green → sky
        Season.Summer => ("#F59E0B", "#E11D48"), // تابستان: sun → pomegranate
        Season.Autumn => ("#F97316", "#BE185D"), // پاییز: ember → maroon
        _ => ("#6366F1", "#0E74C9"),             // زمستان: ice violet → deep blue
    };
}
