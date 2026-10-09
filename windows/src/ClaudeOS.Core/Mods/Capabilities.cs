using System.Collections.Immutable;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Mods;

/// <summary>What a capability is, in words a person can approve.</summary>
/// <summary>A capability, and the data paths it unlocks (<c>calendar.read.next</c> unlocks <c>calendar.next.*</c>).</summary>
public sealed record CapabilityInfo(string Id, string DataPrefix, string Description, Risk Risk, bool TakesArgument = false);

/// <summary>
/// The only things a mod can ask for. A mod declares its capabilities up front, the person approves
/// them in plain words, and the core checks them on every data access. There is no capability
/// that reaches the approval, policy or undo code, so no mod can.
/// </summary>
public static class Capabilities
{
    public static readonly ImmutableDictionary<string, CapabilityInfo> Known = new[]
    {
        new CapabilityInfo("system.time", "system.time", "See the current date and time", Risk.Low),
        new CapabilityInfo("system.battery", "system.battery", "See your battery level and whether it is charging", Risk.Low),
        new CapabilityInfo("system.performance", "system.performance", "See CPU and memory use", Risk.Low),
        new CapabilityInfo("calendar.read.next", "calendar.next", "Read the title and start time of your next meeting", Risk.Medium),
        new CapabilityInfo("files.recent", "files.recent", "See the names of the files you opened recently", Risk.Medium),
        new CapabilityInfo("windows.list", "windows", "See which windows are open", Risk.Medium),
        new CapabilityInfo("claude.ask", "claude", "Ask Claude questions (uses your tokens)", Risk.Medium),
        new CapabilityInfo("network.fetch", "network", "Connect to a website: {0}", Risk.High, TakesArgument: true),
    }.ToImmutableDictionary(c => c.Id);

    /// <summary>"network.fetch:api.example.com" splits into the capability and its argument.</summary>
    public static (string Id, string? Argument) Split(string capability)
    {
        var i = capability.IndexOf(':');
        return i < 0 ? (capability, null) : (capability[..i], capability[(i + 1)..]);
    }

    public static bool TryDescribe(string capability, out CapabilityInfo info, out string text)
    {
        var (id, arg) = Split(capability);
        if (!Known.TryGetValue(id, out info!) || info.TakesArgument != (arg is not null) || (arg is not null && (arg.Length == 0 || arg.Contains('/'))))
        {
            text = "";
            return false;
        }

        text = info.TakesArgument ? string.Format(System.Globalization.CultureInfo.InvariantCulture, info.Description, arg) : info.Description;
        return true;
    }
}

public sealed class CapabilityDeniedException(string message) : Exception(message);

/// <summary>Holds what the person approved for each installed mod and answers, on every call,
/// whether a mod may touch a piece of data.</summary>
public sealed class CapabilityBroker
{
    private readonly Dictionary<string, HashSet<string>> _granted = new(StringComparer.Ordinal);

    public void Grant(string modId, IEnumerable<string> capabilities) => _granted[modId] = [.. capabilities];

    public void Revoke(string modId) => _granted.Remove(modId);

    public IReadOnlySet<string> GrantedTo(string modId) => _granted.TryGetValue(modId, out var g) ? g : new HashSet<string>();

    /// <summary>Is <paramref name="path"/> (for example <c>system.battery.percent</c>) covered by a grant?</summary>
    public bool Allows(string modId, string path) =>
        GrantedTo(modId).Any(g => Capabilities.Known.TryGetValue(Capabilities.Split(g).Id, out var info) && Covers(info.DataPrefix, path));

    /// <summary>Whether a data path sits under a capability's namespace.</summary>
    public static bool Covers(string dataPrefix, string path) => path == dataPrefix || path.StartsWith(dataPrefix + ".", StringComparison.Ordinal);

    public void Require(string modId, string path)
    {
        if (!Allows(modId, path))
        {
            throw new CapabilityDeniedException($"mod '{modId}' has not been granted access to {path}");
        }
    }
}
