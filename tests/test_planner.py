"""The planner loop, driven by a scripted stand-in for the Claude client."""

from types import SimpleNamespace

import pytest

from claudeos.planner import ClaudePlanner, NoPlan, PlannerError
from claudeos.policy import Policy
from claudeos.sandbox import Overlay


def tool_use(id, name, input):
    return SimpleNamespace(type="tool_use", id=id, name=name, input=input)


def text(t):
    return SimpleNamespace(type="text", text=t)


def message(*content, stop_reason="tool_use", stop_details=None):
    return SimpleNamespace(content=list(content), stop_reason=stop_reason, stop_details=stop_details)


class FakeClient:
    """Replays canned responses and records every request."""

    def __init__(self, *responses):
        self.responses = list(responses)
        self.requests = []
        self.beta = SimpleNamespace(messages=SimpleNamespace(stream=self._stream))

    def _stream(self, **kwargs):
        # Snapshot: the planner keeps appending to the same list.
        self.requests.append({**kwargs, "messages": list(kwargs["messages"])})
        response = self.responses.pop(0)

        class Stream:
            def __enter__(self):
                return SimpleNamespace(get_final_message=lambda: response)

            def __exit__(self, *exc):
                return False

        return Stream()


def tool_results(request):
    return {b["tool_use_id"]: b for b in request["messages"][-1]["content"]}


def test_explores_then_submits_a_plan(ws):
    client = FakeClient(
        message(tool_use("t1", "list_dir", {"path": "invoices"})),
        message(tool_use("t2", "read_file", {"path": "invoices/a.txt"}), tool_use("t3", "read_file", {"path": ".env"})),
        message(
            text("Here is the plan."),
            tool_use("t4", "submit_plan", {
                "summary": "Write totals.",
                "actions": [{"type": "write_file", "path": "totals.csv", "content": "Acme Corp,120.00\n"}],
            }),
        ),
    )
    plan = ClaudePlanner(client=client).plan("total my invoices", Overlay(ws), Policy())

    assert plan.intent == "total my invoices"
    assert [a.describe() for a in plan.actions] == ["write totals.csv (17 bytes)"]
    assert plan.reads == ("invoices/a.txt",)
    assert not (ws / "totals.csv").exists()  # planning never writes

    first = client.requests[0]
    assert first["model"] == "claude-opus-5-5"
    assert first["fallbacks"] == "default"
    assert "invoices/" in first["messages"][0]["content"]
    assert ".env" not in first["messages"][0]["content"]  # protected names are hidden

    results = tool_results(client.requests[2])
    assert "Acme Corp" in results["t2"]["content"]
    assert results["t3"]["is_error"] and "protected" in results["t3"]["content"]


def test_policy_rejection_is_fed_back_and_plan_revised(ws):
    events = []
    client = FakeClient(
        message(tool_use("t1", "submit_plan", {
            "summary": "s", "actions": [{"type": "delete_file", "path": ".ssh/id_ed25519"}],
        })),
        message(tool_use("t2", "submit_plan", {
            "summary": "s", "actions": [{"type": "write_file", "path": "notes.md", "content": "new\n"}],
        })),
    )
    planner = ClaudePlanner(client=client, on_event=lambda e, **d: events.append(e))
    plan = planner.plan("clean up", Overlay(ws), Policy())

    rejection = tool_results(client.requests[1])["t1"]
    assert rejection["is_error"] and "protected" in rejection["content"]
    assert events == ["plan_rejected"]
    assert plan.actions[0].path == "notes.md"


def test_model_cannot_rewrite_the_intent(ws):
    client = FakeClient(message(tool_use("t1", "submit_plan", {
        "intent": "something else entirely",
        "summary": "s",
        "actions": [{"type": "write_file", "path": "x.txt", "content": "x"}],
    })))
    plan = ClaudePlanner(client=client).plan("the real intent", Overlay(ws), Policy())
    assert plan.intent == "the real intent"


def test_answer_without_plan_surfaces_the_question(ws):
    client = FakeClient(message(text("Which quarter do you mean?"), stop_reason="end_turn"))
    with pytest.raises(NoPlan, match="Which quarter"):
        ClaudePlanner(client=client).plan("summarize the quarter", Overlay(ws), Policy())


def test_refusal_and_truncation_stop_the_loop(ws):
    refusal = message(stop_reason="refusal", stop_details=SimpleNamespace(category="cyber"))
    with pytest.raises(PlannerError, match="declined.*cyber"):
        ClaudePlanner(client=FakeClient(refusal)).plan("x", Overlay(ws), Policy())

    truncated = message(tool_use("t1", "submit_plan", {"summary": "s"}), stop_reason="max_tokens")
    with pytest.raises(PlannerError, match="output tokens"):
        ClaudePlanner(client=FakeClient(truncated)).plan("x", Overlay(ws), Policy())


def test_blocks_before_a_fallback_boundary_are_not_executed(ws):
    client = FakeClient(
        message(
            text("partial"),
            tool_use("old", "read_file", {"path": "invoices/a.txt"}),
            SimpleNamespace(type="fallback"),
            tool_use("new", "read_file", {"path": "invoices/b.txt"}),
        ),
        message(tool_use("t2", "submit_plan", {
            "summary": "s", "actions": [{"type": "write_file", "path": "x.txt", "content": "x"}],
        })),
    )
    plan = ClaudePlanner(client=client).plan("x", Overlay(ws), Policy())
    echoed = client.requests[1]["messages"][1]["content"]
    assert [getattr(b, "id", b.type) for b in echoed] == ["text", "new"]
    assert set(tool_results(client.requests[1])) == {"new"}
    assert plan.reads == ("invoices/b.txt",)
