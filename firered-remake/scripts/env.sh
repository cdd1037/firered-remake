#!/usr/bin/env bash
# Source this file; share tool discovery with the cross-platform dev entry point.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ -z "${FIRERED_TOOLCHAIN_ROOT:-}" && -f "$ROOT/.tools/toolchain-root" ]]; then read -r FIRERED_TOOLCHAIN_ROOT < "$ROOT/.tools/toolchain-root"; fi
if [[ -z "${FIRERED_ORIGINAL_REFERENCE:-}" && -f "$ROOT/.tools/reference-root" ]]; then read -r FIRERED_ORIGINAL_REFERENCE < "$ROOT/.tools/reference-root"; fi
if [[ -n "${FIRERED_TOOLCHAIN_ROOT:-}" ]]; then export FIRERED_TOOLCHAIN_ROOT; fi
if [[ -n "${FIRERED_ORIGINAL_REFERENCE:-}" ]]; then export FIRERED_ORIGINAL_REFERENCE; fi
if command -v python3 >/dev/null 2>&1; then
 _firered_env="$(python3 "$ROOT/dev" env)" || return 1
else
 _firered_env="$(python "$ROOT/dev" env)" || return 1
fi
eval "$_firered_env"
_firered_dotnet_path="$DOTNET_ROOT"
if command -v cygpath >/dev/null 2>&1; then
 _firered_dotnet_path="$(cygpath -u "$DOTNET_ROOT")"
 GODOT="$(cygpath -u "$GODOT")"
fi
export PATH="$_firered_dotnet_path:$PATH"
unset _firered_env _firered_dotnet_path
