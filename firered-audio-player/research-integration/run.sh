#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
cd "$ROOT"
export DOTNET_CLI_HOME="$ROOT/research-integration/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_TieredCompilation=0
python research-integration/prepare.py
"$TOOLCHAIN_ROOT/dotnet/dotnet" build research-integration/IntegrationCoverage.csproj -c Release --nologo --ignore-failed-sources
"$TOOLCHAIN_ROOT/dotnet/dotnet" research-integration/bin/Release/net10.0/IntegrationCoverage.dll resources/firered.pack research-integration/inventory.json research-integration/render
python - <<'PY'
import json,pathlib
p=pathlib.Path('research-integration');x=json.loads((p/'inventory.json').read_text())
x['songs']=[dict(s,seconds=120) for s in x['songs'] if s['looping']]
(p/'loop-selection.json').write_text(json.dumps(x,indent=2)+'\n')
PY
"$TOOLCHAIN_ROOT/dotnet/dotnet" research-integration/bin/Release/net10.0/IntegrationCoverage.dll resources/firered.pack research-integration/loop-selection.json research-integration/loop-render
python research-integration/verify.py
