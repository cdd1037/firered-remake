#!/usr/bin/env bash
# Source this file; tooling never falls back to PATH versions.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -z "${FIRERED_TOOLCHAIN_ROOT:-}" && -f "$ROOT/.tools/toolchain-root" ]]; then read -r FIRERED_TOOLCHAIN_ROOT < "$ROOT/.tools/toolchain-root"; fi
if [[ -z "${FIRERED_ORIGINAL_REFERENCE:-}" && -f "$ROOT/.tools/reference-root" ]]; then read -r FIRERED_ORIGINAL_REFERENCE < "$ROOT/.tools/reference-root"; fi
TOOLCHAIN_ROOT="${FIRERED_TOOLCHAIN_ROOT:-/workspace/shared/firered-toolchains}"
export FIRERED_TOOLCHAIN_ROOT="$TOOLCHAIN_ROOT"
if [[ -n "${FIRERED_ORIGINAL_REFERENCE:-}" ]]; then export FIRERED_ORIGINAL_REFERENCE; fi
export DOTNET_ROOT="$TOOLCHAIN_ROOT/dotnet"
export GODOT="$TOOLCHAIN_ROOT/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$ROOT/.toolhome"
export NUGET_PACKAGES="$ROOT/.toolhome/nuget"
export XDG_DATA_HOME="$ROOT/.toolhome/share"
export XDG_CONFIG_HOME="$ROOT/.toolhome/config"
export XDG_CACHE_HOME="$ROOT/.toolhome/cache"
export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_MULTILEVEL_LOOKUP=0
mkdir -p "$DOTNET_CLI_HOME" "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME"
