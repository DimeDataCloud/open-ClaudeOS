"""Copy-on-write overlay over one workspace directory.

Local actions are staged here in memory, so they can be previewed as a diff
before anyone approves them. Committing writes an undo journal first, then
applies the changes. This is the Phase 0 stand-in for the MicroVM sandbox: it
covers file edits only; running commands needs a real isolation backend.
"""

from __future__ import annotations

import difflib
import hashlib
import json
import os
import posixpath
import shutil
import tempfile
from dataclasses import dataclass
from pathlib import Path


class PathEscape(ValueError):
    """A path resolves outside the workspace root."""


class ConflictError(RuntimeError):
    """Files on disk changed after they were staged or committed."""


class CommitError(RuntimeError):
    """A commit failed part-way; its journal can still undo what was applied."""


def _sha(data: bytes | None) -> str | None:
    return None if data is None else hashlib.sha256(data).hexdigest()


def _atomic_write(target: Path, data: bytes) -> None:
    target.parent.mkdir(parents=True, exist_ok=True)
    fd, tmp = tempfile.mkstemp(dir=target.parent, prefix=f".{target.name}.", suffix=".tmp")
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(data)
        if target.exists():
            shutil.copymode(target, tmp)
        os.replace(tmp, target)
    except BaseException:
        Path(tmp).unlink(missing_ok=True)
        raise


@dataclass(frozen=True)
class Change:
    path: str
    before: bytes | None  # None: did not exist
    after: bytes | None  # None: deleted

    @property
    def kind(self) -> str:
        if self.before is None:
            return "create"
        return "delete" if self.after is None else "modify"


class Overlay:
    def __init__(self, root: str | os.PathLike[str]):
        self.root = Path(root).resolve()
        if not self.root.is_dir():
            raise NotADirectoryError(f"workspace root {self.root} is not a directory")
        self._staged: dict[str, bytes | None] = {}
        self._base: dict[str, bytes | None] = {}

    def rel(self, path: str) -> str:
        """Normalize a path to workspace-relative POSIX form, refusing escapes.

        Resolution follows symlinks, so a link inside the workspace that points
        outside it is refused too.
        """
        p = Path(path)
        resolved = (p if p.is_absolute() else self.root / p).resolve()
        if resolved == self.root:
            return "."
        if not resolved.is_relative_to(self.root):
            raise PathEscape(f"{path} is outside the workspace {self.root}")
        return resolved.relative_to(self.root).as_posix()

    def _disk(self, rel: str) -> bytes | None:
        p = self.root / rel
        if p.is_file():
            return p.read_bytes()
        if p.exists():
            raise IsADirectoryError(f"{rel} is a directory")
        return None

    def read(self, path: str) -> bytes | None:
        rel = self.rel(path)
        if rel in self._staged:
            return self._staged[rel]
        return self._disk(rel)

    def is_dir(self, path: str) -> bool:
        rel = self.rel(path)
        if rel == "." or (self.root / rel).is_dir():
            return True
        prefix = rel + "/"
        return any(k.startswith(prefix) and v is not None for k, v in self._staged.items())

    def list_dir(self, path: str = ".") -> list[str]:
        """Entries as the directory will look once staged changes apply; dirs end in '/'."""
        rel = self.rel(path)
        base = self.root if rel == "." else self.root / rel
        names: set[str] = set()
        if base.is_dir():
            names = {c.name + ("/" if c.is_dir() else "") for c in base.iterdir()}
        prefix = "" if rel == "." else rel + "/"
        for staged, content in self._staged.items():
            if not staged.startswith(prefix):
                continue
            head, sep, _ = staged[len(prefix):].partition("/")
            if sep:
                if content is not None:
                    names.add(head + "/")
            elif content is None:
                names.discard(head)
            else:
                names.add(head)
        return sorted(names)

    def _is_file(self, rel: str) -> bool:
        if rel in self._staged:
            return self._staged[rel] is not None
        return (self.root / rel).is_file()

    def _touch(self, rel: str) -> None:
        if rel not in self._base:
            self._base[rel] = self._disk(rel)

    def write(self, path: str, data: bytes) -> None:
        rel = self.rel(path)
        if self.is_dir(rel):
            raise IsADirectoryError(f"{path} is a directory")
        parent = posixpath.dirname(rel)
        while parent:
            if self._is_file(parent):
                raise NotADirectoryError(f"{parent} is a file, so {rel} cannot be created")
            parent = posixpath.dirname(parent)
        self._touch(rel)
        self._staged[rel] = data

    def delete(self, path: str) -> None:
        rel = self.rel(path)
        if self.read(rel) is None:
            raise FileNotFoundError(f"{path} does not exist")
        self._touch(rel)
        self._staged[rel] = None

    def move(self, src: str, dst: str) -> None:
        data = self.read(src)
        if data is None:
            raise FileNotFoundError(f"{src} does not exist")
        self.write(dst, data)
        self.delete(src)

    def changes(self) -> list[Change]:
        out = []
        for rel in sorted(self._staged):
            before, after = self._base[rel], self._staged[rel]
            if before != after:
                out.append(Change(rel, before, after))
        return out

    def diff(self) -> str:
        chunks = []
        for c in self.changes():
            try:
                a = [] if c.before is None else c.before.decode().splitlines(keepends=True)
                b = [] if c.after is None else c.after.decode().splitlines(keepends=True)
            except UnicodeDecodeError:
                chunks.append(f"Binary file {c.path}: {c.kind}\n")
                continue
            old = "/dev/null" if c.before is None else f"a/{c.path}"
            new = "/dev/null" if c.after is None else f"b/{c.path}"
            lines = list(difflib.unified_diff(a, b, old, new))
            chunks.append("".join(l if l.endswith("\n") else l + "\n" for l in lines))
        return "".join(chunks)

    def commit(self, journal_dir: Path, commit_id: str) -> Path:
        """Apply staged changes. Returns the undo manifest path.

        Refuses if any touched file changed on disk since it was staged, so an
        approval never lands on content the user did not see.
        """
        changes = self.changes()
        stale = [c.path for c in changes if self.rel(c.path) != c.path or self._disk(c.path) != c.before]
        if stale:
            raise ConflictError(f"changed on disk since preview: {', '.join(stale)}")

        backup_dir = journal_dir / commit_id
        backup_dir.mkdir(parents=True, exist_ok=False)
        entries = []
        for i, c in enumerate(changes):
            backup = None
            if c.before is not None:
                backup = f"{i}.bak"
                (backup_dir / backup).write_bytes(c.before)
            entries.append({
                "path": c.path, "backup": backup, "before_sha256": _sha(c.before), "after_sha256": _sha(c.after),
            })
        created_dirs = sorted(
            {d.relative_to(self.root).as_posix()
             for c in changes if c.after is not None
             for d in (self.root / c.path).parents
             if d != self.root and d.is_relative_to(self.root) and not d.exists()}
        )
        manifest = backup_dir / "manifest.json"
        manifest.write_text(json.dumps(
            {"id": commit_id, "root": str(self.root), "entries": entries, "created_dirs": created_dirs}, indent=2
        ))

        try:
            for c in changes:
                target = self.root / c.path
                if c.after is None:
                    target.unlink()
                else:
                    _atomic_write(target, c.after)
        except OSError as e:
            raise CommitError(f"commit {commit_id} failed part-way ({e}); undo it to restore the files") from e
        self._staged.clear()
        self._base.clear()
        return manifest


def undo(manifest_path: Path) -> list[str]:
    """Restore every file a commit touched. All-or-nothing: refuses if any of
    them was edited after the commit. Files a failed commit never reached are
    still in their original state and are left as they are."""
    manifest = json.loads(manifest_path.read_text())
    if manifest.get("undone"):
        raise ConflictError(f"commit {manifest['id']} was already undone")
    root = Path(manifest["root"])
    entries = manifest["entries"]

    edited = []
    for e in entries:
        target = root / e["path"]
        current = target.read_bytes() if target.is_file() else None
        if _sha(current) not in (e["after_sha256"], e["before_sha256"]):
            edited.append(e["path"])
    if edited:
        raise ConflictError(f"edited since commit, refusing to undo: {', '.join(edited)}")

    for e in reversed(entries):
        target = root / e["path"]
        if e["backup"] is None:
            target.unlink(missing_ok=True)
        else:
            _atomic_write(target, (manifest_path.parent / e["backup"]).read_bytes())
    for d in sorted(manifest.get("created_dirs", []), key=len, reverse=True):
        path = root / d
        if path.is_dir() and not any(path.iterdir()):
            path.rmdir()
    manifest["undone"] = True
    manifest_path.write_text(json.dumps(manifest, indent=2))
    return [e["path"] for e in entries]
