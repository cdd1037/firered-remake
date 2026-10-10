#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 3 ]]; then
  echo "usage: bash tests/Game.DamageAbilityGaps.Tests/run.sh CONTENT_REPOSITORY_ROOT PINNED_REFERENCE_ROOT NEW_OUTPUT_DIRECTORY" >&2
  exit 2
fi
source scripts/env.sh
damage_evidence="$3"
if [[ -e "$damage_evidence" ]]; then echo "Output already exists: $damage_evidence" >&2; exit 2; fi
mkdir -p "$damage_evidence"
dotnet --version > "$damage_evidence/sdk-version.log"
python3 tests/Game.DamageAbilityGaps.Tests/source_probe.py "$2" "$damage_evidence" | tee "$damage_evidence/source-build-run.log"
dotnet restore tests/Game.DamageAbilityGaps.Tests/Game.DamageAbilityGaps.Tests.csproj --locked-mode --nologo -p:NuGetAudit=false > "$damage_evidence/restore-final.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.DamageAbilityGaps.Tests/Game.DamageAbilityGaps.Tests.csproj --no-restore --nologo -v:minimal > "$damage_evidence/build-final.log" 2>&1
dotnet tests/Game.DamageAbilityGaps.Tests/bin/Debug/net10.0/Game.DamageAbilityGaps.Tests.dll "$1" "$damage_evidence/source-vectors.json" | tee "$damage_evidence/run-final.log"
