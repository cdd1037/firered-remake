#!/usr/bin/env python3
"""Drive the bounded Mt. Moon combination checks; this does not operate a desktop."""
import fcntl
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
if len(sys.argv) != 2 or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_.-]{0,70}", sys.argv[1]):
    raise SystemExit("Expected one new short run name")
OUT = ROOT / "evidence/recovery-mtmoon" / sys.argv[1]
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


def run(name, command, locked=False):
    log = OUT / (name + ".log")
    with log.open("w", encoding="utf-8") as stream:
        if locked:
            with open("/tmp/firered-remake-build.lock", "a") as lock:
                fcntl.flock(lock, fcntl.LOCK_EX)
                result = subprocess.run(list(map(str, command)), cwd=ROOT, env=ENV, stdout=stream, stderr=subprocess.STDOUT)
        else:
            result = subprocess.run(list(map(str, command)), cwd=ROOT, env=ENV, stdout=stream, stderr=subprocess.STDOUT)
    steps.append({"name": name, "exitCode": result.returncode, "log": str(log.relative_to(ROOT))})
    print(name, "exit", result.returncode, flush=True)
    if result.returncode:
        raise RuntimeError(name + " failed; see " + str(log))

def suite(name, *args):
    run(name, [DOTNET, ROOT / f"tests/{name}/bin/Debug/net10.0/{name}.dll", *args])


try:
    run("build", [DOTNET, "build", "FireRed.sln", "-m:1", "-nr:false"], True)
    run("six-foundation-runners", ["python3", "dev", "test"])
    for name in ["MtMoonContentRecovery", "MtMoonBattleRecovery", "MtMoonTrainerRecovery", "MtMoonItemsRecovery", "MoonStoneRecovery", "MtMoonTrainerArtRecovery", "MtMoonBattleTerrainRecovery"]:
        suite("Game." + name + ".Tests", ROOT)
    suite("Game.MtMoonEncounterRecovery.Tests", ROOT, REFERENCE)
    run("battle-source", ["python3", "tests/Game.MtMoonBattleRecovery.Tests/source_oracle.py", "--check", "--reference", REFERENCE])
    run("trainer-source", ["python3", "tests/Game.MtMoonTrainerRecovery.Tests/source_oracle.py"])
    suite("Game.MtMoonCampaignRecovery.Tests", ROOT,
        ROOT / "tests/Game.MagikarpRecovery.Tests/evidence/campaign-02/natural-purchased.json", OUT / "route")
    suite("Game.MtMoonItemActionsRecovery.Tests", ROOT, OUT / "route/before-cave.json")
    suite("Game.MtMoonCampaignAuditRecovery.Tests", ROOT, OUT / "independent-boundaries", OUT / "route")
    run("independent-prior-route-fixes", [DOTNET,
        ROOT / "tests/Game.MtMoonCampaignAuditRecovery.Tests/bin/Debug/net10.0/Game.MtMoonCampaignAuditRecovery.Tests.dll",
        "fixes", ROOT, OUT / "independent-fixes"])
    run("save-upgrade-cli", ["python3", "tests/Game.RecoverySaveUpgrade.Tests/verify.py"])
    run("synth-manifest", ["python3", "scripts/export-mtmoon-synth-manifest.py", "--check"])
    run("presentation", ["python3", "tests/Game.MtMoonUiRecovery.Tests/verify_godot.py"])

    result = {"passed": True, "steps": steps,
        "nativeWindowVerifiedByThisRunner": False, "audibleVerified": False,
        "scope": "Selected current MtMoon combination matrix; not all repository tests or historical native replay."}
except Exception as error:
    result = {"passed": False, "steps": steps, "error": str(error)}
(OUT / "result.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"passed": result["passed"], "steps": len(steps), "result": str(OUT / "result.json"), "error": result.get("error")}))
raise SystemExit(0 if result["passed"] else 1)
