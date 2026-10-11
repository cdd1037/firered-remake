#!/usr/bin/env bash
# Runs the recovered M04 source against an explicit authenticated content root.
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROJECT_ROOT="${1:?Pass restored complete product catalog root}"
OUTPUT="${2:?Pass a new evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Output already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PROJECT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SourceTargeting.Tests/source_probe.py "${FIRERED_ORIGINAL_REFERENCE:?Pinned reference root required}" "$OUTPUT"
cc -std=c11 -O2 -Wall -Wextra -Werror tests/Game.SourceTargeting.Tests/source_damage_oracle.c -o "$OUTPUT/source-damage-oracle"
# Dependencies are BCL/local projects only; repeatable restore is locked.
dotnet restore tests/Game.SourceTargeting.Tests/Game.SourceTargeting.Tests.csproj --configfile tests/Game.SourceTargeting.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$OUTPUT/restore.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceTargeting.Tests/Game.SourceTargeting.Tests.csproj --no-restore -m:1 -nr:false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceTargeting.Tests/bin/Debug/net10.0/Game.SourceTargeting.Tests.dll "$PROJECT_ROOT" "$OUTPUT" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
