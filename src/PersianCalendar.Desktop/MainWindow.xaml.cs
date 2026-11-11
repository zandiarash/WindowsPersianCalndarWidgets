using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PersianCalendar.Desktop.Services;
using PersianCalendar.Desktop.ViewModels;
using static PersianCalendar.Desktop.Services.NativeMethods;

namespace PersianCalendar.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly WidgetSettings _settings;
    private readonly ThemeService _theme;
    private nint _hwnd;

    public MainWindow(MainViewModel vm, WidgetSettings settings, ThemeService theme)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _settings = settings;
        _theme = theme;

        ThemeService.ApplyHeroSeason(vm.TodaySeason);
        ThemeService.ApplyAccentSeason(vm.DisplayedSeason);

        vm.MonthChanged += AnimateMonth;
        vm.PropertyChanged += OnViewModelPropertyChanged;
        theme.ThemeChanged += ApplyBackdrop;

        ContextMenu = new ContextMenu();
        ContextMenuOpening += (_, _) => BuildMenu(ContextMenu);

        SourceInitialized += OnSourceInitialized;
        Loaded += (_, _) => { RestorePosition(); PlayEntrance(); };
        PreviewKeyDown += OnKeyDown;
    }

    // ======================= Native window setup =======================

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(_hwnd)!;
        source.CompositionTarget.BackgroundColor = Colors.Transparent;
        source.AddHook(WndProc);

        // Tool window: hidden from Alt+Tab and the taskbar, like a real desktop gadget.
        var ex = GetWindowLongPtr(_hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(_hwnd, GWL_EXSTYLE, (ex | WS_EX_TOOLWINDOW) & ~(nint)WS_EX_APPWINDOW);

        ApplyBackdrop();
        ApplyLayer();
    }

    /// <summary>Windows 11 acrylic backdrop + rounded corners drawn by DWM (native shadow included).</summary>
    private void ApplyBackdrop()
    {
        if (_hwnd == 0) return;
        SetDwm(_hwnd, DwmWindowAttribute.UseImmersiveDarkMode, _theme.IsDark ? 1 : 0);
        SetDwm(_hwnd, DwmWindowAttribute.WindowCornerPreference, (int)DwmCorner.Round);
        SetDwm(_hwnd, DwmWindowAttribute.SystemBackdropType, (int)DwmBackdrop.Acrylic);
        _ = DwmExtendFrameIntoClientArea(_hwnd, new Margins(-1));
    }

    public void ApplyLayer()
    {
        Topmost = _settings.Layer == WindowLayer.Topmost;
        if (_hwnd != 0 && _settings.Layer == WindowLayer.Desktop)
            SetWindowPos(_hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch (msg)
        {
            // "Pinned to desktop": keep the widget underneath every other window.
            case WM_WINDOWPOSCHANGING when _settings.Layer == WindowLayer.Desktop:
                var pos = Marshal.PtrToStructure<WindowPos>(lParam);
                if ((pos.flags & SWP_NOZORDER) == 0)
                {
                    pos.hwndInsertAfter = HWND_BOTTOM;
                    Marshal.StructureToPtr(pos, lParam, false);
                }
                break;

            // Keep the acrylic backdrop alive when the widget is not the active window.
            case WM_NCACTIVATE when wParam == 0:
                handled = true;
                return DefWindowProc(hwnd, msg, 1, lParam);
        }
        return 0;
    }

    // ======================= Position =======================

    private void RestorePosition()
    {
        var area = SystemParameters.WorkArea;
        var (left, top) = (_settings.Left ?? area.Right - ActualWidth - 24, _settings.Top ?? area.Top + 24);

        // Keep it reachable if a monitor was disconnected.
        var (vl, vt) = (SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop);
        var (vw, vh) = (SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        Left = Math.Clamp(left, vl, vl + vw - 80);
        Top = Math.Clamp(top, vt, vt + vh - 80);
    }

    private void DragArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        DragMove();
        (_settings.Left, _settings.Top) = (Left, Top);
        App.Current.SaveSettings();
    }

    // ======================= Interaction =======================

    private void Days_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var step = e.Delta > 0 ? -1 : 1;
        var command = Keyboard.Modifiers.HasFlag(ModifierKeys.Control)
            ? (step < 0 ? _vm.PreviousYearCommand : _vm.NextYearCommand)
            : (step < 0 ? _vm.PreviousMonthCommand : _vm.NextMonthCommand);
        command.Execute(null);
        e.Handled = true;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        ICommand? command = e.Key switch
        {
            Key.Left or Key.PageUp => _vm.PreviousMonthCommand, // ‹ previous
            Key.Right or Key.PageDown => _vm.NextMonthCommand,  // › next
            Key.Up => _vm.PreviousYearCommand,
            Key.Down => _vm.NextYearCommand,
            Key.Home or Key.T => _vm.GoToTodayCommand,
            Key.Escape => new RelayCommand(Hide),
            _ => null,
        };
        if (command is null) return;
        command.Execute(null);
        e.Handled = true;
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        BuildMenu(ContextMenu);
        ContextMenu.PlacementTarget = MoreButton;
        ContextMenu.Placement = PlacementMode.Bottom;
        ContextMenu.IsOpen = true;
    }

    /// <summary>Shows the same context menu at the mouse (used by the tray icon).</summary>
    public void ShowMenuAtCursor()
    {
        BuildMenu(ContextMenu);
        ContextMenu.PlacementTarget = null;
        ContextMenu.Placement = PlacementMode.MousePoint;
        ContextMenu.IsOpen = true;
    }

    private void BuildMenu(ContextMenu menu)
    {
        var app = App.Current;
        menu.Items.Clear();

        menu.Items.Add(Item("چسبیده به دسکتاپ", "", _settings.Layer == WindowLayer.Desktop, () => app.SetLayer(WindowLayer.Desktop)));
        menu.Items.Add(Item("همیشه روی پنجره‌ها", "", _settings.Layer == WindowLayer.Topmost, () => app.SetLayer(WindowLayer.Topmost)));
        menu.Items.Add(Item("پنجرهٔ معمولی", "", _settings.Layer == WindowLayer.Normal, () => app.SetLayer(WindowLayer.Normal)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("پوستهٔ سیستم", "", _settings.Theme == AppTheme.System, () => app.SetTheme(AppTheme.System)));
        menu.Items.Add(Item("پوستهٔ روشن", "", _settings.Theme == AppTheme.Light, () => app.SetTheme(AppTheme.Light)));
        menu.Items.Add(Item("پوستهٔ تیره", "", _settings.Theme == AppTheme.Dark, () => app.SetTheme(AppTheme.Dark)));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("نمایش ثانیه", "", _settings.ShowSeconds, app.ToggleSeconds));
        menu.Items.Add(Item("نمایش روز میلادی در جدول", "", _settings.ShowGregorianNumbers, app.ToggleGregorianNumbers));
        menu.Items.Add(Item("اجرا همراه با ویندوز", "", StartupService.IsEnabled, app.ToggleStartup));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("پنهان کردن", "", false, Hide));
        menu.Items.Add(Item("خروج", "", false, app.ExitApp));

        static MenuItem Item(string header, string glyph, bool isChecked, Action action)
        {
            var item = new MenuItem { Header = header, Icon = new TextBlock { Text = glyph }, IsChecked = isChecked };
            item.Click += (_, _) => action();
            return item;
        }
    }

    // ======================= Visual feedback =======================

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.DisplayedSeason):
                ThemeService.ApplyAccentSeason(_vm.DisplayedSeason);
                break;
            case nameof(MainViewModel.TodaySeason):
                ThemeService.ApplyHeroSeason(_vm.TodaySeason);
                break;
        }
    }

    private void AnimateMonth(int direction)
    {
        var duration = TimeSpan.FromMilliseconds(320);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        DaysTranslate.BeginAnimation(TranslateTransform.XProperty,
            // Next month slides in from the right (› side), previous from the left; X is mirrored in RTL.
            new DoubleAnimation(-36 * direction, 0, duration) { EasingFunction = ease });
        DaysList.BeginAnimation(OpacityProperty, new DoubleAnimation(0.15, 1, duration) { EasingFunction = ease });
    }

    private void PlayEntrance()
    {
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)) { EasingFunction = ease });
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0, _vm.YearProgress, TimeSpan.FromMilliseconds(1100)) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(200), FillBehavior = FillBehavior.Stop });
    }

    /// <summary>Renders the widget (over a backdrop-like fill) to a PNG — used by <c>--snapshot</c> for docs/CI.</summary>
    public void SaveSnapshot(string path, double scale = 2)
    {
        Root.BeginAnimation(OpacityProperty, null);
        Root.Opacity = 1;
        ProgressScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        UpdateLayout();

        var (w, h) = (Root.ActualWidth, Root.ActualHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var backdrop = _theme.IsDark ? Color.FromRgb(0x24, 0x22, 0x36) : Color.FromRgb(0xE9, 0xEC, 0xF5);
            dc.DrawRoundedRectangle(new SolidColorBrush(backdrop), null, new Rect(0, 0, w, h), 8, 8);
            // A VisualBrush of a right-to-left element is captured mirrored; flip it back.
            dc.PushTransform(new MatrixTransform(-1, 0, 0, 1, w, 0));
            dc.DrawRectangle(new VisualBrush(Root), null, new Rect(0, 0, w, h));
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap((int)(w * scale), (int)(h * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
    }

    public void ShowWidget()
    {
        Show();
        WindowState = WindowState.Normal;
        if (_settings.Layer == WindowLayer.Desktop)
            ApplyLayer();
        else
            Activate();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Closing (Alt+F4) just hides the widget; exit from the menu.
        if (!App.Current.IsExiting)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}
