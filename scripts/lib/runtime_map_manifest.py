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
        require(output.is_file(), 'Missing map collection: ' + str(output))
        existing = json.loads(output.read_bytes())
        if isinstance(existing.get('maps'), list):
            existing['maps'].sort(key=lambda row: row['path'])
        require(existing == json.loads(data), 'Stale map collection: ' + str(output))
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        temporary = output.with_suffix('.tmp'); temporary.write_bytes(data); temporary.replace(output)
    return dict(maps=len(entries), runtimeBytes=len(data))


def compose_region(generated, output, package, catalog_name, previous, check=False):
    generated = Path(generated)
    catalog = json.loads(safe(generated, package + '/' + catalog_name).read_bytes())
    paths = [package + '/' + row['mapPath'] for row in catalog['maps']]
    return compose(generated, output, paths, previous=generated / previous, check=check)


def region_paths(generated, regions):
    """Select current published maps, excluding declared reference-only imports.

    Regions are explicit input selection, not chronological predecessor stages.
    Alternate layouts with the same map identity need an explicit path selection
    rather than silently replacing a base map.
    """
    paths = []
    for region in regions:
        package = 'maps/regions/' + region
        root = safe(generated, package)
        require(root.is_dir(), 'Missing map region: ' + region)
        references = set()
        for catalog in sorted(root.glob('*catalog.json')):
            document = json.loads(catalog.read_bytes())
            references.update(row['mapPath'] for row in document.get('maps', [])
                              if row.get('role') == 'existingPalletReference')
        selected = []
        for path in sorted(root.glob('*.json')):
            if path.name in references:
                continue
            document = json.loads(path.read_bytes())
            if document.get('schemaVersion') == 3 and document.get('stableId'):
                selected.append(package + '/' + path.name)
        require(selected, 'No current runtime maps in region: ' + region)
        paths.extend(selected)
    require(paths and len(paths) == len(set(paths)), 'Empty/duplicate region selection')
    return paths


def verify_package(stage, output, generated):
    """Compare fresh map outputs with either loose PNGs or verified packed pixels.

    Atlas pruning removes only packed image rows from the package manifest. All
    other manifest fields and every resource JSON value remain exact.
    """
    import importlib.util
    from PIL import Image
    from generated_output_compare import generated_output_matches, _same_json
    from project_paths import PROJECT_ROOT
    stage, output, generated = map(Path, (stage, output, generated))
    atlas = None

    def packed():
        nonlocal atlas
        if atlas is None:
            path = PROJECT_ROOT / 'scripts/imports/maps/import-map-atlases.py'
            spec = importlib.util.spec_from_file_location('_verify_package_atlas', path)
            helper = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(helper)
            atlas = helper.AtlasReader(generated / 'maps/atlases')
        return atlas

    for path in sorted(stage.rglob('*')):
        if not path.is_file() or path == stage / 'manifest.json':
            continue
        target = safe(output, path.relative_to(stage).as_posix())
        if path.suffix == '.png' and not target.is_file():
            actual = packed().read(target.relative_to(generated).as_posix())
            with Image.open(path) as image:
                expected = image.convert('RGBA')
            require(actual.size == expected.size and actual.tobytes() == expected.tobytes(),
                    'Packed map pixels differ: ' + str(target))
        else:
            require(generated_output_matches(target, path.read_bytes()), 'Map output differs: ' + str(target))
    manifest = stage / 'manifest.json'
    if manifest.is_file():
        target = output / 'manifest.json'
        require(target.is_file(), 'Missing map manifest: ' + str(target))
        expected = json.loads(manifest.read_bytes())
        actual = json.loads(target.read_bytes())
        if _same_json(actual, expected):
            return
        pruned = dict(expected)
        images = packed().catalog['images']
        for key in ('outputs', 'files', 'artifacts'):
            if not isinstance(expected.get(key), list):
                continue
            kept = []
            for row in expected[key]:
                relative = row if isinstance(row, str) else row.get('path') if isinstance(row, dict) else None
                logical = safe(output, relative).relative_to(generated).as_posix() if relative else None
                if logical not in images:
                    kept.append(row)
            pruned[key] = kept
            if key == 'outputs' and 'outputCount' in expected and len(kept) != len(expected[key]):
                pruned['outputCount'] = len(kept)
        require(_same_json(actual, pruned), 'Map manifest differs beyond atlas pruning: ' + str(target))
