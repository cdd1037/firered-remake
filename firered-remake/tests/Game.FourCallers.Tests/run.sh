#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="${1:?Pass the complete authenticated content/tool root}"
REFERENCE_ROOT="${2:?Pass pinned pokefirered checkout}"
OUTPUT="${3:?Pass a new evidence path}"
if [[ -e "$OUTPUT" ]]; then echo 'Evidence directory already exists' >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.FourCallers.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/source-probe.log"
python3 - "$OUTPUT" <<'PY'
import json,pathlib,sys,xml.etree.ElementTree as E
json.loads((pathlib.Path(sys.argv[1])/'source-proof.json').read_text())
E.parse('tests/Game.FourCallers.Tests/Game.FourCallers.Tests.csproj')
json.loads(pathlib.Path('tests/Game.FourCallers.Tests/packages.lock.json').read_text())
json.loads(pathlib.Path('tests/Game.FourCallers.Tests/baseline-a51e8a6.json').read_text())
PY
flock /tmp/firered-remake-build.lock dotnet restore tests/Game.FourCallers.Tests/Game.FourCallers.Tests.csproj --locked-mode --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:NuGetAudit=false -v:q > "$OUTPUT/restore.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.FourCallers.Tests/Game.FourCallers.Tests.csproj --no-restore -m:1 -nr:false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.FourCallers.Tests/bin/Debug/net10.0/Game.FourCallers.Tests.dll "$PRODUCT_ROOT" "$OUTPUT" > "$OUTPUT/run.log" 2>&1
python3 - "$OUTPUT" <<'PY'
import json,pathlib,sys
summary=json.loads((pathlib.Path(sys.argv[1])/'summary.json').read_text())
assert summary['failures']==0 and summary['checks']>0 and summary['cleanTurns']>0
PY
cat "$OUTPUT/run.log"
