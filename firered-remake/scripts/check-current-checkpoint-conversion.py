#!/usr/bin/env python3
"""Focused real CLI conversion checks; never overwrite the supplied milestone."""
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
assert new["RulesVersion"] == "firered-current-campaign-v1"
assert old["RulesVersion"] != new["RulesVersion"]
assert {k: v for k, v in old.items() if k != "RulesVersion"} == {
    k: v for k, v in new.items() if k != "RulesVersion"}
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
          "wholeGameplayPayloadUnchanged": True}
(output / "result.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"passed": True, "cases": len(observations),
                  "generation": new["Generation"], "target": str(target)}))
