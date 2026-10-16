#!/usr/bin/env python3
"""Focused real CLI conversion checks; never overwrite the supplied milestone."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import subprocess
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument("--project-root", required=True, type=Path)
p.add_argument("--dotnet", required=True, type=Path)
p.add_argument("--tooling-dll", required=True, type=Path)
p.add_argument("--source", required=True, type=Path)
p.add_argument("--output", required=True, type=Path)
a = p.parse_args()
root, source, output = a.project_root.resolve(), a.source.resolve(), a.output.resolve()
output.mkdir(parents=True, exist_ok=False)
original = source.read_bytes()
observations = []

def run(name, src, dst, success):
    cmd = [str(a.dotnet.resolve()), str(a.tooling_dll.resolve()),
           "convert-current-checkpoint", str(src), str(dst)]
    result = subprocess.run(cmd, cwd=root, capture_output=True, text=True)
    (output / (name + ".log")).write_text(result.stdout + result.stderr)
    assert (result.returncode == 0) == success, (name, result.returncode, result.stderr[-1000:])
    assert source.read_bytes() == original, "original milestone was modified"
    observations.append({"case": name, "exitCode": result.returncode,
                         "sourceBytesUnchanged": True})

target = output / "current.json"
run("valid", source, target, True)
converted = target.read_bytes()
old, new = json.loads(original), json.loads(converted)
assert old["RulesVersion"] == "firered-current-campaign-v2"
assert new["RulesVersion"] == "firered-current-campaign-v3"
assert new["ContentVersion"] == old["ContentVersion"] == "firered-current-content-v4"
expected_payload = json.loads(json.dumps(old["Payload"]))
expected_payload.pop("ReleasedPokemon", None)
if expected_payload.get("HallOfFame"):
    expected_payload["HallOfFame"].pop("RibbonRecipients", None)
owned = list(expected_payload["Party"])
owned += [p for box in expected_payload["Storage"]["Boxes"] for p in box["Slots"] if p]
if (expected_payload.get("Route5DayCare") or {}).get("Resident"):
    owned.append(expected_payload["Route5DayCare"]["Resident"]["Pokemon"])
for slot in ("First", "Second"):
    if (expected_payload.get("FourIslandDayCare") or {}).get(slot):
        owned.append(expected_payload["FourIslandDayCare"][slot]["Pokemon"])
ids = {p["InstanceId"] for p in owned}
for key in ("EvolutionHistory", "PrizePokemon", "DayCareEggs"):
    if expected_payload.get(key) is not None:
        expected_payload[key] = [p for p in expected_payload[key] if p["InstanceId"] in ids]
exploration = expected_payload["Exploration"]
if exploration.get("Captured") is not None:
    exploration["Captured"] = [p for p in exploration["Captured"] if p["InstanceId"] in ids]
# Retained settlements must be an unchanged ordered subset. All other remaining
# values are compared exactly; the CLI also applies the current semantic validator.
retained = new["Payload"]["Exploration"]["Battles"]
remaining = iter(exploration["Battles"])
for row in retained:
    assert any(candidate == row for candidate in remaining), "settlement changed or reordered"
assert retained[-1:] == exploration["Battles"][-1:]
exploration["Battles"] = retained
assert expected_payload == new["Payload"], "conversion changed remaining gameplay values"
assert old["Generation"] == new["Generation"]
assert old["SchemaVersion"] == new["SchemaVersion"]
assert old["GameEdition"] == new["GameEdition"]
run("refuse-existing-target", source, target, False)
assert target.read_bytes() == converted
run("refuse-source-as-target", source, source, False)
bad = output / "bad-envelope.json"
bad.write_bytes(original.replace(b'"Generation":', b'"Generatoin":', 1))
bad_target = output / "bad-target.json"
run("refuse-malformed-before-publication", bad, bad_target, False)
assert not bad_target.exists()
double_target = output / "double-converted.json"
run("refuse-current-as-legacy", target, double_target, False)
assert not double_target.exists()
for name, arguments in [
    ("refuse-retired-upgrade-router", ["save", "upgrade-celio", str(source), str(output / "retired-upgrade.json")]),
    ("refuse-retired-lab-replay", ["scenario", "replay-campaign-window", str(output / "retired-replay.json")]),
]:
    result = subprocess.run([str(a.dotnet.resolve()), str(a.tooling_dll.resolve()), *arguments],
                            cwd=root, capture_output=True, text=True)
    (output / (name + ".log")).write_text(result.stdout + result.stderr)
    assert result.returncode == 2, (name, result.returncode)
    assert not Path(arguments[-1]).exists(), "retired command published an output"
    assert source.read_bytes() == original, "original milestone was modified"
    observations.append({"case": name, "exitCode": result.returncode,
                         "sourceBytesUnchanged": True})
assert not list(output.glob(".firered-checkpoint-*.tmp"))
report = {"passed": True, "cases": observations, "source": str(source),
          "target": str(target), "sourceBytesUnchanged": True,
          "generation": new["Generation"], "payloadHash": new["PayloadHash"],
          "remainingGameplayValuesUnchanged": True, "retiredEntityFieldsRemoved": True}
(output / "result.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"passed": True, "cases": len(observations),
                  "generation": new["Generation"], "target": str(target)}))
