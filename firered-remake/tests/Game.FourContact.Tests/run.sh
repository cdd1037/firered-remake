#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="$(realpath "${1:?Pass authenticated product root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned original source root}")"
OUTPUT="${3:?Pass a new evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Evidence directory already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourContact.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/source-probe.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourContact.Tests/Game.FourContact.Tests.csproj --configfile "$ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/contact-build.log" 2>&1
dotnet tests/Game.FourContact.Tests/bin/Debug/net10.0/Game.FourContact.Tests.dll "$CONTENT_ROOT" "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/contact-run.log" 2>&1
python3 - "$OUTPUT/report.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['failures']==0 and r['vectors']==156 and r['turns']==19 and r['legacyWholeTurnHashes']==30
print(json.dumps(r))
PY
