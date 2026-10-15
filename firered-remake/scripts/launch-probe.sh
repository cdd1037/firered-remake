#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$ROOT"
export FIRERED_EVIDENCE="$ROOT/evidence/p0-window-runtime.json"
exec "$GODOT" --path "$ROOT/src/Game.Godot" "$@" -- --probe > "$ROOT/evidence/p0-window.log" 2>&1
