#!/usr/bin/env bash
set -euo pipefail
candidate_root="$(cd "$(dirname "$0")/../.." && pwd)"
content_root="$(realpath "${1:?Pass authenticated content root}")"
output="${3:?Pass new output directory}"
[[ ! -e "$output" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$output"
output="$(realpath "$output")"
source "$content_root/scripts/env.sh"
cd "$candidate_root"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedPreparedMoves.Tests/Game.SharedPreparedMoves.Tests.csproj --configfile "$content_root/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$output/build.log" 2>&1
dotnet tests/Game.SharedPreparedMoves.Tests/bin/Debug/net10.0/Game.SharedPreparedMoves.Tests.dll "$content_root" "$output/result.json" > "$output/run.log" 2>&1
cat "$output/run.log"
