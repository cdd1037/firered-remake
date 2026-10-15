#!/usr/bin/env python3
"""Reproduce the tiny generated contract in memory from authenticated git objects."""
import importlib.util
import json
from pathlib import Path
import sys

root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("hall_pc_import", root / "scripts/import-hall-pc.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
lock = json.loads((root / "scripts/hall-pc.lock.json").read_text())
rebuilt = module.build(Path(sys.argv[1]))
data = (json.dumps(rebuilt, ensure_ascii=False, indent=2) + "\n").encode()
assert data == (root / "content/generated/hall-pc/presentation.json").read_bytes()
assert [row["path"] for row in rebuilt["inputs"]] == [row["path"] for row in lock["inputs"]]
print(json.dumps(dict(passed=True, sourceCommit=module.COMMIT,
    originalInputCount=len(rebuilt["inputs"]), outputBytes=len(data)), indent=2))
