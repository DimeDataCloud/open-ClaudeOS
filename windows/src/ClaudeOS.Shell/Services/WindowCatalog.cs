using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ClaudeOS.Core.Layout;
using ClaudeOS.Shell.Interop;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// Reads the desktop the layout engine reasons about: monitors with their usable area (taskbar
/// excluded), and every visible window with its <em>true</em> on-screen bounds (DWM's extended
/// frame, not the rectangle that includes invisible resize borders). Also moves windows, undoing
/// that border difference so a window lands exactly where the engine said.
/// </summary>
internal static unsafe class WindowCatalog
{
    private sealed class Scan
    {
        public readonly List<MonitorInfo> Monitors = [];
        public readonly List<nint> Windows = [];
    }

    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow", "Windows.UI.Core.CoreWindow",
    };

    public static Desktop Capture(nint ignoreWindow = 0)
    {
        var scan = new Scan();
        var handle = GCHandle.Alloc(scan);
        try
        {
            Native.EnumDisplayMonitors(0, 0, &MonitorProc, GCHandle.ToIntPtr(handle));
            Native.EnumWindows(&WindowProc, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        var foreground = Native.GetForegroundWindow();
        var windows = new List<WindowInfo>();
        foreach (var hwnd in scan.Windows)
        {
            if (hwnd == ignoreWindow || !TryBounds(hwnd, out var bounds))
            {
                continue;
            }

            var title = Title(hwnd);
            Native.GetWindowThreadProcessId(hwnd, out var pid);
            var monitor = MonitorIdAt(bounds);
            windows.Add(new WindowInfo(
                hwnd,
                title,
                pid.ToString(System.Globalization.CultureInfo.InvariantCulture),
                bounds,
                monitor,
                IsActive: hwnd == foreground,
                IsMinimized: Native.IsIconic(hwnd),
                IsMaximized: Native.IsZoomed(hwnd),
                OnCurrentDesktop: true,
                CanMove: true));
        }

        Native.GetCursorPos(out var cursor);
        var focus = Native.MonitorFromPoint(cursor, 2).ToString();
        var monitors = scan.Monitors.Count > 0 ? scan.Monitors : [new MonitorInfo("fallback", new Rect(0, 0, 1920, 1040))];
        var tablet = Native.GetSystemMetrics(Native.SM_TABLETPC) != 0 && Native.GetSystemMetrics(Native.SM_CONVERTIBLESLATEMODE) == 0;
        return new Desktop(monitors, windows, monitors.Any(m => m.Id == focus) ? focus : monitors[0].Id, tablet ? Posture.Tablet : Posture.Laptop);
    }

    /// <summary>Move a window so its <em>visible</em> frame occupies <paramref name="to"/>.</summary>
    public static bool Move(long handle, Rect to)
    {
        var hwnd = (nint)handle;
        if (Native.IsZoomed(hwnd))
        {
            Native.ShowWindow(hwnd, Native.SW_RESTORE);
        }

        if (!Native.GetWindowRect(hwnd, out var outer) || Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var frame, sizeof(Native.RECT)) != 0)
        {
            return false;
        }

        int left = frame.Left - outer.Left, top = frame.Top - outer.Top, right = outer.Right - frame.Right, bottom = outer.Bottom - frame.Bottom;
        return Native.SetWindowPos(hwnd, 0, to.X - left, to.Y - top, to.Width + left + right, to.Height + top + bottom, Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    public static (Rect WorkArea, double Scale) FocusMonitor()
    {
        var desktop = Capture();
        var m = desktop.FocusMonitor;
        return (m.WorkArea, m.Scale);
    }

    private static bool TryBounds(nint hwnd, out Rect bounds)
    {
        bounds = default;
        if (Native.DwmGetWindowAttribute(hwnd, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var r, sizeof(Native.RECT)) != 0)
        {
            if (!Native.GetWindowRect(hwnd, out r))
            {
                return false;
            }
        }

        bounds = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        return !bounds.IsEmpty;
    }

    private static string Title(nint hwnd)
    {
        var length = Native.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return "";
        }

        var buffer = new char[length + 1];
        var read = Native.GetWindowText(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, Math.Max(0, read));
    }

    private static string MonitorIdAt(Rect bounds)
    {
        var (cx, cy) = bounds.Center;
        return Native.MonitorFromPoint(new Native.POINT { X = (int)cx, Y = (int)cy }, 2).ToString();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int MonitorProc(nint monitor, nint hdc, Native.RECT* rect, nint data)
    {
        var scan = (Scan)GCHandle.FromIntPtr(data).Target!;
        var info = new Native.MONITORINFO { cbSize = (uint)sizeof(Native.MONITORINFO) };
        if (Native.GetMonitorInfo(monitor, ref info))
        {
            var scale = Native.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1.0;
            var work = info.rcWork;
            scan.Monitors.Add(new MonitorInfo(monitor.ToString(), new Rect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top), scale, (info.dwFlags & 1) != 0));
        }

        return 1;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static int WindowProc(nint hwnd, nint data)
    {
        var scan = (Scan)GCHandle.FromIntPtr(data).Target!;
        if (!Native.IsWindowVisible(hwnd) || Native.GetWindowTextLength(hwnd) == 0)
        {
            return 1;
        }

        if ((Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE) & Native.WS_EX_TOOLWINDOW) != 0)
        {
            return 1;
        }

        // Windows on other virtual desktops, and UWP windows that are suspended, are "cloaked".
        if (Native.DwmGetWindowAttributeInt(hwnd, Native.DWMWA_CLOAKED, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
        {
            return 1;
        }

        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == Native.GetCurrentProcessId())
        {
            return 1;
        }

        scan.Windows.Add(hwnd);
        return 1;
    }
}
