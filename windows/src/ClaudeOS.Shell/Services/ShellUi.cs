using ClaudeOS.Core.Layout;
using ClaudeOS.Shell.Controls;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Safety;
using ClaudeOS.Core.Presence;
using ClaudeOS.Shell.Views;
using Microsoft.UI.Dispatching;

namespace ClaudeOS.Shell.Services;

/// <summary>The WinUI side of <see cref="IShellUi"/>: opens the windows, always on the UI thread.</summary>
internal sealed class ShellUi(DispatcherQueue dispatcher, Placer placer, PresenceMachine presence, HabitService habits) : IShellUi
{
    // Windows the user has not closed. Holding them here keeps them alive.
    private readonly List<object> _open = [];

    /// <summary>The windows that are open right now, in the order they opened.</summary>
    public IReadOnlyList<object> Open => _open;

    /// <summary>Close every window this opened (the self-test cleans up with it).</summary>
    public void CloseAll()
    {
        foreach (var window in _open.OfType<Microsoft.UI.Xaml.Window>().ToList())
        {
            window.Close();
        }
    }

    public void Post(Action action)
    {
        if (dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            dispatcher.TryEnqueue(() => action());
        }
    }

    public async Task<bool> EnsureKeyAsync() =>
        KeyVault.Has || await OnUi(() => new KeyWindow().AskAsync());

    public Task<bool> ApproveAsync(ApprovalModel model) => OnUi(() =>
    {
        var window = new ApprovalWindow(model);
        Keep(window, window);
        return window.AskAsync();
    });

    public Task<IChartWindow> ShowChartAsync(string altText, string svg, int widthDips, int heightDips, Func<IChartWindow, Task> edit) => OnUi(async () =>
    {
        var placement = placer.Place(Placer.Physical(widthDips, heightDips), kind: "chart");
        var window = new SubjectWindow(altText, svg, presence, edit);
        Keep(window, window);
        await window.ShowAsync(placement.Bounds);
        Watch(window, "chart");
        return (IChartWindow)window;
    });

    public void ShowWidget(string name, Func<RenderedNode> bind, WidgetPlacement placement) => Post(() =>
    {
        var anchor = placement.Anchor switch
        {
            "top-left" => Anchor.TopLeft,
            "top-right" => Anchor.TopRight,
            "bottom-left" => Anchor.BottomLeft,
            "bottom-right" => Anchor.BottomRight,
            _ => Anchor.Auto,
        };
        var spot = placer.Place(Placer.Physical(placement.Width, placement.Height), anchor, kind: "widget");
        var window = new WidgetWindow(name, bind);
        Keep(window, window);
        window.ShowAt(spot.Bounds);
        Watch(window, "widget");
    });

    /// <summary>Where the person puts a window after it opens is what the habit tracker learns from.</summary>
    private void Watch(Microsoft.UI.Xaml.Window window, string kind) =>
        new MoveWatcher(window, bounds => habits.Observe(kind, bounds)).ArmAfter(TimeSpan.FromMilliseconds(1500));

    private void Keep(Microsoft.UI.Xaml.Window window, object keep)
    {
        _open.Add(keep);
        window.Closed += (_, _) => _open.Remove(keep);
    }

    private Task<T> OnUi<T>(Func<Task<T>> work)
    {
        if (dispatcher.HasThreadAccess)
        {
            return work();
        }

        var source = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(async () =>
        {
            try
            {
                source.SetResult(await work());
            }
            catch (Exception e)
            {
                source.SetException(e);
            }
        }))
        {
            source.SetException(new InvalidOperationException("The window system is shutting down."));
        }

        return source.Task;
    }
}
