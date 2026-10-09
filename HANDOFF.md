# Handoff

Where the project stands, what has been decided, and how to pick it up. Last
updated 2026-10-09, at the end of the first build session.

## What open-ClaudeOS is

An open-source, intent-driven layer for the desktop: you say what you want, an
AI plans it, and deterministic code shows you exactly what will happen and runs
only what you approve. **Probabilistic planning, deterministic execution.** The
README is the front door; [docs/vision.md](docs/vision.md) is the original
concept.

## What exists

| Item | Where | State |
|---|---|---|
| Python reference (Phase 0) | `src/claudeos/`, `tests/` | 23 tests. The executable spec the C# core was ported from; plans have byte-identical digests in both |
| **C# core** | `windows/src/ClaudeOS.Core` | Actions, policy, consent, overlay and undo, intent grammar and router, layout engine, charts, mods, presence, planner, token ledger. 434 xUnit tests, passing on Linux and on Windows in CI. Zero dependencies; Native AOT verified in CI |
| Claude adapter | `windows/src/ClaudeOS.Claude` | The only reference to the official Anthropic SDK, behind `IModelClient`. Tested against a fake API; **never run against the live API** (no key in CI) |
| CLI | `windows/src/ClaudeOS.Cli` | `do`, `apply`, `undo`, `log`, `chart`, `route`, `theme`, `demo-data`. Runs the whole core on any OS |
| **Windows shell** | `windows/src/ClaudeOS.Shell` | WinUI 3 on Windows App SDK 2.5.1: Intent Bar, presence orb, subject-only chart windows, approval window with hold-to-approve, widget windows, key entry, tray, device check, a self-test. Compiles for x64 and ARM64 in CI and packages as a signed MSIX. Compiles, installs and passes its own self-test on a Windows desktop in CI; **not yet run on your Surface** |
| Design system | `design/` | `tokens.json` → CSS, XAML and C# (CI fails on drift); interactive prototype driven by real core output; the screenshots in `docs/images` |
| Docs | `docs/`, `README.md` | [design](docs/design.md), [testing](docs/testing.md), architecture, challenges, threat model, stack decision (with an "as built" section), Windows plan (with milestone status), roadmap |
| CI | `.github/workflows/` | `ci.yml` (Python, .NET on Linux and Windows, design sync, Native AOT guard), `shell.yml` (compile x64 and ARM64), `package.yml` (signed MSIX, a run-it self-test on a Windows desktop, an AOT probe of the shell) |

## Where it stands (end of the first session, 2026-10-09)

- **Branch and pull request.** Work is on `ccr-6c2d7909-nzeecq` (draft PR #1 into `main`),
  pushed and clean. The repository is public.
- **CI is green.** Every job passes at `4647a75`, including the signed MSIX for x64 and ARM64
  and the install-and-self-test on a Windows desktop (which also takes the screenshots in
  `docs/images`). The rolling **Latest CI build** release carries the packages from that run.
- **Native AOT of the shell, measured again.** The probe (it runs with every package build, as the
  *Native AOT probe* job, and never gates anything) published the shell for `win-x64` without errors.
  The warnings are the Anthropic SDK's own trim and AOT warnings, plus three small ones in our
  code that are worth tidying (`PresenceOrb` should be `partial`; `IntentBarWindow.xaml.cs:112`
  compares references with `==`; `SelfTest.cs:163` dereferences a possible null). It compiles; it was not run,
  and it would fail at the first Claude request, so the shell still ships on the regular runtime.
- **Tested:** 434 xUnit tests (Linux and Windows), 23 Python tests, about 91% line coverage of the
  hand-written core, injection and fuzz suites on the safety code, differential tests against Python.
- **Not tested, and cannot be from CI:** the app on a Surface, any real Claude call (no key was
  ever available), a restart with a saved `habits.json` on Windows, and hands-on use.
- **Not built:** web mods, a learned NPU model, a graphical Linux shell, dedicated windows for
  table, report and diagram artifacts.
- **Open questions for you:** the name, private vulnerability reporting, and the PR/default
  branch arrangement (all under "For you" below).

## Decisions made in this session

You asked for no questions, so these defaults were taken; each is easy to change.

- **License: Apache-2.0** (the recommended option). `LICENSE`, `NOTICE`.
- **Name unchanged.** "Claude" is Anthropic's trademark; the README says so. Settle
  the name, or get permission, before a wide release.
- **Stack as decided, with two measured changes** (details: [stack-decision.md, "As built"](docs/stack-decision.md#as-built-2026-10-09)):
  frameless WinUI windows instead of XAML islands in Win32 popups, and **no Native
  AOT for the shell**: the official Anthropic SDK needs reflection-based JSON and
  fails at runtime under AOT. The core and CLI are AOT-clean and CI guards it.
- **Charts are SVG from the core, drawn natively.** One renderer serves the shell, the CLI
  and the prototype. In the shell, `SvgScene` turns the SVG subset the renderer emits into
  XAML shapes and text, because the system's SVG image control drops `<text>` and
  8-digit colours (found by the first CI screenshots: charts came out as bare bars).
- **Design**: warm paper and ink, one earned accent, springs not durations, the
  Spark as the only character. Principles and states in [docs/design.md](docs/design.md).
- **Consent stays deterministic.** The approval card is generated from typed
  actions; external actions need a hold; no model output approves anything.
- **Pull request base.** The repository had only the working branch, so there is
  no base to open a PR against. See "For you" below.

## For you (the owner)

0. **Published, and CI is green.** The repository is public and every job passed on
   `64c2d8d` and again on `4647a75`: the Python reference, the core on Linux and Windows, the design
   sync, the Native AOT build, both Windows compiles of the shell, and the signed MSIX for x64
   and ARM64. That includes the shell changes made after `489180f` (habit persistence and the
   on-device classifier in the bar), which had not been compiled before. Still to do on the
   GitHub side: turn on **private vulnerability reporting** (Settings → Code security) because
   [SECURITY.md](SECURITY.md) points reporters there; decide the name, because "Claude" is
   Anthropic's trademark ([NOTICE](NOTICE)); and merge or retarget the pull request (its base is
   `main`; the default branch is currently the working branch). `research_notes/` and `reports/`
   were read through before publication: they are public-source research, and the few
   third-party email addresses they quoted from package registries were removed.

1. **Install it on the Surface.** Releases → **Latest CI build** (a rolling pre-release
   the package workflow refreshes on every push): download
   `ClaudeOS.Shell_*_ARM64.msix` and `claudeos-test-ARM64.cer`. Trust the `.cer` once
   (Local Machine → Trusted People), open the `.msix`, press `Ctrl+Alt+Space`. (GitHub
   Actions artifacts are not used: this account's artifact storage was full, so
   uploads silently vanished.) [docs/testing.md](docs/testing.md) has the ten-minute
   walk-through.
2. **Run "Check this device"** from the tray menu and keep the report: it measures
   the M0 targets (hotkey latency, memory, frame rate, window enumeration, OCR).
3. **Stable signing (optional).** Run `tools/New-TestCertificate.ps1` once and
   store the PFX (base64) and its password as the repository secrets
   `CLAUDEOS_TEST_PFX_BASE64` and `CLAUDEOS_TEST_PFX_PASSWORD`. Then every build is
   signed by the same certificate and you trust it only once. Never commit them.
4. **Add your API key** from the tray menu (**Claude key…**). It goes to the Windows
   credential store, not a file. Then try the walk-through's chart and plan steps;
   this is the first contact between the prompts and the live API, so expect to tune
   them.
5. **Pull request.** `main` did not exist, so it was created at `f2ea2a3` (the commit before this
   build started) and a draft PR (#1) was opened from the working branch. Review it, and
   change the base or the default branch if you prefer another arrangement.

## What is next

In order:

1. **M0 on the device.** The shell has been run only by its own self-test on a
   GitHub-hosted Windows x64 VM (all checks pass, including the planted-email plan
   through the real approval window; see [docs/testing.md](docs/testing.md)).
   Real-device issues will be about focus, DPI, shadow on
   frameless windows, Windows' acrylic, and the Copilot key. The checklist and the
   fallbacks are in [stack-decision.md](docs/stack-decision.md#what-would-change-this-decision).
2. **Live Claude pass.** Run the walk-through with a real key; tune the system prompts
   in `ClaudeOS.Core/Planning` (they are plain constants with tests around them).
3. **On-device routing, the NPU part.** A CPU classifier already sits between the grammar and the
   cloud (`LocalIntentClassifier`, measured on unseen phrases: see [testing](docs/testing.md)).
   What is left is a learned model on the NPU behind the same `IIntentClassifier`, if M0 shows
   it is worth it. Real phrases that route wrongly belong in `IntentTraining`.
4. **Habits, further.** The loop works end to end (three drags, an offer in the bar, "yes",
   a card, a layout-rule mod, the next chart lands there). What it has seen and which offers it
   has made are saved in `habits.json` in the state folder, so a restart keeps a habit in
   progress and never repeats an offer, answered either way (written and tested in the core;
   the shell wiring compiles and the Windows self-test drives the whole loop in CI, but with the saved
   file isolated, so a real restart on your machine is not yet checked). Still worth adding: rules for files opened
   from the bar.
5. **More artifact types** (report, table, diagram) as new recipes, the same way
   charts work: Claude writes a compact spec, local code validates and renders it. Today these
   requests go to the planner, which can write new files (a spreadsheet, a document) with no
   card and an Undo; a recipe would add a window, a live preview and "Edit with Claude".
6. **Web mods**, only with their sandbox (WebView2 with no network by default). Scripted
   mods exist now as formulas (see `examples/mods` and `claudeos mod`); a general scripting
   engine is not needed until a real mod outgrows them.
7. **A graphical Linux shell** on the same core (Uno Platform is the likely route). A terminal Intent
   Bar (`claudeos shell`) already runs on Linux and macOS, including as a native binary, so the
   core is proven portable; what is missing is the graphical front end.

## Working on it

```sh
# core, CLI and tests (.NET 10 SDK)
cd windows && dotnet test tests/ClaudeOS.Core.Tests
dotnet run --project src/ClaudeOS.Cli -- apply ../examples/invoices-demo.plan.json --root /tmp/demo

# the Python reference
pip install -e ".[claude,dev]" && pytest

# design: regenerate everything derived from tokens.json, and the prototype's data
cd design && node build-tokens.mjs
dotnet run --project ../windows/src/ClaudeOS.Cli -- demo-data
```

- The Windows shell only builds on Windows. To iterate on it from anywhere, push and read
  the *Shell (Windows)* run: it prints only the compiler errors. A run builds in about
  two and a half minutes.
- `research_notes/` and `reports/` hold the research behind the design decisions.
- Commit messages end with the Co-Authored-By and Claude-Session lines used throughout
  this history. Secrets, keys and certificates never go in the repository.
