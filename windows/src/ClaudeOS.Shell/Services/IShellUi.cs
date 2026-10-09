using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Mods;

namespace ClaudeOS.Shell.Services;

/// <summary>A chart on screen that Claude can be asked to change.</summary>
internal interface IChartWindow
{
    Task<string?> AskAsync(string title, string placeholder);

    Task UpdateAsync(string altText, string svg);

    /// <summary>Show the presence on the chart while Claude works on it.</summary>
    void SetBusy(bool busy);
}

/// <summary>
/// Everything the agent needs from the screen, and nothing about how it is drawn. Implementations
/// marshal to the UI thread themselves, so the agent can call these from anywhere.
/// </summary>
internal interface IShellUi
{
    /// <summary>Run on the UI thread (presence events, which windows listen to).</summary>
    void Post(Action action);

    /// <summary>True once a Claude API key is stored; asks for one if not.</summary>
    Task<bool> EnsureKeyAsync();

    /// <summary>Show an approval card and wait for the answer.</summary>
    Task<bool> ApproveAsync(ApprovalModel model);

    Task<IChartWindow> ShowChartAsync(string altText, string svg, int widthDips, int heightDips, Func<IChartWindow, Task> edit);

    void ShowWidget(string name, Func<RenderedNode> bind, WidgetPlacement placement);
}
