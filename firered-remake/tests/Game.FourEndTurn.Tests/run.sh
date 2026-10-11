#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="${1:?Pass authenticated product content root}"
REFERENCE_ROOT="${2:?Pass pinned source root}"
OUTPUT="${3:?Pass a new evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Evidence directory already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourEndTurn.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/source-probe.log" 2>&1
for suite in Game.FourEndTurn.Tests Game.FourEndTurnParity.Tests; do
 flock /tmp/firered-remake-build.lock dotnet restore "tests/$suite/$suite.csproj" --configfile tests/Game.FourEndTurn.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$OUTPUT/$suite-restore.log" 2>&1
 flock /tmp/firered-remake-build.lock dotnet build "tests/$suite/$suite.csproj" --no-restore -m:1 -nr:false -v:q > "$OUTPUT/$suite-build.log" 2>&1
done
dotnet tests/Game.FourEndTurn.Tests/bin/Debug/net10.0/Game.FourEndTurn.Tests.dll "$CONTENT_ROOT" "$OUTPUT" > "$OUTPUT/four-turn-run.log" 2>&1
dotnet tests/Game.FourEndTurnParity.Tests/bin/Debug/net10.0/Game.FourEndTurnParity.Tests.dll "$CONTENT_ROOT" > "$OUTPUT/singles-parity-run.log" 2>&1
cat "$OUTPUT/four-turn-run.log"
