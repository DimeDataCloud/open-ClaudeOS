using ClaudeOS.Core.Mods;
using ClaudeOS.Shell.Controls;
using ClaudeOS.Shell.Interop;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using WinRT.Interop;

namespace ClaudeOS.Shell.Views;

/// <summary>
/// A mod's widget, drawn with native controls and refreshed once a second from the capabilities the
/// person approved. Like every subject window it has no chrome; it floats where the layout engine
/// found room, drags from anywhere, and goes away with Esc or the context menu.
/// </summary>
internal sealed partial class WidgetWindow : Window
{
    private readonly nint _hwnd;
    private readonly Func<RenderedNode> _bind;
    private readonly DispatcherQueueTimer _timer;

    public WidgetWindow(string name, Func<RenderedNode> bind)
    {
        _bind = bind;
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(true, false);
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.Title = name;
        AppWindow.IsShownInSwitchers = false;
        var corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(uint));
        var none = Native.DWMWA_COLOR_NONE;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_BORDER_COLOR, ref none, sizeof(uint));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(Host, name);

        Root.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed)
            {
                Native.ReleaseCapture();
                Native.SendMessage(_hwnd, Native.WM_NCLBUTTONDOWN, Native.HTCAPTION, 0);
            }
        };
        Root.RightTapped += (_, e) => ShowMenu(e.GetPosition(Root));
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, e) => { e.Handled = true; Close(); };
        Root.KeyboardAccelerators.Add(escape);

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += (_, _) => Refresh();
        Closed += (_, _) => _timer.Stop();
    }

    /// <summary>Place the widget (physical pixels) and start updating it.</summary>
    public void ShowAt(ClaudeOS.Core.Layout.Rect physical)
    {
        Refresh();
        AppWindow.MoveAndResize(new RectInt32(physical.X, physical.Y, physical.Width, physical.Height));
        AppWindow.Show(activateWindow: false);
        _timer.Start();
    }

    /// <summary>The text currently drawn in the widget (the self-test checks it is live).</summary>
    public string Text => Controls.TreeText.Of(Host.Content as DependencyObject);

    private void Refresh()
    {
        try
        {
            Host.Content = WidgetView.Build(_bind());
        }
        catch (Exception e) when (e is CapabilityDeniedException or ModException)
        {
            // The approval no longer covers what the manifest asks for: stop showing it.
            _timer.Stop();
            Host.Content = new TextBlock { Text = "This widget needs your review again.", Style = (Style)Application.Current.Resources["CosBodyText"], TextWrapping = TextWrapping.Wrap };
        }
    }

    private void ShowMenu(Windows.Foundation.Point at)
    {
        var menu = new MenuFlyout();
        var pin = new ToggleMenuFlyoutItem { Text = "Keep on top" };
        pin.Click += (_, _) => ((OverlappedPresenter)AppWindow.Presenter).IsAlwaysOnTop = pin.IsChecked;
        menu.Items.Add(pin);
        menu.Items.Add(new MenuFlyoutSeparator());
        var close = new MenuFlyoutItem { Text = "Close" };
        close.Click += (_, _) => Close();
        menu.Items.Add(close);
        menu.ShowAt(Root, new FlyoutShowOptions { Position = at });
    }
}
