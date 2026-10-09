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
| **C# core** | `windows/src/ClaudeOS.Core` | Actions, policy, consent, overlay and undo, intent grammar and router, layout engine, charts, mods, presence, planner, token ledger. 233 xUnit tests on Linux and Windows. Zero dependencies; Native AOT verified in CI |
| Claude adapter | `windows/src/ClaudeOS.Claude` | The only reference to the official Anthropic SDK, behind `IModelClient`. Tested against a fake API; **never run against the live API** (no key in CI) |
| CLI | `windows/src/ClaudeOS.Cli` | `do`, `apply`, `undo`, `log`, `chart`, `route`, `theme`, `demo-data`. Runs the whole core on any OS |
| **Windows shell** | `windows/src/ClaudeOS.Shell` | WinUI 3 on Windows App SDK 2.5.1: Intent Bar, presence orb, subject-only chart windows, approval window with hold-to-approve, widget windows, key entry, tray, device check, a self-test. Compiles for x64 and ARM64 in CI and packages as a signed MSIX. Compiles, installs and passes its own self-test on a Windows desktop in CI; **not yet run on your Surface** |
| Design system | `design/` | `tokens.json` → CSS, XAML and C# (CI fails on drift); interactive prototype driven by real core output; the screenshots in `docs/images` |
| Docs | `docs/`, `README.md` | [design](docs/design.md), [testing](docs/testing.md), architecture, challenges, threat model, stack decision (with an "as built" section), Windows plan (with milestone status), roadmap |
| CI | `.github/workflows/` | `ci.yml` (Python, .NET on Linux and Windows, design sync, Native AOT guard), `shell.yml` (compile x64 and ARM64), `package.yml` (signed MSIX, a run-it self-test on a Windows desktop, an AOT probe of the shell) |

## Decisions made in this session

You asked for no questions, so these defaults were taken; each is easy to change.

- **License: Apache-2.0** (the recommended option). `LICENSE`, `NOTICE`.
- **Name unchanged.** "Claude" is Anthropic's trademark; the README says so. Settle
  the name, or get permission, before a wide release.
- **Stack as decided, with two measured changes** (details: [stack-decision.md, "As built"](docs/stack-decision.md#as-built-2026-10-09)):
  frameless WinUI windows instead of XAML islands in Win32 popups, and **no Native
  AOT for the shell**: the official Anthropic SDK needs reflection-based JSON and
  fails at runtime under AOT. The core and CLI are AOT-clean and CI guards it.
- **Charts are SVG from the core**, shown with `SvgImageSource`, so one renderer serves the
  shell, the CLI and the prototype.
- **Design**: warm paper and ink, one earned accent, springs not durations, the
  Spark as the only character. Principles and states in [docs/design.md](docs/design.md).
- **Consent stays deterministic.** The approval card is generated from typed
  actions; external actions need a hold; no model output approves anything.
- **Pull request base.** The repository had only the working branch, so there is
  no base to open a PR against. See "For you" below.

## For you (the owner)

1. **Install it on the Surface.** Actions → *Package (Windows)* → the latest run →
   artifact `open-ClaudeOS-ARM64` (also `x64`). Trust the `.cer` once (Local
   Machine → Trusted People), open the `.msix`, press `Ctrl+Alt+Space`. If the
   artifact is missing, GitHub's artifact storage quota for the account was
   exhausted (it recalculates every few hours); re-run the workflow later.
   [docs/testing.md](docs/testing.md) has the ten-minute walk-through.
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
5. **Pull request base.** Create `main` from the first commit (or rename this branch
   to `main` on GitHub), then open a PR from the working branch.

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
3. **On-device routing.** Add the Windows ML classifier behind `IIntentClassifier`;
   the grammar already handles the common phrases.
4. **More artifact types** (report, table, diagram) as new recipes, the same way
   charts work: Claude writes a compact spec, local code validates and renders it.
5. **Scripted and web mods**, only with their sandboxes (Jint with time and memory
   limits; WebView2 with no network by default).
6. **Linux shell** on the same core (Uno Platform is the likely route).

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
