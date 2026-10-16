#!/usr/bin/env python3
"""Bounded local checks for v3 map generation; never runs a full resource import."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import ast
import base64
import contextlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
import tempfile

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT / 'scripts/lib'))
sys.path.insert(0, str(ROOT / 'tools/import-regional-service-maps'))
from map_runtime import COMMIT, VERSION, encode, normalize_runtime, publish_map_package, runtime_cells
from layout_variants import import_layout_variant
from generated_output_compare import generated_output_matches
from field_item_services import service_script_graph
from runtime_map_manifest import compose
import import_regional_service_maps as regional


def module(relative):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(path.stem, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def need(value, message):
    if not value:
        raise ValueError(message)


class LocalBytes(dict):
    def __missing__(self, path):
        value = (ROOT / 'content/raw/pokefirered/037335f4' / path).read_bytes()
        self[path] = value
        return value


class Source:
    def __init__(self): self.data = LocalBytes()
    def read(self, path): return self.data[path]
    def text(self, path): return self.read(path).decode()
    def json(self, path): return json.loads(self.read(path))
    def units(self, path, code):
        data = self.read(path)
        return list(struct.unpack('<' + str(len(data) // struct.calcsize(code)) + code, data))
    def ref(self, path, symbol, line=None):
        return dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path, symbol=symbol)
    def scripts(self, name):
        for leaf in ('scripts.inc', 'text.inc'):
            path = f'data/maps/{name}/{leaf}'
            if (ROOT / 'content/raw/pokefirered/037335f4' / path).is_file(): self.read(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    cases = []
    with tempfile.TemporaryDirectory(prefix='map-importer-v3-check-') as temporary:
        temporary = Path(temporary)
        shared = regional.SHARED
        src = shared.Source(ROOT / 'content/raw/pokefirered/037335f4')
        directory = 'celadon-city-game-corner-prize-room'; out = temporary / directory
        world = shared.import_map(src, 'CeladonCity_GameCorner_PrizeRoom', 'map.' + directory, out)
        publish_map_package(temporary)
        data = (temporary / (directory + '.json')).read_bytes(); doc = json.loads(data)
        original = normalize_runtime(json.loads((ROOT / 'content/generated/maps/regions/regional-service-maps' / (directory + '.json')).read_bytes()))
        need(doc == original and doc['events'] == world['events'], 'Ordinary room reconstruction differs')
        need(not (out / 'world.json').exists() and not {'cells', 'borderCells', 'embeddedSource', 'scripts'} & doc.keys(), 'Retained rich map data')
        cases.append(dict(case='shared-prize-room-render', status='passed', outputFiles=len(list(temporary.iterdir())), runtimeBytes=len(data), sourceEqual=True))

        tower = module('scripts/imports/maps/import-trainer-tower-region.py')
        seafoam = module('scripts/imports/story/import-seafoam-resources.py')
        reader = module('scripts/imports/source/import-cinnabar-source-preparation.py')
        src = Source(); name = 'TrainerTower_1F'; events = src.json(f'data/maps/{name}/map.json'); src.scripts(name)
        layout = next(v for v in src.json('data/layouts/layouts.json')['layouts'] if v.get('id') == 'LAYOUT_TRAINER_TOWER_1F_DOUBLES')
        shared.TILESETS = tower.TILESETS
        world = shared.import_map(tower.LayoutSource(src, name, layout), name, 'map.trainer-tower-1f', temporary / 'tower')
        world['events'] = events; variant = tower.compact(world, src)
        need(variant['events'] == events and variant['schemaVersion'] == 3 and 'scripts' not in variant, 'Tower header/format differs')
        need(base64.b64decode(variant['blocks']) == src.read(layout['blockdata_filepath']), 'Tower selected layout differs')
        cases.append(dict(case='tower-doubles-layout', status='passed', originalEventsPreserved=True))
        name = 'SeafoamIslands_B3F'; src.scripts(name)
        shared.TILESETS = reader.TILESETS = seafoam.TILESETS
        base = reader.decode(src, name)
        world, layout_variant = seafoam.alternate_world(src, reader, shared, base, temporary / 'seafoam')
        variant = json.loads((temporary / 'seafoam/runtime-map.json').read_bytes())
        need(variant['events'] == base['events'] and variant['schemaVersion'] == 3, 'Seafoam header/format differs')
        need(base64.b64decode(variant['blocks']) == src.read(world['layout']['blockdata_filepath']), 'Seafoam selected layout differs')
        need(not {'cells', 'embeddedSource', 'scripts'} & variant.keys(), 'Seafoam retained rich data')
        cases.append(dict(case='seafoam-stopped-current-layout', status='passed', originalEventsPreserved=True))

        prep = module('scripts/lib/prepare_first_sevii.py')
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        src = reader.Source(raw_root=raw)
        need(src.reference is None and src.lock()['inputMode'] == 'offline-raw-cache', 'Raw input impersonates a Git checkout')
        name = 'ThreeIsland_DunsparceTunnel'; header_path = f'data/maps/{name}/map.json'
        original_header = src.read(header_path)
        src.read(f'data/maps/{name}/scripts.inc'); src.read(f'data/maps/{name}/text.inc')
        shared.TILESETS = reader.TILESETS = prep.TILESETS
        base = reader.decode(src, name)
        shared.import_map(src, name, base['stableId'], temporary / 'tunnel-base')
        world, layout_variant = import_layout_variant(src, reader, shared, base,
            'LAYOUT_THREE_ISLAND_DUNSPARCE_TUNNEL_DUG_OUT', temporary / 'tunnel-dug-out')
        ordinary = json.loads((temporary / 'tunnel-base/runtime-map.json').read_bytes())
        variant = json.loads((temporary / 'tunnel-dug-out/runtime-map.json').read_bytes())
        need(src.read(header_path) == original_header and src.json(header_path) == base['events'], 'Alternate layout mutated raw source')
        need(ordinary['events'] == variant['events'] == base['events'], 'Tunnel header/warps/actors changed with layout')
        for doc, selected in ((ordinary, base), (variant, world)):
            need(base64.b64decode(doc['blocks'], validate=True) == src.read(selected['layout']['blockdata_filepath']), 'Tunnel map grid differs from source')
            need(base64.b64decode(doc['borderBlocks'], validate=True) == src.read(selected['layout']['border_filepath']), 'Tunnel border differs from source')
            need(doc['schemaVersion'] == 3 and not {'cells', 'embeddedSource', 'scripts'} & doc.keys(), 'Tunnel map is not compact')
        def reachable(doc):
            cells = {(cell['x'], cell['y']): cell for cell in runtime_cells(doc)}
            start = (3, 4); seen = {start}; queue = [start]
            while queue:
                x, y = queue.pop()
                for point in ((x-1,y), (x+1,y), (x,y-1), (x,y+1)):
                    cell = cells.get(point)
                    if point not in seen and cell and cell['collision'] == 0 and cell['elevation'] == 3:
                        seen.add(point); queue.append(point)
            return seen
        need((25, 5) not in reachable(ordinary) and (25, 5) in reachable(variant), 'National Dex layout does not gate east exit')
        hidden = base['events']['bg_events'][0]
        need((hidden['x'], hidden['y'], hidden['item'], hidden['underfoot']) == (21, 3, 'ITEM_NUGGET', False), 'Hidden Nugget source event differs')
        recipe = json.loads((ROOT / 'tools/import-story/recipe.json').read_bytes())
        need(hidden['flag'] in recipe['constantSymbols'], 'Hidden Nugget flag is missing from executable story constants')
        interaction = {(20, 3), (22, 3), (21, 2), (21, 4)}
        need(not interaction & reachable(ordinary) and interaction & reachable(variant), 'Hidden Nugget accessibility differs')
        for bad in ('../map.json', '/tmp/map.json', 'missing-input.bin'):
            try: src.read(bad)
            except (ValueError, FileNotFoundError): pass
            else: raise ValueError('Raw reader accepted unsafe or missing input: ' + bad)
        for symbol in ('LAYOUT_DOES_NOT_EXIST', 'LAYOUT_ONE_ISLAND'):
            try: import_layout_variant(src, reader, shared, base, symbol, temporary / 'invalid-variant')
            except ValueError: pass
            else: raise ValueError('Invalid alternate layout accepted: ' + symbol)
        need(not (temporary / 'invalid-variant').exists(), 'Invalid variant wrote output before validation')
        try: reader.Source(raw, raw_root=raw)
        except ValueError: pass
        else: raise ValueError('Ambiguous Git/raw source mode accepted')
        cases.append(dict(case='tunnel-national-dex-layout', status='passed',
                          originalEventsPreserved=True, sourceUnmodified=True,
                          eastExitOnlyReachableAfterLayoutChange=True, hiddenNuggetGeometryVerified=True))

        inventory = prep.script_index(src, reader)
        services = json.loads((ROOT / 'content/generated/data/regions/first-sevii/services.json').read_bytes())
        need(set(services) == {'serviceBindings'} and len(services['serviceBindings']) == 1, 'Tunnel service contract differs')
        binding = services['serviceBindings'][0]
        root = base['events']['object_events'][0]['script']
        graph = service_script_graph(inventory, root)
        expected = dict(id='first-sevii:' + root, mapId=base['stableId'], scriptId=root,
                        sourceLocalId=1, backgroundX=None, backgroundY=None,
                        eventGroup='object_events', eventIndex=0, sourceEvent=base['events']['object_events'][0],
                        programIds=graph['programIds'], itemSymbols=graph['itemSymbols'])
        need(binding == expected, 'Prospector binding is not its source event/script closure')
        need(graph['itemSymbols'] == ['ITEM_NUGGET'] and
             {'Std_ObtainItem', 'EventScript_NoRoomForItem',
              'ThreeIsland_DunsparceTunnel_EventScript_NoRoomForNugget'} <= set(graph['programIds']),
             'Gift macro/full-bag branches missing from the prospector graph')
        compiled = json.loads((ROOT / 'content/generated/data/story.json').read_bytes())['Compilation']['Programs']
        need(set(graph['programIds']) <= compiled.keys(), 'Gift service depends on an uncompiled source program')
        damaged = dict(inventory, blocks=[block for block in inventory['blocks'] if block['label'] != 'Std_ObtainItem'])
        try: service_script_graph(damaged, root)
        except ValueError: pass
        else: raise ValueError('Incomplete giveitem standard program inventory accepted')
        cases.append(dict(case='prospector-source-gift-service', status='passed',
                          reachablePrograms=len(graph['programIds']), sourceEventUnchanged=True,
                          fullBagBranchesRetained=True, incompleteStandardProgramRejected=True))

        lost = module('scripts/imports/maps/import-lost-cave-region.py')
        lost_prep = module('scripts/lib/prepare_first_sevii.py')
        lost_prep.CORE = lost.MAPS; lost_prep.SUPPORT = []; lost_prep.ANCHOR = []
        lost_inventory = lost_prep.script_index(src, reader)
        existing = json.loads((ROOT / 'content/generated/maps/regions/lost-cave/contracts.json').read_bytes())['serviceBindings'][0]
        home = dict(sourceName='FiveIsland_ResortGorgeous_House', stableId=existing['mapId'],
                    events=src.json('data/maps/FiveIsland_ResortGorgeous_House/map.json'))
        need(lost.selphy_service(src, [home], lost_inventory) == existing, 'Shared service graph changed the Lost Cave binding')
        cases.append(dict(case='lost-cave-shared-service-graph-parity', status='passed',
                          existingBindingUnchanged=True))

        town = module('scripts/imports/presentation/import-first-sevii-town-map.py')
        recipe = json.loads((ROOT / 'scripts/recipes/config/first-sevii-town-map.json').read_bytes())
        inputs = town.load_sources(recipe, raw, None)
        town_outputs = town.convert(inputs)
        need(town_outputs == town.convert(inputs), 'Town-map conversion is not deterministic')
        for relative, expected in town_outputs.items():
            need(generated_output_matches(ROOT / 'content/generated/ui/first-sevii-town-map' / relative, expected),
                 'Town-map output differs: ' + relative)
        positions = json.loads(town_outputs['town-map.json'])['positions']
        tunnel = next(row for row in positions if row['stableMapId'] == base['stableId'])
        need((tunnel['sectionId'], tunnel['region'], tunnel['width'], tunnel['height'], tunnel['override'], tunnel['requiresEscapeWarp']) ==
             ('MAPSEC_THREE_ISLE_PATH', 1, base['width'], base['height'], None, True), 'Tunnel map projection must use its entrance on the Sevii page')
        damaged = dict(inputs)
        damaged['src/region_map.c'] = inputs['src/region_map.c'].replace(b'case MAP_TYPE_UNDERGROUND:', b'case MAP_TYPE_TOWN_CHANGED:')
        try: town.convert(damaged)
        except ValueError: pass
        else: raise ValueError('Changed underground map projection semantics accepted')
        cases.append(dict(case='town-map-tunnel-source-projection', status='passed',
                          sourcePage=1, requiresEntrancePosition=True, deterministic=True))

        borders = module('scripts/imports/maps/import-map-borders.py'); borders.pallet.MAPS = borders.pallet.MAPS[:1]
        raw = ROOT / 'content/raw/pokefirered/037335f4'
        with contextlib.redirect_stdout(io.StringIO()):
            borders.generate(raw, raw, ROOT / 'scripts/recipes/maps/map-borders-source.lock.json', temporary / 'borders')
        border = json.loads((temporary / 'borders/pallet/border.json').read_bytes())
        need(set(border) == {'schemaVersion', 'stableId', 'mapPath', 'sourceRef', 'ruleSourceRef', 'repeatRule', 'artifacts'}, 'Border stores duplicate geometry')
        need(border['schemaVersion'] == 2 and border['mapPath'] == 'maps/regions/opening/pallet.json', 'Border map reference differs')
        need(border == json.loads((ROOT / 'content/generated/maps/borders/pallet/border.json').read_bytes()), 'Border contract changed')
        cases.append(dict(case='pallet-border-reference', status='passed', unchangedAtlasCount=len(border['artifacts'])))

        generated = temporary / 'generated'; generated.mkdir()
        registry = json.loads((ROOT / 'content/generated/maps/metadata.json').read_bytes())
        borders.pallet.write_source_registry(generated, registry['metadata'])
        need(json.loads((generated / 'maps/metadata.json').read_bytes()) == registry, 'Registry reconstruction differs')
        for number in (1, 2):
            directory = generated / f'region/map-{number}'; directory.mkdir(parents=True)
            payload = dict(doc, stableId=f'map.test-{number}')
            (directory / 'runtime-map.json').write_bytes(encode(payload))
        original_maps = {p: p.read_bytes() for p in generated.rglob('runtime-map.json')}
        first, second = generated / 'collection-one.json', generated / 'collection-two.json'
        compose(generated, first, ['region/map-1/runtime-map.json'])
        compose(generated, second, ['region/map-2/runtime-map.json'], previous=first)
        compose(generated, second, ['region/map-2/runtime-map.json'], previous=first, check=True)
        manifest = json.loads(second.read_bytes())
        need(manifest['sourceRegistry'] == {'path': 'maps/metadata.json'} and all(set(e) == {'stableId', 'path'} for e in manifest['maps']), 'Manifest contains copied metadata')
        need(all(p.read_bytes() == b for p, b in original_maps.items()), 'Canonical map duplicated or modified')
        bad = generated / 'region/map-2/runtime-map.json'; legacy = json.loads(bad.read_bytes()); legacy['schemaVersion'] = 1; bad.write_bytes(encode(legacy))
        try: compose(generated, second, ['region/map-2/runtime-map.json'], previous=first)
        except ValueError: pass
        else: raise ValueError('Legacy runtime schema accepted')
        cases.append(dict(case='reference-only-composition-and-registry', status='passed', maps=2, obsoleteCopiesRemoved=True, canonicalMapsUnchanged=True, schema1Rejected=True))
    fly = module('scripts/imports/story/import-route16-fly-house.py')
    source = Source(); retained, anchors = fly.retained_maps(source, ROOT)
    house = dict(stableId='map.route-16-house', events=source.json('data/maps/Route16_House/map.json'))
    contract = fly.north_contract(source, retained, house, anchors)
    existing = json.loads((ROOT / 'content/generated/maps/regions/route16-fly-house/contracts.json').read_bytes())
    for key in ('upperFootAdmissions', 'allowedWarps', 'topology', 'cutTree', 'presetDouble', 'excludedWarps'):
        need(contract[key] == existing[key], 'Retained Route16 contract differs: ' + key)
    for world, anchor in zip(retained, anchors, strict=True):
        compact = json.loads((ROOT / anchor['resourcePath']).read_bytes())
        need('cells' not in compact and len(world['cells']) == world['width'] * world['height'], 'Runtime cell inspection altered stored format')
    cases.append(dict(case='retained-route16-north-contract', status='passed',
                      anchorMaps=len(retained), inspectedCells=sum(len(w['cells']) for w in retained),
                      upperFootCounts=[row['cellCount'] for row in contract['upperFootAdmissions']],
                      sourceContractUnchanged=True, storedMapsRemainCompact=True))
    files = [p for folder in ('scripts', 'tools') for p in (ROOT / folder).rglob('*.py')]
    for path in files: ast.parse(path.read_text(), filename=str(path))
    cases.append(dict(case='python-syntax', status='passed', scripts=len(files)))
    report = dict(status='passed', converterVersion=VERSION, command='python3 scripts/checks/verify-map-importer-unification.py',
                  cases=cases, fullResourceImportRun=False, canonicalGeneratedAssetsModified=False)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True); args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps(report, sort_keys=True))


if __name__ == '__main__': main()
