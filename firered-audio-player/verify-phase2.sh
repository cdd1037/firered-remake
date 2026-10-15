#!/bin/sh
set -eu
PACK=${1:?Usage: verify-phase2.sh /absolute/path/to/firered.pack}
PACK=$(realpath "$PACK")
shift
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
OUT="$ROOT/results/phase2"
mkdir -p "$OUT"
"$ROOT/build.sh" > "$OUT/build.log"
dotnet test "$ROOT/../firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj" --filter TestCategory=Audio > "$OUT/tests.txt"
for profile in modern-dry source-gain source-linear source-pitch source-envelope source-reverb source-bandlimited source-filtered; do
    "$ROOT/run.sh" render "$PACK" select 3 "$OUT/select-$profile.wav" "$profile"
    "$ROOT/run.sh" render "$PACK" pallet 95 "$OUT/pallet-$profile.wav" "$profile"
done
"$ROOT/run.sh" render "$PACK" mix 12 "$OUT/mix-modern-dry.wav" modern-dry
"$ROOT/run.sh" render "$PACK" mix 12 "$OUT/mix-source-filtered.wav" source-filtered
"$ROOT/run.sh" render "$PACK" mix 12 "$OUT/mix-source-filtered-repeat.wav" source-filtered
for song in pallet select mix; do cmp "$ROOT/results/$song.wav" "$OUT/$song-modern-dry.wav"; done
cmp "$OUT/mix-source-filtered.wav" "$OUT/mix-source-filtered-repeat.wav"
if "$ROOT/run.sh" render "$PACK" select 1 "$OUT/invalid-profile.wav" invalid > "$OUT/invalid-profile.log" 2>&1; then exit 1; fi
[ ! -e "$OUT/invalid-profile.wav" ]
python3 "$ROOT/tools/compare_profiles.py"
python3 "$ROOT/tools/analyze_modern.py"
"$ROOT/run.sh" benchmark "$PACK" source-filtered > "$OUT/benchmark-filtered.json"
