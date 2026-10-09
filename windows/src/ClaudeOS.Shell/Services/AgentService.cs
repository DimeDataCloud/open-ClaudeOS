using System.Collections.Immutable;
using ClaudeOS.Claude;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Artifacts;
using ClaudeOS.Core.Design;
using ClaudeOS.Core.Intent;
using ClaudeOS.Core.Layout;
using ClaudeOS.Core.Mods;
using ClaudeOS.Core.Planning;
using ClaudeOS.Core.Presence;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Shell.Services;

/// <summary>
/// Where Claude comes in. Everything here is reached only after the local router has decided that
/// the request needs thinking: opening, finding and moving windows never get this far. The agent
/// makes charts and widgets (a compact validated spec, run by local code) and plans everything else
/// as typed actions behind the approval card. It talks to the screen only through
/// <see cref="IShellUi"/>, and tells the presence what it is really doing.
/// </summary>
internal sealed class AgentService
{
    private readonly PresenceMachine _presence;
    private readonly StateDir _state;
    private readonly TokenLedger _ledger;
    private readonly IShellUi _ui;
    private readonly Func<Appearance> _appearance;
    private readonly CapabilityBroker _broker = new();
    private readonly IReadOnlyList<IDataProvider> _providers;
    private readonly ModStore _mods;
    private readonly Policy _policy = new();
    private readonly Func<IModelClient>? _modelOverride;
    private readonly string _workspaceRoot;

    /// <param name="modelOverride">Replaces the Claude client, and the need for a key. Only the self-test passes it.</param>
    /// <param name="workspaceRoot">Where Claude may look and save. Defaults to the person's Documents folder.</param>
    public AgentService(PresenceMachine presence, StateDir state, TokenLedger ledger, FileIndex files, IShellUi ui, Func<Appearance> appearance, Func<IModelClient>? modelOverride = null, string? workspaceRoot = null)
    {
        _modelOverride = modelOverride;
        _workspaceRoot = workspaceRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        _presence = presence;
        _state = state;
        _ledger = ledger;
        _ui = ui;
        _appearance = appearance;
        _providers = ShellProviders.Create(files);
        var modsRoot = Path.Combine(state.Path, "mods");
        Directory.CreateDirectory(modsRoot);
        _mods = new ModStore(modsRoot, _broker);
    }

    /// <summary>Where Claude may look and where new things are saved.</summary>
    private string WorkspaceRoot => _workspaceRoot;

    /// <summary>Hands Claude the request and returns the line the bar shows when it is done.</summary>
    public async Task<string> RunAsync(UserIntent intent, string text, CancellationToken ct = default)
    {
        if (_modelOverride is null && !await _ui.EnsureKeyAsync())
        {
            Say(new Failed("Claude is not connected"));
            return "Claude is not connected. Add your key from the tray menu to use this.";
        }

        IModelClient client = _modelOverride?.Invoke() ?? AnthropicModelClient.WithKey(KeyVault.Get());
        var settings = new ModelSettings(Effort: Effort.Medium);
        var maker = new ArtifactMaker(client, settings, _ledger, Emit);
        Say(new Agent(new AgentEvent("request")));
        try
        {
            return intent switch
            {
                MakeIntent { Kind: MakeKind.Chart } => await MakeChartAsync(text, maker, ct),
                MakeIntent { Kind: MakeKind.Widget or MakeKind.Mod } => await MakeWidgetAsync(text, maker, ct),
                _ => await PlanAsync(text, new ClaudePlanner(client, new ModelSettings(Effort: Effort.High), _ledger, Emit), ct),
            };
        }
        catch (NoPlanException e)
        {
            // Claude answered with a question or a refusal instead of a plan: that is the answer.
            Say(new Finished(e.Message));
            return e.Message;
        }
        catch (Exception e) when (e is PlannerException or BudgetExceededException)
        {
            _state.Log("agent_failed", Audit.Of(("error", Audit.Str(e.Message))));
            Say(new Failed(e.Message));
            return e.Message;
        }
        catch (OperationCanceledException)
        {
            Say(new Declined());
            return "Stopped.";
        }
    }

    /// <summary>The layout-rule mods that are on, for the placer.</summary>
    public IReadOnlyList<ModManifest> ActiveRules() =>
        [.. _mods.LoadAll().Where(m => m.Status == ModStatus.Active && m.Manifest is { Kind: ModKind.LayoutRule }).Select(m => m.Manifest!)];

    /// <summary>The person said yes to an offered placement: it becomes a layout-rule mod, through the usual card.</summary>
    public async Task<string> InstallRuleAsync(Suggestion offer)
    {
        var proposal = _mods.Review(HabitRules.ToManifestJson(offer));
        if (Directory.Exists(Path.Combine(_mods.Root, proposal.Manifest.Id)))
        {
            return "That rule is already in place.";
        }

        Say(new PlanReady(1, "Low", false));
        if (!await _ui.ApproveAsync(ApprovalModel.FromMod(proposal)))
        {
            Say(new Declined());
            return "Okay. Nothing was added.";
        }

        Say(new Approved());
        await Creations.ApplyAsync(proposal.Plan, new Overlay(_mods.Root), _policy, _state);
        _mods.RecordApproval(proposal.Manifest, proposal.ManifestJson);
        Say(new Finished($"Done. New {offer.ContentKind}s will open there", Undoable: true));
        return $"Done. New {offer.ContentKind}s will open there";
    }

    /// <summary>Widgets the person added earlier come back at start-up, with only the access they approved.</summary>
    public void RestoreWidgets()
    {
        foreach (var installed in _mods.LoadAll().Where(m => m.Status == ModStatus.Active && m.Manifest is { Kind: ModKind.Widget, View: not null }))
        {
            ShowWidget(installed.Manifest!);
        }
    }

    // ------------------------------------------------------------------------------ charts

    private async Task<string> MakeChartAsync(string text, ArtifactMaker maker, CancellationToken ct)
    {
        var workspace = new Overlay(WorkspaceRoot);
        var data = await maker.MakeChartAsync(text, workspace, _policy, ct);
        return await ShowChartAsync(data, text, maker, ct);
    }

    private async Task<string> ShowChartAsync(ChartData data, string request, ArtifactMaker maker, CancellationToken ct)
    {
        var render = Render(data);
        await Save(data, render, request);
        var current = data;

        // "Edit with Claude…" asks for one sentence and revises the recipe that made this chart.
        async Task Edit(IChartWindow window)
        {
            var instruction = await window.AskAsync("Change this chart", "Make the bars blue, or group it by quarter");
            if (instruction is null || (_modelOverride is null && !await _ui.EnsureKeyAsync()))
            {
                return;
            }

            window.SetBusy(true);
            Say(new Agent(new AgentEvent("request")));
            try
            {
                var revised = await maker.EditChartAsync(ChartSpecJson.Write(current.Spec), instruction, new Overlay(WorkspaceRoot), _policy);
                var next = Render(revised);
                await Save(revised, next, request + " — " + instruction);
                await window.UpdateAsync(next.AltText, next.Svg);
                current = revised;
                Say(new Finished("Changed the chart", Undoable: true));
            }
            catch (Exception e) when (e is PlannerException or BudgetExceededException)
            {
                Say(new Failed(e.Message));
            }
            finally
            {
                window.SetBusy(false);
            }
        }

        await _ui.ShowChartAsync(render.AltText, render.Svg, render.Size.Width, render.Size.Height, Edit);
        Say(new Finished($"Charted {data.SourceRows:N0} {(data.SourceRows == 1 ? "row" : "rows")} on this PC", Undoable: true));
        _state.Log("chart", Audit.Of(("rows", Audit.Str(data.SourceRows.ToString(System.Globalization.CultureInfo.InvariantCulture))), ("claude_saw", Audit.Str("profile only"))));
        return $"Charted {data.SourceRows:N0} rows";
    }

    private ChartRender Render(ChartData data)
    {
        var theme = ThemeResolver.Resolve(_appearance());
        var size = ChartRenderer.PreferredSize(data);
        return ChartRenderer.Render(data, new ChartStyle(theme, size.Width, size.Height));
    }

    /// <summary>A chart is two new files, so it saves straight away with Undo available.</summary>
    private async Task Save(ChartData data, ChartRender render, string request)
    {
        var slug = new string((data.Spec.Title ?? $"{data.YColumn.Name}-by-{data.XColumn.Name}").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        slug = slug.Length == 0 ? "chart" : slug[..Math.Min(slug.Length, 48)];
        var dir = $"Claude/Artifacts/{DateTime.Now:yyyy-MM-dd}";
        var overlay = new Overlay(WorkspaceRoot);
        var name = slug;
        for (var n = 2; overlay.Read($"{dir}/{name}.svg") is not null; n++)
        {
            name = $"{slug}-{n}";
        }

        var plan = new Plan($"chart: {request}", "Saves the chart recipe and its picture as new files.", [
            new WriteFile($"{dir}/{name}.chart.json", ChartSpecJson.Write(data.Spec) + "\n"),
            new WriteFile($"{dir}/{name}.svg", render.Svg),
        ]);
        await Creations.ApplyAsync(plan, overlay, _policy, _state);
    }

    // ------------------------------------------------------------------------------ widgets

    private async Task<string> MakeWidgetAsync(string text, ArtifactMaker maker, CancellationToken ct)
    {
        var proposal = await maker.MakeModAsync(text, _mods, new ViewBinder(_broker, _providers), ct);
        if (Directory.Exists(Path.Combine(_mods.Root, proposal.Manifest.Id)))
        {
            Say(new Failed($"You already have a mod called {proposal.Manifest.Name}"));
            return $"You already have a mod called {proposal.Manifest.Name}. Ask for it under another name.";
        }

        Say(new PlanReady(1, proposal.Capabilities.Length == 0 ? "Low" : proposal.Capabilities.Max(c => c.Risk).ToString(), proposal.Capabilities.Any(c => c.Risk == Risk.High)));
        if (!await _ui.ApproveAsync(ApprovalModel.FromMod(proposal)))
        {
            Say(new Declined());
            return "Okay. Nothing was added.";
        }

        Say(new Approved());
        await Creations.ApplyAsync(proposal.Plan, new Overlay(_mods.Root), _policy, _state, ct: ct);
        _mods.RecordApproval(proposal.Manifest, proposal.ManifestJson);
        ShowWidget(proposal.Manifest);
        Say(new Finished($"Added {proposal.Manifest.Name}", Undoable: true));
        return $"Added {proposal.Manifest.Name}";
    }

    private void ShowWidget(ModManifest manifest)
    {
        var binder = new ViewBinder(_broker, _providers);
        var placement = manifest.Placement ?? new WidgetPlacement("top-right", 240, 120);
        _ui.ShowWidget(manifest.Name, () => binder.Bind(manifest), placement);
    }

    // ------------------------------------------------------------------------------ everything else

    private async Task<string> PlanAsync(string text, ClaudePlanner planner, CancellationToken ct)
    {
        var overlay = new Overlay(WorkspaceRoot);
        _state.Log("intent", Audit.Of(("intent", Audit.Str(text))));
        var plan = await planner.PlanAsync(text, overlay, _policy, ct);

        var outcome = await Session.ReviewAndApplyAsync(
            plan,
            overlay,
            _policy,
            async (card, _) =>
            {
                Say(new PlanReady(card.Items.Length, card.Risk.ToString(), card.Items.Any(i => i.Effect == Effect.External)));
                var approved = await _ui.ApproveAsync(ApprovalModel.FromCard(card));
                Say(approved ? new Approved() : new Declined());
                return approved;
            },
            new OutboxConnector(_state.Outbox),
            _state,
            ct: ct);

        switch (outcome.Status)
        {
            case Session.Status.Refused:
                // Policy refused the whole plan before asking. Show why, so it is not a silent no.
                await _ui.ApproveAsync(ApprovalModel.FromCard(outcome.Card));
                Say(new Failed("That plan was refused by policy"));
                return "That plan was refused by policy. Nothing changed.";
            case Session.Status.Declined:
                return "Okay. Nothing changed.";
            default:
                var changes = outcome.Result?.Manifest is not null;
                var sent = outcome.Result?.External.Count ?? 0;
                var message = changes && sent > 0 ? "Done. Files changed, and the message is queued." : changes ? "Done" : sent > 0 ? "Done. The message is queued." : "Done";
                Say(new Finished(message, Undoable: changes));
                return message;
        }
    }

    private void Emit(AgentEvent e) => Say(new Agent(e));

    private void Say(PresenceEvent e) => _ui.Post(() => _presence.Handle(e));
}
