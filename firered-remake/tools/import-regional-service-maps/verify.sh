#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
baseline="$1"
reference="$2"
export FIRERED_TOOLCHAIN_ROOT="$3"
source scripts/env.sh
python3 tools/import-regional-service-maps/build_source_lock.py "$reference"
python3 tools/import-regional-service-maps/import_regional_service_maps.py --check
python3 tests/Game.RegionalServiceMaps.Tests/test_import.py --reference "$reference" --baseline "$baseline"
python3 tools/import-regional-service-maps/prepare_proof.py "$baseline"
flock /tmp/firered-remake-build.lock dotnet build proof/tests/Game.RegionalServiceMaps.Tests/Game.RegionalServiceMaps.Tests.csproj --configfile proof/NuGet.Config -p:RestoreLockedMode=true -m:1 -nr:false -p:UseSharedCompilation=false
flock /tmp/firered-remake-build.lock dotnet proof/tests/Game.RegionalServiceMaps.Tests/bin/Debug/net10.0/Game.RegionalServiceMaps.Tests.dll "$PWD"
