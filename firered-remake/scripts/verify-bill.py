#!/usr/bin/env python3
"""Drive the bounded Bill combination checks; this does not operate a desktop."""
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
OUT = ROOT / "evidence/bill-combination" / sys.argv[1]
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
    for project in ["BillRegion", "BillBattle", "BillItems", "BillMachines", "BillCampaign", "BillReturnCampaign", "BillCampaignAudit"]:
        run("build-" + project, [DOTNET, "build", ROOT / "tests" / ("Game." + project + ".Tests"), "-p:RestoreLockedMode=true", "-m:1", "-nr:false"], True)
    for project in ["BillRegion", "BillBattle", "BillItems", "BillMachines"]:
        suite("Game." + project + ".Tests", ROOT)
    suite("Game.BillCampaign.Tests", ROOT, ROOT / "tests/Game.CeruleanGym.Tests/Fixtures/after-misty.json", OUT / "north-route")
    suite("Game.BillReturnCampaign.Tests", ROOT, ROOT / "tests/Game.BillCampaign.Tests/Fixtures/after-ss-ticket.json", OUT / "return-route")
    suite("Game.BillCampaignAudit.Tests", ROOT, OUT / "independent")
    run("bill-source-audio", ["python3", "tests/Game.BillSynthRecovery.Tests/test_source.py"])
    run("bill-presentation", ["python3", "tests/Game.BillPresentation.Tests/verify_godot.py"])
    if source_hashes() != before:
        raise RuntimeError("Production source changed during verification")
    result = {"passed": True, "sourceHashes": before, "steps": steps, "nativeWindowOperated": False, "audibleVerified": False}
except Exception as error:
    result = {"passed": False, "sourceHashes": before, "steps": steps, "error": str(error)}
(OUT / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
raise SystemExit(0 if result["passed"] else 1)
