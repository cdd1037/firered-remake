#!/usr/bin/env python3
"""Authenticated local S.S. Anne source maps and progression contract; no gameplay activation."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse
import collections
import importlib.util
import json
import pathlib
import re
import shutil
import tempfile
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-ssanne-region-import-v1'
MAP_NAMES = tuple([f'SSAnne_1F_{part}' for part in ('Corridor',*[f'Room{i}' for i in range(1,8)])] +
    [f'SSAnne_2F_{part}' for part in ('Corridor',*[f'Room{i}' for i in range(1,7)])] +
    ['SSAnne_3F_Corridor'] + [f'SSAnne_B1F_{part}' for part in ('Corridor',*[f'Room{i}' for i in range(1,6)])] +
    ['SSAnne_CaptainsOffice','SSAnne_Deck','SSAnne_Exterior','SSAnne_Kitchen'])
def map_directory(name):
    suffix = name.removeprefix('SSAnne_').replace('CaptainsOffice','Captains_Office')
    suffix = re.sub(r'Room(\d+)',r'Room_\1',suffix)
    return 'ssanne-' + suffix.replace('_','-').lower()
MAPS = [(n,'map.'+map_directory(n),map_directory(n)) for n in MAP_NAMES]
TILESETS = {'gTileset_General':'primary/general', 'gTileset_VermilionCity':'secondary/vermilion_city', 'gTileset_SSAnne':'secondary/ss_anne'}
EXTERNAL_ROOTS = ['VermilionCity_MapScripts', 'VermilionCity_EventScript_CheckTicketLeft',
    'VermilionCity_EventScript_CheckTicketRight','VermilionCity_EventScript_ExitedTicketCheck']
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_ssanne_shared_map_import', ROOT / 'scripts/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='ssanne-region')

def read_lock(path=None):
    target = path or ROOT / 'scripts/ssanne-region.lock.json'
    require_safe_target(target.parent, target)
    data = target.read_bytes()
    lock = json_bytes(data)
    if (type(lock.get('schemaVersion')) is not int or lock['schemaVersion'] != 1 or lock.get('repository') != 'pret/pokefirered' or (lock.get('commit') != COMMIT) or (lock.get('profile') != PROFILE) or (lock.get('gameVersion') != 'FIRERED') or (type(lock.get('revision')) is not int) or (lock['revision'] != 0) or (lock.get('completeGame') is not False) or (lock.get('maps') != [x[0] for x in MAPS]) or (not lock.get('files'))):
        raise ValueError('Invalid locked source identity or scope')
    seen = set()
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path']:
            raise ValueError('Unsafe source path')
        if entry['path'] in seen:
            raise ValueError('Duplicate source path')
        seen.add(entry['path'])
        if (not entry['roles']):
            raise ValueError('Malformed source entry')
    # The lock itself is reviewed and fixed; changing source AND its hash does not evade validation.
    return lock

for key in ('ROOT', 'COMMIT', 'TREE',  'PROFILE', 'MAPS', 'TILESETS', 'read_lock'):
    setattr(SHARED, key, globals()[key])

def script_inventory(src, worlds):
    blocks, aliases, preambles, local_macros = {}, {}, {}, []
    script_paths = [p for p, e in src.entries.items() if 'script-label-closure' in e['roles'] and p.endswith(('.inc', '.s'))]
    for path in sorted(script_paths):
        current = None
        macro = None
        preambles[path] = []
        for number, line in enumerate(src.text(path).splitlines(), 1):
            definition = re.fullmatch(r'\s*\.macro\s+(\w+)(.*)', line)
            if definition:
                if macro is not None: raise ValueError('Nested regional macro')
                macro = dict(name=definition[1], arguments=definition[2].strip(), lines=[], sourceRef=src.ref(path, definition[1], number))
                local_macros.append(macro)
                current = None
                continue
            if macro is not None:
                if line.strip() == '.endm': macro = None
                else: macro['lines'].append(dict(line=number, text=line))
                continue
            label = re.fullmatch(r'(\w+)::?', line.split('@')[0].strip())
            if label:
                name = label[1]
                if name in blocks:
                    raise ValueError('Duplicate script label: ' + name)
                current = dict(label=name, sourceRef=src.ref(path, name, number), lines=[])
                blocks[name] = current
            elif current is not None:
                current['lines'].append(dict(line=number, text=line))
            else:
                preambles[path].append(dict(line=number, text=line))
            alias = re.fullmatch(r'\s*\.equ\s+(\w+),\s*(\w+)\s*', line)
            if alias:
                aliases[alias[1]] = alias[2]
    selected_map_prefixes = tuple(f'data/maps/{name}/' for name, _, _ in MAPS)
    roots = {label for label, block in blocks.items() if block['sourceRef']['path'].startswith(selected_map_prefixes)}
    for world in worlds:
        for kind in ('object_events', 'coord_events', 'bg_events'):
            roots.update(e['script'] for e in world['events'][kind] if e.get('script') and e['script'] not in ('0', '0x0', 'NULL'))
    roots.update(['Std_MsgboxNPC', 'Std_MsgboxDefault', 'Std_MsgboxSign', 'Std_MsgboxYesNo',
                  'Std_MsgboxAutoclose', 'Std_FindItem', 'EventScript_HiddenItemScript',
                  'EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach', 'EventScript_PC', 'Std_ReceivedItem', 'EventScript_AfterWhiteOutHeal', 'EventScript_FieldWhiteOut', 'EventScript_FieldPoison'])
    roots.update(EXTERNAL_ROOTS)
    roots.add('EventScript_CutTree')
    required = {'SSAnne_CaptainsOffice_EventScript_Captain','SSAnne_Exterior_ExitSSAnne',
                'SSAnne_2F_Corridor_EventScript_RivalTrigger','SSAnne_1F_Room6_EventScript_Woman'}
    if not required <= roots: raise ValueError('Critical S.S. Anne entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    selected, pending = set(), sorted(roots)
    references, unresolved = [], []
    commands, specials, movements = collections.defaultdict(list), collections.defaultdict(list), collections.defaultdict(list)
    while pending:
        label = pending.pop()
        if label in selected:
            continue
        selected.add(label)
        block = blocks[label]
        for line in block['lines']:
            text = line['text'].split('@')[0].strip()
            if not text or text.startswith(('.string', '.include', '#')):
                continue
            op = text.split()[0]
            location = dict(label=label, path=block['sourceRef']['path'], line=line['line'])
            commands[op].append(location)
            if op in ('special', 'specialvar'):
                specials[text.split(',')[-1].strip().split()[-1]].append(location)
            if label.endswith('Movement') or 'Movement_' in label:
                movements[op].append(location)
            # Target operands are validated independently of naming conventions. A renamed
            # unknown label must not disappear just because it lacks an EventScript prefix.
            operands = text.split(None, 1)[1] if ' ' in text else ''
            target = None
            if op in ('call', 'goto', 'goto_if_questlog', 'message'):
                target = operands.strip()
            elif op in ('map_script', 'map_script_2', 'applymovement') or op.startswith(('goto_if_', 'call_if_')):
                target = operands.split(',')[-1].strip()
            elif op == 'case':
                target = re.split(r'[,\s]+', operands.strip())[-1]
            elif op in ('msgbox', 'giveitem_msg', 'pokemart'):
                target = operands.split(',')[0].strip()
            if op in ('trainerbattle_single', 'trainerbattle_no_intro'):
                for extra in operands.split(',')[1:4] if op == 'trainerbattle_single' else operands.split(',')[1:2]:
                    if extra.strip() not in ('0', 'NULL') and aliases.get(extra.strip(), extra.strip()) not in blocks:
                        unresolved.append(dict(symbol=extra.strip(), **location))
            if target and target not in ('0', '0x0', 'NULL') and aliases.get(target, target) not in blocks:
                unresolved.append(dict(symbol=target, **location))
            for token in re.findall(r'\b[A-Za-z_]\w*\b', text):
                token = aliases.get(token, token)
                if token in blocks:
                    references.append(dict(source=label, target=token, line=line['line']))
                    if token not in selected:
                        pending.append(token)
                elif re.match(r'(?:EventScript_|Common_Movement_|Movement_|Text_|.*_(?:EventScript|Movement|Text)_|CableClub_On).*', token):
                    unresolved.append(dict(symbol=token, **location))
    if unresolved:
        raise ValueError('Unresolved source label dependencies: ' + ', '.join(sorted({x['symbol'] for x in unresolved})))
    return dict(schemaVersion=1, profile=PROFILE, executable=False, roots=sorted(roots),
                blocks=[blocks[x] for x in sorted(selected)], aliases=aliases,
                filePreambles=preambles, localMacros=local_macros, references=references, unresolvedLabelReferences=unresolved,
                commands=[dict(opcode=k, occurrences=v, runtimeStatus='sourceOnly') for k, v in sorted(commands.items())],
                specials=[dict(symbol=k, occurrences=v, runtimeStatus='requiresNamedHandlerAudit') for k, v in sorted(specials.items())],
                movements=[dict(opcode=k, occurrences=v, runtimeStatus='requiresMovementAudit') for k, v in sorted(movements.items())])


def constants_inventory(src, worlds, scripts):
    result = SHARED.constants_inventory(src, worlds, scripts)
    # The post-departure pier retry writes the original port value even before Sevii travel exists.
    # Keep its numeric dependencies source-backed; importing constants does not admit ferry routes.
    expected = {'SEAGALLOP_VERMILION_CITY': 0, 'VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F': 0x4076,
                'VAR_MAP_SCENE_VERMILION_CITY': 0x407e, 'VAR_VERMILION_CITY_TICKET_CHECK_TRIGGER': 0x4053,
                'NPC_TEXT_COLOR_MALE': 0, 'LOCALID_PLAYER': 255, 'LOCALID_VERMILION_FERRY_SAILOR': 6}
    for name, value in expected.items():
        if result['values'].get(name, {}).get('value') != value:
            raise ValueError('Post-departure pier constant differs: ' + name)
    for index, location in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
        result['values'][location['id']] = dict(value=index, sourceRef=src.ref('src/data/heal_locations.json', location['id']))
    return result

def declaration(src, path, symbol, pattern):
    matches = list(re.finditer(pattern, src.text(path), re.M | re.S))
    if len(matches) != 1:
        raise ValueError('Missing/ambiguous declaration: ' + symbol)
    match = matches[0]
    return dict(symbol=symbol, sourceText=match[0], sourceRef=src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1))


def regional_npcs(src, worlds, out):
    # Every source local ID is retained; no event filtering or renumbering.
    return npc_inventory(src, worlds, out)


def dependency_inventory(src, worlds, scripts, constants, connections, warps):
    by_label = {b['label']:b for b in scripts['blocks']}
    def block(label):
        return dict(label=label,sourceRef=by_label[label]['sourceRef'],
                    sourceText='\n'.join(l['text'] for l in by_label[label]['lines']))
    def verify(label, *lines):
        record=block(label)
        if any(line not in record['sourceText'] for line in lines): raise ValueError('Progression contract differs: '+label)
        return record
    trainer_symbols=sorted({token for b in scripts['blocks'] if b['label'].startswith('SSAnne_')
        for line in b['lines'] for token in re.findall(r'\bTRAINER_[A-Z0-9_]+\b',line['text'])})
    trainers=[];species=set();moves=set()
    for symbol in trainer_symbols:
        row=declaration(src,'src/data/trainers.h',symbol,r'^    \['+symbol+r'\] = \{\n.*?^    \},')
        party=re.search(r'\.party = (\w+)\((\w+)\)',row['sourceText'])
        if not party or party[1] not in ('NO_ITEM_DEFAULT_MOVES','NO_ITEM_CUSTOM_MOVES'): raise ValueError('Unsupported trainer party format')
        row.update(sourceTrainerId=constants['values'][symbol]['value'],partyFormat=party[1],runtimeStatus='sourceOnly')
        row['party']=declaration(src,'src/data/trainer_parties.h',party[2],r'^static const struct \w+ '+party[2]+r'\[\] = \{\n.*?^\};')
        row['members']=[]
        for member in re.findall(r'    \{\n(.*?)    \},',row['party']['sourceText'],re.S):
            fields=dict(re.findall(r'\.(\w+) = ([^\n]+),',member))
            if set(fields) != ({'iv','lvl','species','moves'} if party[1]=='NO_ITEM_CUSTOM_MOVES' else {'iv','lvl','species'}): raise ValueError('Unknown trainer member fields')
            sp,level=fields['species'],int(fields['lvl']);species.add(sp)
            pointer=re.search(r'^    \['+sp+r'\] = (\w+),$',src.text('src/data/pokemon/level_up_learnset_pointers.h'),re.M)
            if pointer is None: raise ValueError('Missing learnset pointer')
            learn=declaration(src,'src/data/pokemon/level_up_learnsets.h',pointer[1],r'^static const u16 '+pointer[1]+r'\[\] = \{\n.*?^\};')
            natural=[]
            for lv,mv in re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)',learn['sourceText']):
                if int(lv)<=level and mv not in natural: natural=(natural+[mv])[-4:]
            slots=re.findall(r'MOVE_\w+',fields['moves']) if 'moves' in fields else natural
            if not slots or len(slots)>4: raise ValueError('Invalid move slots')
            moves.update(m for m in slots if m!='MOVE_NONE')
            row['members'].append(dict(species=sp,level=level,sourceIv=int(fields['iv']),heldItem='ITEM_NONE',
                moves=slots,moveSource='custom' if 'moves' in fields else 'levelUpLastFour',learnsetSource=learn['sourceRef']))
        row['species']=sorted({m['species'] for m in row['members']})
        trainers.append(row)
    if len(trainers)!=19 or len([t for t in trainers if t['symbol'].startswith('TRAINER_RIVAL_')])!=3: raise ValueError('SS Anne trainer inventory differs')
    items=set()
    pickups=[]
    for w in worlds:
        for i,e in enumerate(w['events']['bg_events']):
            if e.get('type')=='hidden_item':
                items.add(e['item']);pickups.append(dict(map=w['events']['id'],eventIndex=i,kind='hidden',declaration=e,sourceRef=w['sourceRef']))
        for i,e in enumerate(w['events']['object_events'],1):
            label=e.get('script','');b=by_label.get(label)
            if b:
                for line in b['lines']:
                    found=re.search(r'finditem (ITEM_\w+)(?:, (\d+))?',line['text'])
                    if found:
                        items.add(found[1]);pickups.append(dict(map=w['events']['id'],localId=i,kind='visible',declaration=e,item=found[1],quantity=int(found[2] or '1'),sourceRef=b['sourceRef']))
    items.update(['ITEM_SS_TICKET','ITEM_HM01']);moves.update(['MOVE_CUT','MOVE_BRICK_BREAK','MOVE_REST'])
    itemdata=src.json('src/data/items.json')
    itemrows=itemdata['items'] if isinstance(itemdata,dict) else itemdata
    selected_items=[]
    for symbol in sorted(items):
        matches=[x for x in itemrows if x.get('itemId')==symbol or x.get('item_id')==symbol]
        if len(matches)!=1: raise ValueError('Missing item declaration: '+symbol)
        selected_items.append(dict(symbol=symbol,declaration=matches[0],sourceRef=src.ref('src/data/items.json',symbol)))
    move_records=[declaration(src,'src/data/battle_moves.h',mv,r'^    \['+mv+r'\] =\s*\{\n.*?^    \},') for mv in sorted(moves)]
    mapping=declaration(src,'src/data/party_menu.h','sTMHMMoves',r'^static const u16 sTMHMMoves\[\] =\n\{.*?^\};')
    machine_moves=re.findall(r'\bMOVE_\w+\b',mapping['sourceText'])
    machines=[]
    for item,index,move in [('ITEM_TM31',30,'MOVE_BRICK_BREAK'),('ITEM_TM44',43,'MOVE_REST'),('ITEM_HM01',50,'MOVE_CUT')]:
        if len(machine_moves)!=58 or machine_moves[index]!=move: raise ValueError('Machine source mapping differs')
        machines.append(dict(item=item,move=move,sourceMachineIndex=index,consumable=index<50,sourceRef=mapping['sourceRef']))
    compatibility=[]
    for sp in sorted(species):
        entry=declaration(src,'src/data/pokemon/tmhm_learnsets.h',sp,r'^    \['+sp+r'\]\s*= TMHM_LEARNSET\(.*?\),')
        tokens=re.findall(r'TMHM\((\w+)\)',entry['sourceText'])
        compatibility.append(dict(species=sp,machines=[dict(item=m['item'],canLearn=(m['item'].removeprefix('ITEM_')+'_'+m['move'].removeprefix('MOVE_')) in tokens) for m in machines],sourceRef=entry['sourceRef']))
    species_records=[]
    for sp in sorted(species):
        row=declaration(src,'src/data/pokemon/species_info.h',sp,r'^    \['+sp+r'\] =\s*\{\n.*?^    \},')
        pointer=re.search(r'^    \['+sp+r'\] = (\w+),$',src.text('src/data/pokemon/level_up_learnset_pointers.h'),re.M)[1]
        row['learnset']=declaration(src,'src/data/pokemon/level_up_learnsets.h',pointer,r'^static const u16 '+pointer+r'\[\] = \{\n.*?^\};')
        species_records.append(row)
    ticket=verify('VermilionCity_EventScript_CheckTicket','goto_if_unset FLAG_GOT_SS_TICKET','setvar VAR_VERMILION_CITY_TICKET_CHECK_TRIGGER, 1')
    ticket.update(requiredFlag='FLAG_GOT_SS_TICKET',consumesTicket=False,triggerVariable='VAR_VERMILION_CITY_TICKET_CHECK_TRIGGER',acceptedValue=1,
        entries=[block(x) for x in EXTERNAL_ROOTS[1:]],reject=block('VermilionCity_EventScript_DontHaveSSTicket'),afterDeparture=block('VermilionCity_EventScript_CheckSeagallopPresentTrigger'))
    captain=verify('SSAnne_CaptainsOffice_EventScript_Captain','giveitem_msg SSAnne_CaptainsOffice_Text_ObtainedHM01FromCaptain, ITEM_HM01, 1, MUS_OBTAIN_KEY_ITEM',
        'setflag FLAG_GOT_HM01','setvar VAR_MAP_SCENE_VERMILION_CITY, 1')
    captain.update(item='ITEM_HM01',quantity=1,flag='FLAG_GOT_HM01',sceneVariable='VAR_MAP_SCENE_VERMILION_CITY',sceneValue=1,
        repeat=block('SSAnne_CaptainsOffice_EventScript_AlreadyGotCut'),unusedNoRoom=block('SSAnne_CaptainsOffice_EventScript_NoRoomForCut'))
    healer=verify('SSAnne_1F_Room6_EventScript_Woman','msgbox SSAnne_1F_Room6_Text_TakeAShortRest, MSGBOX_YESNO','call EventScript_OutOfCenterPartyHeal')
    healer.update(map='MAP_SSANNE_1F_ROOM6',healScript=block('EventScript_OutOfCenterPartyHeal'),decline=block('SSAnne_1F_Room6_EventScript_DeclineHeal'),setsRespawn=False)
    rival=verify('SSAnne_2F_Corridor_EventScript_RivalTrigger','setvar VAR_MAP_SCENE_S_S_ANNE_2F_CORRIDOR, 1')
    rival.update(sceneVariable='VAR_MAP_SCENE_S_S_ANNE_2F_CORRIDOR',beforeScene=0,afterScene=1,
        variants=[dict(playerStarterValue=i,script='SSAnne_2F_Corridor_EventScript_Rival'+name,trainer='TRAINER_RIVAL_SS_ANNE_'+name.upper()) for i,name in [(2,'Squirtle'),(1,'Bulbasaur'),(0,'Charmander')]],
        triggers=next(w['events']['coord_events'] for w in worlds if w['events']['id']=='MAP_SSANNE_2F_CORRIDOR'))
    departure=verify('SSAnne_Exterior_ExitSSAnne','special DoSSAnneDepartureCutscene','setvar VAR_MAP_SCENE_VERMILION_CITY, 2','warp MAP_VERMILION_CITY, 23, 34')
    departure.update(onFrame=block('SSAnne_Exterior_OnFrame'),special='DoSSAnneDepartureCutscene',specialSourceRef=src.ref('src/ss_anne.c','DoSSAnneDepartureCutscene',82),
        beforeScene=1,afterShipScene=2,afterCityScene=3,returnMap='MAP_VERMILION_CITY',returnX=23,returnY=34,
        cityOnFrame=block('VermilionCity_OnFrame'),cityCallback=block('VermilionCity_EventScript_ExitSSAnne'),
        movement=block('SSAnne_Exterior_Movement_Exit'),presentationAssets=['graphics/ss_anne/wake.png','graphics/ss_anne/smoke.png'])
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,
        ticket=ticket,captain=captain,healer=healer,rival=rival,departure=departure,
        trainers=trainers,trainerSpecies=sorted(species),species=species_records,moves=move_records,items=selected_items,pickups=pickups,
        machines=machines,machineCompatibility=compatibility,
        ambientSpecies=sorted({sp for b in scripts['blocks'] if b['label'].startswith('SSAnne_') for l in b['lines'] for sp in re.findall(r'playmoncry (SPECIES_\w+)',l['text'])}),
        sourceAudio=sorted(set(re.findall(r'\b(?:MUS|SE)_\w+',src.text('src/ss_anne.c')))|{w['events']['music'] for w in worlds}|{a for b in scripts['blocks'] if b['label'].startswith('SSAnne_') for l in b['lines'] for a in re.findall(r'\b(?:MUS|SE)_\w+',l['text'])}),
        coordinateEvents=[dict(map=w['events']['id'],index=i,declaration=e,sourceRef=w['sourceRef'],enabled=False) for w in worlds for i,e in enumerate(w['events']['coord_events'])],
        externalWarps=[w for w in warps if w['runtimeStatus'].startswith('external')],
        cutBoundary=dict(move='MOVE_CUT',machine='ITEM_HM01',requiredFieldBadge='FLAG_BADGE02_GET',
            sourceScript='EventScript_CutTree',script=verify('EventScript_CutTree','goto_if_unset FLAG_BADGE02_GET','checkpartymove MOVE_CUT'),sourceRef=src.ref('data/scripts/field_moves.inc','EventScript_CutTree'),runtimeIntegrated=False,
            reason='Machine teaching, field badge/party checks, tree removal, and Vermilion Gym/Surge remain separate runtime work'),
        unsupported=dict(opcodes=[x['opcode'] for x in scripts['commands']],specials=[x['symbol'] for x in scripts['specials']],runtimeIntegration=False),
        limitations=['Authenticated source/art only; no handlers, campaign registration, battle execution, GUI or audio certification.',
            'Source-visible ferry/postgame branches remain unadmitted; no substitute ship departure or reward.',
            'Map/NPC images are static source frames; ship subsprites, smoke/wake, horn and departure task timing need presentation integration.',
            'Trainer species/moves/items are compact source contracts; no new battle capability is enabled.'])




def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out);require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()):raise ValueError('Input and output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.ssanne-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp);worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=connections_and_warps(src,worlds);scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=regional_npcs(src,worlds,stage/'npc')
        dependencies=dependency_inventory(src,worlds,scripts,constants,connections,warps)
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps,inputFiles=src.lock['files'])
        for filename,doc in package_documents(out.parent, constants, {'ssanne-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies}).items():
            (stage/filename).write_bytes(canonical(doc).encode('utf-8'))

        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(len(w['cells']) for w in worlds),borderCellCount=sum(len(w['borderCells']) for w in worlds),
            warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),
            trainerCount=len(dependencies['trainers']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
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
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/ssanne-region')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize:materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
