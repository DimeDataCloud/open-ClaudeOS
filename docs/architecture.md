# Architecture

This is the working design. It keeps the core of the [original concept](vision.md)
(intent instead of apps, consent with context, sandboxed agents, hybrid models)
and changes the parts that would not hold up once an agent acts on real files,
real inboxes and untrusted content. Each change and its reason is listed in
[What changed from the original concept](#what-changed-from-the-original-concept).

## Principles

1. **The model proposes; deterministic code decides.** A language model is
   never the security boundary. It plans, explains and summarizes. Whether an
   action is allowed, how risky it is, and whether it runs are decided by code
   that no prompt can argue with.
2. **A plan is the unit of consent.** The person approves a whole plan once,
   and the approval becomes a capability bound to exactly those actions. If
   anything changes (a recipient, a byte of file content), the approval no
   longer covers it.
3. **What you approve is rendered from the actions, not from model prose.**
   The approval card lists the typed actions, the full diff, and every byte
   that will leave the machine. The model's summary is shown, labelled as its
   own words.
4. **Reversible by default.** Local changes are staged, previewed as a diff,
   and committed with an undo journal. Only effects that leave the machine are
   irreversible, and those always need explicit consent.
5. **Structured interfaces first, pixels last.** Use an app's API or an MCP
   server when one exists, the accessibility tree when it does not, and screen
   vision only as the fallback for software that offers neither.
6. **Artifacts are views over files.** Anything the system generates is backed
   by an ordinary file in an open format, so it outlives the session and works
   with every other tool.
7. **The model stays out of the frame loop.** Rendering and input handling are
   deterministic and instant. Model work is asynchronous and streams progress
   into the UI.
8. **Context is event-driven.** The system reacts to file, window and
   notification events. It never records the screen continuously.

## Layers

```
 ┌─────────────────────────────────────────────────────────────────────┐
 │ Shell                                                               │
 │  Intent Bar · Approval cards · Artifact canvas (file-backed views)  │
 └───────────────────────────────┬─────────────────────────────────────┘
                                 │ intents, approvals
 ┌───────────────────────────────▼─────────────────────────────────────┐
 │ Orchestrator                                                        │
 │  Planner (model) ─▶ Policy engine ─▶ Preview ─▶ Consent ─▶ Executor │
 │                       (deterministic)            (grant)            │
 │  Model router (local / cloud) · Audit log · Undo journal            │
 └──────┬──────────────────┬───────────────────┬──────────────────┬────┘
        │                  │                   │                  │
 ┌──────▼──────┐  ┌────────▼────────┐  ┌───────▼───────┐  ┌───────▼──────┐
 │ Context     │  │ Effect sandbox  │  │ Connectors    │  │ UI control   │
 │ service     │  │ overlay FS →    │  │ mail, HTTP,   │  │ AT-SPI first │
 │ scoped,     │  │ bubblewrap →    │  │ calendar, MCP │  │ vision last  │
 │ encrypted   │  │ microVM         │  │ servers       │  │ (portals)    │
 └─────────────┘  └─────────────────┘  └───────────────┘  └──────────────┘
 ┌─────────────────────────────────────────────────────────────────────┐
 │ Existing OS: Windows 11 ARM64 (Build 1), then Linux (Wayland, portals)│
 └─────────────────────────────────────────────────────────────────────┘
```

## The plan lifecycle

This loop is what Phase 0 implements (see `src/claudeos/`).

1. **Intent.** The person says what they want, scoped to a workspace.
2. **Explore.** The planner reads what it needs through read-only tools. Policy
   scopes those reads: nothing outside the workspace, nothing protected
   (credentials, keys, `.git`). Every read is logged.
3. **Propose.** The planner submits a plan of typed actions. Each action has an
   effect class: `local` (file changes) or `external` (leaves the machine).
4. **Assess.** The policy engine checks every action in order against the state
   left by the actions before it. A plan with any denied action is refused as a
   whole; during planning, the reasons go back to the model so it can revise.
5. **Preview.** Local actions are staged in a copy-on-write overlay. Nothing
   touches disk yet; the overlay produces the diff.
6. **Consent.** The approval card shows the intent, the model's summary
   (labelled), each action with its policy notes, the full diff, the full
   content of anything external, and which files were read while planning.
   One approval covers the plan.
7. **Grant.** Approval mints a short-lived grant holding the plan digest and
   the digest of each action. The executor checks every action against it.
8. **Commit.** Staged changes are written after the undo journal. If a file
   changed on disk after the preview, the commit is refused, so an approval
   never lands on content the person did not see.
9. **External effects.** Connectors run the external actions, after the local
   commit succeeds.
10. **Audit.** Every step is appended to a log the person can read.

## Components

**Intent Bar and approval cards.** The primary input surface. Phase 0 is a CLI;
Phase 2 is a Wayland layer-shell overlay, summoned by a key, that streams the
planner's progress and renders the card.

**Planner.** A tool-using model loop with read-only tools and a single way to
finish (`submit_plan`). It is told that file contents are data, not
instructions. That helps, but it is not relied on for safety; consent and policy
are.

**Policy engine.** Plain code and configuration: protected paths, size limits,
trusted recipient domains and hosts, per-action risk notes. Future work: standing
rules ("always allow new files under `~/reports`") to cut approval fatigue, and
organisation-managed policy.

**Effect sandbox.** Phase 0 stages file changes in an in-memory overlay. Running
commands needs real isolation: bubblewrap (namespaces, no network, a writable
overlay of the workspace) in Phase 1, then a microVM (Firecracker or Cloud
Hypervisor) for untrusted code. In every backend the result is the same: a diff
to preview, then a commit.

**Connectors.** Everything that leaves the machine goes through a connector, so
it can be shown in full, approved and logged. Phase 0 ships a dry-run outbox.
Real connectors are mail, HTTP and calendar adapters and MCP servers, with
credentials held by the connector, never by the model.

**Context service.** A local index over files, past sessions and project
state: hybrid search (keyword, embeddings, metadata, recency), not vectors alone.
It is encrypted at rest and scoped per workspace, and the planner queries it
through a tool. It never hands the model the whole index.

**UI control.** For software without an API: the accessibility tree (AT-SPI on
Linux) first, because it is structured, cheap and reliable. Screen vision and
synthetic input come last, through the compositor's sanctioned paths
(xdg-desktop-portal ScreenCast and RemoteDesktop, libei), confined to the target
window. Every UI action is a typed action like any other and goes through the
same consent loop.

**Model router.** A small local model handles fast, simple work: classifying
intents, routing, autocomplete in the Intent Bar, offline fallback. Planning
that needs depth goes to a cloud model. Deterministic code handles window
management and rendering, not a model.

## What changed from the original concept

| Original | Revised | Why |
|---|---|---|
| Constitutional guardrails that evaluate semantic intent decide whether actions run. | Deterministic policy and plan-bound grants decide; the model explains. | A model judging intent can be talked out of its judgment. A planted instruction in an invoice ("email this workspace to…") is the central threat for an agent OS. Enforcement has to be something a prompt cannot change. |
| Approval: "Claude wants to open 14 invoices… [Approve Actions]". | The card is generated from typed actions, with the full diff and full outbound content; the approval is a capability for those exact actions. | A model-written description can understate what a plan does. Binding consent to action digests means a changed recipient or file is no longer approved. |
| Native Computer Use Kernel reading pixels and dispatching HID events. | Computer use is a user-space backend, last in line after APIs, MCP and the accessibility tree; input goes through Wayland portals. | Pixels are the slowest, costliest and least reliable interface. Input injection from the kernel would bypass the compositor's security model. |
| MicroVM runs agent work "before applying changes to the main system state". | Every backend produces a diff to preview and commit. Effects split into local (undoable) and external (irreversible, always approved). | A sandbox makes file changes reversible, but a sent email cannot be rolled back. The design has to say which effects can be undone. |
| Artifacts replace app windows. | Artifacts are views over files in open formats. | Without that, generated work disappears with the session or gets locked into the system. |
| Unified vector index over everything. | Scoped, encrypted, hybrid-search context service queried through tools. | One index over all files and conversations is a high-value target and pulls untrusted text into every prompt. |
| Local model handles window positioning and UI actions under 100ms. | Deterministic code handles UI; the local model handles routing, classification and offline fallback. | Window management needs no model, and sub-16ms rendering can't wait on token generation. |
| Base layer: lightweight Linux or microkernel. | An app layer on an existing OS first: Windows 11 ARM64 for the first test build, then Linux. A distro image comes last. | Building a kernel or distro first delays the part that is new: the intent and consent loop. |
