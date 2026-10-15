#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PACK="$ROOT/resources/firered.pack"
OUT="$ROOT/results/phase3"
"$ROOT/build.sh" > "$OUT/build.log"
"$ROOT/run.sh" selftest "$PACK" > "$OUT/tests.txt"
python3 "$ROOT/tools/inventory_cry.py" > "$OUT/inventory.log"
"$ROOT/run.sh" render "$PACK" cry-charmander 4 "$OUT/charmander.wav" source-filtered
"$ROOT/run.sh" render "$PACK" cry-charmander 4 "$OUT/charmander-repeat.wav" source-filtered
"$ROOT/run.sh" render "$PACK" cry-mix 12 "$OUT/cry-mix.wav" source-filtered
"$ROOT/run.sh" render "$PACK" cry-mix 12 "$OUT/cry-mix-repeat.wav" source-filtered
cmp "$OUT/charmander.wav" "$OUT/charmander-repeat.wav"
cmp "$OUT/cry-mix.wav" "$OUT/cry-mix-repeat.wav"
for config in 'pallet 95' 'select 3' 'mix 12'; do
    set -- $config
    "$ROOT/run.sh" render "$PACK" "$1" "$2" "$OUT/$1-rollback.wav" source-filtered
    cmp "$OUT/$1-rollback.wav" "$ROOT/results/phase2/$1-source-filtered.wav"
    "$ROOT/run.sh" render "$PACK" "$1" "$2" "$OUT/$1-modern-rollback.wav" modern-dry
    cmp "$OUT/$1-modern-rollback.wav" "$ROOT/results/$1.wav"
done
if "$ROOT/run.sh" render "$PACK" cry-unknown 1 "$OUT/invalid-cry.wav" source-filtered > "$OUT/invalid-cry.log" 2>&1; then exit 1; fi
[ ! -e "$OUT/invalid-cry.wav" ]
python3 "$ROOT/tools/analyze_cry.py" > "$OUT/comparison.log"
"$ROOT/run.sh" benchmark-cry "$PACK" source-filtered > "$OUT/benchmark.json"
