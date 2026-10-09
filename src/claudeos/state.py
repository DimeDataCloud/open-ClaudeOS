"""Where the orchestrator keeps its own records: undo journal, outbox, audit log."""

from __future__ import annotations

import json
import os
import time
from pathlib import Path
from typing import Any


def default_state_dir() -> Path:
    if env := os.environ.get("CLAUDEOS_STATE_DIR"):
        return Path(env)
    base = os.environ.get("XDG_STATE_HOME") or Path.home() / ".local" / "state"
    return Path(base) / "claudeos"


class StateDir:
    def __init__(self, path: str | os.PathLike[str] | None = None):
        self.path = Path(path) if path else default_state_dir()
        self.journal = self.path / "journal"
        self.outbox = self.path / "outbox"
        self.audit_log = self.path / "audit.jsonl"
        for d in (self.journal, self.outbox):
            d.mkdir(parents=True, exist_ok=True)

    def log(self, event: str, **data: Any) -> None:
        """Append-only audit trail of what was read, proposed, approved and done."""
        record = {"ts": time.strftime("%Y-%m-%dT%H:%M:%S%z"), "event": event, **data}
        with self.audit_log.open("a", encoding="utf-8") as f:
            f.write(json.dumps(record, default=str) + "\n")

    def read_log(self, limit: int = 20) -> list[dict[str, Any]]:
        if not self.audit_log.exists():
            return []
        lines = self.audit_log.read_text(encoding="utf-8").splitlines()
        return [json.loads(line) for line in lines[-limit:]]

    def commits(self) -> list[Path]:
        """Undo manifests, oldest first (commit ids start with a timestamp)."""
        return sorted(self.journal.glob("*/manifest.json"))
