#!/usr/bin/env bash
set -euo pipefail
source "$(dirname "$0")/env.sh"
cd "$ROOT"
export FIRERED_EVIDENCE="$ROOT/evidence/pallet-runtime.json"
export FIRERED_REFERENCE_ROOT="$ROOT/content/generated/pallet"
export FIRERED_REFERENCE_SOURCE="$ROOT/content/raw/pokefirered/037335f4"
: > "$ROOT/evidence/pallet-runtime.jsonl"
if [[ "${1:-}" == "export" ]]; then
  unset DOTNET_ROOT
  exec "$ROOT/exports/linux/firered-probe.x86_64" -- --pallet > "$ROOT/evidence/pallet-window.log" 2>&1
else
  exec "$GODOT" --path "$ROOT/src/Game.Godot" -- --pallet > "$ROOT/evidence/pallet-window.log" 2>&1
fi
