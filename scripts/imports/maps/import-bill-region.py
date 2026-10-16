#!/usr/bin/env python3
"""Authenticated local Routes 24/25 and Sea Cottage source import; no gameplay activation."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from map_source import read_source_lock
from map_scripts import parse_scripts, script_closure

from shared_constants import package_documents
from map_runtime import generated_root, publish_map_package
import argparse
import collections
import io
import struct
from PIL import Image
import importlib.util
import json
import os
import pathlib
import re
import shutil
import tempfile
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-bill-region-import-v1'
MAPS = [('Route24', 'map.route-24', 'route-24'), ('Route25', 'map.route-25', 'route-25'),
        ('Route25_SeaCottage', 'map.route-25-sea-cottage', 'route-25-sea-cottage')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
            'gTileset_CeruleanCity': 'secondary/cerulean_city', 'gTileset_SeaCottage': 'secondary/sea_cottage'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_bill_shared_map_import', ROOT / 'scripts/imports/maps/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='bill-region')

def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/bill-region.lock.json', maps=MAPS, profile=PROFILE)

for key in ('ROOT', 'COMMIT', 'TREE',  'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
    setattr(SHARED, key, globals()[key])

def script_inventory(src, worlds):
    parsed = parse_scripts(src)
    blocks, aliases, preambles, local_macros = parsed
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo',
                  'Std_MsgboxAutoclose', 'Std_FindItem', 'EventScript_HiddenItemScript',
                  'EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach', 'EventScript_PC', 'Std_ReceivedItem', 'EventScript_AfterWhiteOutHeal', 'EventScript_FieldWhiteOut', 'EventScript_FieldPoison'])
    required = {'Route24_EventScript_RocketTriggerLeft', 'Route25_SeaCottage_OnTransition', 'Route25_SeaCottage_EventScript_RunCellSeparator'}
    roots.update(['CeruleanCity_OnTransition', 'CeruleanCity_EventScript_RivalTriggerLeft', 'CeruleanCity_EventScript_RivalTriggerMid', 'CeruleanCity_EventScript_RivalTriggerRight'])
    if not required <= roots:
        raise ValueError('Critical Bill entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='sourceOnly')
    return result


def constants_inventory(src, worlds, scripts):
    result = SHARED.constants_inventory(src, worlds, scripts)
    for index, location in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
        result['values'][location['id']] = dict(value=index, sourceRef=src.ref('src/data/heal_locations.json', location['id']))
    return result

def declaration(src, path, symbol, pattern):
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1:
        raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))


def encounter_inventory(src):
    path = 'src/data/wild_encounters.json'
    group = next(g for g in src.json(path)['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    rows = [e for e in group['encounters'] if e['base_label'] in ('sRoute24_FireRed', 'sRoute25_FireRed')]
    if len(rows) != 2 or any(len(e['land_mons']['mons']) != 12 for e in rows):
        raise ValueError('Original FireRed route encounters differ')
    return dict(schemaVersion=1, gameVersion='FIRERED', revision=0,
                fields=group['fields'], maps=[dict(declaration=e, sourceRef=src.ref(path, e['base_label'])) for e in rows],
                landSpecies=sorted({m['species'] for e in rows for m in e['land_mons']['mons']}),
                deferredWaterAndFishingSpecies=sorted({m['species'] for e in rows for k in ('water_mons', 'fishing_mons') for m in e[k]['mons']}),
                waterAndFishingStatus='Original tables retained; Surf and fishing require separate runtime support')


def animation_inventory(src, out):
    """Original teleporter task declarations and RGB atlas, never a recolored approximation."""
    out.mkdir()
    labels_path = 'include/constants/metatile_labels.h'
    labels = dict((name, int(token, 16)) for name, token in re.findall(r'^#define\s+(METATILE_SeaCottage_\w+)\s+(0x[0-9A-Fa-f]+)', src.text(labels_path), re.M))
    if len(labels) != 10:
        raise ValueError('Sea Cottage animation metatile inventory differs')
    bases = ['data/tilesets/primary/building', 'data/tilesets/secondary/sea_cottage']
    sheets = [Image.open(io.BytesIO(src.data[b + '/tiles.png'])) for b in bases]
    metas = [src.units(b + '/metatiles.bin', 'H') for b in bases]
    attrs = [src.units(b + '/metatile_attributes.bin', 'I') for b in bases]
    palettes = [[tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in line.split())
                 for line in src.text(bases[int(i >= 7)] + f'/palettes/{i:02}.pal').splitlines()[3:]] for i in range(13)]
    palettes[0][0] = (0, 0, 0)
    def tile(entry):
        tid, palette = entry & 1023, entry >> 12
        which = int(tid >= 640); local = tid - which * 640; sheet = sheets[which]
        if palette >= 13 or local * 64 >= sheet.width * sheet.height: raise ValueError('Animation tile out of range')
        image = Image.new('RGBA', (8, 8)); pixels = []
        for y in range(8):
            for x in range(8):
                xx, yy = (7-x if entry & 1024 else x), (7-y if entry & 2048 else y)
                index = sheet.getpixel((local % (sheet.width // 8) * 8 + xx, local // (sheet.width // 8) * 8 + yy))
                pixels.append((*palettes[palette][index], 255 if index else 0))
        image.putdata(pixels); return image
    layers = [Image.new('RGBA', (len(labels) * 16, 16)) for _ in range(3)]
    records = []
    for index, (symbol, mid) in enumerate(sorted(labels.items(), key=lambda row: row[1])):
        which = int(mid >= 640); local = mid - which * 640
        attribute = attrs[which][local]; entries = metas[which][local*8:local*8+8]; kind = attribute >> 29 & 3
        groups = {0: [[0x3014]*4, entries[:4], entries[4:]], 1: [entries[:4], entries[4:], [0]*4], 2: [entries[:4], [0]*4, entries[4:]]}.get(kind)
        if groups is None: raise ValueError('Reserved animation layer')
        for layer, group in zip(layers, groups):
            for q, value in enumerate(group): layer.paste(tile(value), (index*16 + q%2*8, q//2*8))
        records.append(dict(symbol=symbol, metatileId=mid, atlasX=index*16, atlasY=0, width=16, height=16,
            rawAttributes=attribute, tileEntries=entries, sourceRef=src.ref(labels_path, symbol),
            metatilesSourceRef=src.ref(bases[which]+'/metatiles.bin', f'metatile_{local:03x}')))
    composed = Image.new('RGBA', layers[0].size, (0,0,0,255))
    for layer in layers: composed = Image.alpha_composite(composed, layer)
    artifacts=[]
    for name, image in zip(['bottom','middle','top','composite'],layers+[composed]):
        filename='teleporter-'+name+'.png';image.save(out/filename,optimize=False,compress_level=9)
        artifacts.append(dict(path='animation/'+filename, width=image.width,height=image.height))
    door_path = 'graphics/door_anims/teleporter.png'
    door = Image.open(io.BytesIO(src.data[door_path]))
    if door.mode != 'P' or door.size != (16,96): raise ValueError('Original teleporter door sheet differs')
    rgba=Image.new('RGBA',door.size);rgba.putdata([(*palettes[8][p],255 if p else 0) for p in door.get_flattened_data()])
    rgba.save(out/'teleporter-door.png',optimize=False,compress_level=9)
    artifacts.append(dict(path='animation/teleporter-door.png',width=16,height=96))
    path='src/special_field_anim.c'
    task_symbols=['AnimateTeleporterHousing','Task_DrawTeleporterHousing','AnimateTeleporterCable','Task_DrawTeleporterCable']
    task_records=[declaration(src,path,s,r'^(?:static )?void '+s+r'\([^\n]*\)\n\{\n.*?^\}') for s in task_symbols]
    door_records=[declaration(src,'src/field_door.c',s,r'^static const struct DoorAnimFrame '+s+r'\[\] = \{\n.*?^\};') for s in ['sDoorAnimFrames_OpenLarge','sDoorAnimFrames_CloseLarge']]
    if 'DOOR_SIZE_1x2, sDoorAnimTiles_Teleporter, sDoorAnimPalettes_Teleporter' not in src.text('src/field_door.c'):
        raise ValueError('Original teleporter door binding differs')
    camera_records=[declaration(src,'src/field_specials.c',s,r'^void '+s+r'\(void\)\n\{\n.*?^\}') for s in ['SpawnCameraObject','RemoveCameraObject']]
    return dict(schemaVersion=1,metatiles=records,artifacts=artifacts,tasks=task_records,
        camera=camera_records,door=dict(sourceRef=src.ref(door_path,'sDoorAnimTiles_Teleporter'),paletteSourceRef=src.ref(bases[1]+'/palettes/08.pal','sDoorAnimPalettes_Teleporter'),
            texturePath='animation/teleporter-door.png',frameWidth=16,frameHeight=32,frameCount=3,frames=door_records),
        sourceTiming=dict(housingStates=13,housingFramesPerState=16,cablePositions=4,cableFramesPerPosition=4,doorFramesPerState=4),
        coordinateContract='Teleporter tasks derive positions from the player destination; source C declarations remain authoritative.')


def dependency_inventory(src, worlds, scripts, constants, connections, warps, encounters):
    trainer_symbols = sorted({token for block in scripts['blocks'] if block['label'].startswith(('Route24_', 'Route25_', 'CeruleanCity_'))
        for line in block['lines'] for token in re.findall(r'\bTRAINER_[A-Z0-9_]+\b', line['text'])})
    trainers=[]
    for symbol in trainer_symbols:
        record=declaration(src,'src/data/trainers.h',symbol,r'^    \['+symbol+r'\] = \{\n.*?^    \},')
        party=re.search(r'\.party = (\w+)\((\w+)\)',record['sourceText'])
        if not party: raise ValueError('Unknown trainer party type')
        record.update(sourceTrainerId=constants['values'][symbol]['value'],partyFormat=party[1],runtimeStatus='sourceOnly')
        record['party']=declaration(src,'src/data/trainer_parties.h',party[2],r'^static const struct \w+ '+party[2]+r'\[\] = \{\n.*?^\};')
        record['species']=sorted(set(re.findall(r'\bSPECIES_[A-Z0-9_]+\b',record['party']['sourceText'])))
        trainers.append(record)
    city=src.json('data/maps/CeruleanCity/map.json');route24=next(w for w in worlds if w['events']['id']=='MAP_ROUTE24')
    north=next(c for c in city['connections'] if c['map']=='MAP_ROUTE24');south=next(c for c in route24['events']['connections'] if c['map']=='MAP_CERULEAN_CITY')
    if north != dict(map='MAP_ROUTE24',offset=12,direction='up') or south != dict(map='MAP_CERULEAN_CITY',offset=-12,direction='down'):
        raise ValueError('Cerulean/Route24 original offsets differ')
    previews=[]
    for name in ['Eevee','Flareon','Jolteon','Vaporeon']:
        path='src/data/graphics/pokemon.h';front='gMonFrontPic_'+name;palette='gMonPalette_'+name
        def graphics(symbol):
            m=re.search(r'\b'+symbol+r'\[\] = INCBIN_U(?:32|16)\("([^"]+)"\)',src.text(path))
            if not m: raise ValueError('Missing Bill preview asset declaration: '+symbol)
            return dict(path=m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'),sourceRef=src.ref(path,symbol))
        previews.append(dict(species='SPECIES_'+name.upper(),sourceSpeciesId=constants['values']['SPECIES_'+name.upper()]['value'],front=graphics(front),palette=graphics(palette),
            runtimeStatus='requiresIndependentPortraitAndSeenDexSupport',battleCatalogRequired=False))
    return dict(schemaVersion=1,completeGame=False,trainers=trainers,
        trainerSpecies=sorted({s for t in trainers for s in t['species']}),encounterSpecies=encounters['landSpecies'],
        ceruleanNorthConnection=dict(sourceEdgeY=0,destinationEdgeY=route24['height']-1,sourceXToDestinationX=-12,
            sourceConnection=north,reverseConnection=south,sourceRef=src.ref('data/maps/CeruleanCity/map.json','MAP_CERULEAN_CITY'),enabled=False),
        coordinateEvents=[dict(map=w['events']['id'],index=i,declaration=e,sourceRef=w['sourceRef'],enabled=False) for w in worlds for i,e in enumerate(w['events']['coord_events'])],
        exteriorBoundaries=[c for c in connections if c['runtimeStatus']=='externalBoundary'],externalWarps=[w for w in warps if w['runtimeStatus'].startswith('external')],
        billPcPreviews=previews,progressionDependencies=[
            dict(feature='north rival',sourceScript='CeruleanCity_EventScript_Rival',requires='Three starter battles, Fame Checker reward and original actor/scene persistence'),
            dict(feature='Nugget Bridge',sourceScript='Route24_EventScript_BattleRocket',requires='Nugget capacity retry, award before battle, then scene 1; original loss may award again on retry'),
            dict(feature='route trainers',requires='16 new initial trainer battles and source approach ranges; Vs Seeker rematches remain separately gated'),
            dict(feature='Bill help',sourceScript='Route25_SeaCottage_EventScript_BillAskForHelp',requires='Gendered Yes/No text; No returns and continues help, all four player facings, Clefairy movement and door'),
            dict(feature='cell separator',sourceScript='Route25_SeaCottage_EventScript_RunCellSeparator',requires='Temporary flag, camera, source housing/cable/door animation, human actor swap and persistent helped flag'),
            dict(feature='SS Ticket',sourceScript='Route25_SeaCottage_EventScript_BillGiveSSTicket',requires='Key item capacity retry, duplicate/ticket flags, hide bridge Rocket, Bill PC naming and city blocker relocation on next city transition'),
            dict(feature='Bill PC',sourceScript='Route25_SeaCottage_EventScript_OpenBillsMonList',requires='Reenter cottage after ticket; four portrait/seen-Dex choices and cancel without requiring battle species'),
            dict(feature='later travel',requires='Route5/9 and SS Anne remain external to this data increment')],
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False))


def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.bill-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=connections_and_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=npc_inventory(src,worlds,stage/'npc');encounters=encounter_inventory(src);animations=animation_inventory(src,stage/'animation')
        dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps,encounters)
        catalog=dict(schemaVersion=1,commit=COMMIT,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps)
        for filename,doc in package_documents(generated_root(out), constants, {'bill-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies,'encounters.json':encounters,'animations.json':animations}).items():
            (stage/filename).write_bytes(canonical(doc).encode('utf-8'))

        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,


            outputs=outputs)
        data=canonical(manifest).encode('utf-8')
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')):require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir():dest.mkdir(parents=True,exist_ok=True)
            else:dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True));return manifest


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/bill')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
