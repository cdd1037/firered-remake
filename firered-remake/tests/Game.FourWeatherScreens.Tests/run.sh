#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="$(realpath "${1:?Pass authenticated product root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned source root}")"
OUTPUT="${3:?Pass a new evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Evidence directory already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourWeatherScreens.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/source-probe.log" 2>&1
python3 tests/Game.FourEndTurn.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/residual-source-probe.log" 2>&1
python3 tests/Game.FourActionTurns.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/action-source-probe.log" 2>&1
python3 tests/Game.SourceWeatherAbilities.Tests/extract-source-vectors.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/weather-source-probe.log" 2>&1
for suite in Game.FourWeatherScreens.Tests Game.FourEndTurn.Tests Game.FourEndTurnParity.Tests Game.FourActionTurns.Tests Game.CalledFieldEffects.Tests Game.SourceWeatherAbilities.Tests; do
 flock /tmp/firered-remake-build.lock dotnet restore "tests/$suite/$suite.csproj" --configfile tests/Game.FourEndTurn.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$OUTPUT/$suite-restore.log" 2>&1
 flock /tmp/firered-remake-build.lock dotnet build "tests/$suite/$suite.csproj" --no-restore -m:1 -nr:false -v:q > "$OUTPUT/$suite-build.log" 2>&1
 case "$suite" in
  Game.FourWeatherScreens.Tests) arguments=("$CONTENT_ROOT" "$REFERENCE_ROOT" "$OUTPUT") ;;
  Game.FourEndTurn.Tests|Game.FourActionTurns.Tests) arguments=("$CONTENT_ROOT" "$OUTPUT") ;;
  Game.SourceWeatherAbilities.Tests) arguments=("$CONTENT_ROOT" "$CONTENT_ROOT" "$REFERENCE_ROOT" "$OUTPUT/source-vectors.json") ;;
  *) arguments=("$CONTENT_ROOT") ;;
 esac
 dotnet "tests/$suite/bin/Debug/net10.0/$suite.dll" "${arguments[@]}" > "$OUTPUT/$suite-run.log" 2>&1
 tail -1 "$OUTPUT/$suite-run.log"
done
python3 - "$CANDIDATE_ROOT" "$CONTENT_ROOT" "$OUTPUT" <<'PY'
from pathlib import Path
import hashlib,json,sys
candidate,baseline,out=map(Path,sys.argv[1:])
golden='tests/Game.FourEndTurnParity.Tests/baseline-3a0344d.json'
a=(candidate/golden).read_bytes();b=(baseline/golden).read_bytes();assert a==b
line=next(l for l in (out/'Game.FourEndTurnParity.Tests-run.log').read_text().splitlines() if l.startswith('EXACT_SINGLE_PHASE_HASHES '))
hashes=json.loads(line.split(' ',1)[1]);assert len(hashes)==64
(out/'golden-control.json').write_text(json.dumps({'goldenByteIdentical':True,'goldenSha256':hashlib.sha256(a).hexdigest(),'matchingFrozenPhaseHashes':len(hashes)},indent=2)+'\n')
print('Frozen singles golden unchanged; all 64 phase hashes match')
PY
