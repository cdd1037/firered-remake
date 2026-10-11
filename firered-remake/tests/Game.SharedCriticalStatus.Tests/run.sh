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
python3 tests/Game.SharedCriticalStatus.Tests/source_probe.py --source "$source_root" --report "$output/source.json" > "$output/source.log"
python3 tests/Game.SharedCriticalStatus.Tests/contact_probe.py --source "$source_root" --report "$output/contact-source.json" > "$output/contact-source.log"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedCriticalStatus.Tests --configfile tests/Game.SharedCriticalStatus.Tests/NuGet.Config -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$output/build.log" 2>&1
dotnet tests/Game.SharedCriticalStatus.Tests/bin/Debug/net10.0/Game.SharedCriticalStatus.Tests.dll "$content_root" "$source_root" "$output/results.json" > "$output/tests.log" 2>&1
cat "$output/tests.log"
python3 - "$output/results.json" <<'PY'
import json,sys
r=json.load(open(sys.argv[1]));assert r['passed'] and r['checks']>0 and r['turns']>0 and r['baseline']==576 and r['corrections']==6
PY
