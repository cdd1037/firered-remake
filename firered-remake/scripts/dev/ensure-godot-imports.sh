#!/usr/bin/env bash
# Restore the required font cache after a clean checkout or workspace recovery.
set -euo pipefail
source "$(dirname "$0")/../env.sh"

project="$ROOT/src/Game.Godot"
font="$project/Assets/ProbeFont.ttc"
if [[ ! -s "$font" ]]; then
  printf 'Required font source is missing or empty: %s. Restore this asset before launching.\n' "$font" >&2
  exit 1
fi

font_import_present() {
  local destination
  [[ -f "$font.import" ]] || return 1
  destination="$(sed -n '/^\[remap\]$/,/^\[deps\]$/s/^path="\(res:\/\/[^" ]*\)"$/\1/p' "$font.import")"
  [[ "$destination" == res://.godot/imported/*.fontdata && -s "$project/${destination#res://}" ]]
}

if font_import_present; then exit 0; fi
if [[ ! -x "$GODOT" ]]; then
  printf 'Required official Godot executable is unavailable: %s. Restore the pinned toolchain before importing.\n' "$GODOT" >&2
  exit 1
fi

# Share the build lock: importing may refresh Godot's generated project files.
exec 9>/tmp/firered-remake-build.lock
if ! flock --exclusive --timeout 120 9; then
  printf 'Could not acquire the FireRed build lock within 120 seconds; required font import was not run. Retry after the other build finishes.\n' >&2
  exit 1
fi
if font_import_present; then exit 0; fi

printf 'Required font import cache is missing; restoring it with official Godot.\n' >&2
if timeout --signal=TERM --kill-after=10s 180s "$GODOT" --headless --editor --path "$project" --import --quit; then
  if font_import_present; then exit 0; fi
  printf 'Godot import finished without creating the required ProbeFont.ttc font cache. Check the import errors above and retry bash scripts/dev/ensure-godot-imports.sh.\n' >&2
else
  status=$?
  printf 'Required Godot font import failed (exit %s; time limit 180 seconds). Check the import errors above and retry bash scripts/dev/ensure-godot-imports.sh.\n' "$status" >&2
fi
exit 1
