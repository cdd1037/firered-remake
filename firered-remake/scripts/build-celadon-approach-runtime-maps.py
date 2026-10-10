#!/usr/bin/env python3
"""Build the 113-map Celadon approach package, preserving all prior 98 maps byte for byte."""
import argparse
import importlib.util
import json
from pathlib import Path
import hashlib
ROOT = Path(__file__).resolve().parent.parent
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
PREVIOUS_MANIFEST = 'a0fb56162cc143d71e991559d637b2e6cb0d47f3cf90adc2b5fd660fcef78486'
REGION_MANIFEST = 'f487d67103ec1db904fb0bee6caa95f1b1f96f5725ad850e83e42b6f3f868d3f'
REGION_CATALOG = 'b36aa010a286c7f04de62cd3d805fdaff2a4d6ab1194cbda49dc4923277e9c96'
REGION_LOCK = 'b09ce111e7a091e7cc551f0d4660fb468f9a805340af0f80056b12d1db00242a'
SOURCE = '756a0c0bfe4ab147b9443e71a8937a5b2a661a65e192919c5ac2691b89589f89'
MAXIMUM_MAP_BYTES = 2 * 1024 * 1024

def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe(root, relative):
    path = Path(relative)
    require(bool(relative) and not path.is_absolute() and '\\' not in relative and ':' not in relative
            and all(part not in ('', '.', '..') for part in relative.split('/'))
            and path.as_posix() == relative, 'Unsafe runtime path')
    target = Path(root) / path
    require(not any(p.is_symlink() for p in [target, *target.parents]), 'Symlink source/output rejected')
    return target


def bounded(root, relative, maximum):
    with safe(root, relative).open('rb') as stream:
        raw = stream.read(maximum + 1)
    require(0 < len(raw) <= maximum, 'Runtime input size out of bounds: ' + relative)
    return raw


def authenticated(root, entry, maximum):
    require(type(entry['byteLength']) is int and 0 < entry['byteLength'] <= maximum, 'Invalid runtime input length')
    raw = bounded(root, entry['path'], maximum)
    require(len(raw) == entry['byteLength'] and sha(raw) == entry['sha256'], 'Pinned runtime input differs: ' + entry['path'])
    return raw


def export_runtime(generated, output, check=False):
    generated, output = Path(generated), Path(output)
    for kind in ['raw', 'generated']:
        for protected in (ROOT/'content'/kind).iterdir():
            if kind == 'generated' and protected.name == 'celadon-approach-runtime-maps': continue
            require(not output.resolve().is_relative_to(protected.resolve()) and
                    not protected.resolve().is_relative_to(output.resolve()), 'Existing profile overlap')
    for protected in [ROOT/'scripts', ROOT/'src', ROOT/'tests', ROOT/'.git']:
        require(not output.resolve().is_relative_to(protected.resolve()) and not protected.resolve().is_relative_to(output.resolve()), 'Protected project path overlap')
    source_locks = ROOT/'content/sources'
    require(not output.resolve().is_relative_to(source_locks.resolve()) and
            not source_locks.resolve().is_relative_to(output.resolve()), 'Source lock/output overlap')
    converter = ROOT / 'scripts/export-runtime-maps.py'
    require(sha(bounded(ROOT, 'scripts/export-runtime-maps.py', 64 * 1024)) == BASE_HASH, 'Reviewed base converter differs')
    spec = importlib.util.spec_from_file_location('_celadon-approach_region_runtime_base', converter)
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    previous, region = generated / 'rocktunnel-runtime-maps', generated / 'celadon-approach-region'
    for source in (previous, region, ROOT / 'scripts'):
        require(not source.resolve().is_relative_to(output.resolve()) and
                not output.resolve().is_relative_to(source.resolve()), 'Source/output overlap')
    old_bytes = bounded(previous, 'manifest.json', 256 * 1024)
    require(sha(old_bytes) == PREVIOUS_MANIFEST, 'Prior 98-map manifest differs')
    old = json.loads(old_bytes)
    require(len(old['maps']) == 98 and old['converterVersion'] == base.VERSION and
            old['sourceCommit'] == base.COMMIT, 'Prior runtime collection differs')
    manifest_bytes = bounded(region, 'manifest.json', 128 * 1024)
    require(sha(manifest_bytes) == REGION_MANIFEST, 'Celadon approach source manifest differs')
    source_manifest = json.loads(manifest_bytes)
    require(source_manifest['mapCount'] == 15 and source_manifest['fullSourceHash'] == SOURCE and
            source_manifest['sourceCommit'] == base.COMMIT, 'Celadon approach source profile differs')
    inventory = {entry['path']: entry for entry in source_manifest['outputs']}
    catalog_bytes = authenticated(region, inventory['celadon-approach-catalog.json'], 256 * 1024)
    require(sha(catalog_bytes) == REGION_CATALOG, 'Celadon approach source catalog differs')
    catalog = json.loads(catalog_bytes)
    lock = bounded(ROOT, 'content/sources/celadon-approach-region.lock.json', 256 * 1024)
    require(sha(lock) == REGION_LOCK and json.loads(lock)['files'] == catalog['inputFiles'], 'Celadon approach source lock differs')
    payloads, entries = {}, []
    for entry in old['maps']:
        require(entry['path'] not in payloads, 'Duplicate prior runtime path')
        payloads[entry['path']] = authenticated(previous, entry, MAXIMUM_MAP_BYTES)
        entries.append(entry)
    registry = old['sourceRegistry']
    require(registry['path'] == 'source-registry.json', 'Prior source registry path differs')
    payloads[registry['path']] = authenticated(previous, registry, 256 * 1024)
    ship_lock = bounded(previous, 'ssanne-source-lock.json', 256 * 1024)
    require(sha(ship_lock) == '72da55dad3f2bfe3b5ba9255d88be231a2873c349d4d831cf76a1f4446f396d1', 'Prior ship source lock differs')
    payloads['ssanne-source-lock.json'] = ship_lock
    gym_lock = bounded(previous, 'vermilion-gym-source-lock.json', 256 * 1024)
    require(sha(gym_lock) == '569b92619d2165828b1c05b84eba3795c2e71a4cd5a23936f1aa230414bf0ec8', 'Prior Gym source lock differs')
    payloads['vermilion-gym-source-lock.json'] = gym_lock
    city_lock = bounded(previous, 'city-services-source-lock.json', 256 * 1024)
    require(sha(city_lock) == '4b71a3820a2d50ff80c32589c60979cb519dd7aeecec9bec13696bf1ad317eb5', 'Prior city source lock differs')
    payloads['city-services-source-lock.json'] = city_lock
    route11_lock = bounded(previous, 'route11-region-source-lock.json', 256 * 1024)
    require(sha(route11_lock) == '6fcb8bd7fc9402db22376e642252b4fad9fcfed927aeec48db5d2d4b669b4304', 'Prior Route11 source lock differs')
    payloads['route11-region-source-lock.json'] = route11_lock
    rock_lock = bounded(previous, 'rocktunnel-region-source-lock.json', 256 * 1024)
    require(sha(rock_lock) == '4c687593e368f67cc7db73c0255de541a8cb5b19590326c5562993087f4114c8', 'Prior RockTunnel source lock differs')
    payloads['rocktunnel-region-source-lock.json'] = rock_lock
    payloads['celadon-approach-region-source-lock.json'] = lock
    new_cells = new_borders = new_warps = 0
    require(len(catalog['maps']) == 15, 'Celadon approach map collection incomplete')
    for map_spec in catalog['maps']:
        directory = map_spec['directory']
        rich_entry, compact_entry = inventory[directory + '/world.json'], inventory[directory + '/runtime-map.json']
        raw = authenticated(region, rich_entry, 16 * 1024 * 1024)
        encoded = authenticated(region, compact_entry, MAXIMUM_MAP_BYTES)
        compact = base.convert(raw, rich_entry['sha256'], SOURCE)
        require(encoded == base.encode(compact), 'Celadon approach compact derivative differs: ' + directory)
        require(compact['stableId'] == map_spec['stableId'] and compact['sourceRef']['symbol'] == map_spec['sourceMapSymbol'],
                'Celadon approach map identity differs')
        path = 'celadon-approach-region/' + directory + '/runtime-map.json'
        require(path not in payloads, 'Duplicate new runtime path')
        payloads[path] = encoded
        entries.append(dict(stableId=compact['stableId'], path=path, byteLength=len(encoded), sha256=sha(encoded),
                            sourcePath='celadon-approach-region/' + directory + '/world.json', sourceByteLength=len(raw),
                            sourceWorldSha256=rich_entry['sha256'], fullSourceHash=SOURCE))
        new_cells += compact['width'] * compact['height']
        new_borders += compact['borderWidth'] * compact['borderHeight']
        new_warps += len(compact['events']['warp_events'])
    require((new_cells, new_borders, new_warps) == (6280, 60, 58), 'Celadon approach source geometry differs')
    entries.sort(key=lambda entry: entry['path'])
    require(len(entries) == 113 and len({entry['stableId'] for entry in entries}) == 113, 'Runtime map collection incomplete')
    manifest = dict(schemaVersion=1, converterVersion=base.VERSION, sourceCommit=base.COMMIT,
                    converterSha256=sha(Path(__file__).read_bytes()), maps=entries, sourceRegistry=registry)
    payloads['manifest.json'] = base.encode(manifest)
    # Validate every input and every final/staging destination before any writes. Manifest is published last.
    for relative in payloads:
        target = safe(output, relative)
        safe(output, target.with_suffix('.tmp').relative_to(output).as_posix())
    for relative, encoded in payloads.items():
        target = safe(output, relative)
        if check:
            require(target.is_file() and bounded(output, relative, max(len(encoded), 1)) == encoded, 'Stale output: ' + relative)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            stage = target.with_suffix('.tmp')
            stage.write_bytes(encoded)
            stage.replace(target)
    return dict(maps=113, preservedMaps=98, newCells=new_cells, newBorderCells=new_borders, newWarps=new_warps,
                runtimeBytes=sum(map(len, payloads.values())), manifestSha256=sha(payloads['manifest.json']),
                converterSha256=manifest['converterSha256'], sourceLockSha256=sha(lock))


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--generated',type=Path,default=ROOT/'content/generated')
    parser.add_argument('--output',type=Path,default=ROOT/'content/generated/celadon-approach-runtime-maps')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    print(json.dumps(export_runtime(args.generated,args.output,args.check),sort_keys=True))
