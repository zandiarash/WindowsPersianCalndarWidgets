using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace PersianCalendar.WidgetProvider.Com;

/// <summary>Classic COM IClassFactory, declared with the source-generated COM interop (no built-in COM required).</summary>
[GeneratedComInterface]
[Guid("00000001-0000-0000-C000-000000000046")]
internal partial interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(nint pUnkOuter, in Guid riid, out nint ppvObject);

    [PreserveSig]
    int LockServer([MarshalAs(UnmanagedType.Bool)] bool fLock);
}

/// <summary>Hands the Widgets host our (singleton) <see cref="WidgetProvider"/> through its WinRT ABI.</summary>
[GeneratedComClass]
internal sealed partial class WidgetProviderFactory : IClassFactory
{
    private const int CLASS_E_NOAGGREGATION = unchecked((int)0x80040110);

    public int CreateInstance(nint pUnkOuter, in Guid riid, out nint ppvObject)
    {
        ppvObject = 0;
        if (pUnkOuter != 0) return CLASS_E_NOAGGREGATION;

        var provider = MarshalInspectable<IWidgetProvider>.FromManaged(WidgetProvider.Instance);
        try
        {
            return Marshal.QueryInterface(provider, riid, out ppvObject);
        }
        finally
        {
            Marshal.Release(provider);
        }
    }

    public int LockServer(bool fLock) => 0;
}

internal static partial class Ole32
{
    public const uint CLSCTX_LOCAL_SERVER = 0x4;
    public const uint REGCLS_MULTIPLEUSE = 0x1;

    [LibraryImport("ole32.dll")]
    public static partial int CoRegisterClassObject(in Guid rclsid, nint pUnk, uint dwClsContext, uint flags, out uint lpdwRegister);

    [LibraryImport("ole32.dll")]
    public static partial int CoRevokeClassObject(uint dwRegister);
}
