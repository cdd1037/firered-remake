#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_PROJECT="$(realpath "${1:?Pass the authenticated content project}")"
OUTPUT="${2:?Pass a new evidence directory}"
NUGET_CONFIG="${3:?Pass the official local NuGet config}"
if [[ -e "$OUTPUT" ]]; then echo "Evidence directory already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_PROJECT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceLifecycle.Tests/Game.SourceLifecycle.Tests.csproj --configfile "$NUGET_CONFIG" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceLifecycle.Tests/bin/Debug/net10.0/Game.SourceLifecycle.Tests.dll "$CONTENT_PROJECT" > "$OUTPUT/result.log" 2>&1
cat "$OUTPUT/result.log"
