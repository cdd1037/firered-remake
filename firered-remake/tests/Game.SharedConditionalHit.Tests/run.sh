#!/usr/bin/env bash
set -euo pipefail
candidate_root="$(cd "$(dirname "$0")/../.." && pwd)"
content_root="$(realpath "${1:?Pass authenticated content project root}")"
source_root="$(realpath "${2:?Pass pinned pokefirered source root}")"
output="${3:?Pass a new output directory}"
[[ ! -e "$output" ]] || { echo 'Output directory already exists' >&2; exit 2; }
mkdir -p "$output"
output="$(realpath "$output")"
cd "$candidate_root"
source "$content_root/scripts/env.sh"
python3 tests/Game.SharedConditionalHit.Tests/source_probe.py --source "$source_root" --output "$output/source" > "$output/source.log"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedConditionalHit.Tests/Game.SharedConditionalHit.Tests.csproj --configfile "$content_root/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$output/build.log" 2>&1
dotnet tests/Game.SharedConditionalHit.Tests/bin/Debug/net10.0/Game.SharedConditionalHit.Tests.dll "$content_root" "$output/results.json" > "$output/tests.log" 2>&1
cat "$output/tests.log"
python3 - "$output/results.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['passed'] and r['groups']==8 and r['checks']>=1381 and r['turns']==41 and r['baseline']==192
PY
