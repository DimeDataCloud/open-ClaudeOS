"""Deterministic policy: what may be touched, and how risky each action is.

No model is consulted here. The model's job is to propose; this module decides
what is refused outright and what the person must be told before consenting.
"""

from __future__ import annotations

from dataclasses import dataclass, field, replace
from enum import IntEnum
from fnmatch import fnmatch

from .actions import Action, DeleteFile, Effect, HttpRequest, MoveFile, SendEmail, WriteFile
from .plan import Plan
from .sandbox import Overlay, PathEscape

# Matched against every component of a workspace-relative path. Secrets are
# denied for reading too: what the agent cannot read, it cannot leak.
DEFAULT_PROTECTED = (
    ".ssh", ".gnupg", ".aws", ".kube", ".docker", ".git", ".netrc",
    ".env", ".env.*", "*.pem", "*.key", "*.p12", "*.kdbx", "id_rsa*", "id_ed25519*", "id_ecdsa*",
)


class PolicyDenied(Exception):
    pass


class Risk(IntEnum):
    LOW = 1  # creates new files; nothing existing is touched
    MEDIUM = 2  # changes or removes existing files; undoable after commit
    HIGH = 3  # leaves the machine; cannot be undone


@dataclass(frozen=True)
class Assessment:
    action: Action
    risk: Risk
    notes: tuple[str, ...] = ()
    denied: str | None = None


@dataclass(frozen=True)
class Policy:
    protected: tuple[str, ...] = DEFAULT_PROTECTED
    trusted_email_domains: frozenset[str] = field(default_factory=frozenset)
    trusted_hosts: frozenset[str] = field(default_factory=frozenset)
    max_write_bytes: int = 5_000_000

    def check_path(self, overlay: Overlay, path: str) -> str:
        try:
            rel = overlay.rel(path)
        except PathEscape as e:
            raise PolicyDenied(str(e)) from None
        if rel != "." and any(fnmatch(part, pat) for part in rel.split("/") for pat in self.protected):
            raise PolicyDenied(f"{rel} is a protected path")
        return rel

    def is_protected_name(self, name: str) -> bool:
        return any(fnmatch(name.rstrip("/"), pat) for pat in self.protected)

    def assess(self, action: Action, overlay: Overlay) -> Assessment:
        try:
            risk, notes = self._assess(action, overlay)
        except (PolicyDenied, OSError) as e:
            return Assessment(action, Risk.HIGH if action.effect is Effect.EXTERNAL else Risk.MEDIUM, denied=str(e))
        return Assessment(action, risk, tuple(notes))

    def _assess(self, action: Action, overlay: Overlay) -> tuple[Risk, list[str]]:
        if isinstance(action, WriteFile):
            rel = self.check_path(overlay, action.path)
            size = len(action.content.encode())
            if size > self.max_write_bytes:
                raise PolicyDenied(f"{rel}: {size} bytes exceeds the {self.max_write_bytes}-byte write limit")
            if overlay.is_dir(rel):
                raise PolicyDenied(f"{rel} is a directory")
            if overlay.read(rel) is None:
                return Risk.LOW, ["new file"]
            return Risk.MEDIUM, ["overwrites existing file"]

        if isinstance(action, DeleteFile):
            rel = self.check_path(overlay, action.path)
            if overlay.read(rel) is None:
                raise PolicyDenied(f"{rel} does not exist")
            return Risk.MEDIUM, ["deletes file (restorable with undo)"]

        if isinstance(action, MoveFile):
            src = self.check_path(overlay, action.src)
            dst = self.check_path(overlay, action.dst)
            if overlay.read(src) is None:
                raise PolicyDenied(f"{src} does not exist")
            if overlay.is_dir(dst):
                raise PolicyDenied(f"{dst} is a directory; give the full destination file path")
            if overlay.read(dst) is not None:
                return Risk.MEDIUM, ["overwrites existing file at destination"]
            return Risk.MEDIUM, ["moves existing file"]

        if isinstance(action, SendEmail):
            notes = ["leaves this machine; cannot be undone"]
            for domain in sorted(action.domains() - self.trusted_email_domains):
                notes.append(f"recipient domain not on your trusted list: {domain}")
            return Risk.HIGH, notes

        if isinstance(action, HttpRequest):
            notes = ["leaves this machine; cannot be undone"]
            if action.method not in ("GET", "HEAD"):
                notes.append(f"{action.method} sends data to the remote host")
            if action.host() not in self.trusted_hosts:
                notes.append(f"host not on your trusted list: {action.host()}")
            return Risk.HIGH, notes

        raise PolicyDenied(f"no policy rule for action type {action.kind!r}")

    def preview(self, plan: Plan, overlay: Overlay) -> list[Assessment]:
        """Assess each action against the state left by the ones before it, and
        stage the local ones that pass, so `overlay.diff()` shows the result."""
        out = []
        for action in plan.actions:
            a = self.assess(action, overlay)
            if a.denied is None and action.effect is Effect.LOCAL:
                try:
                    stage(action, overlay)
                except OSError as e:
                    a = replace(a, denied=str(e))
            out.append(a)
        return out


def stage(action: Action, overlay: Overlay) -> None:
    if isinstance(action, WriteFile):
        overlay.write(action.path, action.content.encode())
    elif isinstance(action, DeleteFile):
        overlay.delete(action.path)
    elif isinstance(action, MoveFile):
        overlay.move(action.src, action.dst)
    else:
        raise TypeError(f"{action.kind} is not a local action")
