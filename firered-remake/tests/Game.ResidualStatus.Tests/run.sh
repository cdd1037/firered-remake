#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="${1:?Pass authenticated product content root}"
REFERENCE_ROOT="${2:?Pass pinned source root}"
OUTPUT="${3:?Pass a fresh absolute evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Use a fresh evidence directory: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
if [[ -z "${FIRERED_TOOLCHAIN_ROOT:-}" ]]; then read -r FIRERED_TOOLCHAIN_ROOT < "$CONTENT_ROOT/.tools/toolchain-root"; fi
export DOTNET_ROOT="$FIRERED_TOOLCHAIN_ROOT/dotnet"
export PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_HOME="$CANDIDATE_ROOT/.toolhome" NUGET_PACKAGES="$CANDIDATE_ROOT/.toolhome/nuget"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_MULTILEVEL_LOOKUP=0
cd "$CANDIDATE_ROOT"
python3 tests/Game.ResidualStatus.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source-probe.log" 2>&1
python3 - "$OUTPUT/source/source-vectors.json" <<'PY'
import hashlib,json,sys
path=sys.argv[1]
source=json.load(open(path));frozen=json.load(open('tests/Game.ResidualStatus.Tests/source-manifest.json'))
assert source['sourceCommit']==frozen['sourceCommit']
assert hashlib.sha256(open(path,'rb').read()).hexdigest()==frozen['vectorSha256'],'Source vector snapshot changed'
PY
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.ResidualStatus.Tests/Game.ResidualStatus.Tests.csproj --configfile tests/Game.ResidualStatus.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$OUTPUT/restore.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.ResidualStatus.Tests/Game.ResidualStatus.Tests.csproj --no-restore -m:1 -nr:false -v:q > "$OUTPUT/build.log" 2>&1
if dotnet tests/Game.ResidualStatus.Tests/bin/Debug/net10.0/Game.ResidualStatus.Tests.dll "$CONTENT_ROOT" "$OUTPUT/source/source-vectors.json" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1; then result=0; else result=$?; fi
python3 - "$OUTPUT/result.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['groups']>0 and r['checks']>0 and r['oracleCases']>0
print(json.dumps({k:r[k] for k in ['groups','checks','failures','completedTurns','oracleCases','runtime']}))
PY
exit "$result"
