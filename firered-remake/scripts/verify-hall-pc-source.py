#!/usr/bin/env python3
"""Reproduce the tiny generated contract in memory from authenticated git objects."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location("hall_pc_import", root / "scripts/import-hall-pc.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
lock = json.loads((root / "scripts/hall-pc.lock.json").read_text())
assert hashlib.sha256((root / "scripts/import-hall-pc.py").read_bytes()).hexdigest() == lock["importerSha256"]
rebuilt = module.build(Path(sys.argv[1]))
data = (json.dumps(rebuilt, ensure_ascii=False, indent=2) + "\n").encode()
assert data == (root / "content/generated/hall-pc/presentation.json").read_bytes()
assert hashlib.sha256(data).hexdigest() == lock["outputSha256"]
assert rebuilt["inputs"] == lock["inputs"]
print(json.dumps(dict(passed=True, sourceCommit=module.COMMIT, sourceTree=module.TREE,
    originalInputCount=len(rebuilt["inputs"]), outputBytes=len(data), outputSha256=lock["outputSha256"]), indent=2))
