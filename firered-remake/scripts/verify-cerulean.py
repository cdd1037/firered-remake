#!/usr/bin/env python3
"""Drive the bounded Cerulean combination checks; this does not operate a desktop."""
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
if len(sys.argv) != 2 or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,70}", sys.argv[1]):
    raise SystemExit("Expected one new short run name")
OUT = ROOT / "evidence/cerulean-combination" / sys.argv[1]
if OUT.exists():
    raise SystemExit("Refusing to replace prior evidence")
OUT.mkdir(parents=True)
TOOLS = Path(os.environ.get("FIRERED_TOOLCHAIN_ROOT") or (ROOT / ".tools/toolchain-root").read_text().strip())
REFERENCE = Path(os.environ.get("FIRERED_ORIGINAL_REFERENCE") or (ROOT / ".tools/reference-root").read_text().strip())
DOTNET = TOOLS / "dotnet/dotnet"
ENV = os.environ | {"FIRERED_TOOLCHAIN_ROOT": str(TOOLS), "FIRERED_ORIGINAL_REFERENCE": str(REFERENCE),
    "DOTNET_ROOT": str(DOTNET.parent), "DOTNET_CLI_HOME": str(ROOT / ".toolhome"),
    "NUGET_PACKAGES": str(ROOT / ".toolhome/nuget")}
steps = []

def source_hashes():
    return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
        for p in sorted((ROOT / "src").rglob("*.cs")) if not {"bin", "obj", ".godot"}.intersection(p.parts)}

def run(name, command, locked=False):
    log = OUT / (name + ".log")
    with log.open("w", encoding="utf-8") as stream:
        if locked:
            with open("/tmp/firered-remake-build.lock", "a") as lock:
                fcntl.flock(lock, fcntl.LOCK_EX)
                result = subprocess.run(list(map(str, command)), cwd=ROOT, env=ENV, stdout=stream, stderr=subprocess.STDOUT)
        else:
            result = subprocess.run(list(map(str, command)), cwd=ROOT, env=ENV, stdout=stream, stderr=subprocess.STDOUT)
    steps.append({"name": name, "exitCode": result.returncode, "log": str(log.relative_to(ROOT)),
        "logSha256": hashlib.sha256(log.read_bytes()).hexdigest()})
    print(name, "exit", result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(name + " failed; see " + str(log))

def suite(name, *args):
    run(name, [DOTNET, ROOT / f"tests/{name}/bin/Debug/net10.0/{name}.dll", *args])

before = source_hashes()
try:
    for project in ["CeruleanServices", "CeruleanGym", "CeruleanGymAudit", "CeruleanBattle", "CeruleanBattleAudit", "GymBattleTerrain"]:
        name = "Game." + project + ".Tests"
        run("build-" + project, [DOTNET, "build", ROOT / "tests" / name, "-p:RestoreLockedMode=true", "-m:1", "-nr:false"], True)
    suite("Game.CeruleanBattle.Tests", ROOT)
    suite("Game.CeruleanBattleAudit.Tests", ROOT)
    suite("Game.GymBattleTerrain.Tests", ROOT)
    run("battle-source", ["python3", "scripts/cerulean-battle-source-oracle.py", "--check"])
    run("terrain-source-pixels", ["python3", "tests/Game.GymBattleTerrain.Tests/test_import.py"])
    suite("Game.CeruleanServices.Tests", ROOT, OUT / "services")
    suite("Game.CeruleanGym.Tests", ROOT, ROOT / "tests/Game.CeruleanGym.Tests/Fixtures/before-misty.json", OUT / "win")
    suite("Game.CeruleanGym.Tests", ROOT, ROOT / "tests/Game.CeruleanGym.Tests/Fixtures/before-misty-loss.json", OUT / "loss", "--loss")
    suite("Game.CeruleanGymAudit.Tests", ROOT, OUT / "win", OUT / "independent")
    run("battle-presentation", ["python3", "tests/Game.CeruleanBattleUi.Tests/verify_godot.py"])
    if source_hashes() != before:
        raise RuntimeError("Production source changed during verification")
    result = {"passed": True, "steps": steps, "sourceHashes": before,
        "nativeWindowVerifiedByThisRunner": False, "audibleVerified": False,
        "scope": "Selected current Cerulean combination matrix; not all repository tests or historical native replay."}
except Exception as error:
    result = {"passed": False, "steps": steps, "sourceHashes": before, "error": str(error)}
(OUT / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"passed": result["passed"], "steps": len(steps), "result": str(OUT / "result.json"), "error": result.get("error")}))
raise SystemExit(0 if result["passed"] else 1)
