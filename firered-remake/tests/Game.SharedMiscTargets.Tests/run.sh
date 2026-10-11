#!/usr/bin/env bash
set -euo pipefail
CANDIDATE_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PRODUCT_ROOT="$(realpath "${1:?Pass authenticated content root}")"
REFERENCE_ROOT="$(realpath "${2:?Pass pinned original FireRed source root}")"
OUTPUT="${3:?Pass a new output directory}"
[[ ! -e "$OUTPUT" ]] || { echo 'Output already exists' >&2; exit 2; }
mkdir -p "$OUTPUT"
OUTPUT="$(realpath "$OUTPUT")"
source "$PRODUCT_ROOT/scripts/env.sh"
cd "$CANDIDATE_ROOT"
python3 tests/Game.SharedMiscTargets.Tests/source_probe.py --source "$REFERENCE_ROOT" --report "$OUTPUT/source-result.json" > "$OUTPUT/source.log" 2>&1
flock /tmp/firered-remake-build.lock "$DOTNET_ROOT/dotnet" build tests/Game.SharedMiscTargets.Tests/Game.SharedMiscTargets.Tests.csproj --configfile "$PRODUCT_ROOT/.toolhome/recovery-local-only.config" -p:RestoreLockedMode=true -p:NuGetAudit=false -m:1 -nr:false -p:UseSharedCompilation=false -v:q > "$OUTPUT/build.log" 2>&1
"$DOTNET_ROOT/dotnet" tests/Game.SharedMiscTargets.Tests/bin/Debug/net10.0/Game.SharedMiscTargets.Tests.dll "$PRODUCT_ROOT" "$OUTPUT/result.json" > "$OUTPUT/run.log" 2>&1
python3 - "$OUTPUT" "$PRODUCT_ROOT" <<'PY'
import hashlib,json,re,sys
from pathlib import Path
out,root=map(Path,sys.argv[1:]);result=json.loads((out/'result.json').read_text());source=json.loads((out/'source-result.json').read_text())
assert result['passed'] and result['checks']>1500 and len(result['results'])>=10 and all(r['passed'] for r in result['results'])
assert source['passed'] and sum(source['counts'].values())==14884
# The loader dependency roots are explicit. Keep the manifest portable without
# requiring ptrace (which is unavailable in the managed executor).
classes=['PretCanonicalPokemonCatalog','PretMetronomeMoveCatalog','PretRocketHideoutPokemon','PretCeladonGymPokemon','PretCeladonApproachPokemon','PretRockTunnelPokemon','PretRoute11Pokemon','PretSsAnnePokemon','PretVermilionPokemon','PretBillPokemon','PretCeruleanPokemon','PretMtMoonPokemon','PretRoute3Pokemon','PretRoute22Pokemon','PretPewterPokemon','PretForestPokemon','PretRoute1PokemonCatalog','PretOldManTutorial','PretPokedexMenu','PretVermilionGymTrainers','PretEeveePokemon','PretSnorlaxPokemon','PretPokemonTowerPokemon','PretFuchsiaRoutePokemon']
paths=set();roots=set();loaders=[]
for name in classes:
 code=Path('src/Game.Content')/(name+'.cs')
 assert code.is_file(),code
 loaders.append({'path':str(code),'sha256':hashlib.sha256(code.read_bytes()).hexdigest()})
 text=code.read_text()
 for part in re.findall(r'content/(?:raw|generated|sources)/[a-zA-Z0-9_-]+',text):roots.add(part)
 for part in re.findall(r'scripts/[a-zA-Z0-9_.-]+\.json',text):
  p=root/part
  if p.is_file():paths.add(p)
for name in {part.split('/')[2] for part in roots}:
 for prefix in ('content/raw/','content/generated/','content/sources/'):
  if (root/(prefix+name)).is_dir():roots.add(prefix+name)
 lock=root/('scripts/'+name+'.lock.json')
 if lock.is_file():paths.add(lock)
for part in sorted(roots):
 d=root/part
 if d.is_dir():paths.update(p for p in d.rglob('*') if p.is_file())
assert paths
manifest={'contentRoot':str(root),'method':'declared authenticated loader dependency roots; not an observed filesystem trace','dependencyRoots':sorted(roots),'loaders':loaders,'files':[{'path':str(p.relative_to(root)),'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(paths)]}
(out/'input-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
print(json.dumps({'passed':True,'checks':result['checks'],'groups':len(result['results']),'sourceVectors':sum(source['counts'].values()),'canonicalSourceCorrections':len(result['differences']),'contentInputs':len(paths),'runtime':result['runtime']}))
PY
