"""Compose map collections as references to one canonical generated map tree."""
import json
from pathlib import Path

from map_runtime import COMMIT, VERSION, encode, require


def safe(root, relative):
    path = Path(relative)
    require(relative and not path.is_absolute() and '\\' not in relative and ':' not in relative
            and all(part not in ('', '.', '..') for part in relative.split('/')),
            'Unsafe map reference: ' + relative)
    target = Path(root) / path
    require(not any(p.is_symlink() for p in (target, *target.parents)), 'Symlink map path rejected')
    return target


def read_map(generated, relative):
    doc = json.loads(safe(generated, relative).read_bytes())
    require(doc.get('schemaVersion') == 2 and doc.get('converterVersion') == VERSION,
            'Regenerate map with the current importer: ' + relative)
    require(doc.get('scriptPaths') and 'scripts' not in doc and 'cells' not in doc and 'embeddedSource' not in doc,
            'Map must use the compact runtime format: ' + relative)
    return doc


def compose(generated, output, directories, *, previous=None, expected_count=None, check=False):
    generated, output = Path(generated), Path(output)
    require(generated.resolve() != output.resolve() and not generated.resolve().is_relative_to(output.resolve()),
            'Runtime collection output cannot contain the canonical generated tree')
    entries = []
    if previous is not None:
        previous = Path(previous)
        require(previous.resolve() != output.resolve(), 'Source/output overlap')
        old = json.loads(safe(previous, 'manifest.json').read_bytes())
        require(old.get('schemaVersion') == 2 and old.get('converterVersion') == VERSION,
                'Regenerate the prior runtime manifest')
        entries = [dict(stableId=e['stableId'], path=e['path']) for e in old['maps']]
    preserved = len(entries)
    new_cells = new_borders = new_warps = 0
    for directory in sorted(directories):
        relative = directory.rstrip('/') + '/runtime-map.json'
        doc = read_map(generated, relative)
        entries.append(dict(stableId=doc['stableId'], path=relative))
        new_cells += doc['width'] * doc['height']
        new_borders += doc['borderWidth'] * doc['borderHeight']
        new_warps += len(doc['events']['warp_events'])
    entries.sort(key=lambda e: e['path'])
    require(len({e['path'] for e in entries}) == len(entries) and len({e['stableId'] for e in entries}) == len(entries),
            'Duplicate runtime map reference')
    require(expected_count is None or len(entries) == expected_count, 'Runtime map collection incomplete')
    for entry in entries:
        target = safe(generated, entry['path'])
        require(not target.resolve().is_relative_to(output.resolve()), 'Canonical map/output overlap')
        require(read_map(generated, entry['path'])['stableId'] == entry['stableId'], 'Map reference identity differs')
    require(safe(generated, 'map-source-registry.json').is_file(), 'Missing shared map source registry')
    data = encode(dict(schemaVersion=2, converterVersion=VERSION, sourceCommit=COMMIT, maps=entries,
                       sourceRegistry=dict(path='map-source-registry.json')))
    target = safe(output, 'manifest.json')
    obsolete = [p for leaf in ('world.json', 'runtime-map.json', 'source-registry.json', '*source-lock.json') for p in output.rglob(leaf)] if output.exists() else []
    for path in obsolete:
        safe(output, path.relative_to(output).as_posix())
    if check:
        require(target.is_file() and target.read_bytes() == data and not obsolete, 'Stale runtime map collection')
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage = safe(output, 'manifest.json.tmp'); stage.write_bytes(data); stage.replace(target)
        for path in obsolete:
            path.unlink()
    return dict(maps=len(entries), preservedMaps=preserved, newCells=new_cells, newBorderCells=new_borders,
                newWarps=new_warps, runtimeBytes=len(data))


def compose_region(generated, output, package, catalog_name, previous, expected_count, check=False):
    generated = Path(generated)
    catalog = json.loads(safe(generated, package + '/' + catalog_name).read_bytes())
    directories = [package + '/' + row['directory'] for row in catalog['maps']]
    return compose(generated, output, directories, previous=generated / previous,
                   expected_count=expected_count, check=check)
