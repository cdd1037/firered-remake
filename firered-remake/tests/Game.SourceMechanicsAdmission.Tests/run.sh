#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 1 || ! $1 =~ ^[A-Za-z0-9_-]+$ ]]; then
  echo 'usage: bash tests/Game.SourceMechanicsAdmission.Tests/run.sh UNIQUE_RUN' >&2
  exit 2
fi
source scripts/env.sh
mechanics_output="$PWD/tests/Game.SourceMechanicsAdmission.Tests/evidence/$1"
if [[ -e "$mechanics_output" ]]; then echo "Output already exists: $mechanics_output" >&2; exit 2; fi
mkdir -p "$mechanics_output"
python3 tests/Game.DamageAbilityGaps.Tests/source_probe.py "$FIRERED_ORIGINAL_REFERENCE" "$mechanics_output" > "$mechanics_output/oracle-build.log"
for mechanics_project in Game.SourceMechanicsAdmission.Tests Game.SourceMechanicsParity.Tests; do
  flock /tmp/firered-remake-build.lock dotnet build "tests/$mechanics_project/$mechanics_project.csproj" --configfile .toolhome/recovery-local-only.config -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$mechanics_output/$mechanics_project.build.log" 2>&1
done
dotnet tests/Game.SourceMechanicsAdmission.Tests/bin/Debug/net10.0/Game.SourceMechanicsAdmission.Tests.dll "$PWD" "$PWD" "$mechanics_output/source-oracle" > "$mechanics_output/admission.log"
dotnet tests/Game.SourceMechanicsParity.Tests/bin/Debug/net10.0/Game.SourceMechanicsParity.Tests.dll "$PWD" tests/Game.SourceMechanicsParity.Tests/baseline-1579dfc.json > "$mechanics_output/legacy-parity.json"
cat "$mechanics_output/admission.log"
