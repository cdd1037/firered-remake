#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/../env.sh"
cd "$ROOT"
export FIRERED_EVIDENCE="$ROOT/evidence/harness-runtime.json"
: > "$ROOT/evidence/harness-runtime.harness.jsonl"
if [[ "${1:-}" == "export" ]]; then
  unset DOTNET_ROOT
  exec "$ROOT/exports/linux/firered-probe.x86_64" -- --harness > "$ROOT/evidence/harness-window.log" 2>&1
else
  exec "$GODOT" --path "$ROOT/src/Game.Godot" -- --harness > "$ROOT/evidence/harness-window.log" 2>&1
fi
