#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass the existing product catalog root}"
REFERENCE_ROOT="${2:?Pass the pinned original source root}"
OUTPUT="${3:?Pass a new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Evidence output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourTurnMoves.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log"
cmp tests/Game.FourTurnMoves.Tests/source-vectors.json "$OUTPUT/source/source-vectors.json"
for project in Game.FourTurnMoves.Tests Game.TurnMoveParity.Tests; do
    flock /tmp/firered-remake-build.lock dotnet build "tests/$project/$project.csproj" --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/$project-build.log" 2>&1
done
dotnet tests/Game.FourTurnMoves.Tests/bin/Debug/net10.0/Game.FourTurnMoves.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/source/source-vectors.json" "$OUTPUT/result.json" > "$OUTPUT/four-turns.log" 2>&1
dotnet tests/Game.TurnMoveParity.Tests/bin/Debug/net10.0/Game.TurnMoveParity.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/legacy-candidate.json" tests/Game.TurnMoveParity.Tests/legacy-baseline-08f-ally-immunity.json > "$OUTPUT/legacy.log" 2>&1
cat "$OUTPUT/four-turns.log" "$OUTPUT/legacy.log"
