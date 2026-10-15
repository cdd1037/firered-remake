#!/bin/sh
set -eu
PACK=${1:?Usage: research-music/run.sh /absolute/path/to/firered.pack}
PACK=$(realpath "$PACK")
shift
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
export DOTNET_CLI_HOME="$ROOT/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_TieredCompilation=0
OUT=${1:-"$ROOT/results/phase5-music"}
python "$ROOT/research-music/prepare.py" --out-dir "$OUT"
"$TOOLCHAIN_ROOT/dotnet/dotnet" build "$ROOT/research-music" -c Release --nologo --ignore-failed-sources
"$TOOLCHAIN_ROOT/dotnet/dotnet" "$ROOT/research-music/bin/Release/net10.0/MusicCoverage.dll" "$PACK" "$OUT/selection.json" "$OUT"
