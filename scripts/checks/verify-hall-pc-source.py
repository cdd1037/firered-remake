#!/usr/bin/env python3
"""Reproduce the tiny generated contract in memory from authenticated git objects."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import importlib.util
import json
from pathlib import Path
import sys

root = _PROJECT_ROOT
spec = importlib.util.spec_from_file_location("hall_pc_import", root / "scripts/imports/story/import-hall-pc.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
lock = json.loads((root / "scripts/recipes/story/hall-pc.lock.json").read_text())
rebuilt = module.build(Path(sys.argv[1]))
data = (json.dumps(rebuilt, ensure_ascii=False, indent=2) + "\n").encode()
assert data == (root / "content/generated/ui/hall-pc.json").read_bytes()
assert "inputs" not in rebuilt
print(json.dumps(dict(passed=True, sourceCommit=module.COMMIT,
    originalInputCount=len(lock["inputs"]), outputBytes=len(data)), indent=2))
