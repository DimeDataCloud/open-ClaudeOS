using ClaudeOS.Core.Layout;
using ClaudeOS.Shell.Interop;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace ClaudeOS.Shell.Views;

/// <summary>
/// A window that shows one thing. Chrome is not hidden, it is absent: no title bar, no toolbar, no
/// tag. Drag anywhere to move it, drag the edges to resize, Esc closes it, and the only menu is the
/// one on right-click (or press and hold). It still has a name for screen readers.
/// </summary>
internal sealed partial class SubjectWindow : Window
{
    private readonly nint _hwnd;
    private readonly string _svg;
    private readonly Func<SubjectWindow, Task>? _edit;

    public SubjectWindow(string altText, string svg, Func<SubjectWindow, Task>? edit = null)
    {
        _svg = svg;
        _edit = edit;
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(true, false); // keeps the invisible resize edges
        presenter.IsMaximizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.Title = altText; // screen readers and the window list read this; nothing draws it
        AutomationProperties_SetName(Picture, altText);

        var corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(uint));
        var none = Native.DWMWA_COLOR_NONE;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_BORDER_COLOR, ref none, sizeof(uint));

        Root.PointerPressed += OnPointerPressed;
        Root.RightTapped += OnRightTapped;
        Root.Holding += OnHolding;
        var escape = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Escape };
        escape.Invoked += (_, e) => { e.Handled = true; Close(); };
        Root.KeyboardAccelerators.Add(escape);
        var closeKey = new KeyboardAccelerator { Key = Windows.System.VirtualKey.W, Modifiers = Windows.System.VirtualKeyModifiers.Control };
        closeKey.Invoked += (_, e) => { e.Handled = true; Close(); };
        Root.KeyboardAccelerators.Add(closeKey);
    }

    private static void AutomationProperties_SetName(DependencyObject element, string name) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, name);

    public async Task ShowAsync(Rect physical)
    {
        var source = new SvgImageSource();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(System.Text.Encoding.UTF8.GetBytes(_svg));
            await writer.StoreAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        await source.SetSourceAsync(stream);
        Picture.Source = source;
        AppWindow.MoveAndResize(new RectInt32(physical.X, physical.Y, physical.Width, physical.Height));
        AppWindow.Show();
        Activate();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (e.GetCurrentPoint(Root).Properties.IsLeftButtonPressed)
        {
            // The classic frameless-window drag: hand the click to the system as if it were on a title bar.
            Native.ReleaseCapture();
            Native.SendMessage(_hwnd, Native.WM_NCLBUTTONDOWN, Native.HTCAPTION, 0);
        }
    }

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs e) => ShowMenu(e.GetPosition(Root));

    private void OnHolding(object sender, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState == Microsoft.UI.Input.HoldingState.Started)
        {
            ShowMenu(e.GetPosition(Root));
        }
    }

    private void ShowMenu(Windows.Foundation.Point at)
    {
        var menu = new MenuFlyout();
        var pin = new ToggleMenuFlyoutItem { Text = "Pin on top" };
        pin.Click += (_, _) => ((OverlappedPresenter)AppWindow.Presenter).IsAlwaysOnTop = pin.IsChecked;
        menu.Items.Add(pin);

        var copy = new MenuFlyoutItem { Text = "Copy" };
        copy.Click += (_, _) =>
        {
            var package = new DataPackage();
            package.SetText(_svg);
            Clipboard.SetContent(package);
        };
        menu.Items.Add(copy);

        var save = new MenuFlyoutItem { Text = "Save as…" };
        save.Click += async (_, _) => await SaveAsync();
        menu.Items.Add(save);

        if (_edit is not null)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            var edit = new MenuFlyoutItem { Text = "Edit with Claude…" };
            edit.Click += async (_, _) => await _edit(this);
            menu.Items.Add(edit);
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        var close = new MenuFlyoutItem { Text = "Close" };
        close.Click += (_, _) => Close();
        menu.Items.Add(close);
        menu.ShowAt(Root, new FlyoutShowOptions { Position = at });
    }

    private async Task SaveAsync()
    {
        var picker = new FileSavePicker { SuggestedFileName = "chart", SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeChoices.Add("SVG picture", [".svg"]);
        InitializeWithWindow.Initialize(picker, _hwnd);
        if (await picker.PickSaveFileAsync() is { } file)
        {
            await FileIO.WriteTextAsync(file, _svg);
        }
    }
}
