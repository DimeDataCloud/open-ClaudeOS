#!/usr/bin/env python3
"""Fail if a relative link or image in a Markdown file points at something that does not exist,
or at a heading anchor that is not in the target file. External links are not fetched."""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SKIP = {".git", "node_modules", "bin", "obj", "research_notes", "reports"}
LINK = re.compile(r"!?\[[^\]]*\]\(([^)\s]+)(?:\s+\"[^\"]*\")?\)")
FENCE = re.compile(r"^(```|~~~)")


def slug(heading: str) -> str:
    text = re.sub(r"[`*_]", "", heading.strip().lower())
    text = re.sub(r"[^\w\s-]", "", text)
    return re.sub(r"\s", "-", text)


def anchors(path: Path) -> set[str]:
    found: set[str] = set()
    fenced = False
    for line in path.read_text(encoding="utf-8").splitlines():
        if FENCE.match(line.strip()):
            fenced = not fenced
        elif not fenced and line.startswith("#"):
            found.add(slug(line.lstrip("#")))
    return found


def main() -> int:
    problems = []
    for md in sorted(ROOT.rglob("*.md")):
        if SKIP & set(md.relative_to(ROOT).parts):
            continue
        fenced = False
        for number, line in enumerate(md.read_text(encoding="utf-8").splitlines(), 1):
            if FENCE.match(line.strip()):
                fenced = not fenced
                continue
            if fenced:
                continue
            for target in LINK.findall(line):
                if re.match(r"^[a-z][a-z0-9+.-]*:", target) or target.startswith("#") and False:
                    continue
                path_part, _, fragment = target.partition("#")
                resolved = md if path_part == "" else (md.parent / path_part).resolve()
                if path_part and not resolved.exists():
                    problems.append(f"{md.relative_to(ROOT)}:{number}: missing {target}")
                elif fragment and resolved.suffix == ".md" and slug(fragment) not in anchors(resolved):
                    problems.append(f"{md.relative_to(ROOT)}:{number}: no heading '#{fragment}' in {resolved.relative_to(ROOT)}")
    for p in problems:
        print(p)
    print(f"{len(problems)} broken link(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
