#!/usr/bin/env bash
set -euo pipefail
PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
if [[ $# -lt 1 || $# -gt 4 ]]; then echo "usage: run.sh PINNED_SOURCE [CATALOG_ROOT] [ITEM_PACK_ROOT] [NEW_OUTPUT]" >&2; exit 2; fi
REFERENCE_ROOT="$1"
CATALOG_ROOT="${2:-$PROJECT_ROOT}"
ITEM_PACK_ROOT="${3:-$CATALOG_ROOT}"
source "$PROJECT_ROOT/scripts/env.sh"
OUT="${4:-$PROJECT_ROOT/tests/Game.SourceMoneyItems.Tests/generated/run-$(date -u +%Y%m%dT%H%M%S%N)}"
if [[ -e "$OUT" ]]; then echo "Output already exists: $OUT" >&2; exit 2; fi
mkdir -p "$OUT"
printf '%s\n' '<?xml version="1.0" encoding="utf-8"?><configuration><packageSources><clear/></packageSources></configuration>' > "$OUT/NuGet.Config"
python3 "$PROJECT_ROOT/tests/Game.SourceMoneyItems.Tests/source_probe.py" "$REFERENCE_ROOT" "$OUT/oracle" > "$OUT/source.log"
flock -w 240 /tmp/firered-remake-build.lock dotnet build "$PROJECT_ROOT/tests/Game.SourceMoneyItems.Tests/Game.SourceMoneyItems.Tests.csproj" --configfile "$OUT/NuGet.Config" -p:RestoreLockedMode=true -p:NuGetAudit=false -p:UseSharedCompilation=false -m:1 -nr:false -v:q > "$OUT/build.log"
dotnet "$PROJECT_ROOT/tests/Game.SourceMoneyItems.Tests/bin/Debug/net10.0/Game.SourceMoneyItems.Tests.dll" "$CATALOG_ROOT" "$ITEM_PACK_ROOT" "$OUT/oracle/source-vectors.json" > "$OUT/result.log"
python3 - "$OUT" <<'PY'
from pathlib import Path
import json,sys
out=Path(sys.argv[1]);result=json.loads((out/'result.log').read_text().splitlines()[-1]);assert result['passed'] and result['groups']>=6 and result['turns']>=12 and result['oracle']==48 and result['runtime']=='10.0.12'
(out/'result.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result))
PY
