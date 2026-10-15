#!/usr/bin/env python3
"""Import the pinned original Saffron Copycat house as inert source evidence.

Only reviewed, hash-pinned local map codecs execute. The existing city and actor
textures are authenticated and reused; Mimic service/item admission is separate.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse
import importlib.util
import json
import os
from pathlib import Path
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'copycat-house'
RESOURCE_ID = 'firered-copycat-house-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
MAPS = ['SaffronCity_CopycatsHouse_1F', 'SaffronCity_CopycatsHouse_2F']
DIRECTORIES = ['saffron-city-copycats-house-1f', 'saffron-city-copycats-house-2f']
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_GenericBuilding1': 'secondary/generic_building_1'}
TOWN_PACKAGE = 'content/generated/silph-region'
TOWN_RESOURCE = 'saffron-city/runtime-map.json'
NPC_REUSE = {
    'OBJ_EVENT_GFX_BALDING_MAN': 'content/generated/pewter-region/npc/balding-man.png',
    'OBJ_EVENT_GFX_WOMAN_3': 'content/generated/viridian-region/npc/woman-3.png',
    'OBJ_EVENT_GFX_CHANSEY': 'content/generated/fuchsia-route-region/npc/chansey.png',
    'OBJ_EVENT_GFX_DODUO': 'content/generated/first-sevii-region/npc/doduo.png',
    'OBJ_EVENT_GFX_PIKACHU': 'content/generated/city-services/npc/pikachu.png',
    'OBJ_EVENT_GFX_JIGGLYPUFF': 'content/generated/pewter-region/npc/jigglypuff.png',
    'OBJ_EVENT_GFX_LITTLE_GIRL': 'content/generated/pewter-region/npc/little-girl.png',
    'OBJ_EVENT_GFX_RED_NORMAL': 'content/generated/city-services/npc/red-normal.png'}


def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink path rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def retained_town(src, current):
    manifest_path = current / TOWN_PACKAGE / 'manifest.json'; safe(manifest_path)
    manifest = manifest_path.read_bytes()
    row = next(e for e in json.loads(manifest)['outputs'] if e['path'] == TOWN_RESOURCE)
    path = current / TOWN_PACKAGE / TOWN_RESOURCE; safe(path)
    raw = path.read_bytes()
    original = src.json('data/maps/SaffronCity/map.json')
    need(json.loads(raw)['events'] == original, 'Retained Saffron events differ')
    entrances = [dict(index=i, event=e) for i, e in enumerate(original['warp_events'])
                 if e['dest_map'] == 'MAP_SAFFRON_CITY_COPYCATS_HOUSE_1F']
    need(len(entrances) == 1 and entrances[0]['index'] == 1 and entrances[0]['event']['dest_warp_id'] == '1',
         'Original Saffron entrance differs')
    return dict(map='map.saffron-city', operation='reuse-existing-source-map', currentPackage=TOWN_PACKAGE,
                 currentResource=row,
                entrances=entrances, geometryChanges=[], eventChanges=[])


def event_contract(worlds, scripts, retained, links):
    need([(w['width'], w['height'], len(w['cells']), len(w['borderBlocks'])) for w in worlds] ==
         [(13, 10, 130, 4), (12, 9, 108, 4)], 'Original Copycat house geometry differs')
    need([(len(w['events']['object_events']), len(w['events']['warp_events']), len(w['events']['coord_events']),
           len(w['events']['bg_events'])) for w in worlds] == [(3, 4, 0, 0), (4, 1, 0, 3)], 'Original house events differ')
    bylabel = {b['label']: b for b in scripts['blocks']}
    body = lambda name: '\n'.join(r['text'] for r in bylabel[name]['lines'])
    wrapper = body('SaffronCity_CopycatsHouse_2F_EventScript_Copycat')
    tutor = body('EventScript_MimicTutor')
    need('checkitem ITEM_POKE_DOLL\n\tgoto_if_eq VAR_RESULT, TRUE, EventScript_MimicTutor' in wrapper,
         'Original Doll requirement differs')
    need('setvar VAR_0x8005, MOVETUTOR_MIMIC' in tutor and
         'goto_if_eq VAR_RESULT, 0, EventScript_MimicDeclined\n\tremoveitem ITEM_POKE_DOLL\n\tsetflag FLAG_TUTOR_MIMIC' in tutor,
         'Original successful-learning-only Doll consumption differs')
    return dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT, retainedTown=retained, topology=links,
        maps=[dict(map=w['stableId'], sourceMap=w['events']['id'], width=w['width'], height=w['height'],
                   layout=w['events']['layout'], actors=[dict(localId=i, event=e) for i, e in enumerate(w['events']['object_events'], 1)],
                   warps=w['events']['warp_events'], backgrounds=w['events']['bg_events']) for w in worlds],
        mimic=dict(actorMap=worlds[1]['stableId'], actorLocalId=4, tutorIndex=7, item='ITEM_POKE_DOLL',
                   wrapper='SaffronCity_CopycatsHouse_2F_EventScript_Copycat', script='EventScript_MimicTutor',
                   completionFlag='FLAG_TUTOR_MIMIC', consumeOnlyAfterLearning=True,
                   dependency='Separate item catalog/shop and tutor executor admission required.'),
        limitations=['Original maps and script source closure; current-game composition and service admission are separate.',
                     'Town geometry and events are retained unchanged.',
                     'All actor and player art reuse existing authenticated resources.'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--current-project-root', type=Path)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target, dep, ref, current = [Path(os.path.abspath(p)) for p in
        [args.project_root, args.dependency_root, args.reference_root, args.current_project_root or args.dependency_root]]
    for path in [target, dep, ref, current]: safe(path)
    need(not (target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py', 'copycat_house_reader')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE = dep, TILESETS, RESOURCE_ID
    util = module(dep / 'scripts/import-fossil-lab-region.py', 'copycat_house_utils'); util.MAPS = MAPS
    src = reader.Source(ref)
    worlds = [reader.decode(src, name) for name in MAPS]
    for world, directory in zip(worlds, DIRECTORIES, strict=True): world['stableId'] = 'map.' + directory
    retained = retained_town(src, current)
    scripts = util.script_inventory(src, reader, worlds)
    constants = util.constants_inventory(src, scripts, worlds)
    need(constants['values']['MOVETUTOR_MIMIC']['value'] == 7 and constants['values']['ITEM_POKE_DOLL']['value'] == 80,
         'Original tutor or item ID differs')
    for name in MAPS:
        for leaf in ['scripts.inc', 'text.inc']: src.read(f'data/maps/{name}/' + leaf, 'live-map-script')
    links = reader.topology(src, worlds)
    entrance = retained['entrances'][0]; event = entrance['event']; end = worlds[0]['events']['warp_events'][1]
    links['warps'].append(dict(sourceMap='MAP_SAFFRON_CITY', sourceIndex=entrance['index'], source=event,
        destinationMap=worlds[0]['events']['id'], destinationIndex=1, destination=end, dynamic=False, reciprocalIndex=True))
    contract = event_contract(worlds, scripts, retained, links)
    shared = reader.load_helper('_regional_service_shared.py'); shared.PROFILE, shared.TILESETS = RESOURCE_ID, TILESETS
    converter = reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='copycat-house-', dir=target) as temp:
        stage = Path(temp)
        for name, directory in zip(MAPS, DIRECTORIES, strict=True): shared.import_map(src, name, 'map.' + directory, stage / directory)
        npcs = shared.npc_inventory(src, worlds, stage / 'npc')
        need({r['graphicsId'] for r in npcs['graphics']} == set(NPC_REUSE), 'House actor cohort differs')
        for row in npcs['graphics']:
            path = NPC_REUSE[row['graphicsId']]; existing = current / path; safe(existing)
            rendered = stage / row['texturePath']
            need(rendered.read_bytes() == existing.read_bytes(), 'Reused actor differs')
            row.update(texturePath=path,  reuseExisting=True); rendered.unlink()
        (stage / 'npc').rmdir()
        entries = []
        for world, directory in zip(worlds, DIRECTORIES, strict=True):
            path = stage / directory / 'runtime-map.json'; rich = path.read_bytes()
            compact = converter.normalize_runtime(json.loads(rich)); util.compact_check(compact, world, src)
            data = converter.encode(compact); (stage / directory / 'runtime-map.json').write_bytes(data)
            entries.append(dict(sourceName=world['sourceName'], stableId=world['stableId'], directory=directory,
                sourceMapSymbol=world['events']['id'], path=directory + '/runtime-map.json'))
        lock = src.lock(); lock.update(gameVersion='FIRERED', revision=0, maps=MAPS, completeGame=False)
        lock_bytes = canonical(lock)
        docs = package_documents(target / 'content/generated', constants, {'catalog.json': dict(schemaVersion=1, resourceId=RESOURCE_ID, sourceCommit=COMMIT,
                     maps=entries, inputFiles=lock['files'], tilesets=TILESETS,
                    mapSymbols={**{w['events']['id']: w['stableId'] for w in worlds}, 'MAP_SAFFRON_CITY': 'map.saffron-city'}),
                'scripts.json': scripts,  'contracts.json': contract,
                'npc-source.json': npcs, 'geometry.json': dict(maps=reader.geometry_inventory(worlds), tilesets=TILESETS)}, check=args.check)
        for name, value in docs.items(): (stage / name).write_bytes(canonical(value))
        outputs = [dict(path=p.relative_to(stage).as_posix())
                   for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest = dict(schemaVersion=1, profile=RESOURCE_ID, resourceId=RESOURCE_ID, sourceCommit=COMMIT,
              inputCount=len(src.entries), mapCount=2,
            cellCount=238, borderCellCount=8, directedWarpCount=6, objectCount=7, reusedNpcGraphicsCount=8,
            scriptBlockCount=len(scripts['blocks']), unresolvedLabelCount=0, outputs=outputs)
        manifest_bytes = canonical(manifest); (stage / 'manifest.json').write_bytes(manifest_bytes)
        writes = [(target / 'scripts/copycat-house.lock.json', lock_bytes),
                  (target / 'scripts/copycat-house.contract.json', canonical(contract))]
        writes += [(target / 'content/raw/pokefirered/037335f4' / p, b) for p, b in sorted(src.data.items())]
        writes += [(target / 'content/generated' / PACKAGE / p.relative_to(stage), p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path, data in writes:
            safe(path)
            if path.suffix == '.json': json.loads(data)
            if args.check: need(path.is_file() and path.read_bytes() == data, 'Regeneration differs: ' + str(path))
        if not args.check:
            for path, data in writes: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(data)
        print(json.dumps(dict(
            inputCount=len(src.entries), outputCount=len(outputs), scriptBlockCount=len(scripts['blocks']), check=args.check), indent=2))


if __name__ == '__main__': main()
