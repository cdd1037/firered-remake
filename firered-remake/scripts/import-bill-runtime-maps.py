#!/usr/bin/env python3
"""Authenticated 44-map compact derivative. Keeps all prior converters and source worlds frozen."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
ROUTE3_HASH = '559ef404effcfb4a13cc81ac9dd9e66f3e234181abebc8da278ebd9161660477'
CERULEAN_HASH = '7eca0867416f7e051064bae1ab4457a9ab67dc2c52f0d3158bfd0c6793a35643'
SOURCE = '9da829b988590aad4b0b57950e066bf651ff7fde614b6d43294682606cb62173'
MTMOON_HASH = 'bc1a2eb48ee7365f21bc75c3d9ec74cb2c1c971ae4a8bd1bee59b1b5f4727bcf'
NEW = {'bill-region/route-24': ('1400e93a5d0eabfc5a3c10f2ad804d73576a64fba7dfe19a596d76ecd92ef3a3', '9da829b988590aad4b0b57950e066bf651ff7fde614b6d43294682606cb62173'), 'bill-region/route-25': ('21a27d42594192f7ba7fa7b5b276601c7d9341f3aa3a84d81a83e84dc6a8de74', '9da829b988590aad4b0b57950e066bf651ff7fde614b6d43294682606cb62173'), 'bill-region/route-25-sea-cottage': ('c78cbfd1ad7e89ba21eb17547e7135fe8e0836d48d6f9f9becc1b202f9d4ec5c', '9da829b988590aad4b0b57950e066bf651ff7fde614b6d43294682606cb62173')}
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
 cerulean_source = ROOT/'scripts/import-cerulean-runtime-maps.py'
 if hashlib.sha256(cerulean_source.read_bytes()).hexdigest() != CERULEAN_HASH: raise ValueError('Cerulean converter differs')
 specc=importlib.util.spec_from_file_location('_bill_runtime_cerulean',cerulean_source)
 cerulean=importlib.util.module_from_spec(specc);specc.loader.exec_module(cerulean)
 payloads, entries, metadata = {}, [], None
 for relative, (digest, provenance) in sorted((base.INPUTS | route3.NEW | mtmoon.NEW | cerulean.NEW | NEW).items()):
  raw = (generated/relative/'world.json').read_bytes()
  compact = base.convert(raw, digest, provenance)
  data = base.encode(compact); path = relative+'/runtime-map.json'; payloads[path] = data
  entries.append(dict(stableId=compact['stableId'],path=path,byteLength=len(data),sha256=base.sha(data),sourcePath=relative+'/world.json',sourceByteLength=len(raw),sourceWorldSha256=digest,fullSourceHash=provenance))
  candidate = json.loads(raw).get('metadata')
  if candidate is not None:
   base.require(metadata is None or metadata == candidate, 'Base source metadata differs')
   metadata = candidate
 base.require(len(entries)==44 and len({e['stableId'] for e in entries})==44 and metadata is not None, 'Map collection incomplete')
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
 return dict(maps=44,sourceWorldBytes=sum(e['sourceByteLength'] for e in entries),runtimeBytes=sum(map(len,payloads.values())),manifestSha256=base.sha(payloads['manifest.json']),converterSha256=manifest['converterSha256'])
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--generated',type=Path,default=ROOT/'content/generated');parser.add_argument('--output',type=Path);parser.add_argument('--check',action='store_true');a=parser.parse_args()
 print(json.dumps(export(a.generated,a.output or a.generated/'bill-region/runtime-maps',a.check),sort_keys=True))
