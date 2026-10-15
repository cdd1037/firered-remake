#!/usr/bin/env python3
"""Extract the original Trainer Fan Club without admitting new gameplay.

Pinned Git blobs are inert data. Reviewed local codecs render the single room;
the existing Saffron entrance and all nine NPC sheets are authenticated and reused.
"""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse
import copy
import importlib.util
import json
import os
from pathlib import Path
import re
import sys
import tempfile
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'trainer-fan-club'
PROFILE = 'firered-trainer-fan-club-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
MAP_NAME = 'SaffronCity_PokemonTrainerFanClub'
MAPS = [MAP_NAME]
MAP_ID = 'map.saffron-city-pokemon-trainer-fan-club'
TILESETS = {'gTileset_Building': 'primary/building', 'gTileset_FanClubDaycare': 'secondary/fan_club_daycare'}
ANCHOR_PACKAGE = 'content/generated/silph-region'
ANCHOR_PATH = 'saffron-city/runtime-map.json'
NPC_REUSE = {
    'OBJ_EVENT_GFX_CRUSH_GIRL': 'crush-girl',
    'OBJ_EVENT_GFX_LITTLE_GIRL': 'little-girl',
    'OBJ_EVENT_GFX_YOUNGSTER': 'youngster',
    'OBJ_EVENT_GFX_GENTLEMAN': 'gentleman',
    'OBJ_EVENT_GFX_WOMAN_3': 'woman-3',
    'OBJ_EVENT_GFX_ROCKER': 'rocker',
    'OBJ_EVENT_GFX_BEAUTY': 'beauty',
    'OBJ_EVENT_GFX_BLACK_BELT': 'black-belt',
    'OBJ_EVENT_GFX_RED_NORMAL': 'red-normal'}


def canonical(value): return (json.dumps(value, sort_keys=True, indent=2) + '\n').encode()
def need(value, message):
    if not value: raise ValueError(message)
def safe(path): need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))
def module(path, name):
    safe(path)
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def retained_saffron(src, current):
    manifest_path = current / ANCHOR_PACKAGE / 'manifest.json'
    map_path = current / ANCHOR_PACKAGE / ANCHOR_PATH
    safe(manifest_path); safe(map_path)
    manifest, raw = manifest_path.read_bytes(), map_path.read_bytes()
    row = next(x for x in json.loads(manifest)['outputs'] if x['path'] == ANCHOR_PATH)
    original = src.json('data/maps/SaffronCity/map.json')
    need(json.loads(raw)['events'] == original, 'Retained Saffron events differ')
    entrance = original['warp_events'][14]
    need(entrance == dict(x=47, y=21, elevation=0, dest_map='MAP_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB', dest_warp_id='0'), 'Source entrance differs')
    return dict(map='map.saffron-city', sourceMap=original['id'], operation='reuse-existing-source-map',
                currentPackage=ANCHOR_PACKAGE,
                currentResource=row, entrances=[dict(index=14, event=entrance)], geometryChanges=[], eventChanges=[])


def source_contract(src, world, scripts, constants, anchor):
    blocks = {b['label']: b for b in scripts['blocks']}
    def commands(label):
        return [r['text'].split('@')[0].strip() for r in blocks[label]['lines'] if r['text'].split('@')[0].strip()]
    def ref(path, symbol):
        text = src.text(path)
        pattern = (r'^(?:static )?(?:void|u8|u16|bool16|bool8) ' + re.escape(symbol) + r'\([^\n]*\)\n\{'
                   if path.endswith('.c') else r'^' + re.escape(symbol) + r'::')
        match = re.search(pattern, text, re.M)
        need(match is not None, 'Missing source contract symbol: ' + symbol)
        return src.ref(path, symbol, text[:match.start()].count('\n') + 1)
    journal = MAP_NAME + '_EventScript_PokemonJournalBruno'
    need(commands(journal) == ['lock', 'faceplayer', 'famechecker FAMECHECKER_BRUNO, 2',
        'textcolor NPC_TEXT_COLOR_NEUTRAL', 'msgbox PokemonJournal_Text_SpecialFeatureBruno', 'release', 'end'], 'Source Bruno journal differs')
    objects = world['events']['object_events']
    need((world['width'], world['height'], len(world['cells']), len(world['borderBlocks'])) == (11, 17, 187, 4), 'Room dimensions differ')
    need(len(objects) == 10 and not world['events']['coord_events'] and not world['events']['bg_events'], 'Room events differ')
    need(world['events']['warp_events'] == [dict(x=5, y=15, elevation=3, dest_map='MAP_SAFFRON_CITY', dest_warp_id='14')], 'Room exit differs')
    need([(o['x'], o['y'], o['graphics_id'], o['movement_type'], o['script'], o['flag']) for o in objects[8:]] ==
        [(x, 1, '0', 'MOVEMENT_TYPE_INVISIBLE', journal, 'FLAG_HIDE_POSTGAME_GOSSIPERS') for x in [2, 3]], 'Invisible journal objects differ')
    helper = src.text('src/trainer_fan_club.c')
    gain = re.search(r'static u8 PlayerGainRandomTrainerFan\(struct TrainerFanClub \*fanClub\)\n\{(.*?)\n\}', helper, re.S)[1]
    lose = re.search(r'static u8 PlayerLoseRandomTrainerFan\(struct TrainerFanClub \*fanClub\)\n\{(.*?)\n\}', helper, re.S)[1]
    gain_order = re.findall(r'FANCLUB_MEMBER\d', gain.split('};')[0])
    lose_order = re.findall(r'FANCLUB_MEMBER\d', lose.split('};')[0])
    need(gain_order == ['FANCLUB_MEMBER'+str(i) for i in [2,4,6,1,8,7,5,3]] and
         lose_order == ['FANCLUB_MEMBER'+str(i) for i in [6,7,4,8,5,2,1,3]], 'Random scan order differs')
    need('sCounterIncrements[] = {2, 1, 2, 1};' in helper and
         'fanClub->timer + sCounterIncrements[a1] >= 20' in helper and
         'GetNumFansOfPlayerInTrainerFanClub(fanClub) < 3' in helper and
         'GetNumFansOfPlayerInTrainerFanClub(fanClub) < 5' in helper and
         'gSaveBlock2Ptr->playTimeHours < 999' in helper and
         'gSaveBlock2Ptr->playTimeHours - timer < 12' in helper, 'Counter or clock rules differ')
    flags = ['FLAG_HIDE_SAFFRON_FAN_CLUB_' + x for x in ['BLACK_BELT', 'ROCKER', 'WOMAN', 'BEAUTY']]
    need(all('setflag '+flag in src.text('data/event_scripts.s') and 'FlagClear('+flag+');' in helper for flag in flags), 'Initial/first-clear hide flags differ')
    npc_ids = [0, 3, 1, 2, 5, 4, 6, 7]
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, runtimeIntegrated=False,
        mapId=MAP_ID, retainedAnchor=anchor, actors=[dict(localId=i+1, sourceEvent=e,
            fanMember=npc_ids[i] if i < 8 else None) for i,e in enumerate(objects)],
        reachableFameFacts=[dict(map=MAP_ID, sourceObjectLocalIds=[9,10], sourceEntry=journal,
            sourceRef=blocks[journal]['sourceRef'], person='FAMECHECKER_BRUNO',
            personId=constants['values']['FAMECHECKER_BRUNO']['value'], fact=2, hideFlag='FLAG_HIDE_POSTGAME_GOSSIPERS')],
        firstClear=dict(sourceRef=ref('src/trainer_fan_club.c','UpdateTrainerFanClubGameClear'),
            onceGuard='gotInitialFans', addedMemberBits=[0,1,2], clearedHideFlags=flags, scene=1,
            timerSource='gSaveBlock2Ptr->playTimeHours'),
        firstMeeting=dict(entry=MAP_NAME+'_EventScript_MeetFirstFans', finalScene=2,
            approachMovement=blocks[MAP_NAME+'_Movement_FanApproachPlayer'],
            initialFarTableMembers=[3,4,5,6,7]),
        transition=dict(scene2Special='Script_TryLoseFansFromPlayTime',
            nonFanPositions=[dict(member=i, localId=constants['values'][label]['value'], x=x, y=y, movementType=movement)
                for i,label,x,y,movement in [
                    (0,'LOCALID_TRAINER_FAN_CLUB_BATTLE_GIRL',5,2,'MOVEMENT_TYPE_FACE_UP'),
                    (1,'LOCALID_TRAINER_FAN_CLUB_YOUNGSTER',7,4,'MOVEMENT_TYPE_FACE_LEFT'),
                    (2,'LOCALID_TRAINER_FAN_CLUB_GENTLEMAN',2,2,'MOVEMENT_TYPE_FACE_UP'),
                    (3,'LOCALID_TRAINER_FAN_CLUB_LITTLE_GIRL',3,4,'MOVEMENT_TYPE_FACE_RIGHT'),
                    (4,'LOCALID_TRAINER_FAN_CLUB_ROCKER',4,6,'MOVEMENT_TYPE_FACE_UP'),
                    (5,'LOCALID_TRAINER_FAN_CLUB_WOMAN',10,3,'MOVEMENT_TYPE_FACE_UP'),
                    (6,'LOCALID_TRAINER_FAN_CLUB_BEAUTY',7,5,'MOVEMENT_TYPE_FACE_LEFT'),
                    (7,'LOCALID_TRAINER_FAN_CLUB_BLACK_BELT',9,6,'MOVEMENT_TYPE_FACE_LEFT')]]),
        rules=dict(sourceRef=src.ref('src/trainer_fan_club.c','TrainerFanClub'),
            packedVariable='VAR_FANCLUB_FAN_COUNTER', counterMask=127, initialFansMask=128, fanFlagsShift=8,
            loseFanTimerVariable='VAR_FANCLUB_LOSE_FAN_TIMER', sceneVariable='VAR_MAP_SCENE_SAFFRON_CITY_POKEMON_TRAINER_FAN_CLUB',
            counterIncrements=[2,1,2,1], counterThreshold=20, counterFanLimit=3,
            gainScanOrder=gain_order, loseScanOrder=lose_order, choice='first eligible member whose Random() % 2 is one; otherwise last eligible',
            decay=dict(minimumFans=5, intervalHours=12, disabledAtHours=999, signedHourSubtraction=True, resetsTimerBelowFive=True),
            nonLinkNameFallbacks=dict(default='RIVAL', member5='LT. SURGE', member7='KOGA'),
            sourceCallers=[ref('data/maps/PokemonLeague_LancesRoom/scripts.inc','PokemonLeague_LancesRoom_EventScript_DefeatedLance'),
                ref('data/scripts/hall_of_fame.inc','EventScript_SetDefeatedEliteFourFlagsVars')]),
        pendingIntegration=['No current map mounting or save mutation is included.',
            'Existing saves defer first-clear fan updates and contain no authoritative accumulated play-time hours; compatibility needs an explicit rule before gameplay mounting.',
            'Single-player ordinary progression starts with three fans and does not reach the five-fan decay threshold; preserve those source branches anyway.',
            'Link-record name overrides and link-battle gain/loss are outside single-player runtime admission; never synthesize link records.',
            'The exact source expression (NUM_TRAINER_FAN_CLUB_MEMBERS - 1) is authenticated as a constant key; no generic expression evaluator is added.',
            'Fame macro and ordinary story/movement/flag operations reuse existing runtime; no dialogue branch is removed.'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--current-project-root', type=Path)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target, dep, ref, current = [Path(os.path.abspath(x)) for x in
        [args.project_root, args.dependency_root, args.reference_root, args.current_project_root or args.dependency_root]]
    for path in [target, dep, ref, current]: safe(path)
    need(not(target == ref or target.is_relative_to(ref) or ref.is_relative_to(target)), 'Reference/output overlap')
    reader = module(dep / 'scripts/import-cinnabar-source-preparation.py', 'fan_club_reader')
    util = module(dep / 'scripts/import-fossil-lab-region.py', 'fan_club_util')
    renderer = module(dep / 'scripts/school-house-map-renderer.py', 'fan_club_renderer')
    reader.PRODUCT, reader.TILESETS, reader.PROFILE, util.MAPS = dep, TILESETS, PROFILE, MAPS
    src = reader.Source(ref)
    world = reader.decode(src, MAP_NAME); world['stableId'] = MAP_ID
    anchor = retained_saffron(src, current)
    scripts = util.script_inventory(src, reader, [world])
    constants = util.constants_inventory(src, scripts, [world])
    for path in ['include/constants/trainer_fan_club.h', 'include/constants/fame_checker.h']:
        for line,text in enumerate(src.text(path).splitlines(),1):
            match = re.fullmatch(r'#define\s+((?:FANCLUB_MEMBER\d|NUM_TRAINER_FAN_CLUB_MEMBERS|FAMECHECKER_BRUNO))\s+(\d+)', text)
            if match: constants['values'][match[1]] = dict(value=int(match[2]), sourceRef=src.ref(path,match[1],line))
    path = 'include/constants/vars.h'
    for line,text in enumerate(src.text(path).splitlines(),1):
        match = re.fullmatch(r'#define\s+(VAR_FANCLUB_(?:FAN_COUNTER|LOSE_FAN_TIMER))\s+(0x[0-9A-Fa-f]+)', text)
        if match: constants['values'][match[1]] = dict(value=int(match[2],16),sourceRef=src.ref(path,match[1],line))
    need(constants['values']['VAR_FANCLUB_FAN_COUNTER']['value'] == 0x4038 and
         constants['values']['VAR_FANCLUB_LOSE_FAN_TIMER']['value'] == 0x4039, 'Native fan-variable identities differ')
    expression='(NUM_TRAINER_FAN_CLUB_MEMBERS - 1)'
    path='data/maps/'+MAP_NAME+'/scripts.inc'; text=src.text(path)
    need(text.count(expression)==8 and constants['values']['NUM_TRAINER_FAN_CLUB_MEMBERS']['value']==8, 'Source nonfan comparison differs')
    constants['values'][expression]=dict(value=7,sourceRef=src.ref(path,expression,text[:text.index(expression)].count('\n')+1),
        derivation=dict(symbol='NUM_TRAINER_FAN_CLUB_MEMBERS',operation='subtract',literal=1,
                        symbolSourceRef=constants['values']['NUM_TRAINER_FAN_CLUB_MEMBERS']['sourceRef']))
    constants['nonNumericTokens'] = sorted(set(constants['nonNumericTokens']) - constants['values'].keys())
    for path in ['src/trainer_fan_club.c', 'src/strings.c', 'src/new_game.c', 'src/play_time.c',
                 'include/global.h', 'data/scripts/hall_of_fame.inc', 'data/maps/PokemonLeague_LancesRoom/scripts.inc']:
        src.read(path, 'fan-club-rule-reference')
    contract = source_contract(src, world, scripts, constants, anchor)
    shared = reader.load_helper('_regional_service_shared.py'); shared.TILESETS, shared.PROFILE = TILESETS, PROFILE
    converter = reader.load_helper('export-runtime-maps.py')
    target.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='trainer-fan-club-',dir=target) as temp:
        stage=Path(temp); directory=reader.directory(MAP_NAME)
        rendered=renderer.import_map(src,MAP_NAME,MAP_ID,stage/directory,shared)
        # Invisible journal objects are retained verbatim in the map. They need no
        # sheet; graphics_id 0 is the source placeholder, not a visible new NPC.
        visible = copy.deepcopy(world)
        visible['events']['object_events'] = [e for e in world['events']['object_events'] if e['movement_type'] != 'MOVEMENT_TYPE_INVISIBLE']
        npcs = shared.npc_inventory(src,[visible],stage/'npc')
        need({r['graphicsId'] for r in npcs['graphics']} == set(NPC_REUSE), 'NPC source cohort differs')
        for row in npcs['graphics']:
            name=NPC_REUSE[row['graphicsId']]
            relative='content/generated/trainer-tower-region/npc/'+name+'.png'
            existing=current/relative; safe(existing)
            generated=stage/row['texturePath']
            need(generated.read_bytes() == existing.read_bytes(), 'Reused NPC differs')
            row.update(texturePath=relative,reuseExisting=True);generated.unlink()
        (stage/'npc').rmdir()
        contract['graphicsRule']=dict(clearedVramTiles=rendered['clearedVramTiles'],
            invisibleLocalIds=[9,10], reusedNpcGraphicsCount=len(npcs['graphics']))
        rich_path=stage/directory/'runtime-map.json'; rich=rich_path.read_bytes()
        compact=converter.normalize_runtime(json.loads(rich));util.compact_check(compact,world,src)
        data=converter.encode(compact);(stage/directory/'runtime-map.json').write_bytes(data)
        entry=dict(sourceName=MAP_NAME,stableId=MAP_ID,directory=directory,sourceMapSymbol=world['events']['id'],
            path=directory+'/runtime-map.json')
        lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,maps=MAPS,completeGame=False);lb=canonical(lock)
        docs=package_documents(target / 'content/generated', constants, {'catalog.json':dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            runtimeIntegrated=False,maps=[entry],inputFiles=lock['files'],tilesets=TILESETS,
            mapSymbols={world['events']['id']:MAP_ID,'MAP_SAFFRON_CITY':'map.saffron-city'}),
            'scripts.json':scripts,'contracts.json':contract,
            'npc-source.json':npcs,'geometry.json':dict(maps=reader.geometry_inventory([world]),tilesets=TILESETS)}, check=args.check)
        for name,value in docs.items():(stage/name).write_bytes(canonical(value))
        outputs=[dict(path=p.relative_to(stage).as_posix())
            for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,

            inputCount=len(src.entries),mapCount=1,cellCount=187,borderCellCount=4,objectCount=10,warpCount=1,
            reusedNpcGraphicsCount=9,scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=0,outputs=outputs)
        mb=canonical(manifest);(stage/'manifest.json').write_bytes(mb)
        writes=[(target/'scripts/trainer-fan-club.lock.json',lb),(target/'scripts/trainer-fan-club.contract.json',canonical(contract))]
        writes += [(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]
        writes += [(target/'content/generated'/PACKAGE/p.relative_to(stage),p.read_bytes()) for p in sorted(stage.rglob('*')) if p.is_file()]
        for path,data in writes:
            safe(path)
            if path.suffix=='.json':json.loads(data)
            if args.check:need(path.is_file() and path.read_bytes()==data,'Regeneration differs: '+str(path))
        if not args.check:
            for path,data in writes:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
        print(json.dumps(dict(
            inputCount=len(src.entries),outputCount=len(outputs),scriptBlockCount=len(scripts['blocks']),check=args.check),indent=2))


if __name__=='__main__':main()
