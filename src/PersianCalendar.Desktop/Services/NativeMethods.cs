using System.Runtime.InteropServices;

namespace PersianCalendar.Desktop.Services;

/// <summary>Win32 / DWM interop using source-generated P/Invoke (<see cref="LibraryImportAttribute"/>).</summary>
internal static partial class NativeMethods
{
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const int WM_NCACTIVATE = 0x0086;
    public const int WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
    public const int WM_SETTINGCHANGE = 0x001A;

    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_APPWINDOW = 0x00040000;

    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOACTIVATE = 0x0010;
    public static readonly nint HWND_BOTTOM = 1;

    public enum DwmWindowAttribute : uint
    {
        UseImmersiveDarkMode = 20,
        WindowCornerPreference = 33,
        BorderColor = 34,
        SystemBackdropType = 38,
    }

    public enum DwmBackdrop { Auto = 0, None = 1, Mica = 2, Acrylic = 3, Tabbed = 4 }

    public enum DwmCorner { Default = 0, DoNotRound = 1, Round = 2, RoundSmall = 3 }

    [StructLayout(LayoutKind.Sequential)]
    public struct Margins(int all)
    {
        public int Left = all, Right = all, Top = all, Bottom = all;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WindowPos
    {
        public nint hwnd;
        public nint hwndInsertAfter;
        public int x, y, cx, cy;
        public uint flags;
    }

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, DwmWindowAttribute attribute, in int value, int size);

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmExtendFrameIntoClientArea(nint hwnd, in Margins margins);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static partial nint DefWindowProc(nint hwnd, int msg, nint wParam, nint lParam);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint hIcon);

    public static void SetDwm(nint hwnd, DwmWindowAttribute attribute, int value) =>
        _ = DwmSetWindowAttribute(hwnd, attribute, value, sizeof(int));
}
