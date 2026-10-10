#!/usr/bin/env python3
"""Build the pinned 80-map S.S. Anne profile without rewriting older map payloads."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE_HASH = '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8'
VERMILION_MANIFEST = '2ce94d941d01764791f0211c3dcbcd7245f8a9fff90f7662283b30751e74562b'
SSANNE_MANIFEST = 'f867cb3eba73d9b2995c864d639b8ce5728d0eee069a2a3f9c01515a243b5752'
SSANNE_CATALOG = '17f9ed5f795dd088136cd7fbf8b6fd777f649b87898f0172e20d656999e065fc'
SSANNE_LOCK = '72da55dad3f2bfe3b5ba9255d88be231a2873c349d4d831cf76a1f4446f396d1'
SOURCE = '1650e03d237f1111efb60e3b796359926da3e3039b9d54c94b0612aca96a5c51'
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
    spec = importlib.util.spec_from_file_location('_ssanne_runtime_base', converter)
    base = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(base)
    previous, ship = generated / 'runtime-vermilion-maps', generated / 'ssanne-region'
    for source in (previous, ship, ROOT / 'scripts'):
        require(not source.resolve().is_relative_to(output.resolve()) and
                not output.resolve().is_relative_to(source.resolve()), 'Source/output overlap')
    old_bytes = bounded(previous, 'manifest.json', 256 * 1024)
    require(sha(old_bytes) == VERMILION_MANIFEST, 'Prior 54-map manifest differs')
    old = json.loads(old_bytes)
    require(len(old['maps']) == 54 and old['converterVersion'] == base.VERSION and
            old['sourceCommit'] == base.COMMIT, 'Prior runtime collection differs')
    manifest_bytes = bounded(ship, 'manifest.json', 128 * 1024)
    require(sha(manifest_bytes) == SSANNE_MANIFEST, 'S.S. Anne source manifest differs')
    source_manifest = json.loads(manifest_bytes)
    require(source_manifest['mapCount'] == 26 and source_manifest['fullSourceHash'] == SOURCE and
            source_manifest['sourceCommit'] == base.COMMIT, 'S.S. Anne source profile differs')
    inventory = {entry['path']: entry for entry in source_manifest['outputs']}
    catalog_bytes = authenticated(ship, inventory['ssanne-catalog.json'], 256 * 1024)
    require(sha(catalog_bytes) == SSANNE_CATALOG, 'S.S. Anne source catalog differs')
    catalog = json.loads(catalog_bytes)
    lock = bounded(ROOT, 'scripts/ssanne-region.lock.json', 256 * 1024)
    require(sha(lock) == SSANNE_LOCK and json.loads(lock)['files'] == catalog['inputFiles'], 'S.S. Anne source lock differs')
    payloads, entries = {}, []
    for entry in old['maps']:
        require(entry['path'] not in payloads, 'Duplicate prior runtime path')
        payloads[entry['path']] = authenticated(previous, entry, MAXIMUM_MAP_BYTES)
        entries.append(entry)
    registry = old['sourceRegistry']
    require(registry['path'] == 'source-registry.json', 'Prior source registry path differs')
    payloads[registry['path']] = authenticated(previous, registry, 256 * 1024)
    payloads['ssanne-source-lock.json'] = lock
    new_cells = new_borders = new_warps = 0
    require(len(catalog['maps']) == 26, 'S.S. Anne map collection incomplete')
    for map_spec in catalog['maps']:
        directory = map_spec['directory']
        rich_entry, compact_entry = inventory[directory + '/world.json'], inventory[directory + '/runtime-map.json']
        raw = authenticated(ship, rich_entry, 16 * 1024 * 1024)
        encoded = authenticated(ship, compact_entry, MAXIMUM_MAP_BYTES)
        compact = base.convert(raw, rich_entry['sha256'], SOURCE)
        require(encoded == base.encode(compact), 'S.S. Anne compact derivative differs: ' + directory)
        require(compact['stableId'] == map_spec['stableId'] and compact['sourceRef']['symbol'] == map_spec['sourceMapSymbol'],
                'S.S. Anne map identity differs')
        path = 'ssanne-region/' + directory + '/runtime-map.json'
        require(path not in payloads, 'Duplicate new runtime path')
        payloads[path] = encoded
        entries.append(dict(stableId=compact['stableId'], path=path, byteLength=len(encoded), sha256=sha(encoded),
                            sourcePath='ssanne-region/' + directory + '/world.json', sourceByteLength=len(raw),
                            sourceWorldSha256=rich_entry['sha256'], fullSourceHash=SOURCE))
        new_cells += compact['width'] * compact['height']
        new_borders += compact['borderWidth'] * compact['borderHeight']
        new_warps += len(compact['events']['warp_events'])
    require((new_cells, new_borders, new_warps) == (5450, 104, 58), 'S.S. Anne source geometry differs')
    entries.sort(key=lambda entry: entry['path'])
    require(len(entries) == 80 and len({entry['stableId'] for entry in entries}) == 80, 'Runtime map collection incomplete')
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
    return dict(maps=80, preservedMaps=54, newCells=new_cells, newBorderCells=new_borders, newWarps=new_warps,
                runtimeBytes=sum(map(len, payloads.values())), manifestSha256=sha(payloads['manifest.json']),
                converterSha256=manifest['converterSha256'], sourceLockSha256=sha(lock))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--generated', type=Path, default=ROOT / 'content/generated')
    parser.add_argument('--output', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(export(args.generated, args.output or args.generated / 'runtime-ssanne-maps', args.check), sort_keys=True))
