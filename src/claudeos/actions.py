"""Typed actions: the only things a plan can ask the system to do.

Every action declares its effect class. The effect class, not the model's
description, decides how an action is staged, reviewed and executed.
"""

from __future__ import annotations

import hashlib
import json
from dataclasses import MISSING, asdict, dataclass, fields
from enum import Enum
from typing import Any, ClassVar
from urllib.parse import urlsplit


class Effect(str, Enum):
    LOCAL = "local"  # changes files on this machine; staged, previewed, undoable
    EXTERNAL = "external"  # leaves this machine; cannot be undone once executed


class ActionError(ValueError):
    """An action payload is malformed."""


@dataclass(frozen=True)
class Action:
    kind: ClassVar[str]
    effect: ClassVar[Effect]

    def to_dict(self) -> dict[str, Any]:
        return {"type": self.kind, **asdict(self)}

    def digest(self) -> str:
        canonical = json.dumps(self.to_dict(), sort_keys=True, separators=(",", ":"))
        return hashlib.sha256(canonical.encode()).hexdigest()

    def describe(self) -> str:
        raise NotImplementedError


@dataclass(frozen=True)
class WriteFile(Action):
    kind: ClassVar[str] = "write_file"
    effect: ClassVar[Effect] = Effect.LOCAL
    path: str
    content: str

    def describe(self) -> str:
        return f"write {self.path} ({len(self.content.encode())} bytes)"


@dataclass(frozen=True)
class DeleteFile(Action):
    kind: ClassVar[str] = "delete_file"
    effect: ClassVar[Effect] = Effect.LOCAL
    path: str

    def describe(self) -> str:
        return f"delete {self.path}"


@dataclass(frozen=True)
class MoveFile(Action):
    kind: ClassVar[str] = "move_file"
    effect: ClassVar[Effect] = Effect.LOCAL
    src: str
    dst: str

    def describe(self) -> str:
        return f"move {self.src} -> {self.dst}"


@dataclass(frozen=True)
class SendEmail(Action):
    kind: ClassVar[str] = "send_email"
    effect: ClassVar[Effect] = Effect.EXTERNAL
    to: tuple[str, ...]
    subject: str
    body: str

    def describe(self) -> str:
        return f"send email to {', '.join(self.to)}: {self.subject!r}"

    def domains(self) -> set[str]:
        return {addr.rsplit("@", 1)[-1].lower() for addr in self.to}


@dataclass(frozen=True)
class HttpRequest(Action):
    kind: ClassVar[str] = "http_request"
    effect: ClassVar[Effect] = Effect.EXTERNAL
    method: str
    url: str
    body: str = ""

    def describe(self) -> str:
        return f"{self.method} {self.url}"

    def host(self) -> str:
        return (urlsplit(self.url).hostname or "").lower()


ACTION_TYPES: dict[str, type[Action]] = {
    cls.kind: cls for cls in (WriteFile, DeleteFile, MoveFile, SendEmail, HttpRequest)
}


def parse_action(payload: Any) -> Action:
    """Build an Action from untrusted JSON, rejecting anything malformed."""
    if not isinstance(payload, dict):
        raise ActionError(f"action must be an object, got {type(payload).__name__}")
    kind = payload.get("type")
    cls = ACTION_TYPES.get(kind)  # type: ignore[arg-type]
    if cls is None:
        raise ActionError(f"unknown action type {kind!r}; expected one of {sorted(ACTION_TYPES)}")

    values: dict[str, Any] = {}
    for f in fields(cls):
        if f.name not in payload:
            if f.default is not MISSING:
                continue
            raise ActionError(f"{kind}: missing field {f.name!r}")
        values[f.name] = payload[f.name]
    # The planner's tool schema shares one object shape across action types,
    # so empty values in fields this type does not use are ignored.
    unknown = {
        k for k in set(payload) - {"type"} - {f.name for f in fields(cls)}
        if payload[k] not in (None, "", [])
    }
    if unknown:
        raise ActionError(f"{kind}: unknown fields {sorted(unknown)}")

    if cls is SendEmail:
        to = values["to"]
        if not isinstance(to, list) or not to or not all(isinstance(a, str) and "@" in a for a in to):
            raise ActionError("send_email: 'to' must be a non-empty list of email addresses")
        values["to"] = tuple(to)
    for name, value in values.items():
        if name != "to" and not isinstance(value, str):
            raise ActionError(f"{kind}: field {name!r} must be a string")
    if cls is HttpRequest:
        values["method"] = values["method"].upper()
        if urlsplit(values["url"]).scheme not in ("http", "https"):
            raise ActionError("http_request: url must be http(s)")
    return cls(**values)
