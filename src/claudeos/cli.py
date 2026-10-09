"""Command line front end: the Phase 0 stand-in for the Intent Bar."""

from __future__ import annotations

import argparse
import json
import sys

from .actions import ActionError
from .consent import GrantError
from .executor import OutboxConnector
from .plan import Plan, load_plan
from .policy import Policy
from .sandbox import CommitError, ConflictError, Overlay, undo
from .session import review_and_apply
from .state import StateDir


def _approve(card: str) -> bool:
    print(card, end="\n\n")
    try:
        answer = input("Approve and run? [y/N] ")
    except EOFError:
        return False
    return answer.strip().lower() in ("y", "yes")


def _run(plan: Plan, overlay: Overlay, policy: Policy, state: StateDir) -> int:
    outcome = review_and_apply(plan, overlay, policy, _approve, OutboxConnector(state.outbox), state)
    if outcome.status == "refused":
        print(outcome.card)
        return 1
    if outcome.status == "declined":
        print("Declined. Nothing was changed.")
        return 0
    result = outcome.result
    assert result is not None
    if result.manifest:
        print(f"Applied. Undo with: claudeos undo {result.commit_id}")
    for description, detail in result.external:
        print(f"  {description}: {detail}")
    return 0


def _cmd_do(args: argparse.Namespace, state: StateDir) -> int:
    from .planner import ClaudePlanner, NoPlan, PlannerError

    overlay, policy = Overlay(args.root), _policy(args)
    intent = " ".join(args.intent)

    def on_event(event: str, **data: object) -> None:
        state.log(event, **data)
        if event in ("list_dir", "read_file"):
            print(f"  {event} {data['path']}", file=sys.stderr)
        elif event == "plan_rejected":
            print("  plan revised after policy feedback", file=sys.stderr)

    planner = ClaudePlanner(model=args.model, effort=args.effort, on_event=on_event)
    state.log("intent", intent=intent, root=str(overlay.root))
    print(f"Planning: {intent}", file=sys.stderr)
    try:
        plan = planner.plan(intent, overlay, policy)
    except NoPlan as e:
        print(str(e))
        return 2
    except PlannerError as e:
        print(f"error: {e}", file=sys.stderr)
        return 1
    return _run(plan, overlay, policy, state)


def _cmd_apply(args: argparse.Namespace, state: StateDir) -> int:
    try:
        plan = load_plan(args.plan_file)
    except (OSError, json.JSONDecodeError, ActionError) as e:
        print(f"error: cannot load plan: {e}", file=sys.stderr)
        return 1
    return _run(plan, Overlay(args.root), _policy(args), state)


def _cmd_undo(args: argparse.Namespace, state: StateDir) -> int:
    manifests = [m for m in state.commits() if not json.loads(m.read_text()).get("undone")]
    if args.commit_id:
        manifests = [m for m in manifests if m.parent.name.startswith(args.commit_id)]
    if not manifests:
        print("Nothing to undo.", file=sys.stderr)
        return 1
    manifest = manifests[-1]
    paths = undo(manifest)
    state.log("undone", commit_id=manifest.parent.name, paths=paths)
    print(f"Undid {manifest.parent.name}: restored {len(paths)} file(s).")
    return 0


def _cmd_log(args: argparse.Namespace, state: StateDir) -> int:
    for record in state.read_log(args.n):
        detail = {k: v for k, v in record.items() if k not in ("ts", "event", "plan")}
        print(f"{record['ts']}  {record['event']:<13} {json.dumps(detail)[:160]}")
    return 0


def _policy(args: argparse.Namespace) -> Policy:
    return Policy(
        trusted_email_domains=frozenset(d.lower() for d in args.trust_domain),
        trusted_hosts=frozenset(h.lower() for h in args.trust_host),
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="claudeos", description="Say what you want; review exactly what will happen; approve once.")
    parser.add_argument("--state-dir", help="where the undo journal, outbox and audit log live")
    sub = parser.add_subparsers(dest="command", required=True)

    workspace = argparse.ArgumentParser(add_help=False)
    workspace.add_argument("--root", default=".", help="workspace directory the plan may touch (default: .)")
    workspace.add_argument("--trust-domain", action="append", default=[], metavar="DOMAIN", help="email domain you already trust")
    workspace.add_argument("--trust-host", action="append", default=[], metavar="HOST", help="HTTP host you already trust")

    p = sub.add_parser("do", parents=[workspace], help="plan an intent with Claude, then review and approve it")
    p.add_argument("intent", nargs="+")
    p.add_argument("--model", default="claude-opus-5-5")
    p.add_argument("--effort", default="high", choices=["low", "medium", "high", "xhigh", "max"])
    p.set_defaults(func=_cmd_do)

    p = sub.add_parser("apply", parents=[workspace], help="review and approve a plan from a JSON file (no model needed)")
    p.add_argument("plan_file")
    p.set_defaults(func=_cmd_apply)

    p = sub.add_parser("undo", help="revert the most recent (or the named) commit")
    p.add_argument("commit_id", nargs="?")
    p.set_defaults(func=_cmd_undo)

    p = sub.add_parser("log", help="show the audit log")
    p.add_argument("-n", type=int, default=20)
    p.set_defaults(func=_cmd_log)

    args = parser.parse_args(argv)
    state = StateDir(args.state_dir)
    try:
        return args.func(args, state)
    except (CommitError, ConflictError, GrantError, NotADirectoryError) as e:
        print(f"error: {e}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
