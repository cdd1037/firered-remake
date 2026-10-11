#!/usr/bin/env bash
set -euo pipefail
FIRERED_WG_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
FIRERED_WG_CONTENT=$(realpath "${1:?Pass authenticated content project}")
FIRERED_WG_SOURCE=$(realpath "${2:?Pass pinned FireRed source root}")
FIRERED_WG_OUTPUT=$(realpath -m "${3:?Pass a new output directory}")
[[ ! -e "$FIRERED_WG_OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$FIRERED_WG_OUTPUT"
cd "$FIRERED_WG_ROOT"
source scripts/env.sh
python3 tests/Game.SourceWonderGuard.Tests/source_probe.py "$FIRERED_WG_SOURCE" "$FIRERED_WG_OUTPUT" > "$FIRERED_WG_OUTPUT/source.log"
cmp tests/Game.SourceWonderGuard.Tests/source-vectors.json "$FIRERED_WG_OUTPUT/source-vectors.json"
cp tests/Game.SourceWonderGuard.Tests/non-wg-before-a51e8a6.json "$FIRERED_WG_OUTPUT/non-wg-before.json"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceWonderGuard.Tests/Game.SourceWonderGuard.Tests.csproj --configfile "$FIRERED_WG_CONTENT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$FIRERED_WG_OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceWonderGuard.Tests/bin/Debug/net10.0/Game.SourceWonderGuard.Tests.dll "$FIRERED_WG_CONTENT" "$FIRERED_WG_SOURCE" "$FIRERED_WG_OUTPUT/source-vectors.json" "$FIRERED_WG_OUTPUT/result.json" > "$FIRERED_WG_OUTPUT/run.log" 2>&1
cat "$FIRERED_WG_OUTPUT/run.log"
