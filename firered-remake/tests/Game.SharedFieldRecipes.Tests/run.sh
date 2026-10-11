#!/usr/bin/env bash
set -euo pipefail
content_root=${1:?Pass authenticated content root}
reference_root=${2:?Pass pinned original source root}
output_dir=${3:?Pass a fresh output directory}
[[ ! -e "$output_dir" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$output_dir"
output_dir="$(realpath "$output_dir")"
source "$content_root/scripts/env.sh"
cd "$(dirname "${BASH_SOURCE[0]}")/../.."
python3 tests/Game.SharedFieldRecipes.Tests/source_probe.py "$reference_root" > "$output_dir/source.log"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SharedFieldRecipes.Tests/Game.SharedFieldRecipes.Tests.csproj --configfile "$content_root/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$output_dir/build.log" 2>&1
dotnet tests/Game.SharedFieldRecipes.Tests/bin/Debug/net10.0/Game.SharedFieldRecipes.Tests.dll "$content_root" "$output_dir/result.json" > "$output_dir/run.log" 2>&1
python3 - "$output_dir" <<'PYCODE'
import json,sys,pathlib
p=pathlib.Path(sys.argv[1]);r=json.loads((p/'result.json').read_text())
assert r['groups']>0 and r['checks']>0 and r['failures']==0
assert (p/'run.log').read_text().splitlines()[-1]=='FIELD RECIPES COMPLETE'
print(json.dumps(r,indent=2))
PYCODE
