#!/usr/bin/env python3
"""Authenticated 27-map compact derivative. Keeps the prior 24-map converter frozen."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
SOURCE = 'e90a7fc0415d11f56d9e2f341dc3e5dd26e0befc60bd9385ebfb432b8cadbc1b'
NEW = {
 'route3-region/route-3': ('197493c71c6e0f18d201af9e34b13348d1627d91c7855e11cb1d9d2e2c18a16b', SOURCE),
 'route3-region/route-4': ('2773a2976477fa040374dba1815c509cea03de40c71c1831e6fe129090c7c268', SOURCE),
 'route3-region/route-4-pokemon-center-1f': ('f64fa5ea10fbf098c90ea2397579bd44a63aca1b7876285dc562e5c5df288276', SOURCE),
}
def export(generated, output, check):
 source = ROOT/'scripts/export-runtime-maps.py'
 if hashlib.sha256(source.read_bytes()).hexdigest() != BASE_HASH: raise ValueError('Base converter differs')
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
 payloads, entries, metadata = {}, [], None
 for relative, (digest, provenance) in sorted((base.INPUTS | NEW).items()):
  raw = (generated/relative/'world.json').read_bytes()
  compact = base.convert(raw, digest, provenance)
  data = base.encode(compact); path = relative+'/runtime-map.json'; payloads[path] = data
  entries.append(dict(stableId=compact['stableId'],path=path,byteLength=len(data),sha256=base.sha(data),sourcePath=relative+'/world.json',sourceByteLength=len(raw),sourceWorldSha256=digest,fullSourceHash=provenance))
  candidate = json.loads(raw).get('metadata')
  if candidate is not None:
   base.require(metadata is None or metadata == candidate, 'Base source metadata differs')
   metadata = candidate
 base.require(len(entries)==27 and len({e['stableId'] for e in entries})==27 and metadata is not None, 'Map collection incomplete')
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
 return dict(maps=27,sourceWorldBytes=sum(e['sourceByteLength'] for e in entries),runtimeBytes=sum(map(len,payloads.values())),manifestSha256=base.sha(payloads['manifest.json']),converterSha256=manifest['converterSha256'])
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'runtime-maps-route3',a.check),sort_keys=True))
