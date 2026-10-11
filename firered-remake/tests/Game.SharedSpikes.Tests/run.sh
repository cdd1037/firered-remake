#!/usr/bin/env bash
set -euo pipefail
[[ $# -eq 3 ]] || { echo 'Usage: run.sh CONTENT_ROOT PINNED_SOURCE_ROOT NEW_OUTPUT' >&2; exit 2; }
content_root="$(realpath "$1")"
reference_root="$(realpath "$2")"
output="$(realpath -m "$3")"
project_root="$(cd "$(dirname "$0")/../.." && pwd)"
[[ ! -e "$output" ]] || { echo 'Use a new evidence directory.' >&2; exit 2; }
mkdir -p "$output"
source "$content_root/scripts/env.sh"
cd "$project_root"
config="$content_root/.toolhome/recovery-local-only.config"
python3 tests/Game.SharedSpikes.Tests/build-source-oracle.py --source-root "$reference_root" --output-dir "$output/source" > "$output/source-oracle.json"
for suite in Game.SharedSpikes.Tests Game.SharedSpikesParity.Tests; do
  flock /tmp/firered-remake-build.lock dotnet build "tests/$suite" --configfile "$config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false > "$output/$suite-build.log" 2>&1
done
dotnet tests/Game.SharedSpikes.Tests/bin/Debug/net10.0/Game.SharedSpikes.Tests.dll "$content_root" "$output/result.json" "$output/source-oracle.json" > "$output/run.log"
dotnet tests/Game.SharedSpikesParity.Tests/bin/Debug/net10.0/Game.SharedSpikesParity.Tests.dll "$content_root" "$output/singles-current.json" > "$output/singles-current.log"
# The independently built fixed source baseline is immutable; normal regressions do not rebuild it.
cmp tests/Game.SharedSpikesParity.Tests/legacy-e14578e.json "$output/singles-current.json"
python3 - "$output" <<'PY'
import hashlib,json,pathlib,sys
p=pathlib.Path(sys.argv[1]);raw=p/'source-oracle.json';d=json.loads(raw.read_text())
for key in ['entry_samples','set_samples']:
    samples=d.pop(key);d[key+'_representative']=samples[:2]+samples[-2:]
d['oracle_output_sha256']=hashlib.sha256(raw.read_bytes()).hexdigest()
(p/'source-summary.json').write_text(json.dumps(d,indent=2)+'\n')
left=pathlib.Path('tests/Game.SharedSpikesParity.Tests/legacy-e14578e.json').read_bytes();right=(p/'singles-current.json').read_bytes()
assert left==right and len(json.loads(left))==33
(p/'parity-summary.json').write_text(json.dumps({'baseline_commit':'e14578e','hash_count':33,'equal':True,'sha256':hashlib.sha256(left).hexdigest()},indent=2)+'\n')
PY
tail -n 1 "$output/run.log"
cat "$output/parity-summary.json"
