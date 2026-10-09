"""One pass of the loop: preview a plan, ask once, run exactly what was approved."""

from __future__ import annotations

from dataclasses import dataclass
from typing import Callable

from .consent import Grant, render_card
from .executor import Connector, ExecutionResult, execute
from .plan import Plan
from .policy import Assessment, Policy
from .sandbox import Overlay
from .state import StateDir


@dataclass
class Outcome:
    status: str  # "refused" | "declined" | "applied"
    card: str
    assessments: list[Assessment]
    result: ExecutionResult | None = None


def review_and_apply(
    plan: Plan,
    overlay: Overlay,
    policy: Policy,
    approve: Callable[[str], bool],
    connector: Connector,
    state: StateDir,
) -> Outcome:
    assessments = policy.preview(plan, overlay)
    card = render_card(plan, assessments, overlay.diff(), plan.reads)
    state.log("proposed", plan=plan.to_dict(), digest=plan.digest())

    if any(a.denied for a in assessments):
        state.log("refused", digest=plan.digest(), reasons=[a.denied for a in assessments if a.denied])
        return Outcome("refused", card, assessments)

    if not approve(card):
        state.log("declined", digest=plan.digest())
        return Outcome("declined", card, assessments)

    grant = Grant.for_plan(plan)
    state.log("approved", digest=plan.digest())
    try:
        result = execute(plan, grant, overlay, connector, state)
    except Exception as e:
        state.log("failed", digest=plan.digest(), error=str(e))
        raise
    return Outcome("applied", card, assessments, result)
