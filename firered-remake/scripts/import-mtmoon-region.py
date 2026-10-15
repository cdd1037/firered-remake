#!/usr/bin/env python3
"""Authenticated local Mt. Moon three-floor import; no gameplay activation."""
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
import os
import pathlib
import re
import shutil
import tempfile
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-mtmoon-region-import-v1'
MAPS = [('MtMoon_1F', 'map.mt-moon-1f', 'mt-moon-1f'), ('MtMoon_B1F', 'map.mt-moon-b1f', 'mt-moon-b1f'),
        ('MtMoon_B2F', 'map.mt-moon-b2f', 'mt-moon-b2f')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Cave': 'secondary/cave'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_mtmoon_shared_map_import', ROOT / 'scripts/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='mtmoon-region')

def read_lock(path=None):
    target = path or ROOT / 'scripts/mtmoon-region.lock.json'
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
                  'EventScript_TryDoNormalTrainerBattle', 'EventScript_DoTrainerBattleFromApproach', 'Std_ReceivedItem', 'EventScript_AfterWhiteOutHeal', 'EventScript_FieldWhiteOut', 'EventScript_FieldPoison'])
    required = {'MtMoon_1F_OnTransition', 'MtMoon_B2F_EventScript_MiguelTrigger', 'MtMoon_B2F_EventScript_DomeFossil', 'MtMoon_B2F_EventScript_HelixFossil'}
    if not required <= roots:
        raise ValueError('Critical Mt. Moon entry labels are missing')
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
                for extra in operands.split(',')[1:4]:
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
    path = 'include/constants/trainers.h'
    match = re.search(r'^#define F_TRAINER_FEMALE \(1 << (\d+)\)$', src.text(path), re.M)
    if match is None: raise ValueError('Trainer gender flag declaration differs')
    result['values']['F_TRAINER_FEMALE'] = dict(value=1 << int(match[1]), sourceRef=src.ref(path, 'F_TRAINER_FEMALE', src.text(path)[:match.start()].count('\n')+1))
    for name, _, _ in MAPS:
        path = 'data/maps/' + name + '/scripts.inc'
        for match in re.finditer(r'^\.equ ([A-Z][A-Z0-9_]*), (\d+)$', src.text(path), re.M):
            result['values'][match[1]] = dict(value=int(match[2]), sourceRef=src.ref(path,match[1],src.text(path)[:match.start()].count('\n')+1))
    return result

def dependency_inventory(src, worlds, scripts, constants):
    """Bounded declarative source extraction. Runtime handlers are certified separately."""
    def one(pattern, path):
        found = list(re.finditer(pattern, src.text(path), re.M | re.S))
        if len(found) != 1:
            raise ValueError('Expected one source definition: ' + path + ':' + pattern)
        return found[0]
    def reference(path, symbol, match):
        return src.ref(path, symbol, src.text(path)[:match.start()].count('\n') + 1)
    def fields(body):
        result = {}
        for line in body.splitlines():
            if not line.strip(): continue
            match = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if match is None or match[1] in result: raise ValueError('Unsupported table field: ' + line)
            result[match[1]] = match[2]
        return result
    def constant(symbol):
        return constants['values'][symbol]['value']
    def flags(expression):
        value = 0
        for token in expression.split('|'): value |= constant(token.strip())
        return value
    encounter_path = 'src/data/wild_encounters.json'
    group = next(g for g in src.json(encounter_path)['wild_encounter_groups'] if g['label'] == 'gWildMonHeaders')
    template = src.text('src/data/wild_encounters.json.txt')
    if 'contains(encounter.base_label, "FireRed")' not in template or '#ifdef FIRERED' not in template:
        raise ValueError('Wild encounter version selection differs')
    rate_fields = {f['type']: f for f in group['fields']}
    encounters, species_symbols = [], set()
    for world in worlds:
        rows = [r for r in group['encounters'] if r['map'] == world['events']['id'] and 'FireRed' in r['base_label']]
        if len(rows) != 1:
            raise ValueError('Regional wild table scope differs')
        for row in rows:
            for kind, table in row.items():
                if kind in ('map', 'base_label'): continue
                if kind not in rate_fields or len(table['mons']) != len(rate_fields[kind]['encounter_rates']):
                    raise ValueError('Unexpected wild encounter slots')
                for mon in table['mons']:
                    if set(mon) != {'min_level','max_level','species'} or not 1 <= mon['min_level'] <= mon['max_level'] <= 100:
                        raise ValueError('Malformed wild slot')
                    species_symbols.add(mon['species'])
            encounters.append(dict(table=row, slotWeights=rate_fields['land_mons']['encounter_rates'],
                encounterFields={k:rate_fields[k] for k in row if k not in ('map','base_label')},
                sourceRef=src.ref(encounter_path,row['base_label']),runtimeStatus='sourceOnly',
                progressionScope='mtMoonSourceCandidate'))
    trainers=[]
    charmap={c:int(v,16) for c,v in re.findall(r"^'([A-Z])'\s*=\s*([0-9A-F]{2})$",src.text('charmap.txt'),re.M)}
    for block in scripts['blocks']:
        for line in block['lines']:
            m=re.fullmatch(r'\s*trainerbattle_(single|no_intro) (TRAINER_\w+), (\w+)(?:, (\w+))?\s*',line['text'])
            if not m: continue
            mode,symbol,first,second=m.groups()
            if mode=='single' and not second: raise ValueError('Trainer intro/defeat pair absent')
            intro,defeat=(first,second) if mode=='single' else ('MtMoon_B2F_Text_MiguelIntro',first)
            path='src/data/trainers.h'
            match=one(r'^    \['+symbol+r'\] = \{\n(.*?)^    \},',path);decl=fields(match[1])
            if set(decl)!={'trainerClass','encounterMusic_gender','trainerPic','trainerName','items','doubleBattle','aiFlags','party'} or decl['items']!='{}' or decl['doubleBattle']!='FALSE':
                raise ValueError('Trainer declaration shape differs')
            party_symbol=re.fullmatch(r'NO_ITEM_DEFAULT_MOVES\((\w+)\)',decl['party'])
            if not party_symbol: raise ValueError('Expected original default-move party')
            pp='src/data/trainer_parties.h';pm=one(r'^static const struct TrainerMonNoItemDefaultMoves '+party_symbol[1]+r'\[\] = \{\n(.*?)^\};',pp)
            members=list(re.finditer(r'    \{\n(.*?)    \},\n',pm[1],re.S))
            if not members or ''.join(e[0] for e in members)!=pm[1]: raise ValueError('Unparsed trainer party')
            party=[]
            for member in members:
                f=fields(member[1])
                if set(f)!={'iv','lvl','species'} or not f['iv'].isdigit() or not f['lvl'].isdigit(): raise ValueError('Unsupported party fields')
                species_symbols.add(f['species'])
                party.append(dict(species=f['species'],sourceSpeciesId=constant(f['species']),level=int(f['lvl']),sourceIv=int(f['iv']),heldItem='ITEM_NONE',customMoves=[],speciesName=one(r'^    \['+f['species']+r'\]\s*= _\("([^"]+)"\),$','src/data/text/species_names.h')[1]))
            name=re.fullmatch(r'_\("([A-Z]+)"\)',decl['trainerName'])[1]
            money=one(r'^    \{'+decl['trainerClass']+r', (\d+)\},$','src/battle_main.c')
            trainers.append(dict(symbol=symbol,sourceTrainerId=constant(symbol),declaration=decl,name=name,
                encodedName=[charmap[c] for c in name],baseMoney=int(money[1]),party=party,
                partySourceRef=reference(pp,party_symbol[1],pm),sourceRef=reference(path,symbol,match),
                sourceTrainerClass=constant(decl['trainerClass']),sourceTrainerPic=constant(decl['trainerPic']),
                encounterMusicGender=flags(decl['encounterMusic_gender']),aiFlags=flags(decl['aiFlags']),partyFlags=0,
                scriptEntry=block['label'],introText=intro,defeatText=defeat,postBattleScript=None,musicOption=None,
                scriptSourceRef=block['sourceRef'],runtimeStatus='sourceOnly'))
    trainers.sort(key=lambda t:t['sourceTrainerId'])
    if [t['sourceTrainerId'] for t in trainers]!=[91,108,109,120,121,169,170,181,351,352,353,354]:
        raise ValueError('Mt. Moon trainer roster differs')
    species=[];moves=set()
    for symbol in sorted(species_symbols):
        path='src/data/pokemon/species_info.h'
        m=one(r'^    \['+symbol+r'\] =\s*\n?    ?\{\n(.*?)^    \},',path)
        pointer=one(r'^    \['+symbol+r'\] = (\w+),$','src/data/pokemon/level_up_learnset_pointers.h')[1]
        lm=one(r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};','src/data/pokemon/level_up_learnsets.h')
        rows=lm[1].splitlines()
        if rows[-1].strip()!='LEVEL_UP_END': raise ValueError('Learnset terminator absent')
        learns=[]
        for line in rows[:-1]:
            row=re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),',line)
            if not row: raise ValueError('Unparsed level-up move')
            learns.append(dict(level=int(row[1]),move=row[2]));moves.add(row[2])
        species.append(dict(symbol=symbol,sourceSpeciesId=constant(symbol),declaration=fields(m[1]),
            sourceRef=reference(path,symbol,m),levelUpMoves=learns,
            learnsetSourceRef=reference('src/data/pokemon/level_up_learnsets.h',pointer,lm),runtimeStatus='sourceOnly'))
    move_rows=[]
    for symbol in sorted(moves):
        path='src/data/battle_moves.h';m=one(r'^    \['+symbol+r'\] =\s*\n?    ?\{\n(.*?)^    \},',path)
        move_rows.append(dict(symbol=symbol,sourceMoveId=constant(symbol),declaration=fields(m[1]),sourceRef=reference(path,symbol,m),runtimeStatus='sourceOnly'))
    blocks={b['label']:b for b in scripts['blocks']}
    objects=[];pickups=[]
    for w in worlds:
        for i,obj in enumerate(w['events']['object_events']):
            if obj['type']!='object' or not 0<=obj['x']<w['width'] or not 0<=obj['y']<w['height']: raise ValueError('Unsupported/outside cave object')
            objects.append(dict(map=w['events']['id'],sourceObjectIndex=i,localId=i+1,object=obj,runtimeStatus='sourceOnly'))
            if obj['graphics_id']=='OBJ_EVENT_GFX_ITEM_BALL':
                block=blocks[obj['script']]
                row=next((re.fullmatch(r'\s*finditem (ITEM_\w+)(?:, (\d+))?\s*',ln['text']) for ln in block['lines'] if ln['text'].strip().startswith('finditem ')),None)
                if not row: raise ValueError('Missing item pickup declaration')
                pickups.append(dict(map=w['events']['id'],kind='visible',item=row[1],quantity=int(row[2] or 1),flag=obj['flag'],x=obj['x'],y=obj['y'],localId=i+1,script=obj['script'],sourceRef=block['sourceRef']))
        for event in w['events']['bg_events']:
            if event['type']=='hidden_item':
                pickups.append(dict(map=w['events']['id'],kind='hidden',item=event['item'],quantity=event['quantity'],flag=event['flag'],x=event['x'],y=event['y'],underfoot=event['underfoot'],sourceRef=w['sourceRef']))
    if len(pickups)!=18 or sum(p['kind']=='hidden' for p in pickups)!=8: raise ValueError('Mt. Moon item inventory differs')
    item_symbols={p['item'] for p in pickups}|{'ITEM_DOME_FOSSIL','ITEM_HELIX_FOSSIL'}
    # The generated item table uses TM aliases; retain both the script identifier and table itemId.
    item_source=src.json('src/data/items.json')['items']
    item_rows=[]
    for symbol in sorted(item_symbols):
        number=constant(symbol)
        row=next((r for r in item_source if r['itemId'] in constants['values'] and constant(r['itemId'])==number),None)
        if row is None: raise ValueError('Missing item table: '+symbol)
        item_rows.append(dict(symbol=symbol,sourceItemId=number,declaration=row,sourceRef=src.ref('src/data/items.json',row['itemId']),runtimeStatus='sourceOnly'))
    fossil_labels=[b for label,b in blocks.items() if label.startswith('MtMoon_B2F_') and any(s in label for s in ('Fossil','Miguel','OnTransition'))]
    return dict(schemaVersion=1,profile=PROFILE,runtimeIntegrated=False,completeGame=False,
        trainers=trainers,encounters=encounters,species=species,moves=move_rows,objects=objects,pickups=pickups,items=item_rows,
        trainerBattleModes=[dict(symbol=t['symbol'],mode='noIntro' if t['symbol']=='TRAINER_SUPER_NERD_MIGUEL' else 'single',
            introPresentation='explicitSourceMessageBeforeBattle' if t['symbol']=='TRAINER_SUPER_NERD_MIGUEL' else 'trainerBattleIntro',
            continuation='MtMoon_B2F_EventScript_BattleMiguel' if t['symbol']=='TRAINER_SUPER_NERD_MIGUEL' else None) for t in trainers],
        fossilChoice=dict(sceneVar='VAR_MAP_SCENE_MT_MOON_B2F',trigger=dict(x=14,y=11,scene=0),trainer='TRAINER_SUPER_NERD_MIGUEL',
            completedFlag='FLAG_GOT_FOSSIL_FROM_MT_MOON',choices=[
                dict(item='ITEM_DOME_FOSSIL',localId=constant('LOCALID_DOME_FOSSIL'),pickedFlag='FLAG_GOT_DOME_FOSSIL',otherLocalId=constant('LOCALID_HELIX_FOSSIL'),movement='MtMoon_B2F_Movement_MiguelToHelixFossil'),
                dict(item='ITEM_HELIX_FOSSIL',localId=constant('LOCALID_HELIX_FOSSIL'),pickedFlag='FLAG_GOT_HELIX_FOSSIL',otherLocalId=constant('LOCALID_DOME_FOSSIL'),movement='MtMoon_B2F_Movement_MiguelToDomeFossil')],
            originalBlocks=fossil_labels,runtimeStatus='sourceOnly',
            sourceSemantics=['Both fossils are shown on transition until FLAG_GOT_FOSSIL_FROM_MT_MOON.',
                'Miguel battle success continues with scene 1; defeat/whiteout semantics require battle-handler verification.',
                'No releases without removing either fossil or setting choice flags.',
                'Yes removes chosen object, runs giveitem_msg, moves Miguel and copies his position permanently, removes the other object, then sets choice/completion flags.',
                'The original giveitem_msg macro calls additem and msgreceiveditem without testing VAR_RESULT; no invented bag-full success guarantee is made.']),
        progressionBoundary=dict(enabled=False,currentDestination='Mt. Moon three floors through fossil choice to east Route4',
            externalMaps=['MAP_ROUTE4'],westReturn=dict(sourceMap='MAP_MT_MOON_1F',sourceWarpId=3,destinationMap='MAP_ROUTE4',destinationWarpId=0,x=19,y=5),
            eastExit=dict(sourceMap='MAP_MT_MOON_B1F',sourceWarpId=7,destinationMap='MAP_ROUTE4',destinationWarpId=1,x=32,y=5),
            excluded=['Cerulean City expansion','Route4 eastern trainer, tutors and TM05 activation','Fossil revival on Cinnabar Island'],
            notes=['Route4 destination JSON authenticates both cave ends; Route4 assets and scripts remain owned by the existing Route3 region.',
                'All three floors and source event declarations are retained; content import does not open cave traversal.']),
        supportMatrix=[dict(feature=feature,content='authenticated',runtime='notCertifiedByContentImport') for feature in (
            'three original floors, borders, collision, elevation and source rendering layers','16 indexed warps including two Route4 boundaries',
            '12 original trainers and default-move parties','three FireRed cave encounter tables','10 visible pickups and eight hidden items',
            'Miguel trigger/dialogue battle entry and post-battle scene','Dome or Helix choice, No response, original object flags and permanent Miguel movement')],
        unsupported=dict(opcodes=[c['opcode'] for c in scripts['commands'] if not c['opcode'].startswith('.')],
            nativeSpecials=[s['symbol'] for s in scripts['specials']],other=['No application or native integration by this importer','Escape Rope field use','Moon Stone evolution','TM09/TM46 teaching','VS Seeker use/rematches','Quest Log playback','Cinnabar fossil restoration','Audio synthesis and real listening']),
        limitations=['Source-only certification; this import registers no application handlers or playable profiles.',
            'Default trainer moves are selected by original level-up rules at battle construction; empty customMoves does not mean no moves.',
            'Original item function pointers are metadata, not executable handlers.',
            'Upstream programs are never executed. Assets remain local/private.'])


def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out)
    require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()): raise ValueError('Input/output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.mtmoon-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp)
        worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=connections_and_warps(src,worlds)
        scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=npc_inventory(src,worlds,stage/'npc');dependencies=dependency_inventory(src,worlds,scripts,constants)
        catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps)
        for filename,doc in package_documents(out.parent, constants, {'mtmoon-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies}).items():
            (stage/filename).write_text(canonical(doc))
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
            inputCount=len(src.entries),mapCount=len(worlds),cellCount=sum(len(w['cells']) for w in worlds),borderCellCount=sum(len(w['borderCells']) for w in worlds),
            warpCount=len(warps),connectionCount=len(connections),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),
            trainerCount=len(dependencies['trainers']),encounterTableCount=len(dependencies['encounters']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
        data=canonical(manifest).encode()
        (stage/'manifest.json').write_bytes(data)
        for p in sorted(stage.rglob('*')): require_safe_target(out,out/p.relative_to(stage))
        for p in sorted(stage.rglob('*')):
            dest=out/p.relative_to(stage)
            if p.is_dir(): dest.mkdir(parents=True,exist_ok=True)
            else: dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,dest)
    print(json.dumps({k:v for k,v in manifest.items() if k!='outputs'},sort_keys=True))
    return manifest

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/mtmoon-region')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize: materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
