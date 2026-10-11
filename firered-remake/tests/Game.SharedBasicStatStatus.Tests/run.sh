#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass authenticated content root}"
REFERENCE_ROOT="${2:?Pass pinned original source root}"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/reference/verify-basic-stat-status-source.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/source.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedBasicStatStatus.Tests/Game.SharedBasicStatStatus.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedBasicStatStatus.Tests/bin/Debug/net10.0/Game.SharedBasicStatStatus.Tests.dll "$PRODUCT_ROOT" "$REFERENCE_ROOT" "$OUTPUT/results.json" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
