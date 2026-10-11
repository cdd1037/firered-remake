#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass the existing complete catalog and toolchain root}"
REFERENCE_ROOT="${2:?Pass the pinned pret source root}"
OUTPUT="${3:?Pass a new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Evidence output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SharedHitSequence.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log"
cmp tests/Game.SharedHitSequence.Tests/source-vectors.json "$OUTPUT/source/vectors.json"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedHitSequence.Tests/Game.SharedHitSequence.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedHitSequence.Tests/bin/Debug/net10.0/Game.SharedHitSequence.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
python3 - "$OUTPUT" <<'PY'
import json,pathlib,sys
p=pathlib.Path(sys.argv[1]);r=json.loads((p/'result.json').read_text());s=json.loads((p/'source/source-report.json').read_text())
assert r['groups']>0 and r['checks']>0 and r['failures']==0 and r['actualTurns']>=26 and r['baselineResults']==80
assert all(x['passed'] for x in r['results']) and s['vectors']==51 and all(s['checks'].values()) and s['optimizedAndUbsanAgree']
assert (p/'run.log').read_text().splitlines()[-1].startswith('RESULT groups=')
print(json.dumps(r,indent=2))
PY
