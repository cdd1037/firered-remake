#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 3 ]]; then echo 'Usage: test-six-island-encounters-bounded.sh PRODUCT_ROOT FIXED_COHORT OUTPUT_DIR' >&2; exit 2; fi
ENCOUNTER_SOURCE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PRODUCT_ROOT="$(realpath "$1")"
FIXED_COHORT="$(realpath "$2")"
mkdir -p "$3"
ENCOUNTER_OUTPUT="$(realpath "$3")"
source "$PRODUCT_ROOT/scripts/env.sh"
for assembly in Game.Domain Game.Content Game.Application; do test -s "$FIXED_COHORT/$assembly.dll"; done
for suite in Game.SixIslandEncounters.Tests Game.SixIslandEncounterApp.Tests; do
  flock /tmp/firered-remake-build.lock dotnet build "$ENCOUNTER_SOURCE_ROOT/tests/$suite/$suite.csproj" -m:1 -nr:false --nologo -p:UseSharedCompilation=false \
    "-p:FixedDomainAssembly=$FIXED_COHORT/Game.Domain.dll" "-p:FixedContentAssembly=$FIXED_COHORT/Game.Content.dll" "-p:FixedApplicationAssembly=$FIXED_COHORT/Game.Application.dll" \
    > "$ENCOUNTER_OUTPUT/$suite.build.log" 2>&1
  dotnet "$ENCOUNTER_SOURCE_ROOT/tests/$suite/bin/Debug/net10.0/$suite.dll" "$PRODUCT_ROOT" "$ENCOUNTER_OUTPUT/$suite.json" \
    > "$ENCOUNTER_OUTPUT/$suite.run.log" 2>&1
done
