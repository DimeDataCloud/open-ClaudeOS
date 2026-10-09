using Microsoft.UI.Xaml;

namespace ClaudeOS.Shell.Controls;

/// <summary>
/// Tells you where a window ended up after the <em>person</em> moved it. Moves the shell makes
/// itself (placing a window when it opens) are ignored until <see cref="ArmAfter"/> has passed,
/// and a drag only counts once the window has stopped for a moment.
/// </summary>
internal sealed class MoveWatcher
{
    private readonly Microsoft.UI.Windowing.AppWindow _app;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _settle;
    private bool _armed;

    public MoveWatcher(Window window, Action<ClaudeOS.Core.Layout.Rect> onSettled)
    {
        _app = window.AppWindow;
        _settle = window.DispatcherQueue.CreateTimer();
        _settle.Interval = TimeSpan.FromMilliseconds(1200);
        _settle.IsRepeating = false;
        _settle.Tick += (_, _) => onSettled(new ClaudeOS.Core.Layout.Rect(_app.Position.X, _app.Position.Y, _app.Size.Width, _app.Size.Height));
        _app.Changed += (_, change) =>
        {
            if (_armed && change.DidPositionChange)
            {
                _settle.Stop();
                _settle.Start();
            }
        };
        window.Closed += (_, _) => _settle.Stop();
    }

    public async void ArmAfter(TimeSpan delay)
    {
        await Task.Delay(delay);
        _armed = true;
    }
}
