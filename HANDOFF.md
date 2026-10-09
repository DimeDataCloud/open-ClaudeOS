# Handoff

Where the project stands, what has been decided, and how to start Build 1 in
a new session. Last updated 2026-10-09.

## What open-ClaudeOS is

An open-source, intent-driven layer for the desktop: you say what you want, an
AI plans it, and deterministic code shows you exactly what will happen and
runs only what you approve. The idea behind it: **probabilistic planning,
deterministic execution.** The original concept is in
[docs/vision.md](docs/vision.md).

## What exists

| Item | Where | State |
|---|---|---|
| Phase 0 prototype: planner, policy, consent, overlay sandbox, undo, audit log, CLI | `src/claudeos/`, `tests/` | Working Python; 23 tests pass on Python 3.11 and 3.13. It is the reference design: its behaviour and tests get ported to C#. |
| Demo workspace with a planted prompt injection | `examples/invoices-demo/` | Works with `claudeos apply` and `claudeos do` |
| Architecture and principles | [docs/architecture.md](docs/architecture.md) | Current |
| Plan for each hard problem | [docs/challenges.md](docs/challenges.md) | Current |
| Threat model | [docs/threat-model.md](docs/threat-model.md) | Current |
| Roadmap with an exit test per phase | [docs/roadmap.md](docs/roadmap.md) | Current |
| **Build 1 plan**: Windows on ARM test build for a Surface Pro | [docs/windows-arm64-plan.md](docs/windows-arm64-plan.md) | Final plan; nothing built yet |
| **Stack decision record** | [docs/stack-decision.md](docs/stack-decision.md) | Decided; includes the M0 measurements that would trigger fallbacks |
| Research: Jev decision model | [reports/Jev decision model and Claude.md](reports/Jev%20decision%20model%20and%20Claude.md) | Done |
| Research: Googlebook OS and Gemini app creation | Notes in `research_notes/Googlebook OS and Gemini app creation/` (four files) | Notes complete. The written report was not finished before this handoff; generate it from the notes if needed. The key findings are already folded into the Windows plan. |

## Decisions made

- **Open source.** The license is not chosen yet; Apache-2.0 is recommended.
- **First platform: Windows 11 ARM64**, as an app layer (not a new OS),
  tested on the owner's Surface Pro (Snapdragon X, 120 Hz touch display, 45
  TOPS NPU). Linux follows on the same core.
- **Stack** (details and evidence in docs/stack-decision.md):
  - C# on .NET 10 with Native AOT.
  - WinUI 3 (Windows App SDK) for structure, with the 2026 performance
    optimizations turned on.
  - Windows Composition and Win2D for motion and custom drawing, so
    animation runs on the system compositor at 120 Hz.
  - Win32 and DWM (through CsWin32) for windows; subject-only windows are
    Win32 popups hosting XAML islands.
  - Windows ML with the Qualcomm NPU provider for on-device models: the
    intent classifier, Whisper, and later Aion Instruct. Not Phi Silica,
    which needs a token and is being removed in January 2027.
  - The official `Anthropic` C# SDK, pinned while it is in beta, behind one
    interface. Default model `claude-opus-5-5`.
  - WebView2 only for custom HTML artifacts and web mods; Jint for mod
    scripts.
  - MSIX packaging, signed with a project test certificate.
  - Rejected: Electron, Tauri, WPF, all-C++, Rust by hand, React Native,
    Flutter and Avalonia. Uno Platform is kept as the likely Linux shell.
- **The model never sits between input and the screen.** Opening and finding
  things uses no model; intent routing runs on the NPU.
- **Consent stays deterministic.** Fast classifiers (Jev, or our own) may
  route requests or add warnings, but never approve actions.
- **Placement is geometry, not AI.** The layout engine puts content into
  free screen space deterministically.

## Start here: Build 1, milestone M0 (the spike)

The owner will start the build in a new session. M0's goal is to prove the
risky parts on the real Surface before building features. The full scope is
the M0 row in docs/windows-arm64-plan.md, and the pass/fail targets are in
docs/stack-decision.md, under "What would change this decision".

**Suggested repository layout** (new code goes under `windows/`; the Python
prototype stays where it is):

```
windows/
  ClaudeOS.sln
  src/ClaudeOS.Core/          plain .NET class library: actions, plans, policy, consent,
                              overlay and undo, layout-engine maths, planner interface
  src/ClaudeOS.Shell/         WinUI 3 packaged app (single-project MSIX), win-arm64, Native AOT
  tests/ClaudeOS.Core.Tests/  xUnit; port the Python tests first
.github/workflows/windows-build.yml
```

**M0 checklist**, each item to be shown working on the Surface or given a
recorded fallback:

1. Packaged ARM64 app installs with the test certificate.
2. Global hotkey shows a pre-created Intent Bar in under 50 ms. Test whether
   the Copilot key can be assigned to the app.
3. A frameless Win32 popup hosting a XAML island, with DWM rounded corners,
   shadow and Mica, shows a Win2D chart in under 100 ms.
4. A Composition animation on the bar holds 120 Hz.
5. List visible windows with their true bounds, compute free space, and move
   one window.
6. A tiny intent classifier runs on the NPU through Windows ML in under 20 ms.
7. Windows OCR runs.
8. Register as an Agent Launcher (Windows preview feature).
9. A Native AOT publish works.
10. The resident app idles at under 150 MB.

**Practical constraints for the build session:**

- **Building.** WinUI 3 apps can't be built or run in the Linux cloud
  container, so the Shell must be built by GitHub Actions on a Windows runner,
  publishing `win-arm64`. `ClaudeOS.Core` and its tests are plain .NET and can
  be built and tested in the container if the .NET 10 SDK is installed.
- **Signing.** CI signs the MSIX with a test certificate. Store the PFX and
  its password as repository secrets, never in the repo. The certificate
  subject must match the manifest's `Publisher`. The owner installs the
  `.cer` once into Local Machine → Trusted People.
- **Smart App Control.** If it blocks the build, fix the signing; never ask
  the owner to turn it off.
- **API key.** Asked for on first run and stored with Windows' per-user
  encryption (DPAPI). Never committed.
- **Git.** Commit and push to `ccr-6c2d7909-nzeecq` (the only branch; see open
  decision 3).

## Open decisions for the owner

1. **License:** Apache-2.0 recommended.
2. **Name:** "Claude" is an Anthropic trademark; a public project needs
   permission or a new name.
3. **Pull request base:** the repository has only `ccr-6c2d7909-nzeecq`, so
   there is nothing to open a PR against. Either create `main`, or rename
   this branch to `main` on GitHub.

## Deferred: visual design

Design was paused before it started, so the build can begin first. The
owner's brief, to pick up later:

- clean, intentional minimalism;
- macOS-like fluidity and simplicity;
- Windows-like customizability, with a native backend;
- a Claude presence that comes alive to help you use your computer in a new
  way, saving time and making it feel like an extension of your mind.

The Windows plan already fixes the structural pieces: subject-only windows,
the Intent Bar, approval cards, mods with theme tokens, and motion on the
compositor. A "Design" artifact type is available in the owner's account for
mockups.

## Working on the repository

```sh
pip install -e ".[claude,dev]"
pytest                      # prototype tests, no network needed
cp -r examples/invoices-demo /tmp/demo
claudeos apply examples/invoices-demo.plan.json --root /tmp/demo
```

- `research_notes/` holds raw research notes; `reports/` holds the finished
  reports built from them.
- Commit messages end with the Co-Authored-By and Claude-Session lines used
  throughout this history.
