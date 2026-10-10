#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then echo 'Usage: run.sh AUTHENTICATED_CONTENT_PROJECT PINNED_SOURCE_ROOT' >&2; exit 2; fi
FIRERED_WEATHER_CONTENT=$(realpath "$1")
FIRERED_WEATHER_SOURCE=$(realpath "$2")
FIRERED_WEATHER_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
source "$FIRERED_WEATHER_CONTENT/scripts/env.sh"
cd "$FIRERED_WEATHER_ROOT"
mkdir -p evidence/weather-abilities
python3 tests/Game.SourceWeatherAbilities.Tests/extract-source-vectors.py "$FIRERED_WEATHER_SOURCE" evidence/weather-abilities
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceWeatherAbilities.Tests --configfile tests/Game.SourceWeatherAbilities.Tests/NuGet.Config -m:1 -p:UseSharedCompilation=false
dotnet tests/Game.SourceWeatherAbilities.Tests/bin/Debug/net10.0/Game.SourceWeatherAbilities.Tests.dll "$FIRERED_WEATHER_CONTENT" "$FIRERED_WEATHER_CONTENT" "$FIRERED_WEATHER_SOURCE" evidence/weather-abilities/source-vectors.json
