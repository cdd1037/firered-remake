#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass the existing product catalog root}"
REFERENCE_ROOT="${2:?Pass the pinned source root}"
OUTPUT="${3:?Pass a new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Evidence output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourVolatileTargets.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log"
cmp tests/Game.FourVolatileTargets.Tests/source-vectors.json "$OUTPUT/source/source-vectors.json"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourVolatileTargets.Tests/Game.FourVolatileTargets.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.FourVolatileTargets.Tests/bin/Debug/net10.0/Game.FourVolatileTargets.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/source/source-vectors.json" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
