#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass authenticated existing content root}"
REFERENCE_ROOT="${2:?Pass pinned original FireRed source root}"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SharedLateTurn.Tests/source_probe.py --source "$REFERENCE_ROOT" --report "$OUTPUT/source-proof.json" > "$OUTPUT/source.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedLateTurn.Tests/Game.SharedLateTurn.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedLateTurn.Tests/bin/Debug/net10.0/Game.SharedLateTurn.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
python3 -m json.tool "$OUTPUT/result.json" > /dev/null
cat "$OUTPUT/run.log"
