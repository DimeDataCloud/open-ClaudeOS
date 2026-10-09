import json

from claudeos.cli import main
from claudeos.executor import OutboxConnector
from claudeos.plan import parse_plan
from claudeos.policy import Policy
from claudeos.sandbox import Overlay
from claudeos.session import review_and_apply

PLAN = {
    "intent": "summarize invoices and tell finance",
    "summary": "Write a CSV of invoice totals and email finance.",
    "actions": [
        {"type": "write_file", "path": "reports/invoices.csv", "content": "vendor,total\nAcme Corp,120.00\nGlobex,80.50\n"},
        {"type": "send_email", "to": ["finance@corp.example"], "subject": "Invoices", "body": "Total: 200.50"},
    ],
}


def _review(ws, state, plan, approve):
    return review_and_apply(
        parse_plan(plan), Overlay(ws), Policy(), approve, OutboxConnector(state.outbox), state
    )


def test_declined_plan_changes_nothing(ws, state):
    outcome = _review(ws, state, PLAN, approve=lambda card: False)
    assert outcome.status == "declined"
    assert not (ws / "reports").exists()
    assert list(state.outbox.iterdir()) == []


def test_approved_plan_commits_files_then_runs_external_actions(ws, state):
    seen = []
    outcome = _review(ws, state, PLAN, approve=lambda card: seen.append(card) or True)
    assert outcome.status == "applied"
    assert "+Acme Corp,120.00" in seen[0]
    assert (ws / "reports" / "invoices.csv").read_text().startswith("vendor,total")
    [queued] = state.outbox.iterdir()
    assert json.loads(queued.read_text())["to"] == ["finance@corp.example"]
    events = [r["event"] for r in state.read_log()]
    assert events == ["proposed", "approved", "committed", "external"]


def test_plan_with_any_denied_action_is_refused_before_asking(ws, state):
    bad = {**PLAN, "actions": PLAN["actions"] + [{"type": "delete_file", "path": ".ssh/id_ed25519"}]}
    asked = []
    outcome = _review(ws, state, bad, approve=lambda card: asked.append(card) or True)
    assert outcome.status == "refused"
    assert asked == []
    assert not (ws / "reports").exists()
    assert (ws / ".ssh" / "id_ed25519").exists()


def test_cli_apply_and_undo_round_trip(ws, state, tmp_path, monkeypatch, capsys):
    plan_file = tmp_path / "plan.json"
    plan_file.write_text(json.dumps(PLAN))
    monkeypatch.setattr("builtins.input", lambda prompt: "y")

    assert main(["--state-dir", str(state.path), "apply", str(plan_file), "--root", str(ws)]) == 0
    assert (ws / "reports" / "invoices.csv").exists()
    assert "Undo with: claudeos undo" in capsys.readouterr().out

    assert main(["--state-dir", str(state.path), "undo"]) == 0
    assert not (ws / "reports" / "invoices.csv").exists()
    assert main(["--state-dir", str(state.path), "undo"]) == 1  # nothing left
