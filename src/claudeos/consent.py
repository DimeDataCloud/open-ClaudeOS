"""Consent: one approval per plan, bound to exactly the actions shown.

The approval card is rendered from the typed actions and the policy's notes,
never from model prose, so what the person approves is what will run. The
model's own summary is shown, but labelled as the model's claim.
"""

from __future__ import annotations

import time
from dataclasses import dataclass

from .actions import Effect
from .plan import Plan
from .policy import Assessment, Risk


class GrantError(PermissionError):
    pass


@dataclass(frozen=True)
class Grant:
    """A capability for one plan: these action digests, until expires_at."""

    plan_digest: str
    action_digests: frozenset[str]
    expires_at: float

    @classmethod
    def for_plan(cls, plan: Plan, ttl_seconds: float = 300) -> Grant:
        return cls(plan.digest(), frozenset(a.digest() for a in plan.actions), time.time() + ttl_seconds)

    def check(self, plan: Plan) -> None:
        if time.time() > self.expires_at:
            raise GrantError("approval expired; review the plan again")
        if plan.digest() != self.plan_digest:
            raise GrantError("plan differs from the one that was approved")
        for action in plan.actions:
            self.require(action)

    def require(self, action) -> None:
        if action.digest() not in self.action_digests:
            raise GrantError(f"action was not approved: {action.describe()}")


def plan_risk(assessments: list[Assessment]) -> Risk:
    return max((a.risk for a in assessments), default=Risk.LOW)


def render_card(plan: Plan, assessments: list[Assessment], diff: str, reads: tuple[str, ...] = ()) -> str:
    denied = [a for a in assessments if a.denied]
    lines = [
        f"Intent:  {plan.intent}" if plan.intent else "Intent:  (none given)",
        f"Model's summary (its own words): {plan.summary or '(none)'}",
        "",
        f"What will actually happen ({len(plan.actions)} actions, plan {plan.digest()[:12]}):",
    ]
    for i, a in enumerate(assessments, 1):
        tag = "LOCAL   " if a.action.effect is Effect.LOCAL else "EXTERNAL"
        lines.append(f"  {i:>2}. {tag} {a.action.describe()}")
        for note in a.notes:
            lines.append(f"            - {note}")
        if a.denied:
            lines.append(f"            x DENIED: {a.denied}")
        if a.action.effect is Effect.EXTERNAL:
            lines.extend(_external_detail(a.action))

    if reads:
        lines += ["", f"Files read while planning ({len(reads)}): " + ", ".join(reads)]
    if diff:
        lines += ["", "Staged file changes (nothing is written until you approve):", diff.rstrip()]
    lines += ["", f"Overall risk: {plan_risk(assessments).name}"]
    if denied:
        lines.append(f"Plan refused by policy: {len(denied)} action(s) denied. Nothing will run.")
    return "\n".join(lines)


def _external_detail(action) -> list[str]:
    """Show everything that will leave the machine, in full."""
    return ["            | " + line for line in getattr(action, "body", "").splitlines()]
