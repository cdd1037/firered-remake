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
python3 tests/Game.SharedRechargeRampage.Tests/source_probe.py --source "$REFERENCE_ROOT" --report "$OUTPUT/source.json" > "$OUTPUT/source.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedRechargeRampage.Tests/Game.SharedRechargeRampage.Tests.csproj --configfile "$CONTENT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedRechargeRampage.Tests/bin/Debug/net10.0/Game.SharedRechargeRampage.Tests.dll "$CONTENT_ROOT" "$OUTPUT/result.json" --combined > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
