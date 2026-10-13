#!/usr/bin/env bash
# Current launch preparation; regional launcher dispatch has been retired.
prepare_campaign_launch() {
  source "$(dirname "$0")/env.sh"
  bash "$(dirname "$0")/ensure-godot-imports.sh"
  if [[ $# -gt 1 ]]; then printf 'Usage: %s [compatible-stable-checkpoint.json]\n' "$0" >&2; exit 2; fi
  unset FIRERED_CAMPAIGN_CHECKPOINT
  if [[ $# -eq 1 ]]; then
    if [[ ! -f "$1" ]]; then printf 'Missing checkpoint: %s\n' "$1" >&2; exit 1; fi
    export FIRERED_CAMPAIGN_CHECKPOINT="$(realpath -- "$1")"
  fi
  export FIRERED_PROJECT_ROOT="$ROOT"
}
