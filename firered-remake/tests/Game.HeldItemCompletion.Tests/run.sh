#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
if [[ $# -lt 1 || $# -gt 4 ]]; then
  echo "usage: run.sh PINNED_SOURCE [CATALOG_ROOT] [ITEM_PACK_ROOT] [NEW_OUTPUT]" >&2
  exit 2
fi
REFERENCE_ROOT="$1"
CATALOG_ROOT="${2:-$PROJECT_ROOT}"
ITEM_PACK_ROOT="${3:-$CATALOG_ROOT}"
source "$PROJECT_ROOT/scripts/env.sh"
OUT="${4:-$PROJECT_ROOT/tests/Game.HeldItemCompletion.Tests/generated/run-$(date -u +%Y%m%dT%H%M%S%N)}"
if [[ -e "$OUT" ]]; then echo "Output already exists: $OUT" >&2; exit 2; fi
mkdir -p "$OUT"
python3 "$PROJECT_ROOT/tests/Game.HeldItemCompletion.Tests/source_probe.py" "$REFERENCE_ROOT" "$OUT/oracle" > "$OUT/source.log"
# Lock compilation only. Original-C generation and both runtime verification passes run outside it.
flock -w 240 /tmp/firered-remake-build.lock dotnet build "$PROJECT_ROOT/tests/Game.HeldItemCompletion.Tests/Game.HeldItemCompletion.Tests.csproj" --configfile "$PROJECT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false -v:minimal > "$OUT/build.log"
dotnet "$PROJECT_ROOT/tests/Game.HeldItemCompletion.Tests/bin/Debug/net10.0/Game.HeldItemCompletion.Tests.dll" "$CATALOG_ROOT" "$ITEM_PACK_ROOT" "$OUT/oracle/source-vectors.json" > "$OUT/result.log"
python3 - "$OUT" <<'PY'
from pathlib import Path
import json,sys
root=Path(sys.argv[1]);lines=(root/'result.log').read_text().splitlines()
assert lines[-1]=='HELD_ITEM_COMPLETION_END'
result=json.loads(lines[-2]);assert result['groups']>=15 and result['checks']>14000 and result['failures']==0 and result['turns']>=35 and result['runtime']=='10.0.12'
source=json.loads((root/'oracle/source-vectors.json').read_text());assert len(source['vectors'])==599 and len(source['statVectors'])==1348 and len(source['pins'])==13
(root/'result.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
PY
