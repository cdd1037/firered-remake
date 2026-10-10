#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then echo 'Usage: run.sh AUTHENTICATED_CONTENT_PROJECT PINNED_SOURCE_ROOT' >&2; exit 2; fi
FIRERED_CYCLE_CONTENT=$(realpath "$1")
FIRERED_CYCLE_SOURCE=$(realpath "$2")
FIRERED_CYCLE_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
source "$FIRERED_CYCLE_CONTENT/scripts/env.sh"
cd "$FIRERED_CYCLE_ROOT"
mkdir -p evidence/turn-cycle-abilities
python3 tests/Game.SourceTurnCycleAbilities.Tests/extract-source-vectors.py "$FIRERED_CYCLE_SOURCE" evidence/turn-cycle-abilities
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceTurnCycleAbilities.Tests --configfile tests/Game.SourceTurnCycleAbilities.Tests/NuGet.Config -m:1 -p:UseSharedCompilation=false
dotnet tests/Game.SourceTurnCycleAbilities.Tests/bin/Debug/net10.0/Game.SourceTurnCycleAbilities.Tests.dll "$FIRERED_CYCLE_CONTENT" "$FIRERED_CYCLE_CONTENT" "$FIRERED_CYCLE_SOURCE" evidence/turn-cycle-abilities/source-vectors.json
