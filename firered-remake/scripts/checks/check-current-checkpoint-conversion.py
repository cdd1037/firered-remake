#!/usr/bin/env python3
"""Focused real CLI conversion checks; never overwrite the supplied milestone."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import hashlib
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
original_sha = hashlib.sha256(original).hexdigest()
observations = []

def run(name, src, dst, success):
    cmd = [str(a.dotnet.resolve()), str(a.tooling_dll.resolve()),
           "convert-current-checkpoint", str(src), str(dst)]
    result = subprocess.run(cmd, cwd=root, capture_output=True, text=True)
    (output / (name + ".log")).write_text(result.stdout + result.stderr)
    assert (result.returncode == 0) == success, (name, result.returncode, result.stderr[-1000:])
    assert source.read_bytes() == original, "original milestone was modified"
    observations.append({"case": name, "exitCode": result.returncode,
                         "sourceSha256Unchanged": original_sha})

target = output / "current.json"
run("valid", source, target, True)
converted = target.read_bytes()
old, new = json.loads(original), json.loads(converted)
assert new["RulesVersion"] == "firered-current-campaign-v2"
assert new["ContentVersion"] == "firered-current-content-v4"
assert old["ContentVersion"] != new["ContentVersion"]
expected_payload = json.loads(json.dumps(old["Payload"]))
removed_origins = []
def remove_old_origin(node, path="Payload"):
    if isinstance(node, dict):
        for key, value in list(node.items()):
            if key == "NpcTradeOrigin" and isinstance(value, dict) and "CatalogFingerprint" in value:
                assert set(value) == {"CatalogFingerprint", "TradeId", "ReceivedLevel"}
                del value["CatalogFingerprint"]
                removed_origins.append(path + ".NpcTradeOrigin.CatalogFingerprint")
            remove_old_origin(value, path + "." + key)
    elif isinstance(node, list):
        for index, value in enumerate(node):
            remove_old_origin(value, path + f"[{index}]")
if old["ContentVersion"] == "firered-current-content-v3":
    remove_old_origin(expected_payload)
assert expected_payload == new["Payload"], "conversion changed actual gameplay values"
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
                         "sourceSha256Unchanged": original_sha})
assert not list(output.glob(".firered-checkpoint-*.tmp"))
report = {"passed": True, "cases": observations, "source": str(source),
          "target": str(target), "sourceSha256": original_sha,
          "targetSha256": hashlib.sha256(converted).hexdigest(),
          "generation": new["Generation"], "payloadHash": new["PayloadHash"],
          "wholeGameplayPayloadUnchanged": True, "removedObsoleteOriginFields": removed_origins}
(output / "result.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"passed": True, "cases": len(observations),
                  "generation": new["Generation"], "target": str(target)}))
