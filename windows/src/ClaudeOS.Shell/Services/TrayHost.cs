using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ClaudeOS.Shell.Interop;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// A hidden message-only window that owns three things Windows delivers by message: the global
/// hotkey, the tray icon's clicks, and the "taskbar was recreated" broadcast. WinUI's own message
/// loop pumps it, so it costs nothing while idle.
/// </summary>
internal sealed unsafe class TrayHost : IDisposable
{
    private const int HotkeyId = 0xC05;
    private const int TrayMessage = Native.WM_APP + 1;
    private const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;
    private const uint MF_STRING = 0, MF_SEPARATOR = 0x800;
    private const uint TPM_RIGHTBUTTON = 2, TPM_RETURNCMD = 0x100, TPM_BOTTOMALIGN = 0x20;
    private const int CommandOpen = 1, CommandDiagnostics = 2, CommandQuit = 3, CommandConnect = 4;

    private static TrayHost? s_instance;
    private static uint s_taskbarCreated;

    private nint _hwnd;
    private nint _icon;

    public event Action? SummonRequested;

    public event Action? DiagnosticsRequested;

    public event Action? ConnectRequested;

    public event Action? QuitRequested;

    public bool HotkeyRegistered { get; private set; }

    public string HotkeyDescription => "Ctrl+Alt+Space";

    public void Start(byte[]? iconPng)
    {
        s_instance = this;
        s_taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
        const string className = "ClaudeOS.TrayHost";
        fixed (char* name = className)
        {
            var wc = new Native.WNDCLASSEXW
            {
                cbSize = (uint)sizeof(Native.WNDCLASSEXW),
                lpfnWndProc = &WindowProc,
                hInstance = Native.GetModuleHandle(null),
                lpszClassName = name,
            };
            Native.RegisterClassEx(&wc);
        }

        _hwnd = Native.CreateWindowEx(0, className, "ClaudeOS tray", 0, 0, 0, 0, 0, Native.HWND_MESSAGE, 0, Native.GetModuleHandle(null), 0);
        HotkeyRegistered = _hwnd != 0 && Native.RegisterHotKey(_hwnd, HotkeyId, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_NOREPEAT, Native.VK_SPACE);

        if (iconPng is { Length: > 0 })
        {
            fixed (byte* bits = iconPng)
            {
                _icon = Native.CreateIconFromResourceEx(bits, (uint)iconPng.Length, true, 0x00030000, 24, 24, 0);
            }
        }

        AddIcon();
    }

    private void AddIcon()
    {
        var data = NewData();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
        data.uCallbackMessage = TrayMessage;
        data.hIcon = _icon;
        SetTip(&data, $"open-ClaudeOS ({HotkeyDescription})");
        Native.ShellNotifyIcon(NIM_ADD, &data);
    }

    /// <summary>The tooltip doubles as the quietest possible presence: it says what Claude is doing.</summary>
    public void SetStatus(string text)
    {
        if (_hwnd == 0)
        {
            return;
        }

        var data = NewData();
        data.uFlags = NIF_TIP;
        SetTip(&data, text.Length > 120 ? text[..120] : text);
        Native.ShellNotifyIcon(NIM_MODIFY, &data);
    }

    private Native.NOTIFYICONDATAW NewData() => new() { cbSize = (uint)sizeof(Native.NOTIFYICONDATAW), hWnd = _hwnd, uID = 1 };

    private static void SetTip(Native.NOTIFYICONDATAW* data, string tip)
    {
        for (var i = 0; i < tip.Length && i < 127; i++)
        {
            data->szTip[i] = tip[i];
        }
    }

    private void ShowMenu()
    {
        Native.GetCursorPos(out var at);
        var menu = Native.CreatePopupMenu();
        Native.AppendMenu(menu, MF_STRING, CommandOpen, $"Open the bar\t{HotkeyDescription}");
        Native.AppendMenu(menu, MF_STRING, CommandConnect, "Claude key…");
        Native.AppendMenu(menu, MF_STRING, CommandDiagnostics, "Check this device");
        Native.AppendMenu(menu, MF_SEPARATOR, 0, null);
        Native.AppendMenu(menu, MF_STRING, CommandQuit, "Quit");
        Native.SetForegroundWindow(_hwnd);
        var command = Native.TrackPopupMenu(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_BOTTOMALIGN, at.X, at.Y, 0, _hwnd, 0);
        Native.DestroyMenu(menu);
        switch (command)
        {
            case CommandOpen: SummonRequested?.Invoke(); break;
            case CommandDiagnostics: DiagnosticsRequested?.Invoke(); break;
            case CommandConnect: ConnectRequested?.Invoke(); break;
            case CommandQuit: QuitRequested?.Invoke(); break;
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WindowProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        var host = s_instance;
        if (host is not null)
        {
            if (msg == Native.WM_HOTKEY && (int)wParam == HotkeyId)
            {
                host.SummonRequested?.Invoke();
                return 0;
            }

            if (msg == TrayMessage)
            {
                switch ((uint)((nuint)lParam & 0xFFFF))
                {
                    case Native.WM_LBUTTONUP: host.SummonRequested?.Invoke(); break;
                    case Native.WM_RBUTTONUP: host.ShowMenu(); break;
                }

                return 0;
            }

            if (s_taskbarCreated != 0 && msg == s_taskbarCreated)
            {
                host.AddIcon();
                return 0;
            }
        }

        return Native.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hwnd != 0)
        {
            var data = NewData();
            Native.ShellNotifyIcon(NIM_DELETE, &data);
            Native.UnregisterHotKey(_hwnd, HotkeyId);
            Native.DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        if (_icon != 0)
        {
            Native.DestroyIcon(_icon);
            _icon = 0;
        }

        s_instance = null;
    }
}
