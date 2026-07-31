#!/usr/bin/env python3
"""
Read-only duplicate audit for Assets.
It only prints byte-identical files and never deletes anything.
"""

from __future__ import annotations

import hashlib
from collections import defaultdict
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "Assets"

SKIP_DIR_NAMES = {
    ".git",
    "Library",
    "Temp",
    "Logs",
    "Obj",
    "Build",
    "Builds",
}

SKIP_SUFFIXES = {
    ".meta",
}


def digest(path: Path) -> str:
    hasher = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            hasher.update(chunk)
    return hasher.hexdigest()


def main() -> None:
    groups: dict[tuple[int, str], list[Path]] = defaultdict(list)

    for path in ASSETS.rglob("*"):
        if not path.is_file():
            continue
        if any(part in SKIP_DIR_NAMES for part in path.parts):
            continue
        if path.suffix.lower() in SKIP_SUFFIXES:
            continue
        try:
            key = (path.stat().st_size, digest(path))
        except OSError as exc:
            print(f"SKIP {path}: {exc}")
            continue
        groups[key].append(path)

    duplicates = [
        paths
        for paths in groups.values()
        if len(paths) > 1
    ]

    if not duplicates:
        print("Byte-identical duplicates were not found.")
        return

    for index, paths in enumerate(
        sorted(duplicates, key=lambda items: (-items[0].stat().st_size, str(items[0]))),
        start=1,
    ):
        print(f"\nGROUP {index} — {paths[0].stat().st_size} bytes")
        for path in paths:
            print(path.relative_to(ROOT))

    print(
        "\nNothing was deleted. "
        "Before deleting an asset, verify serialized references and SF_05."
    )


if __name__ == "__main__":
    main()
