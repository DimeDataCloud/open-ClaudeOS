using ClaudeOS.Shell.Interop;
using ClaudeOS.Shell.Services;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.System;
using WinRT.Interop;

namespace ClaudeOS.Shell.Views;

/// <summary>Asks for the Claude API key once. Returns true if a key is now stored.</summary>
internal sealed partial class KeyWindow : Window
{
    private readonly TaskCompletionSource<bool> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly nint _hwnd;

    public KeyWindow()
    {
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(true, false);
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.Title = "Connect Claude";
        SystemBackdrop = new DesktopAcrylicBackdrop();
        var corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(uint));

        Forget.Visibility = KeyVault.Has && Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is null ? Visibility.Visible : Visibility.Collapsed;
        Save.Click += (_, _) => TrySave();
        Cancel.Click += (_, _) => Finish(KeyVault.Has);
        Forget.Click += (_, _) => { KeyVault.Clear(); Finish(false); };
        KeyBox.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                TrySave();
            }
            else if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                Finish(KeyVault.Has);
            }
        };
        Closed += (_, _) => _result.TrySetResult(KeyVault.Has);
    }

    public Task<bool> AskAsync()
    {
        var monitor = WindowCatalog.Capture(_hwnd).FocusMonitor;
        var width = (int)Math.Round(500 * monitor.Scale);
        var height = (int)Math.Round(330 * monitor.Scale);
        AppWindow.MoveAndResize(new RectInt32(monitor.WorkArea.X + ((monitor.WorkArea.Width - width) / 2), monitor.WorkArea.Y + ((monitor.WorkArea.Height - height) / 3), width, height));
        AppWindow.Show();
        Native.SetForegroundWindow(_hwnd);
        Activate();
        KeyBox.Focus(FocusState.Programmatic);
        return _result.Task;
    }

    private void TrySave()
    {
        var key = KeyBox.Password.Trim();
        if (!key.StartsWith("sk-ant-", StringComparison.Ordinal) || key.Length < 20)
        {
            Problem.Text = "That doesn't look like an Anthropic API key. It starts with sk-ant-.";
            Problem.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            KeyVault.Set(key);
        }
        catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            Problem.Text = "Windows would not store the key: " + e.Message;
            Problem.Visibility = Visibility.Visible;
            return;
        }

        KeyBox.Password = "";
        Finish(true);
    }

    private void Finish(bool hasKey)
    {
        _result.TrySetResult(hasKey);
        Close();
    }
}
