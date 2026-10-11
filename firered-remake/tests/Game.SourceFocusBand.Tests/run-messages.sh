#!/usr/bin/env bash
set -euo pipefail
CONTENT_ROOT="$(realpath "${1:?Pass product root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned source root}")"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceFocusBand.Tests/Game.SourceFocusBand.Tests.csproj --configfile "$CONTENT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceFocusBand.Tests/bin/Debug/net10.0/Game.SourceFocusBand.Tests.dll "$CONTENT_ROOT" "$REFERENCE_ROOT" "$OUTPUT" --messages > "$OUTPUT/run.log" 2>&1
python3 - "$OUTPUT/result.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['messagesOnly'] and r['groups']==7 and r['failures']==0
print(json.dumps(r))
PY
