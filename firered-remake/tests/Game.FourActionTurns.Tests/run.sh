#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT_ROOT="${1:?Pass the complete product catalog root}"
REFERENCE_ROOT="${2:?Pass the pinned source reference root}"
OUTPUT="${3:?Pass a new evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Output already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PROJECT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourActionTurns.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT"
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.FourActionTurns.Tests/Game.FourActionTurns.Tests.csproj --configfile tests/Game.FourActionTurns.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$OUTPUT/test-restore.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourActionTurns.Tests/Game.FourActionTurns.Tests.csproj --no-restore -m:1 -v:q > "$OUTPUT/test-build-final.log" 2>&1
dotnet tests/Game.FourActionTurns.Tests/bin/Debug/net10.0/Game.FourActionTurns.Tests.dll "$PROJECT_ROOT" "$OUTPUT" > "$OUTPUT/run-final.log" 2>&1
cat "$OUTPUT/run-final.log"
