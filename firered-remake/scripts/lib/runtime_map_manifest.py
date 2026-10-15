"""Compose explicitly named map files into a runtime collection."""
import json
from pathlib import Path
from map_runtime import encode, require


def safe(root, relative):
    path = Path(relative)
    require(relative and not path.is_absolute() and '\\' not in relative and ':' not in relative
            and all(part not in ('', '.', '..') for part in relative.split('/')), 'Unsafe map reference: ' + relative)
    target = Path(root) / path
    require(not any(p.is_symlink() for p in (target, *target.parents)), 'Symlink map path rejected')
    return target


def read_map(generated, relative):
    document = json.loads(safe(generated, relative).read_bytes())
    require(document.get('schemaVersion') == 3 and document.get('scriptPaths')
            and 'sourceRef' not in document and 'cells' not in document and 'embeddedSource' not in document,
            'Regenerate the current compact map: ' + relative)
    return document


def compose(generated, output, paths, *, previous=None, check=False):
    generated, output = Path(generated), Path(output)
    require(output.suffix == '.json' and generated.resolve() != output.resolve(), 'Map collection output must be a JSON file')
    entries = []
    if previous is not None:
        previous = Path(previous)
        require(previous.resolve() != output.resolve(), 'Source/output overlap')
        document = json.loads(previous.read_bytes())
        require(document.get('schemaVersion') == 3 and document.get('maps'), 'Regenerate the previous map collection')
        entries.extend(dict(stableId=row['stableId'], path=row['path']) for row in document['maps'])
    for relative in sorted(paths):
        document = read_map(generated, relative)
        entries.append(dict(stableId=document['stableId'], path=relative))
    entries.sort(key=lambda row: row['path'])
    require(entries and len({row['path'] for row in entries}) == len(entries)
            and len({row['stableId'] for row in entries}) == len(entries), 'Empty/duplicate map collection')
    for row in entries:
        require(read_map(generated, row['path'])['stableId'] == row['stableId'], 'Map reference identity differs')
    require(safe(generated, 'maps/metadata.json').is_file(), 'Missing map metadata')
    data = encode(dict(schemaVersion=3, maps=entries, sourceRegistry=dict(path='maps/metadata.json')))
    if check:
        require(output.is_file() and json.loads(output.read_bytes()) == json.loads(data), 'Stale map collection: ' + str(output))
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        temporary = output.with_suffix('.tmp'); temporary.write_bytes(data); temporary.replace(output)
    return dict(maps=len(entries), runtimeBytes=len(data))


def compose_region(generated, output, package, catalog_name, previous, check=False):
    generated = Path(generated)
    catalog = json.loads(safe(generated, package + '/' + catalog_name).read_bytes())
    paths = [package + '/' + row['mapPath'] for row in catalog['maps']]
    return compose(generated, output, paths, previous=generated / previous, check=check)
