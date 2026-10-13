#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$ROOT/exports/linux"
export FIRERED_EVIDENCE="$ROOT/evidence/p0-export-window-runtime.json"
# Deliberately remove installed runtime discovery: this tests the bundled runtime.
unset DOTNET_ROOT
exec ./firered-probe.x86_64 "$@" -- --probe > "$ROOT/evidence/p0-export-window.log" 2>&1
