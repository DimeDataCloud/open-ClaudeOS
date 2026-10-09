using System.Collections.ObjectModel;
using System.Diagnostics;
using ClaudeOS.Core.Presence;
using ClaudeOS.Shell.Interop;
using ClaudeOS.Shell.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace ClaudeOS.Shell.Views;

/// <summary>
/// The Intent Bar: one input, summoned by a key, answered in place. It is created once at start-up
/// and only shown and hidden afterwards, which is what keeps "hotkey to visible" under 50 ms. It
/// has no title bar, no border and no taskbar button; Windows supplies the rounded corners and
/// the acrylic.
/// </summary>
internal sealed partial class IntentBarWindow : Window
{
    private const double WidthDips = 720;
    private const double RowDips = 48;
    private const double InputDips = 64;

    private readonly BarController _bar;
    private readonly ObservableCollection<BarResult> _results = [];
    private readonly nint _hwnd;
    private bool _shownOnce;
    private Stopwatch? _summonClock;

    public IntentBarWindow(BarController bar)
    {
        _bar = bar;
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        var corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(uint));

        Results.ItemsSource = _results;
        Input.TextChanged += OnTextChanged;
        Input.PreviewKeyDown += OnKeyDown;
        Results.ItemClick += OnItemClick;
        Activated += OnActivated;
        _bar.Presence.Changed += frame => DispatcherQueue.TryEnqueue(() => Show(frame));
    }

    /// <summary>Milliseconds from the hotkey to the bar being visible and focused, last time.</summary>
    public double LastSummonMilliseconds { get; private set; }

    /// <summary>Create the window's visuals once, off-screen, so the first real summon is instant.</summary>
    public void Prewarm()
    {
        AppWindow.MoveAndResize(new RectInt32(-32000, -32000, 720, 64));
        AppWindow.Show(activateWindow: false);
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, async () =>
        {
            await Task.Delay(250);
            AppWindow.Hide();
            _shownOnce = true;
        });
    }

    public void Summon()
    {
        _summonClock = Stopwatch.StartNew();
        var previous = Native.GetForegroundWindow();
        if (previous != _hwnd)
        {
            _bar.RememberForeground(previous);
        }

        Input.Text = "";
        _results.Clear();
        _bar.Presence.Handle(new BarShown());
        Position(rows: 0, statusVisible: false);
        AppWindow.Show();
        Native.SetForegroundWindow(_hwnd);
        Activate();
        Input.Focus(FocusState.Programmatic);
    }

    public void Dismiss()
    {
        AppWindow.Hide();
        _bar.Presence.Handle(new BarHidden());
    }

    private void Position(int rows, bool statusVisible)
    {
        var desktop = WindowCatalog.Capture(_hwnd);
        var monitor = desktop.FocusMonitor;
        var scale = monitor.Scale;
        var height = InputDips + (statusVisible ? 48 : 0) + (rows > 0 ? (rows * RowDips) + 14 : 0);
        var width = (int)Math.Round(WidthDips * scale);
        var h = (int)Math.Round(height * scale);
        var x = monitor.WorkArea.X + ((monitor.WorkArea.Width - width) / 2);
        var y = monitor.WorkArea.Y + (int)(monitor.WorkArea.Height * 0.2);
        AppWindow.MoveAndResize(new RectInt32(x, y, width, h));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated && _shownOnce && _bar.Presence.Frame.State != PresenceState.NeedsYou)
        {
            Dismiss();
            return;
        }

        if (_summonClock is { } clock && e.WindowActivationState != WindowActivationState.Deactivated)
        {
            LastSummonMilliseconds = clock.Elapsed.TotalMilliseconds;
            _summonClock = null;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _bar.Presence.Handle(new Typing(Input.Text.Length));
        _results.Clear();
        foreach (var r in _bar.Suggest(Input.Text))
        {
            _results.Add(r);
        }

        if (_results.Count > 0)
        {
            Results.SelectedIndex = 0;
        }

        Results.Visibility = _results.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        Position(_results.Count, StatusRow.Visibility == Visibility.Visible);
    }

    private async void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Escape:
                e.Handled = true;
                if (_bar.Presence.Frame.State == PresenceState.NeedsYou)
                {
                    _bar.Presence.Handle(new Declined());
                }

                Dismiss();
                break;
            case VirtualKey.Down when _results.Count > 0:
                e.Handled = true;
                Results.SelectedIndex = Math.Min(_results.Count - 1, Results.SelectedIndex + 1);
                break;
            case VirtualKey.Up when _results.Count > 0:
                e.Handled = true;
                Results.SelectedIndex = Math.Max(0, Results.SelectedIndex - 1);
                break;
            case VirtualKey.Enter:
                e.Handled = true;
                await SubmitAsync();
                break;
        }
    }

    private async void OnItemClick(object sender, ItemClickEventArgs e)
    {
        Results.SelectedItem = e.ClickedItem;
        await SubmitAsync();
    }

    private async Task SubmitAsync()
    {
        var text = Input.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var message = await _bar.SubmitAsync(text, Results.SelectedItem as BarResult);
        if (message is not null || _bar.Presence.Frame.State is PresenceState.Done or PresenceState.Understanding)
        {
            // Let the tick settle for a moment, then get out of the way.
            await Task.Delay(700);
            Dismiss();
        }
    }

    private void Show(PresenceFrame frame)
    {
        Orb.Apply(frame, reducedMotion: _bar.Presence.ReducedMotion);
        var showStatus = frame.State is not (PresenceState.Idle or PresenceState.Listening or PresenceState.Dormant);
        StatusRow.Visibility = showStatus ? Visibility.Visible : Visibility.Collapsed;
        Status.Text = frame.Label;
        StatusHint.Text = frame.Hint ?? "";
        StatusHint.Visibility = string.IsNullOrEmpty(frame.Hint) ? Visibility.Collapsed : Visibility.Visible;
        Hint.Text = frame.Suggestion ?? (frame.State == PresenceState.Idle ? "Enter to go · Esc to close" : "");
        Position(_results.Count, showStatus);
    }
}
