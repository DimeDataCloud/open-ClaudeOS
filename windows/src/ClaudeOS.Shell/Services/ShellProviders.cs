using System.Globalization;
using ClaudeOS.Core.Mods;
using ClaudeOS.Shell.Interop;
using Windows.System.Power;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// Live values for widgets. A provider answers only for the paths it really knows. Where this PC
/// has nothing to report (a desktop has no battery; there is no calendar connector yet) the widget
/// shows a dash instead of an invented number.
/// </summary>
internal static class ShellProviders
{
    public static IReadOnlyList<IDataProvider> Create(FileIndex files) =>
    [
        new ClockProvider(),
        new BatteryProvider(),
        new PerformanceProvider(),
        new RecentFilesProvider(files),
        new OpenWindowsProvider(),
    ];
}

internal sealed class BatteryProvider : IDataProvider
{
    public string Prefix => "system.battery";

    public bool TryGet(string path, out string value)
    {
        value = "";
        try
        {
            if (PowerManager.BatteryStatus == BatteryStatus.NotPresent)
            {
                return false;
            }

            value = path switch
            {
                "system.battery.percent" => PowerManager.RemainingChargePercent.ToString(CultureInfo.InvariantCulture),
                "system.battery.charging" => PowerManager.BatteryStatus == BatteryStatus.Charging ? "charging" : "on battery",
                _ => "",
            };
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            return false;
        }

        return value.Length > 0;
    }
}

/// <summary>CPU is the change in busy time between two reads; memory is the system's own load figure.</summary>
internal sealed class PerformanceProvider : IDataProvider
{
    private ulong _lastIdle, _lastTotal;

    public string Prefix => "system.performance";

    public bool TryGet(string path, out string value)
    {
        value = path switch
        {
            "system.performance.cpu" => Cpu(),
            "system.performance.memory" => Memory(),
            _ => "",
        };
        return value.Length > 0;
    }

    private string Cpu()
    {
        if (!Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return "";
        }

        var first = _lastTotal == 0;
        var idleNow = idle.Value;
        var totalNow = kernel.Value + user.Value; // kernel time already includes idle time
        var idleDelta = idleNow - _lastIdle;
        var totalDelta = totalNow - _lastTotal;
        _lastIdle = idleNow;
        _lastTotal = totalNow;
        if (first || totalDelta == 0)
        {
            return "0";
        }

        return Math.Clamp(Math.Round(100.0 * (1.0 - ((double)idleDelta / totalDelta))), 0, 100).ToString(CultureInfo.InvariantCulture);
    }

    private static string Memory()
    {
        var status = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MEMORYSTATUSEX>() };
        return Native.GlobalMemoryStatusEx(ref status) ? status.dwMemoryLoad.ToString(CultureInfo.InvariantCulture) : "";
    }
}

internal sealed class RecentFilesProvider(FileIndex files) : IDataProvider
{
    public string Prefix => "files.recent";

    public bool TryGet(string path, out string value)
    {
        var recent = files.Recent(5);
        value = path switch
        {
            "files.recent.count" => recent.Count.ToString(CultureInfo.InvariantCulture),
            "files.recent.first" => recent.Count > 0 ? System.IO.Path.GetFileName(recent[0]) : "",
            _ => "",
        };
        return value.Length > 0;
    }
}

internal sealed class OpenWindowsProvider : IDataProvider
{
    public string Prefix => "windows";

    public bool TryGet(string path, out string value)
    {
        value = path == "windows.count" ? WindowCatalog.Capture().Windows.Count.ToString(CultureInfo.InvariantCulture) : "";
        return value.Length > 0;
    }
}
