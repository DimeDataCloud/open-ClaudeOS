"""A plan: the intent, the model's own summary, and the exact actions proposed.

The digest covers the actions only. Consent is given to what will happen,
not to how the model chose to describe it.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from typing import Any

from .actions import Action, ActionError, parse_action


@dataclass(frozen=True)
class Plan:
    intent: str
    summary: str
    actions: tuple[Action, ...]
    reads: tuple[str, ...] = ()  # files read while planning; provenance, not consent

    def digest(self) -> str:
        h = hashlib.sha256()
        for action in self.actions:
            h.update(action.digest().encode())
        return h.hexdigest()

    def to_dict(self) -> dict[str, Any]:
        return {
            "intent": self.intent,
            "summary": self.summary,
            "actions": [a.to_dict() for a in self.actions],
            "reads": list(self.reads),
        }


def parse_plan(payload: Any, intent: str = "") -> Plan:
    if not isinstance(payload, dict):
        raise ActionError("plan must be an object")
    actions = payload.get("actions")
    if not isinstance(actions, list) or not actions:
        raise ActionError("plan must contain a non-empty 'actions' list")
    parsed = []
    for i, raw in enumerate(actions):
        try:
            parsed.append(parse_action(raw))
        except ActionError as e:
            raise ActionError(f"actions[{i}]: {e}") from None
    summary = payload.get("summary", "")
    if not isinstance(summary, str):
        raise ActionError("plan 'summary' must be a string")
    if isinstance(payload.get("intent"), str) and payload["intent"]:
        intent = payload["intent"]
    return Plan(intent=intent, summary=summary, actions=tuple(parsed))


def load_plan(path: str) -> Plan:
    with open(path, encoding="utf-8") as f:
        return parse_plan(json.load(f))
