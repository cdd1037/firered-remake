#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
cd "$ROOT"
export DEMO_RESULTS_GROUP=phase6-godot DEMO_CASE=headless-verification
mkdir -p results/phase6-godot
./demo/build.sh > results/phase6-godot/build.log
./demo/start.sh --headless --audio-driver Dummy --verbose > results/phase6-godot/headless-verification.log 2>&1
python3 - <<'PY'
import json,math,pathlib
p=pathlib.Path('results/phase6-godot')
x=json.loads((p/'headless-verification.json').read_text()); log=(p/'headless-verification.log').read_text()
assert 'PCM_DEMO_COMPLETE' in log and 'ERROR:' not in log and 'leaked' not in log.lower()
assert x['elapsedSeconds']>=58 and x['observedKindsMask']==31
assert x['Clipped']==0 and math.isfinite(x['peak']) and x['peak']>0
assert x['capturedFrames']>2_000_000 and x['capturedRms']>0 and x['captureDiscardedFrames']==0
assert x['mutedFrames']>0 and x['mutedRms']==0
assert x['baselineSkips']==0 and x['unplannedSkipsAfterRecovery']==0
assert x['finalSkips']>0 and x['recoveredAfterStall']
events={e['name']:e for e in x['events']}
assert events['before-cancel-active-cry']['cryPlaying'] and not events['after-cancel-active-cry']['cryPlaying']
assert sum('player=0' in s for s in x['loops'])>=6
assert x['queueUpperBoundMs']<=107
print('PASS real PCM capture, mute, active cancel, loop, intentional underrun recovery, release, queue bound')
PY
