#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
export DOTNET_CLI_HOME="$ROOT/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
exec "$TOOLCHAIN_ROOT/dotnet/dotnet" "$ROOT/src/bin/Release/net10.0/FireRedAudio.dll" "$@"
