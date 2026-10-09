# Roadmap

Build the new part first: the intent, plan and consent loop. Hardware, kernel
and driver work is left to the existing OS, because it already does that well.
The first build you can install runs on Windows 11 ARM64 (tested on a Surface
Pro); Linux follows with the same core.
Each phase has an exit test; a phase is done when its test passes, not when its
list is ticked. [challenges.md](challenges.md) explains the reasoning behind
each item.

## Phase 0: orchestration core (prototype exists)

- [x] Typed actions with effect classes (local, external)
- [x] Claude planner with scoped, read-only tools and policy feedback
- [x] Deterministic policy: protected paths, risk notes, trusted domains and hosts
- [x] Copy-on-write overlay: staged changes, diff preview, journaled commit, undo
- [x] Approval cards rendered from actions; plan-bound, expiring grants
- [x] Dry-run outbox for external actions; audit log
- [x] CLI: `do`, `apply`, `undo`, `log`

## Phase 0.5: open-source foundations

- [x] License: Apache-2.0 ([LICENSE](../LICENSE), [NOTICE](../NOTICE)); the name is still open
- [x] [CONTRIBUTING](../CONTRIBUTING.md), [code of conduct](../CODE_OF_CONDUCT.md), [SECURITY](../SECURITY.md) with private reporting
- [x] CI: Python and .NET tests on Linux and Windows, the Windows shell compiled for x64 and ARM64, a signed MSIX, a token-drift check
- [ ] The injection corpus as a required check (one case exists in the planner tests; it should grow into a corpus)
- [x] The trusted core is `ClaudeOS.Core/Safety`; changes to it are reviewed against [CONTRIBUTING](../CONTRIBUTING.md)

**Exit test:** a newcomer can clone, run the demo and open a pull request
using only the repository docs, and security reports have a private channel.

## Build 1: Windows on ARM test build (in progress)

An app layer on Windows 11 ARM64: Intent Bar, instant file opening placed in
free screen space, subject-only windows for generated charts, reports,
diagrams and images, mods, and the Phase 0 safety core. Full plan, stack
choice and milestones (M0 to M4): [windows-arm64-plan.md](windows-arm64-plan.md).

**Exit test:** the Surface test checklist in that plan passes. Per-milestone
status is in [windows-arm64-plan.md](windows-arm64-plan.md#where-each-milestone-stands).

## Phase 1: real effects, still in the terminal

- Command execution in a bubblewrap sandbox (no network, writable overlay),
  producing the same diff and commit flow
- Draft-first connectors (email drafts, HTTP, calendar), with credentials held
  by the connector; MCP servers as a generic connector
- Taint tracking from untrusted reads to outbound payloads
- Standing rules and a policy file
- Budgets: turns, repeated calls, time, tokens, money
- Provider interface: Claude, other hosted models, local runtimes
- Read PDFs and office documents

**Exit test:** every injection fixture is contained (no unapproved external
effect), every local effect can be undone, and a task stops cleanly when it
hits any budget.

## Phase 2: the desktop shell

Build 1 delivers a first Windows version of the shell. This phase finishes it
and brings it to Linux.

- Intent Bar as a Wayland layer-shell overlay: local matching first,
  streaming plan progress
- Approval cards as native UI that highlight what is unusual
- "Undo instead of asking" for low-risk local plans
- Artifact canvas: generated views over files, in a fixed shell frame
- Privileged broker for system changes

**Exit test:** the latency budgets in challenges.md §2.1 are met, and a small
opt-in usability study shows most prompts are for irreversible actions.

## Phase 3: context and events

- Local context service: hybrid search, encrypted, scoped per workspace
- Event triggers (file changes, notifications, calendar) instead of polling
- Quarantined reading of untrusted content (dual-LLM pattern)
- Offline tiers with a local model for routing and simple intents

**Exit test:** idle power overhead indistinguishable from the stock desktop,
and the system stays usable with the network off.

## Phase 4: legacy software and higher-stakes actions

- UI control through the accessibility tree, as typed actions with
  postcondition checks
- Vision fallback through xdg-desktop-portal ScreenCast and RemoteDesktop
- microVM backend (Firecracker or Cloud Hypervisor) for untrusted code
- Compensating actions for effects made through other apps
- Critical effect class (payments, filings) with step-up confirmation

**Exit test:** UI automation stops and asks on every unexpected screen state
in a test suite of app updates and pop-ups.

## Phase 5: distribution

- Immutable image (for example Fedora Atomic or NixOS based) with the shell and
  services preinstalled

## Open questions

- **Name.** "Claude" is an Anthropic trademark; a public project needs
  permission or a different name.
- **License.** Apache-2.0, taken as the recommended default so the project can be shared; the owner can still change it before a first release.
- **Local model** for routing and offline use, and the hardware floor.
- **Fast classifiers** (such as the Jev model under review) as advisory risk
  signals: whether they help, and where they must not be used.
