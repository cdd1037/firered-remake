#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then echo 'Usage: run.sh CONTENT_PROJECT NEW_EVIDENCE_DIRECTORY' >&2; exit 2; fi
FIRERED_COLOR_CONTENT=$(realpath "$1")
FIRERED_COLOR_OUTPUT=$2
if [[ -e "$FIRERED_COLOR_OUTPUT" ]]; then echo 'Use a new evidence directory' >&2; exit 2; fi
mkdir -p "$FIRERED_COLOR_OUTPUT"
FIRERED_COLOR_OUTPUT=$(realpath "$FIRERED_COLOR_OUTPUT")
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
source "$FIRERED_COLOR_CONTENT/scripts/env.sh"
python3 tests/Game.SourceColorChange.Tests/build-source-oracle.py "$FIRERED_ORIGINAL_REFERENCE" "$FIRERED_COLOR_OUTPUT"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceColorChange.Tests --configfile tests/Game.SourceColorChange.Tests/NuGet.Config -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$FIRERED_COLOR_OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceColorChange.Tests/bin/Debug/net10.0/Game.SourceColorChange.Tests.dll "$FIRERED_COLOR_CONTENT" "$FIRERED_COLOR_CONTENT" "$FIRERED_COLOR_OUTPUT/original-color-change-oracle" > "$FIRERED_COLOR_OUTPUT/run.log" 2>&1
cat "$FIRERED_COLOR_OUTPUT/run.log"
