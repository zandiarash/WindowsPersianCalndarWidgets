using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using PersianCalendar.WidgetProvider;
using PersianCalendar.WidgetProvider.Com;

// `--preview <folder>` renders every card part, a Widgets Board mock-up per size/theme, and the data JSON.
if (args is ["--preview", var folder])
{
    CardData.WritePreview(folder, TimeProvider.System.GetLocalNow());
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
