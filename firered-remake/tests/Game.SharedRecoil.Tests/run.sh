#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass existing product catalog root}"
REFERENCE_ROOT="${2:?Pass pinned original source root}"
OUTPUT="${3:?Pass a new evidence directory}"
MODE="${4:-verify}"
[[ "$MODE" == verify || "$MODE" == utility ]] || { echo 'Unknown mode' >&2; exit 2; }
[[ ! -e "$OUTPUT" ]] || { echo 'Evidence output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
if [[ "$MODE" == verify ]]; then
 python3 tests/Game.SharedRecoil.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log"
 cmp tests/Game.SharedRecoil.Tests/source-vectors.json "$OUTPUT/source/vectors.json"
fi
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedRecoil.Tests/Game.SharedRecoil.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
args=()
[[ "$MODE" != utility ]] || args+=(--utility)
dotnet tests/Game.SharedRecoil.Tests/bin/Debug/net10.0/Game.SharedRecoil.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" "${args[@]}" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
