#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
DOTNET=${FIRERED_DOTNET:-dotnet}
if [ -z "${FIRERED_DOTNET:-}" ] && [ -x "$TOOLCHAIN_ROOT/dotnet/dotnet" ]; then
    DOTNET="$TOOLCHAIN_ROOT/dotnet/dotnet"
fi
export DOTNET_CLI_HOME="$ROOT/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
exec "$DOTNET" "$ROOT/src/bin/Release/net10.0/FireRedAudio.dll" "$@"
