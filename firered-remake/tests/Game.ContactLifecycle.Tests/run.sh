#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONTENT_ROOT="$(realpath "${1:?Pass authenticated product root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned original source root}")"
OUTPUT="${3:?Pass a new evidence directory}"
if [[ -e "$OUTPUT" ]]; then echo "Evidence directory already exists: $OUTPUT" >&2; exit 2; fi
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$CONTENT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
dotnet --version > "$OUTPUT/sdk.log"
python3 tests/Game.ContactLifecycle.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/source-probe.log" 2>&1
flock /tmp/firered-remake-build.lock dotnet build tests/Game.ContactLifecycle.Tests/Game.ContactLifecycle.Tests.csproj --configfile "$ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
dotnet tests/Game.ContactLifecycle.Tests/bin/Debug/net10.0/Game.ContactLifecycle.Tests.dll "$CONTENT_ROOT" "$REFERENCE_ROOT" "$OUTPUT" > "$OUTPUT/run.log" 2>&1
mkdir "$OUTPUT/legacy-contact" "$OUTPUT/singles-contact"
python3 tests/Game.FourContact.Tests/source_probe.py "$REFERENCE_ROOT" "$OUTPUT/legacy-contact" > "$OUTPUT/legacy-contact/source-probe.log" 2>&1
for suite in Game.FourContact.Tests Game.SourceContactAbsorb.Tests; do
  flock /tmp/firered-remake-build.lock dotnet build "tests/$suite/$suite.csproj" --configfile "$ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/$suite.build.log" 2>&1
done
dotnet tests/Game.FourContact.Tests/bin/Debug/net10.0/Game.FourContact.Tests.dll "$CONTENT_ROOT" "$REFERENCE_ROOT" "$OUTPUT/legacy-contact" > "$OUTPUT/legacy-contact/run.log" 2>&1
dotnet tests/Game.SourceContactAbsorb.Tests/bin/Debug/net10.0/Game.SourceContactAbsorb.Tests.dll "$CONTENT_ROOT" "$CONTENT_ROOT" > "$OUTPUT/singles-contact/run.log" 2>&1
python3 - "$OUTPUT" <<'PY'
import json, pathlib, sys
p=pathlib.Path(sys.argv[1]);r=json.loads((p/'report.json').read_text());c=json.loads((p/'source-probe.json').read_text());l=json.loads((p/'legacy-contact/report.json').read_text())
assert r['failures']==0 and r['vectors']==165 and r['turns']==25 and r['canonicalHashes']==24 and r['sourceOnly']==6
assert c['vectorCount']==171
assert l['failures']==0 and l['vectors']==156 and l['turns']==19 and l['legacyWholeTurnHashes']==30
print(json.dumps({'contact':r,'historicalContact':l,'originalCVectorCount':c['vectorCount']}))
PY
