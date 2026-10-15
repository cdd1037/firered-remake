#!/usr/bin/env python3
"""Authenticate Elyssa's original room and Stick, without changing trade receipts.

Uses reviewed local readers/renderers; pinned upstream Git blobs are inert data.
Shared NPC trade scripts, species, actors and player textures remain inherited.
"""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'npc-trade-elyssa'
RESOURCE_ID = 'firered-npc-trade-elyssa-v1'
SHOP_PROFILE = 'firered-npc-trade-inventory-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAP = 'VermilionCity_House2'
MAP_ID = 'map.vermilion-city-house2'
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding1': 'secondary/generic_building_1'}
CITY_PATH = 'content/generated/vermilion-region/vermilion-city/runtime-map.json'
NPC_REUSE = {'OBJ_EVENT_GFX_LITTLE_GIRL', 'OBJ_EVENT_GFX_RED_NORMAL'}


def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def stick(src, codec, stage):
    path, symbol = 'src/data/items.json', 'ITEM_STICK'
    text = src.text(path)
    row = next(i for i in json.loads(text)['items'] if i['itemId'] == symbol)
    need(row == dict(english='STICK', itemId=symbol, price=200, holdEffect='HOLD_EFFECT_STICK', holdEffectParam=0,
        description_english="A stick of leek to be held by a\\nFARFETCH'D. It raises FARFETCH'D's\\ncritical-hit ratio.",
        importance=0, registrability=0, pocket='POCKET_ITEMS', type='ITEM_TYPE_BAG_MENU',
        fieldUseFunc='FieldUseFunc_OakStopsYou', battleUsage=0, battleUseFunc='NULL', secondaryId=0), 'Original Stick metadata differs')
    match = re.search(r'^    \{\n      "english": "STICK",.*?^    \}', text, re.M | re.S)
    need(match is not None, 'Stick source range missing')
    ref = dict(path=path, symbol=symbol, startLine=text[:match.start()].count('\n')+1,
        endLine=text[:match.end()].count('\n')+1)
    need(re.search(r'^#define ITEM_STICK\s+225\s*$', src.text('include/constants/items.h'), re.M), 'Stick source number differs')
    need(re.search(r'^#define HOLD_EFFECT_STICK\s+66\s*$', src.text('include/constants/hold_effects.h'), re.M), 'Stick held effect number differs')
    identity = dict(value='pret:ITEM_STICK')
    metadata = dict(id=identity, sourceItemSymbol=symbol, sourceItemId=225, name='STICK', price=200,
        importance=0, registrability=0, pocket=row['pocket'], type=row['type'], holdEffect=row['holdEffect'], holdEffectParam=0,
        fieldUseFunction=row['fieldUseFunc'], battleUsage=0, battleUseFunction='NULL', secondaryId=0,
        canToss=True, canSell=True, canGive=True, source=ref)
    item = {k: metadata[k] for k in ('id', 'sourceItemSymbol', 'sourceItemId', 'name', 'price', 'pocket',
        'importance', 'type', 'fieldUseFunction', 'battleUsage', 'battleUseFunction', 'holdEffectParam', 'source')}
    item.update(description=row['description_english'].replace('\\n', '\n'), healHp=0, cureStatus=0,
        effectBytes=[], useSupport=10, effectSource=None, sourceMetadata=metadata)
    binding = re.search(r'^    \[ITEM_STICK\]\s*= \{(\w+), (\w+)\},', src.text('src/data/item_icon_table.h'), re.M)
    need(binding is not None, 'Stick icon binding missing')
    graphics = src.text('src/data/graphics/items.h'); paths = []
    for sym in binding.groups():
        declaration = re.search(r'^const u32 '+sym+r'\[\]\s*= INCBIN_U32\("([^"]+)"\);', graphics, re.M)
        need(declaration is not None, 'Stick icon source declaration missing')
        paths.append(declaration[1].replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal'))
    image, palette = paths
    width, height, indices, _ = codec.indexed_png(src.read(image))
    need((width, height) == (24, 24), 'Stick icon dimensions differ')
    rgba = codec.rgba(indices, codec.palette(src.read(palette))); png = codec.png(width, height, rgba)
    (stage/'item-stick.png').write_bytes(png)
    icon = dict(item=identity, width=24, height=24, texturePath='item-stick.png', sourcePaths=paths, bindingSource=src.ref('src/data/item_icon_table.h', symbol))
    return dict(sourceCommit=COMMIT, profile=SHOP_PROFILE, previousItemCount=125, itemCount=126,
        additions=[item],  shopsAdded=[], icons=[icon])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target, dep, ref = [Path(os.path.abspath(p)) for p in (args.project_root, args.dependency_root, args.reference_root)]
    for p in (target, dep, ref): safe(p)
    need(not(target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Source/output overlap')
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py', 'elyssa_reader')
    helper = module(dep / 'scripts/import-fossil-lab-region.py', 'elyssa_helper')
    codec = module(dep / 'scripts/import-lab-battle-ui.py', 'elyssa_icon_codec')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    helper.NPC_REUSE = NPC_REUSE
    src = reader.Source(ref); world = reader.decode(src, MAP)
    need(world['stableId'] == MAP_ID and (world['width'], world['height']) == (11, 9), 'Preserved trade map identity/shape differs')
    need(world['layout']['id'] == 'LAYOUT_HOUSE1', 'Original room layout differs')
    events = world['events']; actors = events['object_events']
    need(len(actors) == 1 and actors[0]['graphics_id'] == 'OBJ_EVENT_GFX_LITTLE_GIRL' and
        (actors[0]['x'], actors[0]['y'], actors[0]['elevation'], actors[0]['script']) ==
        (4, 4, 3, 'VermilionCity_House2_EventScript_Elyssa'), 'Original Elyssa actor differs')
    need(events['connections'] is None and not events['coord_events'] and not events['bg_events'], 'Original room events differ')
    need(events['warp_events'] == [dict(x=x, y=7, elevation=3, dest_map='MAP_VERMILION_CITY', dest_warp_id='6')
        for x in (3, 4, 5)], 'Original room exits differ')
    city = src.json('data/maps/VermilionCity/map.json')
    city_door = dict(x=19, y=17, elevation=0, dest_map='MAP_VERMILION_CITY_HOUSE2', dest_warp_id='1')
    need(city['warp_events'][6] == city_door, 'Original city entrance differs')
    retained = dep/CITY_PATH; safe(retained); city_bytes = retained.read_bytes()
    need(json.loads(city_bytes)['events'] == city, 'Retained city differs')
    links = reader.topology(src, [world])
    need(len(links['warps']) == 3 and [w['reciprocalIndex'] for w in links['warps']] == [False, True, False], 'Original reciprocal exits differ')
    src.read('data/maps/map_groups.json', 'source-map-number-projection')
    for leaf in ('scripts.inc', 'text.inc'): src.read(f'data/maps/{MAP}/{leaf}', 'embedded-map-source')
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='elyssa-', dir=target) as folder:
        stage = Path(folder); relative = reader.directory(MAP)
        shared.import_map(src, MAP, MAP_ID, stage/relative)
        npcs = helper.reuse_npcs(src, shared, [world], stage, dep)
        item = stick(src, codec, stage)
        rich_path = stage/relative/'runtime-map.json'; rich = rich_path.read_bytes()
        compact = converter.normalize_runtime(json.loads(rich)); helper.compact_check(compact, world, src)
        encoded = converter.encode(compact); (stage/relative/'runtime-map.json').write_bytes(encoded)
        spec = dict(sourceName=MAP, stableId=MAP_ID, directory=relative, sourceMapSymbol=events['id'],
            path=relative+'/runtime-map.json')
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=[MAP], completeGame=False)
        lock_bytes = canonical(lock)
        contracts = dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, tradeId=3, stableMapId=MAP_ID,
            actorLocalId=1, actor=actors[0], mapTopology=links,
            retainedCity=dict(path=CITY_PATH,  warpIndex=6, warp=city_door),
            sourceMapIdCorrection=dict(symbol='MAP_VERMILION_CITY_HOUSE2', previousUnboundId='map.vermilion-city-house-2', stableId=MAP_ID),
            scriptsOwner='npc-trades', changesToTradeDefinitions=[], newShops=[],
            limitations=['Runtime admission and shared NPC trade script enablement are separate.',
                'Existing NPC trade definitions and receipt semantics remain byte-identical.',
                'No species, machine compatibility, shop stock, trade animation or audio addition.'])
        docs = {'catalog.json':dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT,
             maps=[spec], inputFiles=lock['files'], tilesets=TILESETS),
            'contracts.json':contracts, 'npc-source.json':npcs, 'items.json':item,
            'geometry.json':dict(maps=reader.geometry_inventory([world]), tilesets=TILESETS)}
        for name, value in docs.items(): (stage/name).write_bytes(canonical(value))
        outputs = [dict(path=p.relative_to(stage).as_posix())
            for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=RESOURCE_ID, resourceId=RESOURCE_ID, sourceCommit=COMMIT,

            inputCount=len(src.entries), mapCount=1, cellCount=99, borderCellCount=4, objectCount=1,
            itemCount=1, reusedNpcGraphicsCount=2, outputs=outputs)
        manifest_bytes = canonical(manifest); (stage/'manifest.json').write_bytes(manifest_bytes)
        writes = []
        writes += [(target/'content/raw/pokefirered/037335f4'/p, b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: '+str(path))
            else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(
            inputCount=len(src.entries), outputCount=len(outputs), mapCount=1, itemCount=1, check=args.check), indent=2))


if __name__ == '__main__': main()
