#!/usr/bin/env bash
# The sole maintained game; historical flags are rejected by the startup resolver.
set -euo pipefail
source "$(dirname "$0")/launch-campaign-common.sh"
prepare_campaign_launch "$@"
export FIRERED_PROFILE="${FIRERED_PROFILE:-current}"
export FIRERED_AUDIO="${FIRERED_AUDIO:-1}" FIRERED_PLAYER_ENTRY="${FIRERED_PLAYER_ENTRY:-1}"
export XDG_DATA_HOME="$ROOT/.play/current/data" XDG_CONFIG_HOME="$ROOT/.play/current/config" XDG_CACHE_HOME="$ROOT/.play/current/cache"
mkdir -p "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$ROOT/evidence/current-play"
export FIRERED_EVIDENCE="$ROOT/evidence/current-play/runtime.json"
: > "$ROOT/evidence/current-play/runtime.jsonl"
cd "$ROOT"
exec "$GODOT" --path "$ROOT/src/Game.Godot" -- --profile=current > "$ROOT/evidence/current-play/window.log" 2>&1
