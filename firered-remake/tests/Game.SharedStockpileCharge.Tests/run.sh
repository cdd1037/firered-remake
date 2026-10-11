#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass authenticated existing content and toolchain root}"
REFERENCE_ROOT="${2:?Pass pinned source root}"
OUTPUT="${3:?Pass a fresh output directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SharedStockpileCharge.Tests/source_probe.py --source "$REFERENCE_ROOT" --report "$OUTPUT/source-proof.json" > "$OUTPUT/source.log"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedStockpileCharge.Tests/Game.SharedStockpileCharge.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedStockpileCharge.Tests/bin/Debug/net10.0/Game.SharedStockpileCharge.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
python3 -m json.tool "$OUTPUT/result.json" > /dev/null
python3 -m json.tool "$OUTPUT/result.json.singles-after.json" > /dev/null
cmp tests/Game.SharedStockpileCharge.Tests/singles-before.json "$OUTPUT/result.json.singles-after.json"
cat "$OUTPUT/run.log"
