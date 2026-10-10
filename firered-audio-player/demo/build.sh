#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
TOOLCHAIN_ROOT=${FIRERED_TOOLCHAIN_ROOT:-"$ROOT/../firered-toolchains"}
export DOTNET_ROOT="$TOOLCHAIN_ROOT/dotnet" DOTNET_CLI_HOME="$ROOT/home"
export FIRERED_GODOT_NUPKGS="$TOOLCHAIN_ROOT/godot/Godot_v4.7.2-stable_mono_linux_x86_64/GodotSharp/Tools/nupkgs"
export PATH="$DOTNET_ROOT:$PATH"
"$DOTNET_ROOT/dotnet" build "$ROOT/demo/FireRed.AudioDemo.csproj" -c Debug --nologo --ignore-failed-sources
