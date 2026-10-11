#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass authenticated product content root}"
REFERENCE_ROOT="${2:?Pass pinned original source root}"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Evidence output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
cd "$CANDIDATE_ROOT"
source "$PRODUCT_ROOT/scripts/env.sh"
export DOTNET_CLI_HOME="$CANDIDATE_ROOT/.toolhome"
export NUGET_PACKAGES="$DOTNET_CLI_HOME/nuget"
export XDG_DATA_HOME="$DOTNET_CLI_HOME/share" XDG_CONFIG_HOME="$DOTNET_CLI_HOME/config" XDG_CACHE_HOME="$DOTNET_CLI_HOME/cache"
python3 tests/Game.SharedPartyHealing.Tests/source-proof.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedPartyHealing.Tests/Game.SharedPartyHealing.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedPartyHealing.Tests/bin/Debug/net10.0/Game.SharedPartyHealing.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
