#!/usr/bin/env bash
set -euo pipefail
OHKO_CONTENT="$(realpath "${1:?Pass product root}")"
OHKO_REFERENCE="$(realpath "${2:?Pass pinned source root}")"
OHKO_OUTPUT="$(realpath -m "${3:?Pass new evidence directory}")"
[[ ! -e "$OHKO_OUTPUT" ]] || { echo 'Output exists' >&2; exit 2; }
mkdir -p "$OHKO_OUTPUT/singles"
OHKO_PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
source "$OHKO_CONTENT/scripts/env.sh"
cd "$OHKO_PROJECT"
flock /tmp/firered-remake-build.lock dotnet build tests/Game.SourceFocusBand.Tests/Game.SourceFocusBand.Tests.csproj --configfile "$OHKO_CONTENT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OHKO_OUTPUT/build.log" 2>&1
dotnet tests/Game.SourceFocusBand.Tests/bin/Debug/net10.0/Game.SourceFocusBand.Tests.dll "$OHKO_CONTENT" "$OHKO_REFERENCE" "$OHKO_OUTPUT/singles" --ohko > "$OHKO_OUTPUT/singles.log" 2>&1
dotnet tests/Game.SourceFocusBand.Tests/bin/Debug/net10.0/Game.SourceFocusBand.Tests.dll "$OHKO_CONTENT" "$OHKO_REFERENCE" "$OHKO_OUTPUT/four" --ohko-four > "$OHKO_OUTPUT/four.log" 2>&1
python3 - "$OHKO_OUTPUT" <<'PY'
import hashlib,json,pathlib,sys
p=pathlib.Path(sys.argv[1]);baseline=json.load(open('tests/Game.SourceFocusBand.Tests/ohko-singles-baseline-d449a1d.json'))
data=(p/'singles/scenario-receipts.json').read_bytes()
assert len(data)==baseline['bytes'] and hashlib.sha256(data).hexdigest()==baseline['sha256']
for name in ['singles','four']:
 r=json.load((p/name/'result.json').open());assert r['failures']==0
 print(name,r['groups'],r['checks'])
PY
