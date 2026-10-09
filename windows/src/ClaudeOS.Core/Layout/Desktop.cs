namespace ClaudeOS.Core.Layout;

public enum Posture
{
    /// <summary>Keyboard attached or a mouse in use.</summary>
    Laptop,

    /// <summary>Detached keyboard, touch first. Apps are usually maximized.</summary>
    Tablet,
}

public sealed record MonitorInfo(string Id, Rect WorkArea, double Scale = 1.0, bool IsPrimary = false)
{
    public bool IsPortrait => WorkArea.Height > WorkArea.Width;
}

public sealed record WindowInfo(
    long Handle,
    string Title,
    string Process,
    Rect Bounds,
    string MonitorId,
    bool IsActive = false,
    bool IsMinimized = false,
    bool IsMaximized = false,
    bool OnCurrentDesktop = true,
    bool CanMove = true)
{
    /// <summary>Whether the window takes up screen space the layout engine must respect.</summary>
    public bool IsVisible => !IsMinimized && OnCurrentDesktop && !Bounds.IsEmpty;
}

/// <summary>What the shell reads from Windows before every placement: monitors with their
/// usable area (the taskbar excluded) and every visible window's true on-screen bounds.</summary>
public sealed record Desktop(
    IReadOnlyList<MonitorInfo> Monitors,
    IReadOnlyList<WindowInfo> Windows,
    string FocusMonitorId,
    Posture Posture = Posture.Laptop)
{
    public MonitorInfo FocusMonitor => Monitors.FirstOrDefault(m => m.Id == FocusMonitorId) ?? Monitors[0];

    public WindowInfo? ActiveWindow => Windows.FirstOrDefault(w => w.IsActive && w.IsVisible);
}
