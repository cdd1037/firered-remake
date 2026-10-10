#!/usr/bin/env python3
"""Authenticated 41-map compact derivative. Keeps all prior converters and source worlds frozen."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
ROUTE3_HASH = '559ef404effcfb4a13cc81ac9dd9e66f3e234181abebc8da278ebd9161660477'
SOURCE = '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'
MTMOON_HASH = "bc1a2eb48ee7365f21bc75c3d9ec74cb2c1c971ae4a8bd1bee59b1b5f4727bcf"
NEW = {'cerulean-region/cerulean-city': ('814b414d739485ee7eb3c058bba15905f7feef6fcf5af7f34ad8b9ddae1b9552', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-pokemon-center-1f': ('343a1c6e77ffe0423ed624eb0fb858ced4dccc2d30a8bd68f3f0dea3bbfe3760', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-pokemon-center-2f': ('bf45082bb8c6e60c70df7eec9b7e1226ad63194dabfb9184dae88ecd8836cc2c', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-mart': ('1566b09de0dd9dce692c7a8ef3b635596be213e425f7c021bf22b825b689dc07', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-gym': ('5ab2e3c1d9a9c58dc11f28c7d1bbd6169b6cd4432197aba280247db2882c227a', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-bike-shop': ('3a1aacdd0cb82e0ac59bdb9946727ae448ef183a31a3f65b01d53089832c87bb', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-house-1': ('5ce8e903e6a5d52b9dc53d5bdb9dda54303a9af460e970e88944975c0ec946b8', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-house-2': ('7e0e62dc1b730c364b9aea6a0003116e3e5b9b4e27d1adfb09bd5ee3a657e066', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-house-3': ('6b9d84d05a952310f9c3a8dd2522e53e646f84317bd4350e91a3bd270f3099c8', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-house-4': ('e6eaa67dc9eafef8284928cbf4007819146f4316834a52a02fda104a538cbdc0', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a'), 'cerulean-region/cerulean-city-house-5': ('a5bdde345c63b5c5ba9e92661a1347b192e3181089332003597ed8521e349afc', '17dc2d7c3044fba000c0685bc60514095aaec594e5ac958408e41bf7c3308a2a')}
def export(generated, output, check):
 source = ROOT/'scripts/export-runtime-maps.py'
 if hashlib.sha256(source.read_bytes()).hexdigest() != BASE_HASH: raise ValueError('Base converter differs')
 spec = importlib.util.spec_from_file_location('runtime_base', source)
 base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
 route3_source = ROOT/'scripts/export-route3-runtime-maps.py'
 if hashlib.sha256(route3_source.read_bytes()).hexdigest() != ROUTE3_HASH: raise ValueError('Route3 converter differs')
 spec3=importlib.util.spec_from_file_location('_mtmoon_runtime_route3',route3_source)
 route3=importlib.util.module_from_spec(spec3);spec3.loader.exec_module(route3)
 mtmoon_source = ROOT/'scripts/export-mtmoon-runtime-maps.py'
 if hashlib.sha256(mtmoon_source.read_bytes()).hexdigest() != MTMOON_HASH: raise ValueError('Mt. Moon converter differs')
 specm=importlib.util.spec_from_file_location('_cerulean_runtime_mtmoon',mtmoon_source)
 mtmoon=importlib.util.module_from_spec(specm);specm.loader.exec_module(mtmoon)
 payloads, entries, metadata = {}, [], None
 for relative, (digest, provenance) in sorted((base.INPUTS | route3.NEW | mtmoon.NEW | NEW).items()):
  raw = (generated/relative/'world.json').read_bytes()
  compact = base.convert(raw, digest, provenance)
  data = base.encode(compact); path = relative+'/runtime-map.json'; payloads[path] = data
  entries.append(dict(stableId=compact['stableId'],path=path,byteLength=len(data),sha256=base.sha(data),sourcePath=relative+'/world.json',sourceByteLength=len(raw),sourceWorldSha256=digest,fullSourceHash=provenance))
  candidate = json.loads(raw).get('metadata')
  if candidate is not None:
   base.require(metadata is None or metadata == candidate, 'Base source metadata differs')
   metadata = candidate
 base.require(len(entries)==41 and len({e['stableId'] for e in entries})==41 and metadata is not None, 'Map collection incomplete')
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
 return dict(maps=41,sourceWorldBytes=sum(e['sourceByteLength'] for e in entries),runtimeBytes=sum(map(len,payloads.values())),manifestSha256=base.sha(payloads['manifest.json']),converterSha256=manifest['converterSha256'])
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'cerulean-region/runtime-maps',a.check),sort_keys=True))
