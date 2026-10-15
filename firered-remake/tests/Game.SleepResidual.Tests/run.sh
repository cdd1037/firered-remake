#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass authenticated product content root}"
REFERENCE_ROOT="${2:?Pass pinned FireRed source root}"
OUTPUT="${3:?Pass fresh evidence directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 - <<'PY'
import json,xml.etree.ElementTree as E
from pathlib import Path
for p in Path('tests/Game.SleepResidual.Tests').glob('*.json'):json.loads(p.read_text())
for p in ['Directory.Build.props','tests/Game.SleepResidual.Tests/Game.SleepResidual.Tests.csproj','tests/Game.SleepResidual.Tests/NuGet.Config']:E.parse(p)
print('JSON/XML parsed')
PY
python3 tests/Game.SleepResidual.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/source" > "$OUTPUT/source.log" 2>&1
python3 - "$OUTPUT/source/source-vectors.json" <<'PY'
import hashlib,json,sys
v=open(sys.argv[1],'rb').read();a=json.loads(v);m=json.load(open('tests/Game.SleepResidual.Tests/source-manifest.json'))
assert a['sourceCommit']==m['sourceCommit'] and hashlib.sha256(v).hexdigest()==m['vectorSha256']
assert sum(len(g['rows']) for g in a['groups'].values())==m['vectorCount']>0
PY
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SleepResidual.Tests/Game.SleepResidual.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
if dotnet tests/Game.SleepResidual.Tests/bin/Debug/net10.0/Game.SleepResidual.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/source/source-vectors.json" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1; then result=0; else result=$?; fi
python3 - "$OUTPUT/result.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['groups']>0 and r['checks']>0 and r['oracleCases']>0
print(json.dumps({k:r[k] for k in ['groups','checks','failures','completedTurns','oracleCases','runtime']}))
PY
exit "$result"
