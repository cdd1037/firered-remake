#!/usr/bin/env python3
"""Rebuild ordinary interiors from pinned raw source and unchanged exterior doors.

The source selection is import-only. Runtime geometry, actors and door contracts
come from the catalog, without a parallel room-count/coordinate table.
"""
import argparse
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import tempfile
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as ROOT
from map_runtime import encode, publish_map_package
from PIL import Image

PACKAGE = 'maps/regions/school-house-interiors'
RESOURCE_ID = 'firered-school-house-interiors-v1'
MAPS = ['ViridianCity_School', 'FuchsiaCity_House1', 'ThreeIsland_House2',
        'ViridianCity_House', 'VermilionCity_House3', 'SaffronCity_House',
        'OneIsland_House1', 'OneIsland_House2', 'ThreeIsland_House1',
        'ThreeIsland_House3', 'ThreeIsland_House4', 'ThreeIsland_House5',
        'FiveIsland_House1', 'FiveIsland_House2', 'SixIsland_House']
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_School': 'secondary/school',
            'gTileset_GenericBuilding1': 'secondary/generic_building_1',
            'gTileset_GenericBuilding2': 'secondary/generic_building_2', 'gTileset_Lab': 'secondary/lab'}


def module(path, name):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def require(value, message):
    if not value: raise ValueError(message)


def convert(root, raw, check=False):
    reader = module(ROOT / 'scripts/imports/maps/import-fuchsia-route-region.py', 'interior_source')
    src = reader.Source(raw_root=raw)
    renderer = module(ROOT / 'tools/import-regional-service-maps/_regional_service_shared.py', 'interior_renderer')
    renderer.TILESETS, renderer.PROFILE = TILESETS, RESOURCE_ID
    cleared_renderer = module(ROOT / 'scripts/lib/school-house-map-renderer.py', 'interior_cleared_renderer')
    generated = root / 'content/generated'
    output = generated / PACKAGE
    existing = {}
    for path in (generated / 'maps').rglob('*.json'):
        data = json.loads(path.read_text())
        if isinstance(data, dict) and 'events' in data and 'stableId' in data:
            existing[data['events']['id']] = (path, data)
    events = {name: src.json(f'data/maps/{name}/map.json') for name in MAPS}
    identities = {name: 'map.' + reader.directory(name) for name in MAPS}
    identities.update(ViridianCity_School='map.viridian-school', FuchsiaCity_House1='map.fuchsia-city-house-1',
                      ViridianCity_House='map.viridian-house', VermilionCity_House3='map.vermilion-city-house-3')
    symbols = {event['id']: identities[name] for name, event in events.items()}
    anchors = []
    for name, event in events.items():
        require(event['connections'] is None and not event['coord_events'], 'Unexpected indoor event scope: ' + name)
        for leaf in ('scripts.inc', 'text.inc'): src.read(f'data/maps/{name}/{leaf}')
        for warp in event['warp_events']:
            path, exterior = existing[warp['dest_map']]
            original = src.json('data/maps/' + exterior['sourceMapName'] + '/map.json')
            require(exterior['events'] == original, 'Exterior events differ: ' + str(path))
            index = int(warp['dest_warp_id'])
            entrance = original['warp_events'][index]
            require(entrance['dest_map'] == event['id'], 'Door is not reciprocal: ' + name)
            returned = event['warp_events'][int(entrance['dest_warp_id'])]
            require(returned['dest_map'] == original['id'] and returned['dest_warp_id'] == str(index), 'Door index differs')
            symbols[original['id']] = exterior['stableId']
            anchor = dict(sourceMap=original['id'], map=exterior['stableId'], currentPackage='content/generated',
                          currentResource=dict(path=path.relative_to(generated).as_posix()),
                          entrances=[dict(index=index, event=entrance)])
            if anchor not in anchors: anchors.append(anchor)
    with tempfile.TemporaryDirectory(prefix='ordinary-interiors-') as temporary:
        stage = Path(temporary)
        worlds = [cleared_renderer.import_map(src, name, identities[name], stage / reader.directory(name), renderer) for name in MAPS]
        docs = {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, mapSymbols=symbols, tilesets=TILESETS,
                    maps=[dict(sourceName=name, stableId=identities[name], sourceMapSymbol=events[name]['id'],
                               mapPath=reader.directory(name) + '.json') for name in MAPS]),
                'contracts.json': dict(schemaVersion=1, retainedAnchors=anchors),
                'npc-source.json': dict(schemaVersion=1, dynamicGraphics=[], graphics=[dict(graphicsId=identity)
                    for identity in sorted({actor['graphics_id'] for event in events.values() for actor in event['object_events']} | {'OBJ_EVENT_GFX_RED_NORMAL'})])}
        for name, data in docs.items(): (stage / name).write_bytes(encode(data))
        publish_map_package(stage)
        manifest = dict(schemaVersion=1, resourceId=RESOURCE_ID, outputs=[dict(path=p.name) for p in sorted(stage.iterdir()) if p.is_file()])
        (stage / 'manifest.json').write_bytes(encode(manifest))
        atlas = None
        for path in sorted(stage.iterdir()):
            destination = output / path.name
            if check:
                if path.suffix == '.png':
                    with Image.open(path) as image: expected = image.convert('RGBA')
                    if destination.is_file():
                        with Image.open(destination) as image: actual = image.convert('RGBA')
                    else:
                        if atlas is None:
                            helper = module(ROOT / 'scripts/imports/maps/import-map-atlases.py', 'interior_atlas')
                            atlas = helper.AtlasReader(generated / 'maps/atlases')
                        actual = atlas.read(PACKAGE + '/' + path.name)
                    require(actual.size == expected.size and actual.tobytes() == expected.tobytes(), 'Interior pixels differ: ' + path.name)
                else:
                    require(destination.is_file() and destination.read_bytes() == path.read_bytes(), 'Interior output differs: ' + path.name)
            else:
                output.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(path, destination)
    return dict(maps=len(MAPS), checked=check)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--raw-root', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(convert(args.project_root, args.raw_root or args.project_root / 'content/raw/pokefirered/037335f4', args.check)))
