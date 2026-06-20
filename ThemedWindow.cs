using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace Jester;

/// <summary>
/// Base window that hosts the custom Jester chrome (purple title bar + caption
/// buttons defined in Theme.xaml). It wires the standard window commands to the
/// templated caption buttons and, because a borderless WindowChrome window overhangs
/// the monitor when maximized, insets its content by the measured overhang so the
/// chrome lines up cleanly with the screen and never clips its own title bar.
/// </summary>
public class ThemedWindow : Window
{
    /// <summary>Content inset applied while maximized to absorb the window's overhang.</summary>
    public static readonly DependencyProperty ChromeMarginProperty =
        DependencyProperty.Register(nameof(ChromeMargin), typeof(Thickness), typeof(ThemedWindow),
            new PropertyMetadata(new Thickness(0)));

    public Thickness ChromeMargin
    {
        get => (Thickness)GetValue(ChromeMarginProperty);
        private set => SetValue(ChromeMarginProperty, value);
    }

    public ThemedWindow()
    {
        CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.MaximizeWindowCommand, (_, _) => SystemCommands.MaximizeWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.RestoreWindowCommand, (_, _) => SystemCommands.RestoreWindow(this)));
        CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(this)));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WindowProc);
        UpdateChromeMargin();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // The maximized window rect is final by the time WPF raises this, but re-measure
        // once more after layout settles to be safe on multi-monitor / DPI changes.
        UpdateChromeMargin();
        Dispatcher.BeginInvoke(UpdateChromeMargin, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Measures how far the (maximized) window extends past the monitor's work area and
    /// converts that physical-pixel overhang into a device-independent content inset.
    /// When the window is not maximized the inset is zero. If the work-area constraint in
    /// <see cref="ConstrainMaximizeToWorkArea"/> succeeds there is no overhang and the
    /// inset is zero too, so the two mechanisms never fight.
    /// </summary>
    private void UpdateChromeMargin()
    {
        if (WindowState != WindowState.Maximized)
        {
            ChromeMargin = new Thickness(0);
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out RECT win))
            return;

        IntPtr monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            return;

        RECT work = info.rcWork;
        Matrix toDip = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        Point topLeft = toDip.Transform(new Point(Math.Max(0, work.Left - win.Left), Math.Max(0, work.Top - win.Top)));
        Point bottomRight = toDip.Transform(new Point(Math.Max(0, win.Right - work.Right), Math.Max(0, win.Bottom - work.Bottom)));

        ChromeMargin = new Thickness(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
    }

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    private static IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            ConstrainMaximizeToWorkArea(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static void ConstrainMaximizeToWorkArea(IntPtr hwnd, IntPtr lParam)
    {
        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return;

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(monitor, ref info))
            return;

        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        RECT work = info.rcWork;
        RECT full = info.rcMonitor;

        mmi.ptMaxPosition.X = work.Left - full.Left;
        mmi.ptMaxPosition.Y = work.Top - full.Top;
        mmi.ptMaxSize.X = work.Right - work.Left;
        mmi.ptMaxSize.Y = work.Bottom - work.Top;

        Marshal.StructureToPtr(mmi, lParam, fDeleteOld: true);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);
}
