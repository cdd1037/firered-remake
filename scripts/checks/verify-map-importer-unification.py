#!/usr/bin/env python3
"""Bounded source, layout and field-service regressions for final world resources."""
import argparse
import base64
import copy
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts/lib'))
from field_item_services import service_script_graph
from field_service_features import north_passage
from generated_output_compare import generated_output_matches
from map_runtime import VERSION, runtime_cells
from map_source import tileset_bindings
from selphy_features import selphy_service
from source_features import feature_worlds, script_blocks
from world_map_renderer import render_map
from world_selection import load_selection
from world_source import WorldSource

RAW = ROOT / 'content/raw/pokefirered/037335f4'
GENERATED = ROOT / 'content/generated'


def module(relative):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(path.stem.replace('-', '_'), path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def need(value, message):
    if not value:
        raise ValueError(message)


def reachable(document):
    cells = {(cell['x'], cell['y']): cell for cell in runtime_cells(document)}
    start = (3, 4); seen = {start}; pending = [start]
    while pending:
        x, y = pending.pop()
        for point in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            cell = cells.get(point)
            if point not in seen and cell and cell['collision'] == 0 and cell['elevation'] == 3:
                seen.add(point); pending.append(point)
    return seen


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    source = WorldSource(RAW)
    selected = load_selection(ROOT / 'data/world-maps.json')
    tilesets = tileset_bindings(source)
    atlas = module('scripts/imports/maps/import-map-atlases.py').AtlasReader(GENERATED / 'maps/atlases')
    cases = []
    with tempfile.TemporaryDirectory(prefix='world-feature-check-') as temporary:
        stage = Path(temporary)

        def render(name, variant=None):
            rows = [r for r in selected if r['sourceName'] == name and r.get('variant') == variant]
            need(len(rows) == 1, 'Expected one explicit world choice: ' + name + '/' + str(variant))
            row = rows[0]
            target = stage / row['path']
            before = (RAW / f'data/maps/{name}/map.json').read_bytes()
            doc = render_map(source, name, row['stableId'], target, tilesets=tilesets,
                             layout_symbol=row.get('layout'), render_border=row.get('renderBorder', True),
                             technical_spawn=row.get('technicalSpawn'))
            need(before == (RAW / f'data/maps/{name}/map.json').read_bytes(), 'Rendering mutated original map header')
            events = json.loads(before)
            need(doc['events'] == events, 'Layout choice changed source actors/warps: ' + name)
            layout = next(r for r in source.json('data/layouts/layouts.json')['layouts']
                          if r.get('id') == row.get('layout', events['layout']))
            need(base64.b64decode(doc['blocks'], validate=True) == source.read(layout['blockdata_filepath']),
                 'Selected layout blocks differ: ' + name)
            need(base64.b64decode(doc['borderBlocks'], validate=True) == source.read(layout['border_filepath']),
                 'Selected layout border differs: ' + name)
            need(doc == json.loads((GENERATED / row['path']).read_bytes()), 'Final map differs: ' + name)
            need(doc['schemaVersion'] == 3 and not {'cells', 'embeddedSource', 'scripts', 'sourceRef'} & doc.keys(),
                 'Map retains source-stage data: ' + name)
            for artifact in doc['artifacts']:
                path = Path(row['path']).parent / artifact['path']
                expected = atlas.read(path.as_posix())
                with Image.open(target.parent / artifact['path']) as actual:
                    need(actual.size == expected.size and actual.convert('RGBA').tobytes() == expected.tobytes(),
                         'Final atlas pixels differ: ' + path.as_posix())
            return doc

        render('CeladonCity_GameCorner_PrizeRoom')
        cases.append(dict(case='ordinary-room-source-and-final-pixels', status='passed'))
        render('TrainerTower_1F', 'double')
        render('SeafoamIslands_B3F', 'current-stopped')
        cases.append(dict(case='battle-tower-and-seafoam-alternate-layouts', status='passed', sourceEventsPreserved=True))

        ordinary = render('ThreeIsland_DunsparceTunnel')
        variant = render('ThreeIsland_DunsparceTunnel', 'dug-out')
        need((25, 5) not in reachable(ordinary) and (25, 5) in reachable(variant),
             'National Dex tunnel layout no longer gates the east exit')
        hidden = ordinary['events']['bg_events'][0]
        need((hidden['x'], hidden['y'], hidden['item'], hidden['underfoot']) == (21, 3, 'ITEM_NUGGET', False),
             'Hidden Nugget source event differs')
        interaction = {(20, 3), (22, 3), (21, 2), (21, 4)}
        need(not interaction & reachable(ordinary) and interaction & reachable(variant), 'Hidden Nugget accessibility differs')
        recipe = json.loads((ROOT / 'tools/import-story/recipe.json').read_bytes())
        need(hidden['flag'] in recipe['constantSymbols'], 'Hidden Nugget flag is absent from executable story constants')
        for symbol in ('LAYOUT_DOES_NOT_EXIST', 'LAYOUT_ONE_ISLAND'):
            output = stage / 'invalid' / (symbol + '.json')
            try:
                render_map(source, 'ThreeIsland_DunsparceTunnel', ordinary['stableId'], output,
                           tilesets=tilesets, layout_symbol=symbol)
            except ValueError:
                pass
            else:
                raise ValueError('Unknown or incompatible alternate layout accepted: ' + symbol)
            need(not output.parent.exists(), 'Invalid alternate layout wrote output')
        cases.append(dict(case='national-dex-tunnel-and-hidden-item-geometry', status='passed', invalidLayoutsRejected=True))

        name = 'ThreeIsland_DunsparceTunnel'
        inventory = script_blocks(source, [name])
        root = ordinary['events']['object_events'][0]['script']
        graph = service_script_graph(inventory, root)
        expected = dict(id='first-sevii:' + root, mapId=ordinary['stableId'], scriptId=root,
            sourceLocalId=1, backgroundX=None, backgroundY=None, eventGroup='object_events', eventIndex=0,
            sourceEvent=ordinary['events']['object_events'][0], programIds=graph['programIds'], itemSymbols=graph['itemSymbols'])
        services = json.loads((GENERATED / 'data/regions/first-sevii/services.json').read_bytes())
        need(services == {'serviceBindings': [expected]}, 'Prospector binding differs from its source event/script closure')
        need(graph['itemSymbols'] == ['ITEM_NUGGET'] and {'Std_ObtainItem', 'EventScript_NoRoomForItem',
             'ThreeIsland_DunsparceTunnel_EventScript_NoRoomForNugget'} <= set(graph['programIds']),
             'Prospector gift/full-bag branches are missing')
        programs = json.loads((GENERATED / 'data/story.json').read_bytes())['Compilation']['Programs']
        need(set(graph['programIds']) <= programs.keys(), 'Gift service depends on an uncompiled program')
        damaged = dict(inventory, blocks=[block for block in inventory['blocks'] if block['label'] != 'Std_ObtainItem'])
        try:
            service_script_graph(damaged, root)
        except ValueError:
            pass
        else:
            raise ValueError('Incomplete gift standard-program inventory accepted')
        cases.append(dict(case='prospector-gift-and-full-bag-closure', status='passed', programs=len(graph['programIds'])))

        name = 'FiveIsland_ResortGorgeous_House'
        worlds = feature_worlds(source, [name])
        actual = selphy_service(source, worlds, script_blocks(source, [name]))
        expected = json.loads((GENERATED / 'maps/regions/lost-cave/contracts.json').read_bytes())['serviceBindings'][0]
        need(actual == expected, 'Selphy request/reward source binding differs')
        cases.append(dict(case='selphy-source-reward-service', status='passed'))

        town = module('scripts/imports/presentation/import-first-sevii-town-map.py')
        inputs = {path: (RAW / path).read_bytes() for path in town.PATHS}
        outputs = town.convert(inputs)
        need(outputs == town.convert(inputs), 'Town-map conversion is not deterministic')
        for path, expected in outputs.items():
            need(generated_output_matches(GENERATED / 'ui/first-sevii-town-map' / path, expected), 'Town-map output differs: ' + path)
        positions = json.loads(outputs['town-map.json'])['positions']
        tunnel = next(row for row in positions if row['stableMapId'] == ordinary['stableId'])
        need((tunnel['sectionId'], tunnel['region'], tunnel['width'], tunnel['height'], tunnel['override'], tunnel['requiresEscapeWarp']) ==
             ('MAPSEC_THREE_ISLE_PATH', 1, ordinary['width'], ordinary['height'], None, True), 'Tunnel town-map projection differs')
        damaged = dict(inputs)
        damaged['src/region_map.c'] = inputs['src/region_map.c'].replace(b'case MAP_TYPE_UNDERGROUND:', b'case MAP_TYPE_TOWN_CHANGED:')
        try:
            town.convert(damaged)
        except ValueError:
            pass
        else:
            raise ValueError('Changed underground town-map projection semantics accepted')
        cases.append(dict(case='town-map-tunnel-projection-and-source-mutation', status='passed'))

        # Borders belong to their compact final map, not a second registry or
        # chronological collection. Source block and atlas-pixel checks are shared.
        render('PalletTown')
        cases.append(dict(case='source-border-blocks-and-final-pixels', status='passed'))

    worlds = feature_worlds(source, ['Route16', 'Route16_NorthEntrance_1F', 'Route16_House'])
    actual = north_passage(worlds[:2], worlds[2])
    expected = json.loads((GENERATED / 'maps/regions/route16-fly-house/passage.json').read_bytes())
    need(actual == expected, 'Route16 upper-passage collision admissions differ')
    damaged = copy.deepcopy(worlds)
    damaged[0]['events']['object_events'][6]['x'] += 1
    try:
        north_passage(damaged[:2], damaged[2])
    except ValueError:
        pass
    else:
        raise ValueError('Moved Cut-tree source actor accepted')
    cases.append(dict(case='route16-cut-and-separated-upper-passage', status='passed', sourceMutationRejected=True))
    report = dict(status='passed', converterVersion=VERSION, cases=cases,
                  fullResourceImportRun=False, canonicalGeneratedAssetsModified=False)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, sort_keys=True))


if __name__ == '__main__':
    main()
