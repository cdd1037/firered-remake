#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT="$(realpath "${1:?Pass authenticated content project}")"
OUTPUT="${2:?Pass a new evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Evidence directory already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourRewards.Tests/Game.FourRewards.Tests.csproj --configfile tests/Game.FourRewards.Tests/NuGet.Config -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.FourRewards.Tests/bin/Debug/net10.0/Game.FourRewards.Tests.dll "$CONTENT" > "$OUTPUT/result.log" 2>&1
cat "$OUTPUT/result.log"
