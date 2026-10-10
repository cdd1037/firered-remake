#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 4 || ! $4 =~ ^[A-Za-z0-9_-]+$ ]]; then
  echo 'usage: bash tests/Game.SourceTrappingAbilities.Tests/run.sh CONTENT_ROOT REFERENCE_ROOT TOOLCHAIN_ENV UNIQUE_OUTPUT' >&2
  exit 2
fi
trapping_content="$(realpath "$1")"
trapping_reference="$(realpath "$2")"
trapping_env="$(realpath "$3")"
trapping_output="$PWD/tests/Game.SourceTrappingAbilities.Tests/evidence/$4"
if [[ -e "$trapping_output" ]]; then echo "Output already exists: $trapping_output" >&2; exit 2; fi
source "$trapping_env"
mkdir -p "$trapping_output"
dotnet --version > "$trapping_output/sdk-version.txt"
python3 tests/Game.SourceTrappingAbilities.Tests/source_probe.py "$trapping_reference" "$trapping_output" > "$trapping_output/oracle-build.log"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceTrappingAbilities.Tests/Game.SourceTrappingAbilities.Tests.csproj --configfile tests/Game.SourceTrappingAbilities.Tests/NuGet.Config -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$trapping_output/build.log" 2>&1
dotnet tests/Game.SourceTrappingAbilities.Tests/bin/Debug/net10.0/Game.SourceTrappingAbilities.Tests.dll "$trapping_content" "$trapping_output/source-oracle" "$trapping_reference" > "$trapping_output/run.log"
cat "$trapping_output/run.log"
