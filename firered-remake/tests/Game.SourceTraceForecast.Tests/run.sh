#!/usr/bin/env bash
set -euo pipefail
if [[ $# -ne 2 ]]; then echo 'Usage: run.sh AUTHENTICATED_CONTENT_PROJECT PINNED_SOURCE_ROOT' >&2; exit 2; fi
FIRERED_TRACE_CONTENT=$(realpath "$1")
FIRERED_TRACE_SOURCE=$(realpath "$2")
FIRERED_TRACE_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
source "$FIRERED_TRACE_CONTENT/scripts/env.sh"
cd "$FIRERED_TRACE_ROOT"
mkdir -p evidence/trace-forecast
python3 tests/Game.SourceTraceForecast.Tests/extract-source-vectors.py "$FIRERED_TRACE_SOURCE" evidence/trace-forecast
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceTraceForecast.Tests --configfile tests/Game.SourceTraceForecast.Tests/NuGet.Config -m:1 -p:UseSharedCompilation=false
dotnet tests/Game.SourceTraceForecast.Tests/bin/Debug/net10.0/Game.SourceTraceForecast.Tests.dll "$FIRERED_TRACE_CONTENT" "$FIRERED_TRACE_CONTENT" "$FIRERED_TRACE_SOURCE" evidence/trace-forecast/source-vectors.json
