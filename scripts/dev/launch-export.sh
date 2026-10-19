#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT/exports/linux"
mkdir -p "$ROOT/evidence"
export FIRERED_EVIDENCE="${FIRERED_EVIDENCE:-$ROOT/evidence/current-export-window-runtime.json}"
# The package discovers its resources and self-contained runtime beside itself.
unset DOTNET_ROOT DOTNET_ROOT_X64 DOTNET_HOST_PATH FIRERED_PROJECT_ROOT
exec ./firered.x86_64 "$@" > "$ROOT/evidence/current-export-window.log" 2>&1
