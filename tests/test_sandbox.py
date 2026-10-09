import os

import pytest

from claudeos.sandbox import ConflictError, Overlay, PathEscape, undo


def test_paths_cannot_escape_the_workspace(ws, tmp_path):
    overlay = Overlay(ws)
    (tmp_path / "outside.txt").write_text("x")
    os.symlink(tmp_path / "outside.txt", ws / "link.txt")

    for path in ("../outside.txt", str(tmp_path / "outside.txt"), "link.txt", "invoices/../../outside.txt"):
        with pytest.raises(PathEscape):
            overlay.rel(path)
    assert overlay.rel(str(ws / "invoices" / "a.txt")) == "invoices/a.txt"
    assert overlay.rel(".") == "."


def test_staged_changes_are_visible_but_not_on_disk(ws):
    overlay = Overlay(ws)
    overlay.write("reports/summary.md", b"total 200.50\n")
    overlay.delete("notes.md")
    overlay.move("invoices/b.txt", "archive/b.txt")

    assert overlay.read("reports/summary.md") == b"total 200.50\n"
    assert overlay.read("notes.md") is None
    assert overlay.list_dir(".") == sorted([".env", ".ssh/", "archive/", "invoices/", "reports/"])
    assert overlay.list_dir("invoices") == ["a.txt"]
    assert not (ws / "reports").exists()
    assert (ws / "notes.md").exists()

    diff = overlay.diff()
    assert "+++ b/reports/summary.md" in diff
    assert "--- a/notes.md" in diff and "+++ /dev/null" in diff


def test_commit_then_undo_restores_everything(ws, state):
    overlay = Overlay(ws)
    overlay.write("notes.md", b"new notes\n")
    overlay.write("reports/summary.md", b"summary\n")
    overlay.delete("invoices/a.txt")
    manifest = overlay.commit(state.journal, "c1")

    assert (ws / "notes.md").read_text() == "new notes\n"
    assert (ws / "reports" / "summary.md").exists()
    assert not (ws / "invoices" / "a.txt").exists()

    undo(manifest)
    assert (ws / "notes.md").read_text() == "old notes\n"
    assert not (ws / "reports").exists()  # directories the commit created go too
    assert (ws / "invoices").is_dir()  # pre-existing directories are left alone
    assert (ws / "invoices" / "a.txt").read_text() == "Acme Corp\nTotal: 120.00\n"
    with pytest.raises(ConflictError, match="already undone"):
        undo(manifest)


def test_commit_refuses_if_file_changed_after_preview(ws, state):
    overlay = Overlay(ws)
    overlay.write("notes.md", b"planned\n")
    (ws / "notes.md").write_text("edited by the person meanwhile\n")
    with pytest.raises(ConflictError, match="notes.md"):
        overlay.commit(state.journal, "c1")
    assert (ws / "notes.md").read_text() == "edited by the person meanwhile\n"


def test_undo_refuses_if_file_edited_after_commit(ws, state):
    overlay = Overlay(ws)
    overlay.write("notes.md", b"planned\n")
    manifest = overlay.commit(state.journal, "c1")
    (ws / "notes.md").write_text("later edit\n")
    with pytest.raises(ConflictError, match="notes.md"):
        undo(manifest)
    assert (ws / "notes.md").read_text() == "later edit\n"


def test_failed_commit_can_still_be_undone(ws, state, monkeypatch):
    import claudeos.sandbox as sandbox

    overlay = Overlay(ws)
    overlay.write("invoices/a.txt", b"changed\n")
    overlay.write("notes.md", b"changed\n")
    real_write = sandbox._atomic_write

    def flaky_write(target, data):
        if target.name == "notes.md":
            raise OSError("disk full")
        real_write(target, data)

    monkeypatch.setattr(sandbox, "_atomic_write", flaky_write)
    with pytest.raises(sandbox.CommitError, match="part-way"):
        overlay.commit(state.journal, "c1")
    assert (ws / "invoices" / "a.txt").read_text() == "changed\n"  # first change landed

    monkeypatch.setattr(sandbox, "_atomic_write", real_write)
    undo(state.journal / "c1" / "manifest.json")
    assert (ws / "invoices" / "a.txt").read_text() == "Acme Corp\nTotal: 120.00\n"
    assert (ws / "notes.md").read_text() == "old notes\n"
