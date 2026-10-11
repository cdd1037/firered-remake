#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass existing complete product catalog root}"
REFERENCE_ROOT="${2:?Pass pinned original source root}"
OUTPUT="${3:?Pass new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Evidence output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourProtection.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log"
cmp tests/Game.FourProtection.Tests/source-vectors.json "$OUTPUT/source/source-vectors.json"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourProtection.Tests/Game.FourProtection.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.FourProtection.Tests/bin/Debug/net10.0/Game.FourProtection.Tests.dll "$PRODUCT_ROOT" legacy "$OUTPUT/legacy-candidate.json" tests/Game.FourProtection.Tests/legacy-e14578e.json > "$OUTPUT/legacy.log" 2>&1
dotnet tests/Game.FourProtection.Tests/bin/Debug/net10.0/Game.FourProtection.Tests.dll "$PRODUCT_ROOT" four "$OUTPUT/result.json" "$OUTPUT/source/source-vectors.json" > "$OUTPUT/four-protection.log" 2>&1
cat "$OUTPUT/source.log" "$OUTPUT/legacy.log" "$OUTPUT/four-protection.log"
