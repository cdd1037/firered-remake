#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$ROOT"
OUT=${1:-"$ROOT/results/candidate-verification"}
mkdir -p "$OUT/music"
python3 tools/build_cry_catalog.py > "$OUT/catalog-identity.json"
./build.sh > "$OUT/build.log"
dotnet test "$ROOT/../firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj" --filter TestCategory=Audio > "$OUT/music/tests.txt"
dotnet test "$ROOT/../firered-remake/tests/Game.Audio.Tests/Game.Audio.Tests.csproj" --filter FullyQualifiedName~NormalCryCatalogTests > "$OUT/music/catalog-tests.log"
./research-music/run.sh "$OUT/music" > "$OUT/music.log"
python3 - "$OUT" <<'PY'
import hashlib,json,pathlib,subprocess,sys
root=pathlib.Path.cwd();out=pathlib.Path(sys.argv[1]).resolve()
rows=json.loads((root/'results/phase5-music-expanded/rollback-hashes.json').read_text())
for row in rows:
    path=out/f"rollback-{row['mode']}-{row['profile']}.wav"
    result=subprocess.run([str(root/'run.sh'),'render',str(root/'resources/firered.pack'),row['mode'],str(row['seconds']),str(path),row['profile']],check=True,capture_output=True,text=True)
    row['actualSha256']=hashlib.sha256(path.read_bytes()).hexdigest()
    row['exact']=row['actualSha256']==row['sha256']
    assert row['exact'],row['mode']
(out/'music/rollback-hashes.json').write_text(json.dumps(rows,indent=2))
PY
python3 research-music/verify_results.py "$OUT/music" > "$OUT/summary.json"
