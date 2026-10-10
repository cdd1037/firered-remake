#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
export DOTNET_CLI_HOME="$ROOT/home" DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
"$TOOLCHAIN_ROOT/dotnet/dotnet" build "$ROOT/src" -c Release --nologo --ignore-failed-sources
exec "$TOOLCHAIN_ROOT/dotnet/dotnet" build "$ROOT/core" -c Release --nologo --ignore-failed-sources
