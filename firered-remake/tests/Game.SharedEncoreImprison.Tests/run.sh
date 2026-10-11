#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="$(realpath "${1:?Pass authenticated content root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned original source root}")"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SharedEncoreImprison.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source.json" > "$OUTPUT/source.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedEncoreImprison.Tests/Game.SharedEncoreImprison.Tests.csproj --configfile "$CONTENT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedEncoreImprison.Tests/bin/Debug/net10.0/Game.SharedEncoreImprison.Tests.dll "$CONTENT_ROOT" "$OUTPUT/results.json" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
