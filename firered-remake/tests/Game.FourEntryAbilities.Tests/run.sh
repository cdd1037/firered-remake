#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 3 ]]; then echo 'Usage: run.sh AUTHENTICATED_CONTENT_ROOT PINNED_SOURCE_ROOT NEW_EVIDENCE_DIRECTORY' >&2; exit 2; fi
ENTRY_CONTENT=$(realpath "$1")
ENTRY_SOURCE=$(realpath "$2")
ENTRY_PROJECT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
ENTRY_OUTPUT=$(realpath -m "$3")
source "$ENTRY_CONTENT/scripts/env.sh"
cd "$ENTRY_PROJECT"
if [[ -e "$ENTRY_OUTPUT" ]]; then echo "Use a new evidence directory: $ENTRY_OUTPUT" >&2; exit 2; fi
mkdir -p "$ENTRY_OUTPUT"
python3 tests/Game.FourEntryAbilities.Tests/source_probe.py "$ENTRY_SOURCE" "$ENTRY_OUTPUT" > "$ENTRY_OUTPUT/source-probe.log" 2>&1
python3 tests/Game.SourceTraceForecast.Tests/extract-source-vectors.py "$ENTRY_SOURCE" "$ENTRY_OUTPUT/trace" > "$ENTRY_OUTPUT/trace-oracle.log" 2>&1
python3 tests/Game.SourceWeatherAbilities.Tests/extract-source-vectors.py "$ENTRY_SOURCE" "$ENTRY_OUTPUT/weather" > "$ENTRY_OUTPUT/weather-oracle.log" 2>&1
for suite in Game.FourEntryAbilities.Tests Game.SourceMechanicsParity.Tests Game.SourceBattleIdentity.Tests Game.SourceTraceForecast.Tests Game.SourceWeatherAbilities.Tests; do
  flock /tmp/firered-remake-build.lock dotnet build "tests/$suite" --configfile tests/Game.FourEntryAbilities.Tests/NuGet.Config -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$ENTRY_OUTPUT/$suite.build.log" 2>&1
  case "$suite" in
    Game.FourEntryAbilities.Tests) arguments=("$ENTRY_CONTENT" "$ENTRY_SOURCE" "$ENTRY_OUTPUT") ;;
    Game.SourceMechanicsParity.Tests) arguments=("$ENTRY_CONTENT" tests/Game.SourceMechanicsParity.Tests/baseline-1579dfc.json) ;;
    Game.SourceBattleIdentity.Tests) arguments=("$ENTRY_CONTENT") ;;
    Game.SourceTraceForecast.Tests) arguments=("$ENTRY_CONTENT" "$ENTRY_CONTENT" "$ENTRY_SOURCE" "$ENTRY_OUTPUT/trace/source-vectors.json") ;;
    Game.SourceWeatherAbilities.Tests) arguments=("$ENTRY_CONTENT" "$ENTRY_CONTENT" "$ENTRY_SOURCE" "$ENTRY_OUTPUT/weather/source-vectors.json") ;;
  esac
  dotnet "tests/$suite/bin/Debug/net10.0/$suite.dll" "${arguments[@]}" > "$ENTRY_OUTPUT/$suite.log" 2>&1
  echo "PASS $suite"
done
