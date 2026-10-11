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
python3 tests/Game.FourBerryItems.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/source-probe.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.FourBerryItems.Tests/Game.FourBerryItems.Tests.csproj --configfile tests/Game.FourBerryItems.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$OUTPUT/restore.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourBerryItems.Tests/Game.FourBerryItems.Tests.csproj --no-restore -m:1 -nr:false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.FourBerryItems.Tests/bin/Debug/net10.0/Game.FourBerryItems.Tests.dll "$CONTENT_ROOT" "$OUTPUT" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
