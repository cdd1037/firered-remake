#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PACK="$ROOT/resources/firered.pack"
"$ROOT/build.sh" > "$ROOT/results/build.log"
dotnet test "$ROOT/../firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj" --filter TestCategory=Audio > "$ROOT/results/tests.txt"
for config in 'select 3' 'pallet 95' 'mix 12'; do
    set -- $config
    "$ROOT/run.sh" render "$PACK" "$1" "$2" "$ROOT/results/$1.wav"
done
"$ROOT/run.sh" render "$PACK" mix 12 "$ROOT/results/mix-repeat.wav"
cmp "$ROOT/results/mix.wav" "$ROOT/results/mix-repeat.wav"
if "$ROOT/run.sh" render "$PACK" cry 1 "$ROOT/results/invalid-cry.wav" > "$ROOT/results/invalid-cry.log" 2>&1; then exit 1; fi
if "$ROOT/run.sh" unknown "$PACK" > "$ROOT/results/invalid-command.log" 2>&1; then exit 1; fi
[ ! -e "$ROOT/results/invalid-cry.wav" ]
python3 "$ROOT/specs/audit_sequence.py"
python3 "$ROOT/tools/compare.py"
"$ROOT/run.sh" benchmark "$PACK" > "$ROOT/results/benchmark.json"
