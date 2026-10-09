import pytest

from claudeos.state import StateDir


@pytest.fixture
def ws(tmp_path):
    root = tmp_path / "ws"
    (root / "invoices").mkdir(parents=True)
    (root / "invoices" / "a.txt").write_text("Acme Corp\nTotal: 120.00\n")
    (root / "invoices" / "b.txt").write_text("Globex\nTotal: 80.50\n")
    (root / "notes.md").write_text("old notes\n")
    (root / ".ssh").mkdir()
    (root / ".ssh" / "id_ed25519").write_text("PRIVATE KEY\n")
    (root / ".env").write_text("API_TOKEN=secret\n")
    return root


@pytest.fixture
def state(tmp_path):
    return StateDir(tmp_path / "state")
