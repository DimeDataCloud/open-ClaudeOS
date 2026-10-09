using ClaudeOS.Core.Presence;
using ClaudeOS.Shell.Interop;
using ClaudeOS.Shell.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using WinRT.Interop;

namespace ClaudeOS.Shell.Views;

/// <summary>Runs <see cref="DeviceCheck"/> and shows the result. The orb animates while it counts
/// frames, so the frame-rate line measures a real animation.</summary>
internal sealed partial class DiagnosticsWindow : Window
{
    private readonly nint _hwnd;
    private readonly Func<double?> _summonMs;
    private readonly Func<int> _files;
    private int _frames;

    public DiagnosticsWindow(Func<double?> summonMs, Func<int> files, bool reducedMotion)
    {
        _summonMs = summonMs;
        _files = files;
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(true, false);
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.Title = "Check this device";
        SystemBackdrop = new DesktopAcrylicBackdrop();
        var corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(uint));

        Done.Click += (_, _) => Close();
        Copy.Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(Report.Text);
            Clipboard.SetContent(package);
        };
        Orb.Apply(new PresenceFrame(PresenceState.Working, "Measuring", Intensity: 0.8), reducedMotion);
    }

    public async Task RunAsync()
    {
        var monitor = WindowCatalog.Capture(_hwnd).FocusMonitor;
        var width = (int)Math.Round(620 * monitor.Scale);
        var height = (int)Math.Round(480 * monitor.Scale);
        AppWindow.MoveAndResize(new RectInt32(monitor.WorkArea.X + ((monitor.WorkArea.Width - width) / 2), monitor.WorkArea.Y + ((monitor.WorkArea.Height - height) / 3), width, height));
        AppWindow.Show();
        Activate();

        // Count the frames the UI produces for a second while the orb animates.
        void OnFrame(object? sender, object args) => _frames++;
        CompositionTarget.Rendering += OnFrame;
        await Task.Delay(1000);
        CompositionTarget.Rendering -= OnFrame;

        Report.Text = DeviceCheck.Format(DeviceCheck.Measure(_summonMs(), _files(), _frames));
        Sub.Text = "Measured on this device, just now";
        Orb.Apply(new PresenceFrame(PresenceState.Done, "Done", Intensity: 0.6), false);
    }
}
