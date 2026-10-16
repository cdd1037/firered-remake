#!/usr/bin/env python3
"""Authenticated local Route3 / Route4 / Mt. Moon approach Center import; no gameplay activation."""
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
PROFILE = 'firered-route3-region-import-v1'
MAPS = [('Route3', 'map.route-3', 'route-3'), ('Route4', 'map.route-4', 'route-4'),
        ('Route4_PokemonCenter_1F', 'map.route-4-pokemon-center-1f', 'route-4-pokemon-center-1f')]
TILESETS = {'gTileset_General': 'primary/general', 'gTileset_Building': 'primary/building',
            'gTileset_PewterCity': 'secondary/pewter_city', 'gTileset_CeruleanCity': 'secondary/cerulean_city',
            'gTileset_PokemonCenter': 'secondary/pokemon_center'}
# Reuse the reviewed renderer, binary/source authentication, warp and NPC decoders unchanged.
# The imported module is a private instance; the Route22 importer and its outputs are not edited.
SPEC = importlib.util.spec_from_file_location('_route3_shared_map_import', ROOT / 'scripts/imports/maps/import-route22-region.py')
SHARED = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SHARED)
canonical, json_bytes = SHARED.canonical, SHARED.json_bytes
require_safe_target = SHARED.require_safe_target
Source = SHARED.Source
import_map, connections_and_warps, npc_inventory = SHARED.import_map, SHARED.connections_and_warps, SHARED.npc_inventory
def materialize(raw, reference_repo):
    return SHARED.materialize(raw, reference_repo, source_package='route3-region')

def read_lock(path=None):
    return read_source_lock(path or ROOT / 'scripts/recipes/maps/route3-region.lock.json', maps=MAPS, profile=PROFILE)

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
    required = {'Route3_EventScript_Youngster', 'Route4_PokemonCenter_1F_OnTransition', 'Route4_PokemonCenter_1F_EventScript_MagikarpSalesman'}
    if not required <= roots:
        raise ValueError('Critical Route3 entry labels are missing')
    missing_roots = sorted(roots - blocks.keys())
    if missing_roots:
        raise ValueError('Missing map event script labels: ' + ', '.join(missing_roots))
    result = script_closure(parsed, roots, runtime_status='sourceOnly')
    return result


def constants_inventory(src, worlds, scripts):
    result = SHARED.constants_inventory(src, worlds, scripts)
    path = 'include/constants/trainers.h'
    match = re.search(r'^#define F_TRAINER_FEMALE \(1 << (\d+)\)$', src.text(path), re.M)
    if match is None: raise ValueError('Trainer gender flag declaration differs')
    result['values']['F_TRAINER_FEMALE'] = dict(value=1 << int(match[1]), sourceRef=src.ref(path, 'F_TRAINER_FEMALE', src.text(path)[:match.start()].count('\n')+1))
    for index, location in enumerate(src.json('src/data/heal_locations.json')['heal_locations'], 1):
        if location['id'] == 'HEAL_LOCATION_ROUTE4':
            result['values'][location['id']] = dict(value=index, sourceRef=src.ref('src/data/heal_locations.json', location['id']))
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
    encounters, species_symbols = [], {'SPECIES_MAGIKARP'}
    for world in worlds:
        rows = [r for r in group['encounters'] if r['map'] == world['events']['id'] and 'FireRed' in r['base_label']]
        if len(rows) != (0 if world['events']['id'] == 'MAP_ROUTE4_POKEMON_CENTER_1F' else 1):
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
                progressionScope='nextRouteCandidate' if row['map']=='MAP_ROUTE3' else 'afterMtMoonOrLaterTraversal'))
    trainers=[]
    charmap={c:int(v,16) for c,v in re.findall(r"^'([A-Z])'\s*=\s*([0-9A-F]{2})$",src.text('charmap.txt'),re.M)}
    for block in scripts['blocks']:
        for line in block['lines']:
            m=re.fullmatch(r'\s*trainerbattle_single (TRAINER_\w+), (\w+), (\w+)\s*',line['text'])
            if not m: continue
            symbol,intro,defeat=m.groups();path='src/data/trainers.h'
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
    if [t['sourceTrainerId'] for t in trainers]!=[89,90,105,106,107,116,117,118,119]:
        raise ValueError('Route3/Route4 trainer roster differs')
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
    heal=next(h for h in src.json('src/data/heal_locations.json')['heal_locations'] if h['id']=='HEAL_LOCATION_ROUTE4')
    objects=[];reference_maps={src.json(p)['id']:(p,src.json(p)) for p in src.data if p.startswith('data/maps/') and p.endswith('/map.json')}
    for w in worlds:
        for i,obj in enumerate(w['events']['object_events']):
            record=dict(map=w['events']['id'],sourceObjectIndex=i,localId=i+1,object=obj,runtimeStatus='sourceOnly')
            if obj['type']=='clone':
                path,target=reference_maps[obj['target_map']]
                ti,to=next((i,o) for i,o in enumerate(target['object_events']) if o.get('local_id')==obj['target_local_id'])
                record['cloneTarget']=dict(map=target['id'],sourceObjectIndex=ti,localId=ti+1,object=to,sourceRef=src.ref(path,obj['target_local_id']))
            elif not 0<=obj['x']<w['width'] or not 0<=obj['y']<w['height']: raise ValueError('Object outside map')
            objects.append(record)
    route4=next(w for w in worlds if w['events']['id']=='MAP_ROUTE4')
    grass=[dict(x=c['x'],y=c['y']) for c in route4['cells'] if c['behaviorId']==2]
    blocks={b['label']:b for b in scripts['blocks']}
    magikarp_labels=[b for label,b in blocks.items() if label.startswith('Route4_PokemonCenter_1F_') and 'Magikarp' in label]
    return dict(schemaVersion=1,completeGame=False,
        trainers=trainers,encounters=encounters,species=species,moves=move_rows,objects=objects,
        healLocations=[dict(table=heal,sourceRef=src.ref('src/data/heal_locations.json',heal['id']),runtimeStatus='sourceOnly')],
        magikarpSale=dict(price=constant('MAGIKARP_PRICE'),species='SPECIES_MAGIKARP',level=5,
            boughtFlag='FLAG_BOUGHT_MAGIKARP',originalBlocks=magikarp_labels,
            dependencies=['checkplayergender','showmoneybox','checkmoney','removemoney','updatemoneybox','hidemoneybox',
                'givemon','getpartysize','subvar','bufferboxname','ChangePokemonNickname','ChangeBoxPokemonNickname',
                'ShouldShowBoxWasFullMessage','GetPCBoxToSendMon'],
            resultDestinations={'0':'party','1':'PC','2':'noRoom'},runtimeStatus='sourceOnly'),
        progressionBoundary=dict(currentDestination='Route4 west Mt. Moon approach and Center 1F',
            externalMaps=['MAP_MT_MOON_1F','MAP_MT_MOON_B1F','MAP_CERULEAN_CITY','MAP_ROUTE4_POKEMON_CENTER_2F'],
            caveEntrances=[dict(x=19,y=5,destination='MAP_MT_MOON_1F'),dict(x=32,y=5,destination='MAP_MT_MOON_B1F')],
            route4LandGrassCells=grass,route4LandGrassMinimumX=min(c['x'] for c in grass),
            deferredRoute4Objects=[o for o in objects if o['map']=='MAP_ROUTE4' and o['object']['x']>=32],
            enabled=False,notes=['Full Route4 geography is retained. Its eastern section, Crissy, tutors, TM05, cave exits, Cerulean connection and aquatic encounters are later progression.',
                'The western Mt. Moon entrance is an explicit unimplemented boundary. No cave/fossil storyline is imported or activated.']),
        supportMatrix=[dict(feature=feature,content='authenticated',runtime='notCertifiedByContentImport') for feature in (
            'source maps and three rendering layers','connections and indexed warps','ordinary NPC dialogue and source motion',
            'Route3 eight original trainers and default-move parties','Route3 FireRed land slots','Route4 all original wild tables',
            'Center healing and registered whiteout','Magikarp sale, party/PC/full storage and nickname branches')],
        unsupported=dict(opcodes=[c['opcode'] for c in scripts['commands'] if not c['opcode'].startswith('.')],
            nativeSpecials=[s['symbol'] for s in scripts['specials']],other=['VS Seeker rematches','Route4 eastern progression','Mt. Moon fossil storyline','Center upper-floor multiplayer']))


def import_catalog(raw, out, lock_path=None):
    raw,out=pathlib.Path(raw),pathlib.Path(out)
    require_safe_target(out,out)
    if out.resolve().is_relative_to(raw.resolve()) or raw.resolve().is_relative_to(out.resolve()): raise ValueError('Input/output trees must be disjoint')
    src=Source(raw,lock_path);out.parent.mkdir(parents=True,exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='.route3-region-stage-',dir=out.parent) as temp:
        stage=pathlib.Path(temp)
        worlds=[import_map(src,name,stable,stage/directory) for name,stable,directory in MAPS]
        connections,warps=connections_and_warps(src,worlds)
        scripts=script_inventory(src,worlds);constants=constants_inventory(src,worlds,scripts)
        npcs=npc_inventory(src,worlds,stage/'npc');dependencies=dependency_inventory(src,worlds,scripts,constants)
        catalog=dict(schemaVersion=1,commit=COMMIT,
            maps=[dict(sourceName=name,stableId=stable,sourceMapSymbol=w['events']['id'],directory=directory) for (name,stable,directory),w in zip(MAPS,worlds)],
            connections=connections,warps=warps)
        for filename,doc in package_documents(generated_root(out), constants, {'route3-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':dependencies}).items():
            (stage/filename).write_text(canonical(doc))
        publish_map_package(stage)
        outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
        manifest=dict(schemaVersion=1,


            outputs=outputs)
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
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/maps/regions/route3')
    parser.add_argument('--reference-root',type=pathlib.Path)
    parser.add_argument('--materialize',action='store_true',help='Copy locked local Git blobs; no network or third-party execution.')
    args=parser.parse_args()
    if args.materialize: materialize(args.raw,args.reference_root or pathlib.Path((ROOT/'.tools/reference-root').read_text().strip()))
    import_catalog(args.raw,args.out)
