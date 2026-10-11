#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass existing authenticated content root}"
REFERENCE_ROOT="${2:?Pass pinned original source root}"
OUTPUT="${3:?Pass a new evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Evidence output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
cd "$CANDIDATE_ROOT"
source scripts/env.sh
python3 tests/Game.SharedItemTransfer.Tests/source-proof.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedItemTransfer.Tests/Game.SharedItemTransfer.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.SharedItemTransfer.Tests/bin/Debug/net10.0/Game.SharedItemTransfer.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
cat "$OUTPUT/run.log"
