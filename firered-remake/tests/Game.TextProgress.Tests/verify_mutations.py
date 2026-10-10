#!/usr/bin/env python3
"""Verify the literal source traces reject three realistic timing regressions.

Only our C# implementation/test executable is run. Original reference sources
remain read-only inputs to SourceVerifier. Isolated copies prevent mutation of
the product file or replacement of the real test run's evidence.
"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
EVIDENCE = HERE / "evidence"
DOTNET = Path(os.environ["DOTNET_ROOT"]) / "dotnet"
if not DOTNET.is_file():
    raise SystemExit("Source scripts/env.sh first; the pinned .NET toolchain is required.")

original = (ROOT / "src/Game.Godot/SourceTextProgress.cs").read_text()
mutations = [
    ("configured-speed-not-decremented", "int textSpeed = ConfiguredSpeed - 1;", "int textSpeed = ConfiguredSpeed;"),
    ("newline-consumes-whole-frame", "continue; // RENDER_REPEAT re-enters", "return; // RENDER_REPEAT re-enters"),
    ("press-draws-without-update-frame", "return; // RENDER_UPDATE, including", "continue; // RENDER_UPDATE, including"),
]
results = []
for name, before, after in mutations:
    if original.count(before) != 1:
        raise SystemExit("Mutation target missing or ambiguous: " + name)
    with tempfile.TemporaryDirectory(prefix="firered-text-" + name + "-") as directory:
        scratch = Path(directory)
        files = [
            "global.json", "Directory.Build.props", ".tools/reference-root",
            "content/raw/starter-audit/tree.json",
            "tests/Game.TextProgress.Tests/Game.TextProgress.Tests.csproj",
            "tests/Game.TextProgress.Tests/Program.cs",
            "tests/Game.TextProgress.Tests/SourceVerifier.cs",
            "tests/Game.TextProgress.Tests/source-lock.json",
            "tests/Game.TextProgress.Tests/Fixtures/source-traces.json",
        ]
        for relative in files:
            target = scratch / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / relative, target)
        product = scratch / "src/Game.Godot/SourceTextProgress.cs"
        product.parent.mkdir(parents=True)
        product.write_text(original.replace(before, after))
        project = scratch / "tests/Game.TextProgress.Tests/Game.TextProgress.Tests.csproj"
        command = [str(DOTNET), "build", str(project), "-m:1", "-nr:false"]
        built = subprocess.run(command, cwd=scratch, text=True, capture_output=True, check=False)
        (EVIDENCE / (name + "-build.log")).write_text(built.stdout + built.stderr)
        if built.returncode != 0:
            raise SystemExit("Mutant failed to compile, so no valid rejection evidence: " + name)
        run_command = [str(DOTNET), "run", "--no-build", "--project", str(project), "--", str(scratch)]
        ran = subprocess.run(run_command, cwd=scratch, text=True, capture_output=True, check=False)
        (EVIDENCE / (name + "-run.log")).write_text(ran.stdout + ran.stderr)
        result_path = scratch / "tests/Game.TextProgress.Tests/evidence/results.json"
        if not result_path.is_file():
            raise SystemExit("Mutant produced no result evidence: " + name)
        result = json.loads(result_path.read_text())
        failures = [item["name"] for item in result["results"] if not item["passed"]]
        if ran.returncode != 1 or result["status"] != "failed" or "literal-source-traces-cover-every-frame" not in failures:
            raise SystemExit("Literal source traces did not reject mutant: " + name)
        shutil.copyfile(result_path, EVIDENCE / (name + ".json"))
        results.append({
            "name": name, "status": "rejected", "exitCode": ran.returncode,
            "runtime": result["runtime"], "productSha256": result["productSha256"],
            "failedChecks": failures, "buildCommand": command, "runCommand": run_command,
        })

if len(results) != 3:
    raise SystemExit("Mutation suite is incomplete.")
report = {
    "status": "passed", "mutants": results,
    "originalProductSha256": hashlib.sha256((ROOT / "src/Game.Godot/SourceTextProgress.cs").read_bytes()).hexdigest(),
    "scope": "Three isolated product mutations are rejected by reviewed source-derived literal traces; no reference source execution.",
}
(EVIDENCE / "mutations.json").write_text(json.dumps(report, indent=2) + "\n")
print(json.dumps(report))
