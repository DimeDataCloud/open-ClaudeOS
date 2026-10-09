using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClaudeOS.Core.Layout;

namespace ClaudeOS.Shell.Services;

/// <summary>One measurement: what it is, what we saw, and the target from the stack decision, if there is one.</summary>
internal sealed record Reading(string Name, string Value, bool? Pass = null, string? Target = null);

/// <summary>
/// The M0 checklist as something you can press: it measures what this build can measure, on the
/// machine it is running on, and says plainly what it cannot (so a missing line is never mistaken
/// for a passing one).
/// </summary>
internal static class DeviceCheck
{
    public static List<Reading> Measure(double? lastSummonMs, int filesIndexed, double? uiFramesPerSecond)
    {
        var readings = new List<Reading>
        {
            new("Windows", $"{Environment.OSVersion.Version} ({RuntimeInformation.OSArchitecture})"),
            new("App runs as", RuntimeInformation.ProcessArchitecture.ToString(), RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? true : null, "ARM64 on a Snapdragon X"),
            new("Packaged (MSIX)", IsPackaged() ? "yes" : "no (running unpackaged)", IsPackaged() ? true : null),
        };

        var megabytes = Process.GetCurrentProcess().WorkingSet64 / 1024.0 / 1024.0;
        readings.Add(new("Memory in use", $"{megabytes:0} MB", megabytes < 150, "under 150 MB when idle"));

        readings.Add(lastSummonMs is { } ms
            ? new("Hotkey to bar visible", $"{ms:0} ms", ms < 50, "under 50 ms")
            : new("Hotkey to bar visible", "press the hotkey once, then check again"));

        if (uiFramesPerSecond is { } fps)
        {
            readings.Add(new("Animation frame rate", $"{fps:0} fps", fps >= 100, "120 on a 120 Hz screen"));
        }

        var stopwatch = Stopwatch.StartNew();
        var desktop = WindowCatalog.Capture();
        var captured = stopwatch.Elapsed.TotalMilliseconds;
        stopwatch.Restart();
        var placement = LayoutEngine.Place(desktop, new PlacementRequest(new Size(900, 600)));
        var placed = stopwatch.Elapsed.TotalMilliseconds;
        readings.Add(new("Windows seen", $"{desktop.Windows.Count} on {desktop.Monitors.Count} {(desktop.Monitors.Count == 1 ? "screen" : "screens")}, read in {captured:0.0} ms"));
        readings.Add(new("Free space found", $"{placement.Method} in {placed:0.0} ms", placed < 20, "under 20 ms"));
        readings.Add(new("Files indexed", filesIndexed.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        readings.Add(new("Windows OCR", OcrAvailable(), null));
        readings.Add(new("Intent model on the NPU", "not wired in yet; the grammar router is used"));
        readings.Add(new("Agent Launcher registration", "not wired in yet"));
        readings.Add(new("Native AOT publish", "checked in CI, not by this app"));
        return readings;
    }

    public static string Format(IEnumerable<Reading> readings)
    {
        var all = readings.ToList();
        var width = all.Max(r => r.Name.Length);
        var sb = new StringBuilder();
        foreach (var r in all)
        {
            var mark = r.Pass switch { true => "pass", false => "MISS", null => "    " };
            sb.Append(mark).Append("  ").Append(r.Name.PadRight(width)).Append("  ").Append(r.Value);
            if (r.Target is not null)
            {
                sb.Append("  (target: ").Append(r.Target).Append(')');
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static bool IsPackaged()
    {
        try
        {
            _ = Windows.ApplicationModel.Package.Current.Id;
            return true;
        }
        catch (Exception e) when (e is InvalidOperationException or COMException)
        {
            return false;
        }
    }

    private static string OcrAvailable()
    {
        try
        {
            var engine = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
            return engine is null
                ? "no OCR language pack installed"
                : $"available: {engine.RecognizerLanguage.DisplayName} ({Windows.Media.Ocr.OcrEngine.AvailableRecognizerLanguages.Count} installed)";
        }
        catch (Exception e) when (e is COMException or InvalidOperationException)
        {
            return "not available: " + e.Message;
        }
    }
}
