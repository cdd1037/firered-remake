#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass authenticated content root}"
REFERENCE_ROOT="${2:?Pass pinned original FireRed source root}"
OUTPUT="${3:?Pass a new output directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.DynamicPower.Tests/source_probe.py --source "$REFERENCE_ROOT" --report "$OUTPUT/source-result.json" > "$OUTPUT/source.log" 2>&1
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.DynamicPower.Tests/Game.DynamicPower.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
"$DOTNET_ROOT/dotnet" tests/Game.DynamicPower.Tests/bin/Debug/net10.0/Game.DynamicPower.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
