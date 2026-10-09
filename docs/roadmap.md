# Roadmap

Build the new part first: the intent, plan and consent loop. Hardware, kernel
and distribution work comes last, because stock Linux already does that well.

## Phase 0: orchestration core (this repository today)

- [x] Typed actions with effect classes (local, external)
- [x] Claude planner with scoped, read-only tools and policy feedback
- [x] Deterministic policy: protected paths, risk notes, trusted domains and hosts
- [x] Copy-on-write overlay: staged changes, diff preview, journaled commit, undo
- [x] Approval cards rendered from actions; plan-bound, expiring grants
- [x] Dry-run outbox for external actions; audit log
- [x] CLI: `do`, `apply`, `undo`, `log`

## Phase 1: real effects, still in the terminal

- Command execution in a bubblewrap sandbox (no network, writable overlay of the
  workspace), producing the same diff and commit flow
- Real connectors behind the consent loop: SMTP or provider mail drafts, HTTP,
  calendar; MCP servers as a generic connector
- Read PDFs and office documents (send them to the model as document blocks)
- Standing policy rules and a policy file
- Taint tracking from read files to outbound payloads

## Phase 2: the shell

- Intent Bar as a Wayland layer-shell overlay with streaming plan progress
- Approval cards as native UI
- Artifact canvas: generated dashboards, documents and scripts as views over
  files in the workspace

## Phase 3: context

- Local context service: hybrid search over files, past sessions and project
  state; encrypted and scoped per workspace
- Event triggers (file changes, notifications, calendar) instead of polling

## Phase 4: legacy software and local models

- UI control via the accessibility tree (AT-SPI), as typed actions
- Vision fallback through xdg-desktop-portal ScreenCast and RemoteDesktop
- Local model for routing, classification and offline planning
- microVM backend (Firecracker or Cloud Hypervisor) for untrusted code

## Phase 5: distribution

- Immutable image (for example Fedora Atomic or NixOS based) with the shell and
  services preinstalled

## Open questions

- **Name.** "Claude" is an Anthropic trademark. A community project using it in
  its name may need permission or a different name.
- **License.** Not chosen yet.
- **Which local model** for routing and offline use, and on what hardware floor.
