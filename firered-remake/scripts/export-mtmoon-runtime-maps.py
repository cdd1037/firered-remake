#!/usr/bin/env python3
"""Authenticated 30-map compact derivative. Keeps both prior converters frozen."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
ROUTE3_HASH = '559ef404effcfb4a13cc81ac9dd9e66f3e234181abebc8da278ebd9161660477'
SOURCE = '07b5d04830065a6f148f6a244f870b5c4af4e4b99c772162695de25d79e52f5c'
NEW = {
 'mtmoon-region/mt-moon-1f': ('2c75d44c2cb23a4ec4e198c752cbf7eff555a39b9f1b9711daa120db9f5b9d0f', SOURCE),
 'mtmoon-region/mt-moon-b1f': ('1062fc25fd5213a1c5e5c7a60e29941bbe0edea9ab154a1d7d7b8a7d544bc6fc', SOURCE),
 'mtmoon-region/mt-moon-b2f': ('1725e6eab3a0d9dfcab6a256f666c1340ffd4027bfff9de2f674377c8c3bf2a3', SOURCE),
}
def export(generated, output, check):
 source = ROOT/'scripts/export-runtime-maps.py'
 if hashlib.sha256(source.read_bytes()).hexdigest() != BASE_HASH: raise ValueError('Base converter differs')
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
 route3_source = ROOT/'scripts/export-route3-runtime-maps.py'
 if hashlib.sha256(route3_source.read_bytes()).hexdigest() != ROUTE3_HASH: raise ValueError('Route3 converter differs')
 spec3=importlib.util.spec_from_file_location('_mtmoon_runtime_route3',route3_source)
 route3=importlib.util.module_from_spec(spec3);spec3.loader.exec_module(route3)
 payloads, entries, metadata = {}, [], None
 for relative, (digest, provenance) in sorted((base.INPUTS | route3.NEW | NEW).items()):
  raw = (generated/relative/'world.json').read_bytes()
  compact = base.convert(raw, digest, provenance)
  data = base.encode(compact); path = relative+'/runtime-map.json'; payloads[path] = data
  entries.append(dict(stableId=compact['stableId'],path=path,byteLength=len(data),sha256=base.sha(data),sourcePath=relative+'/world.json',sourceByteLength=len(raw),sourceWorldSha256=digest,fullSourceHash=provenance))
  candidate = json.loads(raw).get('metadata')
  if candidate is not None:
   base.require(metadata is None or metadata == candidate, 'Base source metadata differs')
   metadata = candidate
 base.require(len(entries)==30 and len({e['stableId'] for e in entries})==30 and metadata is not None, 'Map collection incomplete')
 registry = base.encode(dict(schemaVersion=1,converterVersion=base.VERSION,sourceCommit=base.COMMIT,metadata=metadata))
 payloads['source-registry.json'] = registry
 manifest = dict(schemaVersion=1,converterVersion=base.VERSION,sourceCommit=base.COMMIT,converterSha256=base.sha(Path(__file__).read_bytes()),maps=entries,sourceRegistry=dict(path='source-registry.json',byteLength=len(registry),sha256=base.sha(registry)))
 payloads['manifest.json'] = base.encode(manifest)
 for relative,data in payloads.items():
  target=output/relative
  base.require(not any(p.is_symlink() for p in [target,*target.parents]),'Symlink output rejected')
  if check: base.require(target.is_file() and target.read_bytes()==data,'Stale output: '+relative)
  else:
   target.parent.mkdir(parents=True,exist_ok=True); stage=target.with_suffix('.tmp');stage.write_bytes(data);stage.replace(target)
 return dict(maps=30,sourceWorldBytes=sum(e['sourceByteLength'] for e in entries),runtimeBytes=sum(map(len,payloads.values())),manifestSha256=base.sha(payloads['manifest.json']),converterSha256=manifest['converterSha256'])
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'mtmoon-region/runtime-maps',a.check),sort_keys=True))
