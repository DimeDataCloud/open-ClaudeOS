using ClaudeOS.Core.Design;
using ClaudeOS.Core.Planning;
using ClaudeOS.Core.Presence;
using ClaudeOS.Core.Safety;
using ClaudeOS.Shell.Services;
using ClaudeOS.Shell.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace ClaudeOS.Shell;

public partial class App : Application
{
    private IntentBarWindow? _bar;
    private TrayHost? _tray;
    private DispatcherQueue? _dispatcher;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Services?.State.Log("crash", Audit.Of(("message", Audit.Str(e.Exception?.ToString() ?? e.Message))));
    }

    public static new App? Current => Application.Current as App;

    internal AppServices? Services { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Services = AppServices.Create(reducedMotion: !new UISettings().AnimationsEnabled);

        _bar = new IntentBarWindow(Services.Bar);
        _bar.Prewarm();

        var ui = new ShellUi(_dispatcher, Services.Placer, Services.Bar.Presence, Services.Habits);
        Services.Bar.Agent = new AgentService(
            Services.Bar.Presence,
            Services.State,
            Services.Ledger,
            Services.Files,
            ui,
            () => RequestedTheme == ApplicationTheme.Dark ? Appearance.Dark : Appearance.Light);
        Services.Placer.Rules = Services.Bar.Agent.ActiveRules;
        Services.Bar.Agent.RestoreWidgets();

        // A developer/CI switch: if the package's private folder holds selftest.flag, run the shell
        // against itself, write the result next to it, and quit. See docs/testing.md.
        var selfTest = SelfTest.FlagPath();

        _tray = new TrayHost();
        _tray.SummonRequested += () => _dispatcher.TryEnqueue(Summon);
        _tray.QuitRequested += () => _dispatcher.TryEnqueue(Exit);
        _tray.DiagnosticsRequested += () => _dispatcher.TryEnqueue(async () => await new DiagnosticsWindow(() => _bar.LastSummonMilliseconds > 0 ? _bar.LastSummonMilliseconds : null, () => Services.Files.Count, Services.Bar.Presence.ReducedMotion).RunAsync());
        _tray.ConnectRequested += () => _dispatcher.TryEnqueue(async () => await new KeyWindow().AskAsync());
        _tray.Start(TryReadAsset("Square44x44Logo.targetsize-24_altform-unplated.png"));
        Services.Bar.Presence.Changed += frame => _tray.SetStatus(frame.Label.Length > 0 ? $"open-ClaudeOS: {frame.Label}" : "open-ClaudeOS");
        Services.Files.RefreshIfStale(TimeSpan.Zero);

        if (selfTest is not null)
        {
            _dispatcher.TryEnqueue(async () => await new SelfTest(this, Services, _bar, ui).RunAsync(selfTest));
        }
    }

    internal void OnSecondLaunch() => _dispatcher?.TryEnqueue(Summon);

    private void Summon()
    {
        Services?.Files.RefreshIfStale(TimeSpan.FromSeconds(90));
        _bar?.Summon();
    }

    private static byte[]? TryReadAsset(string name)
    {
        try
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", name));
        }
        catch (IOException)
        {
            return null;
        }
    }
}

/// <summary>Everything the shell shares, created once.</summary>
internal sealed class AppServices
{
    public required StateDir State { get; init; }

    public required FileIndex Files { get; init; }

    public required BarController Bar { get; init; }

    public required Placer Placer { get; init; }

    public required HabitService Habits { get; init; }

    public required TokenLedger Ledger { get; init; }

    public static AppServices Create(bool reducedMotion)
    {
        var state = new StateDir();
        var files = new FileIndex();
        var presence = new PresenceMachine(reducedMotion: reducedMotion);
        var placer = new Placer(state);
        var habits = new HabitService(presence, Path.Combine(state.Path, "habits.json"));
        return new AppServices
        {
            State = state,
            Files = files,
            Ledger = new TokenLedger(Path.Combine(state.Path, "tokens.jsonl"), new Budget(DailyUsd: 5, MonthlyUsd: 60)),
            Placer = placer,
            Habits = habits,
            Bar = new BarController(presence, files, state, placer, habits),
        };
    }
}
