"""Runs an approved plan, and nothing else.

Local changes were already staged in the overlay during preview; executing
commits them (with an undo journal) and then hands external actions to
connectors. Every action is checked against the grant first.
"""

from __future__ import annotations

import json
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Protocol

from .actions import Action, Effect, HttpRequest, SendEmail
from .consent import Grant
from .plan import Plan
from .sandbox import Overlay
from .state import StateDir


class Connector(Protocol):
    def send_email(self, action: SendEmail) -> str: ...
    def http_request(self, action: HttpRequest) -> str: ...


class OutboxConnector:
    """Phase 0 connector: records external actions instead of performing them.

    Real connectors (mail, HTTP, MCP servers) plug in behind the same
    interface once the consent path is trusted.
    """

    def __init__(self, outbox: Path):
        self.outbox = outbox

    def _queue(self, action: Action) -> str:
        path = self.outbox / f"{time.strftime('%Y%m%dT%H%M%S')}-{action.digest()[:8]}.json"
        path.write_text(json.dumps(action.to_dict(), indent=2))
        return f"dry run: written to {path}, nothing was sent"

    def send_email(self, action: SendEmail) -> str:
        return self._queue(action)

    def http_request(self, action: HttpRequest) -> str:
        return self._queue(action)


@dataclass
class ExecutionResult:
    commit_id: str
    manifest: Path | None
    external: list[tuple[str, str]] = field(default_factory=list)  # (description, connector result)


def execute(plan: Plan, grant: Grant, overlay: Overlay, connector: Connector, state: StateDir) -> ExecutionResult:
    grant.check(plan)
    commit_id = f"{time.strftime('%Y%m%dT%H%M%S')}-{plan.digest()[:8]}"
    manifest = overlay.commit(state.journal, commit_id) if overlay.changes() else None
    state.log("committed", commit_id=commit_id, manifest=str(manifest) if manifest else None)

    result = ExecutionResult(commit_id, manifest)
    for action in plan.actions:
        if action.effect is not Effect.EXTERNAL:
            continue
        grant.require(action)
        if isinstance(action, SendEmail):
            outcome = connector.send_email(action)
        elif isinstance(action, HttpRequest):
            outcome = connector.http_request(action)
        else:
            raise TypeError(f"no connector for {action.kind}")
        state.log("external", action=action.to_dict(), outcome=outcome)
        result.external.append((action.describe(), outcome))
    return result
