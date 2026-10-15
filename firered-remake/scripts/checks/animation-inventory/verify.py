#!/usr/bin/env python3
"""Source-only inventory checks; no builds, GUI, animation execution or test projects."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import gzip
import hashlib
import json
from pathlib import Path
import sys
sys.dont_write_bytecode = True
import scan

p = argparse.ArgumentParser(description=__doc__)
p.add_argument("--repo", type=Path, required=True)
p.add_argument("--reference", type=Path, required=True)
p.add_argument("--inventory", type=Path, required=True)
p.add_argument("--output", type=Path, required=True)
a = p.parse_args()
if a.output.exists():
    p.error("Refuse to overwrite verification report")
raw = a.inventory.read_bytes()
j = json.loads(gzip.decompress(raw) if a.inventory.suffix == ".gz" else raw)
checks = []


def check(name, value):
    checks.append({"name": name, "passed": bool(value)})
    if not value:
        raise AssertionError(name)


fs = {f["id"]: f for f in j["functions"]}
ms = {m["id"]: m for m in j["moves"]}
ts = {t["name"]: t for t in j["templates"]}
check("all exact 354 move IDs, unique scripts", set(ms) == set(range(1, 355)) and len({m["script"] for m in ms.values()}) == 354)
check("all direct templates resolve to a named callback", all(t in ts and ts[t]["callbackFunction"] in fs for m in ms.values() for t in m["templates"]))
check("all 183 distinct task roots resolve", len({t for m in ms.values() for t in m["visualTasks"] + m["soundTasks"]}) == 183 and all(any(fs[i]["name"] == t for i in m["callbackRoots"]) for m in ms.values() for t in m["visualTasks"] + m["soundTasks"]))
check("all static graph edges resolve", all(t in fs for f in fs.values() for t in f["directCalls"] + f["namedFunctionReferences"]))
check("direct closure is a subset of named-reference closure", all(set(m["directStaticCallClosure"]) <= set(m["knownStaticNamedClosure"]) for m in ms.values()))
check("closure actually follows all recorded named edges", all(set(fs[k]["directCalls"] + fs[k]["namedFunctionReferences"]) <= set(m["knownStaticNamedClosure"]) for m in ms.values() for k in m["knownStaticNamedClosure"] ))
check("all 68 existing oracle function definitions parsed", sum(f["inExistingCOracle"] for f in fs.values()) == 68)
check("six recipes remain six", [m["id"] for m in ms.values() if m["registeredRecipe"]] == [1, 10, 15, 33, 163, 206])
check("zero parsing-derived visual or complete execution claims", all(not m["originalRuntimePixelParityVerified"] and not m["completeExecutionClosureProven"] for m in ms.values()))
check("False Swipe deferred callbacks retained", {"AnimFalseSwipeSlice_Step1", "AnimFalseSwipeSlice_Step2", "AnimFalseSwipeSlice_Step3"} <= {fs[i]["name"] for i in ms[206]["knownStaticNamedClosure"]})
check("Double Slap conditional root not mistaken for admission", bool(ms[3]["conditionalScriptLines"]) and not ms[3]["registeredRecipe"])
check("Mirror Move alias shares Pound source but not runtime admission", ms[119]["line"] == ms[1]["line"] and ms[119]["callbackRoots"] == ms[1]["callbackRoots"] and not ms[119]["registeredRecipe"])
check("stored callback restoration remains a dynamic boundary", any(s["kind"] == "nonliteral-callback-assignment" for f in fs.values() if f["name"] == "SetCallbackToStoredInData6" for s in f["dynamicSites"]))
check("palette platform functions remain outside the scanned scope", any("BeginNormalPaletteFade" in m["outsideScopeCalls"] for m in ms.values()))
check("recursive script macro alias resolves to actual handler", all([fs[i]["name"] for i in c["sourceHandlers"]] == ["Cmd_jumpargeq"] for c in j["scriptCommands"] if c["command"] in {"jumprettrue", "jumpretfalse", "jumpreteq"}))
check("control command coverage is distinct from callback graph", len(j["scriptCommands"]) == 46 and all(c["sourceHandlers"] for c in j["scriptCommands"]))
check("current sources compared with frozen visual evidence, not silently accepted", not j["runtime"]["nativeEvidence"]["freshVerificationPerformed"] and bool(j["runtime"]["nativeEvidence"]["sourceWitnessMatchesCurrent"]))
check("fresh independent scan is deterministic", scan.run(a.repo.resolve(), a.reference.resolve()) == j)
report = {"passed": all(x["passed"] for x in checks), "checks": checks,
          "inventorySha256": hashlib.sha256(raw).hexdigest(),
          "scope": "Read-only source inventory and repeatability; no build, runtime, GUI or new visual acceptance."}
a.output.write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps({"passed": report["passed"], "checks": len(checks), "inventorySha256": report["inventorySha256"]}))
