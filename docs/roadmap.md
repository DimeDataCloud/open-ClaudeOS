# Roadmap

Build the new part first: the intent, plan and consent loop. Hardware, kernel
and distribution work comes last, because stock Linux already does that well.
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

- Choose a license and settle the project name
- `CONTRIBUTING.md`, code of conduct, `SECURITY.md` with private reporting
- CI: tests, lint, and the injection corpus as a required check
- Define the trusted core (policy, consent, executor, sandbox) and its review rules

**Exit test:** a newcomer can clone, run the demo and open a pull request
using only the repository docs, and security reports have a private channel.

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
- **License.** Apache-2.0 recommended; not yet chosen.
- **Local model** for routing and offline use, and the hardware floor.
- **Fast classifiers** (such as the Jev model under review) as advisory risk
  signals: whether they help, and where they must not be used.
