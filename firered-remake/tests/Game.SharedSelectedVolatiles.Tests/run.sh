#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="$(realpath "${1:?Pass authenticated content root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned source root}")"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CANDIDATE_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SharedSelectedVolatiles.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source.json" > "$OUTPUT/source.log" 2>&1
cmp tests/Game.SharedSelectedVolatiles.Tests/source-vectors.json "$OUTPUT/source.json"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedSelectedVolatiles.Tests/Game.SharedSelectedVolatiles.Tests.csproj --configfile "$CONTENT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedSelectedVolatiles.Tests/bin/Debug/net10.0/Game.SharedSelectedVolatiles.Tests.dll "$CONTENT_ROOT" "$OUTPUT/results.json" > "$OUTPUT/run.log" 2>&1
python3 - "$OUTPUT/results.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['checks']>0 and len(r['labels'])==13 and len(r['snapshots'])==12
print(json.dumps({'groups':len(r['labels']),'checks':r['checks'],'singlesSnapshots':len(r['snapshots'])}))
PY
