#!/usr/bin/env bash
set -euo pipefail
HIT_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
HIT_CONTENT=$(realpath "${1:?Authenticated complete content root}")
HIT_REFERENCE=$(realpath "${2:?Pinned pret root}")
HIT_OUTPUT=$(realpath -m "${3:?New evidence directory}")
if [[ -e "$HIT_OUTPUT" ]]; then echo 'Evidence directory already exists' >&2; exit 2; fi
mkdir -p "$HIT_OUTPUT"
source "$HIT_ROOT/scripts/env.sh"
cd "$HIT_ROOT"
python3 tests/Game.SharedHitItems.Tests/source_probe.py "$HIT_REFERENCE" "$HIT_OUTPUT/source" > "$HIT_OUTPUT/source.log" 2>&1
"$DOTNET_ROOT/dotnet" --info > "$HIT_OUTPUT/toolchain.log"
flock -w 55 /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" restore tests/Game.SharedHitItems.Tests --configfile tests/Game.SharedHitItems.Tests/NuGet.Config --locked-mode -p:NuGetAudit=false -v:q > "$HIT_OUTPUT/restore.log" 2>&1
flock -w 55 /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.SharedHitItems.Tests --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$HIT_OUTPUT/build.log" 2>&1
"$DOTNET_ROOT/dotnet" tests/Game.SharedHitItems.Tests/bin/Debug/net10.0/Game.SharedHitItems.Tests.dll "$HIT_CONTENT" "$HIT_OUTPUT" verify "$HIT_OUTPUT/source" > "$HIT_OUTPUT/run.log" 2>&1
cat "$HIT_OUTPUT/run.log"
python3 - "$HIT_OUTPUT" <<'PY'
import json,pathlib,sys,xml.etree.ElementTree as ET
root=pathlib.Path(sys.argv[1])
for p in root.rglob('*.json'): json.loads(p.read_text())
for p in root.rglob('*.jsonl'):
 for row in p.read_text().splitlines(): json.loads(row)
for p in pathlib.Path('tests/Game.SharedHitItems.Tests').glob('*.csproj'): ET.parse(p)
r=json.loads((root/'results.json').read_text());assert r['failures']==0 and r['canonicalHashes']==96 and r['sourceVectors']==72 and r['completeTurns']>=37
PY
