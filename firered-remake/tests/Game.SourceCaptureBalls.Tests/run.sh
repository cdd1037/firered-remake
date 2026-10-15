#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="${1:?authenticated content root}"
REFERENCE_ROOT="${2:?pinned original source root}"
OUTPUT="${3:?fresh output directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
cd "$ROOT"
source "$CONTENT_ROOT/scripts/env.sh"
python3 tests/Game.SourceCaptureBalls.Tests/build-source-oracle.py "$REFERENCE_ROOT" "$OUTPUT/oracle" > "$OUTPUT/source.log"
cmp tests/Game.SourceCaptureBalls.Tests/source-vectors.json "$OUTPUT/oracle/source-vectors.json"
flock -w 240 /tmp/firered-remake-build.lock dotnet build tests/Game.SourceCaptureBalls.Tests/Game.SourceCaptureBalls.Tests.csproj --configfile "$CONTENT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceCaptureBalls.Tests/bin/Debug/net10.0/Game.SourceCaptureBalls.Tests.dll "$CONTENT_ROOT" "$OUTPUT/result" > "$OUTPUT/run.log" 2>&1
python3 - "$OUTPUT/result.json" <<'PYEND'
import json,pathlib,sys
d=json.loads(pathlib.Path(sys.argv[1]).read_text());assert d['passed'] and d['groups']==12 and d['failed']==0;print(d['checks'])
PYEND
