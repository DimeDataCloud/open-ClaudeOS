# Testing

What is tested, where, and what is not. The project is built in a Linux
container and on GitHub Actions, and run on a Surface; this page is honest
about which of those has covered what.

## The shape of it

Everything that decides something lives in `ClaudeOS.Core`, a plain .NET
library with no UI and no dependencies, so it can be tested anywhere. The
WinUI shell only draws what the core says and calls Windows. That split is the
reason most of the product is under test even though the shell cannot run in a
container.

| Layer | How it is checked | Where it runs |
|---|---|---|
| Python reference (Phase 0) | `pytest`, 23 tests | CI (Ubuntu, Python 3.11 and 3.13) |
| Core logic | xUnit, 226 tests (`dotnet test windows/tests/ClaudeOS.Core.Tests`) | CI (Ubuntu and Windows) |
| Anthropic SDK adapter | xUnit against a local fake Messages API (`HttpListener`) | CI |
| Shell compiles | `dotnet build` of the WinUI project, x64 and ARM64 | CI (Windows runner) |
| Design tokens in sync | `node design/build-tokens.mjs --check` | CI |
| Shell runs correctly on a device | the in-app **Check this device** report, and the manual script below | On a Surface; not automated |

## Core tests

```sh
cd windows
dotnet test tests/ClaudeOS.Core.Tests
```

What they pin down:

- **Safety** (`Safety/`): the Python prototype's behaviour, ported test for test.
  Paths cannot escape the workspace (including through a symlinked directory);
  protected names are refused whatever their case, and names that mean something
  else on Windows are refused everywhere; the overlay stages changes without
  touching disk and produces a real unified diff; a commit writes an undo
  journal, refuses if a file changed after the preview, and undo refuses to
  overwrite a file you edited since; a failed commit can still be undone;
  consent grants are bound to the exact plan, so tampering between approval and
  execution is caught; a plan with any denied action is refused before you are
  asked; external actions are high risk and flag unknown recipients; the card
  shows the full outbound payload.
- **Byte-identical digests.** A plan has the same digest in Python and C#. A
  golden fixture (`fixtures/invoices-demo.plan.json`) fails the build if the
  canonical JSON differs by one byte.
- **Intent** (`Intent/`): the grammar handles polite phrasing, stacked
  politeness, typos, window commands and undo; unknown text goes on to the next
  stage, never to a guess. Routing records which stage answered and how long it
  took.
- **Layout** (`Layout/`): free-space search on real-shaped desktops (multi-monitor,
  mixed DPI, a taskbar, a maximized window), the fallback order, shrink steps,
  "put it back" including the case where you moved a window yourself since.
- **Design** (`Design/`): contrast of every role in both themes; any accent you
  can type yields a legible theme (including pure yellow, black and white);
  risk and diff colors cannot be themed away; the generated token files match
  `tokens.json`; the categorical palette stays separable under simulated
  colour-vision deficiency.
- **Artifacts** (`Artifacts/`): the CSV reader, the data profile Claude sees (and
  that its size does not depend on row count), each transform, the chart
  renderer (axes, labels, stacking, bucketed time axes, collision handling),
  and the chart recipe validator that rejects specs instead of guessing. A
  planning test asserts that Claude is shown the profile and not the data, and
  that the recipe it writes is run over every row.
- **Planning** (`Planning/`): the tool loop, the planner revising after policy
  feedback, the token ledger and its budgets, the artifact maker retrying on
  spec errors, and the SDK adapter against a fake API (stop reasons, refusals,
  rate limits, network failures, tool-use round trips).
- **Mods** (`Mods/`): strict manifests (unknown fields are errors), the view
  grammar and its limits, the capability broker checking every read, approval
  bound to the manifest digest (editing a mod re-opens review), disabled and
  invalid mods.
- **Presence** (`Presence/`): the state machine, driven event by event, including
  offline and reduced-motion behaviour.
- **Prompt injection.** `Prompt_injection_in_a_file_is_data_the_policy_still_contains`
  runs the invoices demo, where one invoice contains a planted instruction, and
  asserts that the injected email still reaches the card, flagged, and that
  nothing external runs without approval. `The_model_cannot_rewrite_the_intent`
  checks that a plan cannot change what you asked for.
- **Making things is not gated; changing things is.** `New_files_apply_straight_away_with_undo_and_nothing_else_does`
  pins the rule that only new-file creations skip the card.

No test calls the real Claude API; the planner is driven by a scripted model.
That keeps the suite free, offline and deterministic. It also means the live
behaviour of the prompts has **not** been exercised by this suite; see "Not
covered".

## The Windows shell in CI

Every push compiles the shell for x64 and ARM64 on a Windows runner. That proves
the XAML and C# are consistent and the packages restore; it does not prove the
app behaves. The `package` workflow additionally produces an installable
`.msix` for ARM64 (see the README for installing it).

## On the device: the manual script

The shell has to be run on a Windows 11 machine, ideally the ARM64 Surface it is
built for. Right-click the tray icon and choose **Check this device**; it
measures what this build can measure and reports it with the target from the
[stack decision](stack-decision.md). Then walk through this:

1. **Install.** Trust the `.cer` once (Local Machine, Trusted People), then
   open the `.msix`.
2. **Summon.** Press `Ctrl+Alt+Space` (or click the tray icon). The bar appears
   at once, centered, with the orb breathing. `Esc` closes it.
3. **Open, offline.** Disconnect from the network. Type `open q3 budget` (any
   file in Documents, Desktop or Downloads). Results show as you type. `Enter`
   opens it and the new window lands in free space without covering what you
   were doing.
4. **Windows.** Type `snap left`, then `put it back`.
5. **Connect Claude.** Tray menu, **Claude key…**. Paste a key. It is stored in
   the Windows credential store, not in a file.
6. **A chart.** Put a CSV in Documents. Type `chart spend by month from
   <file>.csv`. The chart opens as its own window with no title bar; drag it
   from anywhere; right-click for the menu; **Edit with Claude…** and say "make
   the bars blue".
7. **A plan.** Type `summarise the invoices in Documents/invoices into a
   spreadsheet and email finance@example.com the total`. The approval card
   lists each action. The email row is marked *Leaves this PC*, the button needs
   a hold. Decline, and confirm nothing changed. Run it again and approve, then
   say `undo`.
8. **A widget.** Type `make a battery widget`. The approval lists exactly what it
   can read. After approving, it floats in a corner and updates each second.
9. **Reduced motion.** Turn off animation effects in Windows settings;
   everything should still work with no movement.

## Not covered (and why you should know)

- **The shell has not been run by the author of this code.** It compiles; the
  first real run is the point of milestone M0. Expect polish bugs on first run
  (focus, DPI, shadow on frameless windows).
- **Live Claude behaviour.** The SDK adapter is tested against a fake server, and
  the prompts have been reviewed but not run against the live API in this
  repository's CI (it has no key, by design). The first run with a real key
  may need prompt tuning.
- **NPU routing, Windows OCR placement, Agent Launcher registration, global
  hotkey conflicts** are on the M0 list; the device check reports what it can
  and says plainly when something is not wired up yet.
- **Native AOT of the shell.** The core and CLI are verified as native binaries in
  CI; the shell is not AOT, because the official Anthropic SDK needs reflection
  (see the [stack decision](stack-decision.md)).
- **Accessibility with a real screen reader** (Narrator) is designed for
  (names, roles, keyboard paths) but not yet verified.
