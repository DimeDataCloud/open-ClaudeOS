# Handoff

Where the project stands, what has been decided, and what comes next. Last
updated 2026-10-09.

## What open-ClaudeOS is

An open-source, intent-driven layer for the desktop: you say what you want, an
AI plans it, and deterministic code shows you exactly what will happen and
runs only what you approve. The idea behind it: **probabilistic planning,
deterministic execution.** The original concept is in
[docs/vision.md](docs/vision.md).

## What exists

| Item | Where | State |
|---|---|---|
| Phase 0 prototype: planner, policy, consent, overlay sandbox, undo, audit log, CLI | `src/claudeos/`, `tests/` | Working Python; 23 tests pass on Python 3.11 and 3.13. Kept as the reference design for the Windows build. |
| Demo workspace with a planted prompt injection | `examples/invoices-demo/` | Works with `claudeos apply` and `claudeos do` |
| Architecture and principles | [docs/architecture.md](docs/architecture.md) | Current |
| Plan for each hard problem | [docs/challenges.md](docs/challenges.md) | Current |
| Threat model | [docs/threat-model.md](docs/threat-model.md) | Current |
| Roadmap with an exit test per phase | [docs/roadmap.md](docs/roadmap.md) | Current |
| Build 1 plan: Windows on ARM test build for a Surface Pro | [docs/windows-arm64-plan.md](docs/windows-arm64-plan.md) | Plan only; nothing built |
| Research: Jev decision model | [reports/Jev decision model and Claude.md](reports/Jev%20decision%20model%20and%20Claude.md) | Done |
| Research: Googlebook OS and Gemini app creation | `reports/Googlebook OS and Gemini app creation.md`, notes in `research_notes/` | Notes done; report being written |

## Decisions made

- **Open source.** The license is not chosen yet; Apache-2.0 is recommended.
- **First platform: Windows 11 ARM64**, as an app layer, tested on the owner's
  Surface Pro. Linux follows on the same core.
- **Stack: native Windows.** C# on .NET 10, WinUI 3 (Windows App SDK),
  Win32 and DWM, and WebView2 only for custom HTML artifacts and web mods. The
  owner's criteria, in order: native feel, then latency, then token use.
  Electron and Tauri were considered and rejected.
- **The model never sits between input and the screen.** Opening and finding
  things uses no model; intent routing runs on the NPU.
- **Consent stays deterministic.** Fast classifiers such as Jev may route
  requests or add warnings, but never approve actions.
- **No more code until the owner says go.** The owner asked for plans before
  building; the Phase 0 prototype predates that request.

## Open decisions for the owner

1. **License:** Apache-2.0 recommended.
2. **Name:** "Claude" is an Anthropic trademark; a public project needs
   permission or a new name.
3. **Pull request base:** the repository has only this branch
   (`ccr-6c2d7909-nzeecq`), so there is nothing to open a PR against. Either
   create `main`, or rename this branch to `main` on GitHub.
4. **When to start Build 1**, beginning with the M0 spike in the Windows plan.

## Next

1. **Confirm the stack.** Stress-test the native .NET and WinUI choice
   against the alternatives on latency, token use and native feel.
2. **Visual design.** The brief: clean, intentional minimalism; macOS-like
   fluidity and simplicity; Windows-like customizability; a native backend; a
   Claude presence that comes alive to help, so using the computer feels like
   an extension of your mind.
3. **Build 1, milestones M0 to M4**, once the owner says go.

## Working on the repository

```sh
pip install -e ".[claude,dev]"
pytest                      # prototype tests, no network needed
claudeos apply examples/invoices-demo.plan.json --root /tmp/demo   # after copying the demo
```

- `research_notes/` holds raw research notes; `reports/` holds the finished
  reports built from them.
- Commits go to `ccr-6c2d7909-nzeecq`.
