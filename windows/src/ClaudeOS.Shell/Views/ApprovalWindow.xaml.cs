using System.Diagnostics;
using ClaudeOS.Core.Safety;
using ClaudeOS.Shell.Interop;
using ClaudeOS.Shell.Services;
using Microsoft.UI.Dispatching;
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
/// Shows the approval card and returns one answer. For anything that stays on this PC the answer is
/// a click (or Enter). For anything that leaves it, the approve control has to be held for most of a
/// second, so a stray Enter or a double-click can never send an email. Esc, closing the window and
/// "Not now" all mean no, and nothing has been changed by then.
/// </summary>
internal sealed partial class ApprovalWindow : Window
{
    private static readonly TimeSpan HoldTime = TimeSpan.FromMilliseconds(750);

    private readonly ApprovalModel _model;
    private readonly TaskCompletionSource<bool> _answer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _holdTimer;
    private readonly Stopwatch _held = new();
    private readonly nint _hwnd;
    private bool _answered;
    private bool _closed;

    public ApprovalWindow(ApprovalModel model)
    {
        _model = model;
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);

        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(true, false);
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        AppWindow.SetPresenter(presenter);
        AppWindow.Title = "Review what I'll do";
        SystemBackdrop = new DesktopAcrylicBackdrop();
        var corner = Native.DWMWCP_ROUND;
        Native.DwmSetWindowAttribute(_hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(uint));

        Fill();

        _holdTimer = DispatcherQueue.CreateTimer();
        _holdTimer.Interval = TimeSpan.FromMilliseconds(16);
        _holdTimer.Tick += OnHoldTick;

        Decline.Click += (_, _) => Answer(false);
        Closed += (_, _) =>
        {
            _closed = true;
            Answer(false); // closing the window by any route means no
        };

        // A plain click approves things that stay on this PC. Holding is for things that leave it, so
        // the button's own Click is ignored there; pointer and key events are watched even though
        // Button marks them handled.
        ApproveHost.Click += (_, _) =>
        {
            if (!_model.HoldToApprove)
            {
                BeginApprove();
            }
        };
        ApproveHost.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, e) =>
        {
            if (_model.HoldToApprove)
            {
                ApproveHost.CapturePointer(e.Pointer);
                BeginApprove();
            }
        }), true);
        ApproveHost.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, e) =>
        {
            ApproveHost.ReleasePointerCapture(e.Pointer);
            EndApprove();
        }), true);
        ApproveHost.PointerCanceled += (_, _) => EndApprove();
        ApproveHost.PointerCaptureLost += (_, _) => EndApprove();
        ApproveHost.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler((_, e) =>
        {
            if (_model.HoldToApprove && e.Key is VirtualKey.Enter or VirtualKey.Space && !e.KeyStatus.WasKeyDown)
            {
                e.Handled = true;
                BeginApprove();
            }
        }), true);
        ApproveHost.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler((_, e) =>
        {
            if (_model.HoldToApprove && e.Key is VirtualKey.Enter or VirtualKey.Space)
            {
                EndApprove();
            }
        }), true);
        Root.KeyDown += (_, e) =>
        {
            if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                Answer(false);
            }
        };
    }

    /// <summary>Shows the window and waits for the person's answer.</summary>
    public Task<bool> AskAsync()
    {
        var desktop = WindowCatalog.Capture(_hwnd);
        var monitor = desktop.FocusMonitor;
        var width = (int)Math.Round(540 * monitor.Scale);
        // Header and footer, then each row: its text, a line per policy note, and the quoted body if it leaves the PC.
        var wanted = 230 + _model.Rows.Sum(r => 70 + (r.Notes.Length * 24) + (r.Denied is null ? 0 : 24) + (r.Quoted.IsEmpty ? 0 : 40 + (18 * Math.Min(r.Quoted.Length, 6)))) + (_model.Diff.Length > 0 ? 60 : 0);
        var height = (int)Math.Min(Math.Round(wanted * monitor.Scale), monitor.WorkArea.Height * 0.86);
        var x = monitor.WorkArea.X + ((monitor.WorkArea.Width - width) / 2);
        var y = monitor.WorkArea.Y + ((monitor.WorkArea.Height - height) / 3);
        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
        AppWindow.Show();
        Native.SetForegroundWindow(_hwnd);
        Activate();
        ApproveHost.Focus(FocusState.Programmatic);
        return _answer.Task;
    }

    private void Fill()
    {
        TitleText.Text = _model.Title;
        if (_model.ModelClaim is { } claim)
        {
            Claim.Text = $"Claude says: {claim}";
            Claim.Visibility = Visibility.Visible;
        }

        foreach (var row in _model.Rows)
        {
            Body.Children.Add(BuildRow(row));
        }

        if (_model.Diff.Length > 0)
        {
            Body.Children.Add(BuildDiff(_model.Diff));
        }

        RiskLine.Text = _model.RiskLine;
        RiskDot.Fill = (Brush)Application.Current.Resources[_model.Risk switch
        {
            Risk.High => "CosStatusSeriousBrush",
            Risk.Medium => "CosStatusWarningBrush",
            _ => "CosStatusGoodBrush",
        }];
        ApproveText.Text = _model.Refused ? "Refused" : _model.ApproveLabel;
        if (_model.Refused)
        {
            ApproveHost.IsEnabled = false;
            ApproveHost.Opacity = 0.4;
        }

        AutomationProperties_SetName(ApproveHost, _model.HoldToApprove ? $"{_model.ApproveLabel}. Press and hold to confirm." : _model.ApproveLabel);
    }

    private static void AutomationProperties_SetName(DependencyObject element, string name) =>
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(element, name);

    private static UIElement BuildRow(ApprovalRow row)
    {
        var card = new Border { Style = (Style)Application.Current.Resources["CosCard"], Padding = new Thickness(16, 12, 16, 12) };
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = row.Badge.ToUpperInvariant(),
            Style = (Style)Application.Current.Resources["CosCaptionText"],
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources[row.External ? "CosStatusSeriousBrush" : "CosInkTertiaryBrush"],
        });
        stack.Children.Add(new TextBlock { Text = row.Text, Style = (Style)Application.Current.Resources["CosBodyText"], TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true });
        foreach (var note in row.Notes)
        {
            stack.Children.Add(new TextBlock { Text = note, Style = (Style)Application.Current.Resources["CosSecondaryText"], TextWrapping = TextWrapping.Wrap });
        }

        if (row.Denied is { } denied)
        {
            stack.Children.Add(new TextBlock
            {
                Text = $"Refused: {denied}",
                Style = (Style)Application.Current.Resources["CosBodyText"],
                Foreground = (Brush)Application.Current.Resources["CosStatusCriticalBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!row.Quoted.IsEmpty)
        {
            // Exactly what will be sent, quoted, so nothing leaves that you have not seen.
            var quote = new Border
            {
                Background = (Brush)Application.Current.Resources["CosSurfaceSunkenBrush"],
                CornerRadius = (CornerRadius)Application.Current.Resources["CosRadiusControl"],
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 6, 0, 0),
                Child = new TextBlock
                {
                    Text = string.Join('\n', row.Quoted),
                    Style = (Style)Application.Current.Resources["CosMonoText"],
                },
            };
            stack.Children.Add(quote);
        }

        card.Child = stack;
        return card;
    }

    private static UIElement BuildDiff(string diff)
    {
        var lines = diff.TrimEnd().Split('\n');
        var panel = new StackPanel { Spacing = 0 };
        foreach (var line in lines.Take(160))
        {
            var brush = line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal) ? "CosDiffAddBrush"
                : line.StartsWith('-') && !line.StartsWith("---", StringComparison.Ordinal) ? "CosDiffRemoveBrush"
                : "CosInkTertiaryBrush";
            panel.Children.Add(new TextBlock
            {
                Text = line.TrimEnd('\r'),
                FontFamily = (FontFamily)Application.Current.Resources["CosFontMono"],
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources[brush],
                TextWrapping = TextWrapping.NoWrap,
            });
        }

        if (lines.Length > 160)
        {
            panel.Children.Add(new TextBlock { Text = $"… {lines.Length - 160} more lines", Style = (Style)Application.Current.Resources["CosCaptionText"] });
        }

        return new Expander
        {
            Header = "Exact file changes",
            IsExpanded = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 220, Content = panel },
        };
    }

    /// <summary>How many rows the card shows (the self-test checks this).</summary>
    public int RowCount => _model.Rows.Length;

    public bool IsAnswered => _answered;

    /// <summary>Press and release exactly as a person would; used by the self-test.</summary>
    public void PressForTest() => BeginApprove();

    public void ReleaseForTest() => EndApprove();

    private void BeginApprove()
    {
        if (_model.Refused || _answered)
        {
            return;
        }

        if (!_model.HoldToApprove)
        {
            Answer(true);
            return;
        }

        if (!_held.IsRunning)
        {
            _held.Restart();
            _holdTimer.Start();
        }
    }

    private void EndApprove()
    {
        _holdTimer.Stop();
        _held.Reset();
        HoldFill.Width = 0;
    }

    private void OnHoldTick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        var fraction = Math.Clamp(_held.Elapsed.TotalMilliseconds / HoldTime.TotalMilliseconds, 0, 1);
        HoldFill.Width = ApproveHost.ActualWidth * fraction;
        if (fraction >= 1)
        {
            EndApprove();
            Answer(true);
        }
    }

    private void Answer(bool approved)
    {
        if (_answered)
        {
            return;
        }

        _answered = true;
        _holdTimer.Stop();
        _answer.TrySetResult(approved);
        if (!_closed)
        {
            Close(); // when this was reached from Closed, the window is already gone
        }
    }
}
