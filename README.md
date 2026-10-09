# open-ClaudeOS

An intent-centric layer for the desktop. You say what you want done; an agent
works out a plan; the system shows you exactly what will happen, and does only
that once you approve.

> Independent community project, not affiliated with or endorsed by Anthropic.
> "Claude" is a trademark of Anthropic.

## The idea

Today's operating systems are app-centric: you open applications, arrange
windows and copy data between them by hand. The idea here is an intent-centric
OS, where natural language, context and agents drive the work, and apps become
one of several ways to get it done.

- [Handoff](HANDOFF.md): where the project stands, decisions made, and what comes next
- [Vision](docs/vision.md): the original concept, as first written
- [Architecture](docs/architecture.md): the working design, and what changed from the concept and why
- [Challenges](docs/challenges.md): the hard problems (security, latency, recovery, power, legacy apps, economics) and the plan for each
- [Build 1: Windows on ARM](docs/windows-arm64-plan.md): the first test build for a Surface Pro: instant file opening, subject-only artifact windows, mods
- [Threat model](docs/threat-model.md): prompt injection and how the design contains it
- [Roadmap](docs/roadmap.md): phases from this prototype to a desktop shell, each with an exit test

## The core loop

```
intent ─▶ explore (read-only, scoped) ─▶ plan ─▶ policy check ─▶ preview (staged diff)
       ─▶ one approval ─▶ grant for those exact actions ─▶ commit (undo journal)
       ─▶ external effects ─▶ audit log
```

1. **The model proposes; deterministic code decides.** Policy and enforcement
   are plain code. No prompt, including one hidden in a file the agent reads,
   can change them.
2. **Consent is per plan and bound to its actions.** The approval card is
   generated from the typed actions (full diff, full outbound content), not
   from the model's description. The approval covers those actions and nothing
   else.
3. **Local changes are undoable; external ones are explicit.** File changes are
   staged, previewed and journaled. Anything that leaves the machine is shown
   in full and always needs approval.

## What is here today: Phase 0

A Python CLI that runs that loop for file and messaging tasks inside one
workspace directory. There is no desktop shell yet; the terminal stands in for
the Intent Bar.

| Module | Role |
|---|---|
| [`planner.py`](src/claudeos/planner.py) | Claude planner: read-only tools, `submit_plan`, revises on policy feedback |
| [`actions.py`](src/claudeos/actions.py) | Typed actions with effect classes (`local`, `external`) |
| [`policy.py`](src/claudeos/policy.py) | Deterministic policy: protected paths, risk notes, trusted domains |
| [`sandbox.py`](src/claudeos/sandbox.py) | Copy-on-write overlay: staged changes, diff, journaled commit, undo |
| [`consent.py`](src/claudeos/consent.py) | Approval card and plan-bound, expiring grants |
| [`executor.py`](src/claudeos/executor.py) | Runs only granted actions; dry-run outbox connector |
| [`session.py`](src/claudeos/session.py), [`cli.py`](src/claudeos/cli.py) | The loop, and the `claudeos` command |

## Try it

```sh
pip install -e ".[claude]"

# Work on a copy of the demo workspace (three invoices, one with a planted prompt injection)
cp -r examples/invoices-demo /tmp/demo

# Without a model: review and apply a hand-written plan
claudeos apply examples/invoices-demo.plan.json --root /tmp/demo --trust-domain yourco.example

# With Claude (needs ANTHROPIC_API_KEY, or a profile from `ant auth login`)
claudeos do "Summarize September's invoices into a spreadsheet and draft an email to finance@yourco.example with the total" --root /tmp/demo

claudeos undo    # revert the last commit
claudeos log     # what was read, proposed, approved and done
```

The approval card for the demo plan:

```
What will actually happen (3 actions, plan eb7851a1b483):
   1. LOCAL    write reports/2026-09-invoices.csv (146 bytes)
            - new file
   2. LOCAL    write reports/2026-09-summary.md (278 bytes)
            - new file
   3. EXTERNAL send email to finance@yourco.example: 'September invoices: 3,420.00 USD'
            - leaves this machine; cannot be undone
            | Hi,
            | ...

Staged file changes (nothing is written until you approve):
--- /dev/null
+++ b/reports/2026-09-invoices.csv
...
Overall risk: HIGH
```

If a plan followed the planted instruction, the extra email would show up on
the card with `recipient domain not on your trusted list: initech-billing.example`.
A plan that touches a protected path such as `.env` or `.ssh/` is refused
whole, before you are asked.

## Limits of Phase 0

- External actions are dry runs: they are written to an outbox, not sent.
- Text files only; PDFs and office documents come in Phase 1.
- No command execution. That needs a real isolation backend (bubblewrap, then
  a microVM) and is Phase 1.
- One workspace root per run.

## Development

```sh
pip install -e ".[claude,dev]"
pytest
```

The tests drive the planner with a scripted stand-in for the Claude client, so
they need no network or API key.

## Status

Early prototype, planned as an open-source project. The license (Apache-2.0
recommended) and the project name are still open questions; see the
[roadmap](docs/roadmap.md#open-questions).
