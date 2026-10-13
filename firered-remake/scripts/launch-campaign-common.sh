#!/usr/bin/env bash
# Sourced by the same-shape late-campaign launchers.
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

launch_campaign() {
  # Keep the profile in positional parameters, without shadowing inherited env.
  prepare_campaign_launch "${@:2}"
  # Fall through in predecessor order. Do not clear inherited later flags.
  case "$1" in
    national-dex) export FIRERED_NATIONAL_DEX=1 ;&
    pokemon-league) export FIRERED_POKEMON_LEAGUE=1 ;&
    victory-road) export FIRERED_VICTORY_ROAD=1 ;&
    league-rival) export FIRERED_LEAGUE_RIVAL=1 ;&
    viridian-gym) export FIRERED_VIRIDIAN_GYM=1 ;&
    first-sevii) export FIRERED_FIRST_SEVII=1 ;&
    cinnabar) export FIRERED_CINNABAR=1 ;&
    saffron-gym)
      export FIRERED_SAFFRON_GYM=1 FIRERED_SILPH=1 FIRERED_SURF=1 FIRERED_SAFARI=1 FIRERED_PRESET_DOUBLES=1 FIRERED_FUCHSIA_GYM=1 FIRERED_FUCHSIA_ROUTE=1 FIRERED_REGIONAL_INTERIORS=1 FIRERED_REGIONAL_SERVICES=1 FIRERED_REGIONAL=1 FIRERED_SNORLAX=1 FIRERED_POKEMON_TOWER=1 FIRERED_ROCKET_HIDEOUT=1 FIRERED_CELADON_GYM=1 FIRERED_CELADON_APPROACH=1 FIRERED_ROCKTUNNEL=1 FIRERED_ROUTE11=1 FIRERED_CITY_SERVICES=1 FIRERED_VERMILION_GYM=1 FIRERED_SS_ANNE=1 FIRERED_VERMILION=1 FIRERED_BILL=1 FIRERED_CERULEAN=1 FIRERED_MTMOON=1 FIRERED_MAGIKARP=1 FIRERED_ROUTE3=1 FIRERED_ROUTE22=1 FIRERED_OAK_RATING=1 FIRERED_TOWN_MAP=1 FIRERED_REPEL=1
      ;;
    *) printf 'Unknown campaign profile: %s\n' "$1" >&2; exit 2 ;;
  esac
  export FIRERED_FIELD_PICKUPS=1 FIRERED_POKEMON_STORAGE=1 FIRERED_EARLY_PARTY=1 FIRERED_EARLY_STARTER=1
  export FIRERED_PEWTER=1 FIRERED_FOREST_TRAINERS=1 FIRERED_FOREST=1 FIRERED_PARTY_TRAINING=1
  export FIRERED_OLD_MAN=1 FIRERED_VIRIDIAN_CENTER=1 FIRERED_VIRIDIAN_REGION=1 FIRERED_CAPTURE=1
  export FIRERED_COMPACT_MAPS="${FIRERED_COMPACT_MAPS:-1}" FIRERED_AUDIO="${FIRERED_AUDIO:-1}" FIRERED_PLAYER_ENTRY="${FIRERED_PLAYER_ENTRY:-1}"
  export XDG_DATA_HOME="$ROOT/.play/$1/data" XDG_CONFIG_HOME="$ROOT/.play/$1/config" XDG_CACHE_HOME="$ROOT/.play/$1/cache"
  mkdir -p "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_CACHE_HOME" "$ROOT/evidence/$1-play"
  export FIRERED_EVIDENCE="$ROOT/evidence/$1-play/runtime.json"
  : > "$ROOT/evidence/$1-play/runtime.jsonl"
  cd "$ROOT"
  exec "$GODOT" --path "$ROOT/src/Game.Godot" -- "--story-$1" > "$ROOT/evidence/$1-play/window.log" 2>&1
}
