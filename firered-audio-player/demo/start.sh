#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
export DOTNET_ROOT="$TOOLCHAIN_ROOT/dotnet" DOTNET_CLI_HOME="$ROOT/home"
export XDG_DATA_HOME="$ROOT/home/xdg-data" XDG_CACHE_HOME="$ROOT/home/xdg-cache" XDG_CONFIG_HOME="$ROOT/home/xdg-config"
mkdir -p "$XDG_DATA_HOME" "$XDG_CACHE_HOME" "$XDG_CONFIG_HOME"
export PATH="$DOTNET_ROOT:$PATH"
exec "$TOOLCHAIN_ROOT/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64" --path "$ROOT/demo" "$@"
