#!/usr/bin/env bash
set -euo pipefail
task_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
task_content="$(realpath "${1:?Authenticated content root}")"
task_output="$(realpath -m "${2:?New evidence directory}")"
[[ ! -e "$task_output" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$task_output"
cd "$task_root"
source scripts/env.sh
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedHitItems.Tests/Game.SharedHitItems.Tests.csproj --configfile .toolhome/recovery-local-only.config -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$task_output/build.log" 2>&1
dotnet tests/Game.SharedHitItems.Tests/bin/Debug/net10.0/Game.SharedHitItems.Tests.dll "$task_content" "$task_output" fixed-two > "$task_output/run.log" 2>&1
cat "$task_output/run.log"
python3 - "$task_output/results.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['failures']==0 and r['groups']==3 and r['fixedTwoHashes']==24
PY
