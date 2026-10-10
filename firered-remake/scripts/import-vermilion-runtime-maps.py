#!/usr/bin/env python3
"""Authenticated 54-map compact package; all 44 Bill payloads and provenance stay byte-identical."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
ROOT = Path(__file__).resolve().parents[1]
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
BILL_MANIFEST = '970f0c808a3f49c085ac11208bf581a40cf931daffa8dace3225b129d05653e0'
SOURCE = 'fffd904ff456351bef0ed808b1043c968f2b03db122a10e056ac3971b88f1759'
NEW = {'vermilion-region/route-5': '10a31486df8d130fd1c91d27655500a54c73b36e3f07ee5eebe134bd9fb9f470', 'vermilion-region/route-5-south-entrance': 'd53d2a65c17d9f984809dca4b001ec1e3e60f437f9e79c7857f49b72fa7c9f1b', 'vermilion-region/underground-path-north-entrance': '608d81f8c7dc7991a54b2c44eb8eab1e52b86303fe7bcd910e7c2797516ba4c7', 'vermilion-region/underground-path-north-south-tunnel': '19c80cad56e200fd6c820f73b43ef0da4cc06bcd29af4bef85830a50e3076e71', 'vermilion-region/underground-path-south-entrance': '7408ceb8eb865f4d49a461fb700cfc87b534280413fb7493cc973a7fa4fa673b', 'vermilion-region/route-6-north-entrance': 'e641d950507f239f1c227309c6634540aaf61e5a2e968bb0d88276520a0d69ec', 'vermilion-region/route-6': '94a8ea841c2ca7da7d035dfc43bbc4fd48b9dd236aaf2a0973321abf72cb02d4', 'vermilion-region/vermilion-city': '3ac2380b6e27b311a72a892a4ba7319ea5908472ff3425afbcd08e5aaa20759e', 'vermilion-region/vermilion-city-pokemon-center-1f': '71cc294c3650f7d5c5dec63cf35d4acabb11ecef4a3401ba1475495ff2c4bd97', 'vermilion-region/vermilion-city-mart': '40b99270b3fb807b3a5212216c10d21964a56c6b3a2179fddb2649118e494c26'}


def export(generated, output, check=False):
    converter = ROOT/'scripts/export-runtime-maps.py'
    if hashlib.sha256(converter.read_bytes()).hexdigest() != BASE_HASH:
        raise ValueError('Reviewed base converter differs')
    spec = importlib.util.spec_from_file_location('_vermilion_runtime_base', converter)
    base = importlib.util.module_from_spec(spec); spec.loader.exec_module(base)
    def safe(root, relative):
        p = Path(relative)
        base.require(not p.is_absolute() and '..' not in p.parts and p.as_posix() == relative, 'Unsafe runtime path')
        target = root/p
        base.require(not any(p.is_symlink() for p in [target,*target.parents]), 'Symlink source/output rejected')
        return target
    previous = generated/'bill-region/runtime-maps'
    base.require(not previous.resolve().is_relative_to(output.resolve()) and
                 not (generated/'vermilion-region').resolve().is_relative_to(output.resolve()), 'Source/output overlap')
    manifest_bytes = safe(previous,'manifest.json').read_bytes()
    base.require(base.sha(manifest_bytes)==BILL_MANIFEST,'Prior 44-map manifest differs')
    old = json.loads(manifest_bytes)
    base.require(len(old['maps'])==44 and old['converterVersion']==base.VERSION and old['sourceCommit']==base.COMMIT,
                 'Prior runtime collection differs')
    payloads, entries = {}, []
    # Preserve authenticated older entries and bytes, including their original fullSourceHash.
    for entry in old['maps']:
        raw = safe(previous,entry['path']).read_bytes()
        base.require(len(raw)==entry['byteLength'] and base.sha(raw)==entry['sha256'],'Prior map differs: '+entry['path'])
        payloads[entry['path']] = raw; entries.append(entry)
    registry_entry = old['sourceRegistry']
    registry = safe(previous,registry_entry['path']).read_bytes()
    base.require(len(registry)==registry_entry['byteLength'] and base.sha(registry)==registry_entry['sha256'], 'Prior source registry differs')
    payloads[registry_entry['path']] = registry
    for relative, expected in sorted(NEW.items()):
        raw = safe(generated,relative+'/world.json').read_bytes()
        compact = base.convert(raw,expected,SOURCE); encoded=base.encode(compact)
        path=relative+'/runtime-map.json'
        base.require(path not in payloads,'Duplicate new map path')
        payloads[path]=encoded
        entries.append(dict(stableId=compact['stableId'],path=path,byteLength=len(encoded),sha256=base.sha(encoded),
            sourcePath=relative+'/world.json',sourceByteLength=len(raw),sourceWorldSha256=expected,fullSourceHash=SOURCE))
    entries.sort(key=lambda e:e['path'])
    base.require(len(entries)==54 and len({e['stableId'] for e in entries})==54,'Map collection incomplete')
    manifest=dict(schemaVersion=1,converterVersion=base.VERSION,sourceCommit=base.COMMIT,
        converterSha256=base.sha(Path(__file__).read_bytes()),maps=entries,sourceRegistry=registry_entry)
    payloads['manifest.json']=base.encode(manifest)
    # Validate all inputs and destinations before writing; publish the manifest last.
    for relative in payloads: safe(output,relative)
    for relative,encoded in payloads.items():
        target=safe(output,relative)
        if check: base.require(target.is_file() and target.read_bytes()==encoded,'Stale output: '+relative)
        else:
            target.parent.mkdir(parents=True,exist_ok=True)
            stage=target.with_suffix('.tmp');safe(output,stage.relative_to(output).as_posix())
            stage.write_bytes(encoded);stage.replace(target)
    return dict(maps=54,preservedMaps=44,sourceWorldBytes=sum(e['sourceByteLength'] for e in entries),
        runtimeBytes=sum(map(len,payloads.values())),manifestSha256=base.sha(payloads['manifest.json']),converterSha256=manifest['converterSha256'])


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--generated',type=Path,default=ROOT/'content/generated')
    p.add_argument('--output',type=Path)
    p.add_argument('--check',action='store_true')
    a=p.parse_args()
    print(json.dumps(export(a.generated,a.output or a.generated/'runtime-vermilion-maps',a.check),sort_keys=True))
