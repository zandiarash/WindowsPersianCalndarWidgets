using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;

namespace PersianCalendar.WidgetProvider;

/// <summary>Per-widget state. <see cref="MonthOffset"/> is persisted by the host as the widget's custom state.</summary>
internal sealed class WidgetState(string id, WidgetSize size)
{
    public string Id { get; } = id;
    public WidgetSize Size { get; set; } = size;
    public bool IsActive { get; set; }
    public int MonthOffset { get; set; }

    public static int ParseOffset(string? customState) =>
        int.TryParse(customState, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
}

/// <summary>
/// The Windows Widgets Board provider. The host calls these members from RPC threads,
/// so all state is guarded by a C# 13 <see cref="Lock"/>.
/// </summary>
[Guid(ClassIdString)]
public sealed partial class WidgetProvider : IWidgetProvider
{
    public const string ClassIdString = "2F6C8ED1-5E7A-4EF8-8DC4-763D4888664D";
    public static readonly Guid ClassId = new(ClassIdString);

    private static readonly Lazy<WidgetProvider> LazyInstance = new(static () => new WidgetProvider());
    public static WidgetProvider Instance => LazyInstance.Value;
    public static ManualResetEvent EmptyWidgetListEvent { get; } = new(false);

    private readonly Lock _gate = new();
    private readonly Dictionary<string, WidgetState> _widgets = [];
    private readonly ITimer _clock;

    private WidgetProvider()
    {
        // Recover widgets that were pinned before this process (re)started.
        foreach (var info in WidgetManager.GetDefault().GetWidgetInfos() ?? [])
        {
            var context = info.WidgetContext;
            _widgets[context.Id] = new WidgetState(context.Id, context.Size)
            {
                IsActive = context.IsActive,
                MonthOffset = WidgetState.ParseOffset(info.CustomState),
            };
        }

        // Refresh active widgets exactly on each minute boundary (clock + midnight rollover).
        _clock = TimeProvider.System.CreateTimer(_ => OnMinute(), null, DelayToNextMinute(), Timeout.InfiniteTimeSpan);
    }

    // ===================== IWidgetProvider =====================

    public void CreateWidget(WidgetContext widgetContext)
    {
        var state = new WidgetState(widgetContext.Id, widgetContext.Size) { IsActive = true };
        lock (_gate) _widgets[state.Id] = state;
        Push(state);
    }

    public void DeleteWidget(string widgetId, string customState)
    {
        lock (_gate)
        {
            _widgets.Remove(widgetId);
            if (_widgets.Count == 0) EmptyWidgetListEvent.Set();
        }
    }

    public void OnActionInvoked(WidgetActionInvokedArgs actionInvokedArgs)
    {
        if (Find(actionInvokedArgs.WidgetContext.Id) is not { } state) return;

        state.MonthOffset = actionInvokedArgs.Verb switch
        {
            "prev" => state.MonthOffset - 1,
            "next" => state.MonthOffset + 1,
            "today" => 0,
            _ => state.MonthOffset,
        };
        Push(state);
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs contextChangedArgs)
    {
        if (Find(contextChangedArgs.WidgetContext.Id) is not { } state) return;
        state.Size = contextChangedArgs.WidgetContext.Size;
        Push(state);
    }

    public void Activate(WidgetContext widgetContext)
    {
        var state = Find(widgetContext.Id) ?? new WidgetState(widgetContext.Id, widgetContext.Size);
        lock (_gate) _widgets[state.Id] = state;
        state.IsActive = true;
        state.Size = widgetContext.Size;
        Push(state);
    }

    public void Deactivate(string widgetId)
    {
        Find(widgetId)?.IsActive = false; // C# 14 null-conditional assignment
    }

    // ===================== Rendering =====================

    private WidgetState? Find(string id)
    {
        lock (_gate) return _widgets.GetValueOrDefault(id);
    }

    private void OnMinute()
    {
        WidgetState[] active;
        lock (_gate) active = [.. _widgets.Values.Where(static w => w.IsActive)];
        foreach (var widget in active) Push(widget);

        _clock.Change(DelayToNextMinute(), Timeout.InfiniteTimeSpan);
    }

    private static void Push(WidgetState state)
    {
        try
        {
            var options = new WidgetUpdateRequestOptions(state.Id)
            {
                Template = CardTemplates.For(state.Size),
                Data = CardData.Build(state.Size, state.MonthOffset, TimeProvider.System),
                CustomState = state.MonthOffset.ToString(CultureInfo.InvariantCulture),
            };
            WidgetManager.GetDefault().UpdateWidget(options);
        }
        catch (COMException)
        {
            // The widget may have been removed between our check and the update.
        }
    }

    private static TimeSpan DelayToNextMinute()
    {
        var now = TimeProvider.System.GetLocalNow();
        return TimeSpan.FromSeconds(60 - now.Second) - TimeSpan.FromMilliseconds(now.Millisecond) + TimeSpan.FromMilliseconds(200);
    }
}
