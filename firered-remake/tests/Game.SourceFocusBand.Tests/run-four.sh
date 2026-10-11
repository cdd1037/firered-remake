#!/usr/bin/env bash
set -euo pipefail
TASK_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="${1:?Pass authenticated content root}"
REFERENCE_ROOT="${2:?Pass pinned pret root}"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$TASK_ROOT"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceFocusBand.Tests/Game.SourceFocusBand.Tests.csproj --configfile "$CONTENT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceFocusBand.Tests/bin/Debug/net10.0/Game.SourceFocusBand.Tests.dll "$CONTENT_ROOT" "$REFERENCE_ROOT" "$OUTPUT" --four > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
