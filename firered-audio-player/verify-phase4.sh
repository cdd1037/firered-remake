#!/bin/sh
set -eu
PACK=${1:?Usage: verify-phase4.sh /absolute/path/to/firered.pack}
PACK=$(realpath "$PACK")
shift
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
cd "$ROOT"
export DOTNET_CLI_HOME="$ROOT/home"
DOTNET="$TOOLCHAIN_ROOT/dotnet/dotnet"
mkdir -p results/phase4
python3 tools/inventory_normal_cries.py
"$DOTNET" build research/CryCoverage.csproj -c Release --nologo > results/phase4/build.log
DOTNET_TieredCompilation=0 "$DOTNET" research/bin/Release/net10.0/CryCoverage.dll "$PACK" results/phase4/inventory.json results/phase4
./build.sh > results/phase4/runtime-build.log
dotnet test "$ROOT/../firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj" --filter TestCategory=Audio > results/phase4/tests.txt
./run.sh render "$PACK" cry-charmander 4 results/phase4/charmander-rollback.wav source-filtered > results/phase4/rollback.log
cmp results/phase4/charmander-rollback.wav results/phase3/charmander.wav
