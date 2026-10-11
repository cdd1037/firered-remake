#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="$(realpath "${1:?Pass authenticated content and official tool environment root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned pret source root}")"
OUTPUT="${3:?Pass a new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SharedDelayedAttacks.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source-vectors.json" > "$OUTPUT/source.log" 2>&1
cmp tests/Game.SharedDelayedAttacks.Tests/source-vectors.json "$OUTPUT/source-vectors.json"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedDelayedAttacks.Tests/Game.SharedDelayedAttacks.Tests.csproj --configfile "$CONTENT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedDelayedAttacks.Tests/bin/Debug/net10.0/Game.SharedDelayedAttacks.Tests.dll "$CONTENT_ROOT" "$OUTPUT/results.json" "$OUTPUT/source-base-damage" > "$OUTPUT/run.log" 2>&1
python3 - "$OUTPUT/results.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['checks']>0 and len(r['labels'])>=13 and len(r['snapshots'])==12 and len(r['oracleInputs'])==12
assert r['cleanTurns']>=28 and r['replays']>=153
print(json.dumps({k:r[k] for k in ['checks','cleanTurns','replays']}))
PY
