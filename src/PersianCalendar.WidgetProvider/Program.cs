using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using PersianCalendar.WidgetProvider;
using Microsoft.Windows.Widgets;
using PersianCalendar.WidgetProvider.Com;

// `--preview <folder>` writes template + data JSON per size (paste into https://adaptivecards.io/designer).
if (args is ["--preview", var folder])
{
    Directory.CreateDirectory(folder);
    foreach (var size in (ReadOnlySpan<WidgetSize>)[WidgetSize.Small, WidgetSize.Medium, WidgetSize.Large])
    {
        var name = size.ToString().ToLowerInvariant();
        File.WriteAllText(Path.Combine(folder, $"{name}.template.json"), CardTemplates.For(size));
        File.WriteAllText(Path.Combine(folder, $"{name}.data.json"), CardData.Build(size, 0, TimeProvider.System));
    }
    return;
}

// The Widgets host launches this exe with -RegisterProcessAsComServer when a widget needs us.
WinRT.ComWrappersSupport.InitializeComWrappers();

var comWrappers = new StrategyBasedComWrappers();
var factory = comWrappers.GetOrCreateComInterfaceForObject(new WidgetProviderFactory(), CreateComInterfaceFlags.None);

Marshal.ThrowExceptionForHR(Ole32.CoRegisterClassObject(
    WidgetProvider.ClassId, factory, Ole32.CLSCTX_LOCAL_SERVER, Ole32.REGCLS_MULTIPLEUSE, out var cookie));

// Stay alive while at least one widget is pinned; the host restarts us on demand.
WidgetProvider.EmptyWidgetListEvent.WaitOne();

_ = Ole32.CoRevokeClassObject(cookie);
Marshal.Release(factory);
