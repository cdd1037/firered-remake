#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass authenticated content root}"
REFERENCE_ROOT="${2:?Pass pinned original FireRed source root}"
OUTPUT="${3:?Pass new output directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FixedDamage.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/oracle" > "$OUTPUT/source.log" 2>&1
cmp tests/Game.FixedDamage.Tests/source-vectors.json "$OUTPUT/oracle/source-vectors.json"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FixedDamage.Tests/Game.FixedDamage.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.FixedDamage.Tests/bin/Debug/net10.0/Game.FixedDamage.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
