#!/usr/bin/env python3
"""Build the pinned 81-map Vermilion Gym profile without rewriting older map payloads."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
PREVIOUS_MANIFEST = '9a27bcfeef98d5a9adb160509a254a08a0add4f1aff3f9800d45f68c574007ac'
REGION_MANIFEST = 'b98bb7d97c8a0af7f154aa1a640cd172696651bd2909ece7a884b3a607d3f894'
REGION_CATALOG = '98f87daa845b8a5dd31b084877c28f4bdf793dc3297bee02139ead6acbb6d5c8'
REGION_LOCK = '569b92619d2165828b1c05b84eba3795c2e71a4cd5a23936f1aa230414bf0ec8'
SOURCE = '86541efd4d1371314ad44f526ea95ec019665aa66f4a2bd2203c4ccbb51e8e39'
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


def export(generated, output, check=False):
    generated, output = Path(generated), Path(output)
    converter = ROOT / 'scripts/export-runtime-maps.py'
    require(sha(bounded(ROOT, 'scripts/export-runtime-maps.py', 64 * 1024)) == BASE_HASH, 'Reviewed base converter differs')
    spec = importlib.util.spec_from_file_location('_vermilion-gym_runtime_base', converter)
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    previous, region = generated / 'runtime-ssanne-maps', generated / 'vermilion-gym-region'
    for source in (previous, region, ROOT / 'scripts'):
        require(not source.resolve().is_relative_to(output.resolve()) and
                not output.resolve().is_relative_to(source.resolve()), 'Source/output overlap')
    old_bytes = bounded(previous, 'manifest.json', 256 * 1024)
    require(sha(old_bytes) == PREVIOUS_MANIFEST, 'Prior 80-map manifest differs')
    old = json.loads(old_bytes)
    require(len(old['maps']) == 80 and old['converterVersion'] == base.VERSION and
            old['sourceCommit'] == base.COMMIT, 'Prior runtime collection differs')
    manifest_bytes = bounded(region, 'manifest.json', 128 * 1024)
    require(sha(manifest_bytes) == REGION_MANIFEST, 'Vermilion Gym source manifest differs')
    source_manifest = json.loads(manifest_bytes)
    require(source_manifest['mapCount'] == 1 and source_manifest['fullSourceHash'] == SOURCE and
            source_manifest['sourceCommit'] == base.COMMIT, 'Vermilion Gym source profile differs')
    inventory = {entry['path']: entry for entry in source_manifest['outputs']}
    catalog_bytes = authenticated(region, inventory['vermilion-gym-catalog.json'], 256 * 1024)
    require(sha(catalog_bytes) == REGION_CATALOG, 'Vermilion Gym source catalog differs')
    catalog = json.loads(catalog_bytes)
    lock = bounded(ROOT, 'scripts/vermilion-gym-region.lock.json', 256 * 1024)
    require(sha(lock) == REGION_LOCK and json.loads(lock)['files'] == catalog['inputFiles'], 'Vermilion Gym source lock differs')
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
    payloads['vermilion-gym-source-lock.json'] = lock
    new_cells = new_borders = new_warps = 0
    require(len(catalog['maps']) == 1, 'Vermilion Gym map collection incomplete')
    for map_spec in catalog['maps']:
        directory = map_spec['directory']
        rich_entry, compact_entry = inventory[directory + '/world.json'], inventory[directory + '/runtime-map.json']
        raw = authenticated(region, rich_entry, 16 * 1024 * 1024)
        encoded = authenticated(region, compact_entry, MAXIMUM_MAP_BYTES)
        compact = base.convert(raw, rich_entry['sha256'], SOURCE)
        require(encoded == base.encode(compact), 'Vermilion Gym compact derivative differs: ' + directory)
        require(compact['stableId'] == map_spec['stableId'] and compact['sourceRef']['symbol'] == map_spec['sourceMapSymbol'],
                'Vermilion Gym map identity differs')
        path = 'vermilion-gym-region/' + directory + '/runtime-map.json'
        require(path not in payloads, 'Duplicate new runtime path')
        payloads[path] = encoded
        entries.append(dict(stableId=compact['stableId'], path=path, byteLength=len(encoded), sha256=sha(encoded),
                            sourcePath='vermilion-gym-region/' + directory + '/world.json', sourceByteLength=len(raw),
                            sourceWorldSha256=rich_entry['sha256'], fullSourceHash=SOURCE))
        new_cells += compact['width'] * compact['height']
        new_borders += compact['borderWidth'] * compact['borderHeight']
        new_warps += len(compact['events']['warp_events'])
    require((new_cells, new_borders, new_warps) == (231, 4, 3), 'Vermilion Gym source geometry differs')
    entries.sort(key=lambda entry: entry['path'])
    require(len(entries) == 81 and len({entry['stableId'] for entry in entries}) == 81, 'Runtime map collection incomplete')
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
    return dict(maps=81, preservedMaps=80, newCells=new_cells, newBorderCells=new_borders, newWarps=new_warps,
                runtimeBytes=sum(map(len, payloads.values())), manifestSha256=sha(payloads['manifest.json']),
                converterSha256=manifest['converterSha256'], sourceLockSha256=sha(lock))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--generated', type=Path, default=ROOT / 'content/generated')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(args.generated, args.output or args.generated / 'runtime-vermilion-gym-maps', args.check), sort_keys=True))
