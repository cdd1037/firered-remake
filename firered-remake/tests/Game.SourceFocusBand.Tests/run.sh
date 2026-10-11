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
export DOTNET_CLI_HOME="$CANDIDATE_ROOT/.toolhome" NUGET_PACKAGES="$CANDIDATE_ROOT/.toolhome/nuget"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SourceFocusBand.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/oracle" > "$OUTPUT/extractor.log" 2>&1
flock -w 55 /tmp/firered-remake-build.lock dotnet restore tests/Game.SourceFocusBand.Tests/Game.SourceFocusBand.Tests.csproj --configfile tests/Game.SourceFocusBand.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$OUTPUT/restore.log" 2>&1
flock -w 55 /tmp/firered-remake-build.lock dotnet build tests/Game.SourceFocusBand.Tests/Game.SourceFocusBand.Tests.csproj --no-restore -m:1 -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceFocusBand.Tests/bin/Debug/net10.0/Game.SourceFocusBand.Tests.dll "$PROJECT_ROOT" "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
