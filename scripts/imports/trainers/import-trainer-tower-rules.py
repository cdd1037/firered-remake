#!/usr/bin/env python3
"""Import the four local Trainer Tower courses from pinned Git blobs, without running upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_source import ItemSource
from item_catalog import inventory_item, item_json
import argparse
import ast
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-trainer-tower-local-v1'
BASE = 'content/generated/data/regions/trainer-tower/'
RAW = 'content/raw/pokefirered/037335f4/'
KINDS = ['SINGLE', 'DOUBLE', 'KNOCKOUT', 'MIXED']
DEPENDENCIES = (
    'content/generated/data/services/metronome-moves.json',
    'content/generated/data/pokemon/canonical/catalog.json',
    'data/game-pokemon.json',
)

def need(value, message):
    if not value: raise ValueError(message)
def ident(symbol): return dict(value='pret:' + symbol)
def stats(values):
    keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    return dict(zip(keys, values), values=values)
def unquote(value):
    return json.loads(re.fullmatch(r'_\(("(?:[^"\\]|\\.)*")\)', value)[1])
def brace_end(text, start):
    need(text[start] == '{', 'Expected opening brace')
    depth = 0
    for i in range(start, len(text)):
        if text[i] == '{': depth += 1
        elif text[i] == '}':
            depth -= 1
            if depth == 0: return i + 1
    raise ValueError('Unclosed C initializer')

def generate(reference, dependency):
    raw = {}
    def read(path):
        if path not in raw:
            b = subprocess.check_output(['git', 'show', COMMIT + ':' + path], cwd=reference)
            raw[path] = b
        return raw[path].decode()
    shared_item = ItemSource(lambda path: read(path).encode()).definition
    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, 'Expected one source match: ' + path + ' ' + pattern)
        return matches[0]
    def ref(path, symbol, start, end):
        return dict(path=path, symbol=symbol, startLine=read(path).count('\n', 0, start) + 1,
                    endLine=read(path).count('\n', 0, end) + 1)
    def matchref(path, symbol, m): return ref(path, symbol, m.start(), m.end())
    def dep(path):
        b = (dependency / path).read_bytes()
        return json.loads(b)
    d = {path: dep(path) for path in DEPENDENCIES}
    canonical = d['content/generated/data/pokemon/canonical/catalog.json']
    species = {s['id']['value'][5:]: s for s in canonical['species']}
    moves = {m['id']['value'][5:]: m for m in d['content/generated/data/services/metronome-moves.json']['moves']}
    admitted = {row['id']['value'][5:] for row in d['data/game-pokemon.json']['trainerTowerOpponents']}

    defs = {}
    for path in ['include/constants/species.h', 'include/constants/moves.h', 'include/constants/items.h',
                 'include/constants/trainers.h', 'include/constants/trainer_tower.h', 'include/constants/easy_chat.h',
                 'include/constants/battle.h', 'include/constants/battle_ai.h', 'include/constants/pokemon.h']:
        for m in re.finditer(r'^#define[ \t]+(\w+)[ \t]+([^\n]+)', read(path), re.M):
            defs[m[1]] = m[2].split('//')[0].strip()
    def number(expr):
        def visit(node):
            if isinstance(node, ast.Constant) and type(node.value) is int: return node.value
            if isinstance(node, ast.Name): return number(defs[node.id])
            if isinstance(node, ast.BinOp):
                a, b = visit(node.left), visit(node.right)
                if isinstance(node.op, ast.LShift): return a << b
                if isinstance(node.op, ast.BitOr): return a | b
                if isinstance(node.op, ast.Add): return a + b
                if isinstance(node.op, ast.Sub): return a - b
            raise ValueError('Unsupported source constant: ' + expr)
        return visit(ast.parse(expr, mode='eval').body)

    def speech(token):
        if token in ['0xFFFF', 'EC_WORD_UNDEFINED']:
            return dict(symbol=token, value=65535, text='')
        macro = re.fullmatch(r'EC_(POKEMON2?|MOVE2?)\((\w+)\)', token)
        if macro:
            group = {'POKEMON': 'POKEMON', 'POKEMON2': 'POKEMON_2', 'MOVE': 'MOVE_1', 'MOVE2': 'MOVE_2'}[macro[1]]
            prefix = 'SPECIES_' if macro[1].startswith('POKEMON') else 'MOVE_'
            symbol = prefix + macro[2]
            path = 'src/data/text/species_names.h' if prefix == 'SPECIES_' else 'src/data/text/move_names.h'
            m = one(path, r'^\s*\[' + symbol + r'\]\s*=\s*_\("([^"]+)"\),?$')
            value = (number('EC_GROUP_' + group) << 9) | number(symbol)
            return dict(symbol=token, value=value, text=m[1])
        expr = defs[token]
        group = re.search(r'EC_GROUP_(\w+)', expr)[1]
        value = number(token)
        path = 'src/data/easy_chat/easy_chat_group_' + group.lower() + '.h'
        word_ids = re.findall(r'\.text = (\w+),', read(path))
        word = word_ids[value & 511]
        m = one(path, r'^static const u8 ' + word + r'\[\] = _\("((?:[^"\\]|\\.)*)"\);$')
        return dict(symbol=token, value=value, text=json.loads('"' + m[1] + '"'))

    path = 'src/trainer_tower_sets.c'; source = read(path)
    floor_matches = list(re.finditer(r'^static const struct TrainerTowerFloor (\w+) = \{\n(.*?)^\};', source, re.M | re.S))
    need(len(floor_matches) == 32, 'Declared floor count')
    pointers = one(path, r'^const struct TrainerTowerFloor \*const gTrainerTowerFloors\[.*?^\};')
    routes = {}
    for kind in KINDS:
        m = re.search(r'\[CHALLENGE_TYPE_' + kind + r'\] = \{(.*?)\}', pointers[0], re.S)
        routes[kind] = re.findall(r'&(sTrainerTowerFloor_\w+)', m[1]); need(len(routes[kind]) == 8, 'Eight local floors')
    referenced = {s for route in routes.values() for s in route}
    header = one(path, r'^const struct EReaderTrainerTowerSetSubstruct gTrainerTowerLocalHeader = \{\n(.*?)^\};')
    need(re.sub(r'\s+', '', header[1]) == '.numFloors=MAX_TRAINER_TOWER_FLOORS,.id=1', 'Exact local set header')
    lookups = 'src/data/pokemon/trainer_class_lookups.h'
    pics_block = one(lookups, r'^const u8 gFacilityClassToPicIndex\[\] =\s*\{.*?^\};')
    classes_block = one(lookups, r'^const u8 gFacilityClassToTrainerClass\[\] =\s*\{.*?^\};')
    pics = dict(re.findall(r'\[(FACILITY_CLASS_\w+)\]\s*=\s*(TRAINER_PIC_\w+)', pics_block[0]))
    classes = dict(re.findall(r'\[(FACILITY_CLASS_\w+)\]\s*=\s*(TRAINER_CLASS_\w+)', classes_block[0]))
    prize_block = one('src/trainer_tower.c', r'^static const u16 sPrizeList\[\] = \{.*?^\};')
    prizes = re.findall(r'ITEM_\w+', prize_block[0]); need(len(prizes) == 15, 'Prize table')
    templates, declared_trainers, declared_mons = [], 0, 0
    for floor in floor_matches:
        symbol, block = floor[1], floor[2]
        header = block[:block.index('.trainers')]
        fields = dict(re.findall(r'\.(\w+) = ([^,\n]+)', header))
        trainers = []
        names = list(re.finditer(r'\.name = _\("([^"]*)"\),', block))
        for ti, name in enumerate(names):
            start = block.rfind('{', 0, name.start()); end = brace_end(block, start)
            trainer_block = block[start:end]
            before_mons = trainer_block[:trainer_block.index('.mons')]
            fields_tr = dict(re.findall(r'\.(\w+) = (.*?)(?:,?\s*)$', before_mons, re.M))
            mons = []
            for mi, match in enumerate(re.finditer(r'\{\s*\.species = (SPECIES_\w+),', trainer_block)):
                mon_end = brace_end(trainer_block, match.start()); mon_block = trainer_block[match.start():mon_end]
                mf = dict(re.findall(r'^\s*\.(\w+) = ([^\n]+)', mon_block, re.M))
                mf = {k: v.split('//')[0].strip().rstrip(',') for k, v in mf.items()}
                expected = {'species','heldItem','moves','otId','abilityNum','personality','nickname','friendship'} | {stat + suffix for stat in ['hp','attack','defense','speed','spAttack','spDefense'] for suffix in ['IV','EV']}
                need(set(mf) == expected, 'Unexpected mon fields: ' + symbol + ' ' + str(set(mf) ^ expected))
                sp = species[mf['species']]; slot = number(mf['abilityNum'])
                ms = re.findall(r'MOVE_\w+', mf['moves']); need(len(ms) == 4, 'Four exact source moves')
                offset = floor.start(2) + start + match.start()
                mons.append(dict(poolIndex=mi, species=ident(mf['species']), sourceSpeciesId=sp['sourceSpeciesId'],
                    heldItem=mf['heldItem'], sourceHeldItemId=number(mf['heldItem']), moves=[ident(m) for m in ms], ppBonuses=0,
                    ivs=stats([number(mf[s+'IV']) for s in ['hp','attack','defense','speed','spAttack','spDefense']]),
                    evs=stats([number(mf[s+'EV']) for s in ['hp','attack','defense','speed','spAttack','spDefense']]),
                    originalTrainerId=number(mf['otId']), personality=number(mf['personality']), abilitySlot=slot,
                    abilityId=sp['ability1' if slot == 0 else 'ability2'], nickname=unquote(mf['nickname']), friendship=number(mf['friendship'])))
            need(len(mons) == 6, 'Six encoded members per trainer')
            facility = fields_tr['facilityClass'].rstrip(','); pic = pics[facility]; cl = classes[facility]
            speeches = {}
            for key in ['speechBefore', 'speechWin', 'speechLose', 'speechAfter']:
                tokens = [s.strip() for s in re.fullmatch(r'\{(.*)\},?', fields_tr[key])[1].split(',')]
                need(len(tokens) == 6, 'Six speech words'); speeches[key] = [speech(t) for t in tokens]
            trainers.append(dict(index=ti, name=name[1], facilityClass=facility, sourceFacilityClass=number(facility),
                trainerClass=cl, sourceTrainerClass=number(cl), trainerPic=pic, sourceTrainerPic=number(pic),
                textColor=number(fields_tr['textColor'].rstrip(',')), **speeches, mons=mons))
        declared_trainers += len(trainers); declared_mons += sum(len(t['mons']) for t in trainers)
        if symbol not in referenced: continue
        prize = number(fields['prize']); fmt = number(fields['challengeType'])
        need(len(trainers) == [1,2,3][fmt], 'Source trainers match floor format')
        templates.append(dict(symbol=symbol, sourceId=number(fields['id']), encodedFloorCount=number(fields['floorIdx']),
            format=fmt, prizeIndex=prize, prizeItem=prizes[prize], trainers=trainers))
    templates.sort(key=lambda x:x['symbol']); by_symbol={t['symbol']:t for t in templates}
    selector_names = ['sSingleBattleChallengeMonIdxs','sDoubleBattleChallengeMonIdxs','sKnockoutChallengeMonIdxs']
    selectors = []
    for symbol in selector_names:
        m=one('src/trainer_tower.c', r'^static const u8 '+symbol+r'\[.*?^\};')
        rows=[[int(n) for n in re.findall(r'\d+', row)] for row in re.findall(r'\{([^{}]*)\}',m[0])]
        need(len(rows)==8, 'Eight selection rows'); selectors.append(rows)
    courses=[]; occurrences=[]
    for ki,kind in enumerate(KINDS):
        floors=[]
        for fi,symbol in enumerate(routes[kind]):
            template=by_symbol[symbol]; fmt=template['format']
            selection=[dict(trainerIndex=0 if fmt==0 else i,poolIndex=n) for i,n in enumerate(selectors[fmt][fi])]
            floors.append(dict(index=fi,templateSymbol=symbol,format=fmt,selection=selection))
            for selection_index,pick in enumerate(selection):
                mon=template['trainers'][pick['trainerIndex']]['mons'][pick['poolIndex']]
                occurrences.append(dict(course=ki,floorIndex=fi,selectionIndex=selection_index,**pick,mon=mon))
        prize=by_symbol[routes[kind][0]]['prizeItem']
        courses.append(dict(kind=ki,prizeItem=prize,sourcePrizeItemId=number(prize),floors=floors))
    used_species={r['mon']['species']['value'][5:] for r in occurrences}
    used_moves={m['value'][5:] for r in occurrences for m in r['mon']['moves']}
    used_items={r['mon']['heldItem'] for r in occurrences}
    need(used_species <= admitted, 'Tower teams must be admitted by the current game')
    need(len(referenced)==28 and declared_trainers==60 and declared_mons==360, 'Declared/referenced floor scope')
    need(sum(len(t['trainers']) for t in templates)==53 and sum(len(t['mons']) for f in templates for t in f['trainers'])==318, 'Referenced trainer pool scope')
    need(len(occurrences)==74 and len(used_species)==63 and len(used_moves)==114 and len(used_items)==23, 'Actual default course coverage')
    need(used_moves <= moves.keys(), 'Selected moves have definitions')
    mismatches=[]; shinies=[]
    for row in occurrences:
        mon=row['mon']; sp=species[mon['species']['value'][5:]]; pid=mon['personality']; ot=mon['originalTrainerId']
        origin={k:v for k,v in row.items() if k!='mon'}
        ordinary_slot=0 if sp['ability2']=='ABILITY_NONE' else pid&1
        if mon['abilitySlot']!=ordinary_slot:mismatches.append(dict(**origin,species=mon['species'],abilitySlot=mon['abilitySlot'],personalitySlot=ordinary_slot))
        shiny=((ot>>16)^(ot&65535)^(pid>>16)^(pid&65535))<8
        if shiny:shinies.append(dict(**origin,species=mon['species']))
        need(mon['abilityId']!='ABILITY_NONE','Selected source slot resolves to an ability')
    need(len(mismatches)==22 and len(shinies)==3, 'Exact fixed-identity exceptions')
    mechanics=[]
    for p,symbol,pattern in [
        ('src/trainer_tower.c','BuildEnemyParty',r'^static void BuildEnemyParty\(void\)\n\{.*?^\}'),
        ('src/trainer_tower.c','GetPartyMaxLevel',r'^static s32 GetPartyMaxLevel\(void\)\n\{.*?^\}'),
        ('src/trainer_tower.c','DoTrainerTowerBattle',r'^static void DoTrainerTowerBattle\(void\)\n\{.*?^\}'),
        ('src/pokemon.c','CreateBattleTowerMon',r'^void CreateBattleTowerMon\([^\n]*\)\n\{.*?^\}'),
        ('src/pokemon.c','CreateMon',r'^void CreateMon\([^\n]*\)\n\{.*?^\}'),
        ('src/pokemon.c','CreateBoxMon',r'^void CreateBoxMon\([^\n]*\)\n\{.*?^\}'),
        ('src/pokemon.c','CalculateMonStats',r'^void CalculateMonStats\([^\n]*\)\n\{.*?^\}'),
        ('src/pokemon.c','GetAbilityBySpecies',r'^u8 GetAbilityBySpecies\([^\n]*\)\n\{.*?^\}')]:
        m=one(p,pattern);mechanics.append(matchref(p,symbol,m))
    pokemon=read('src/pokemon.c')
    need('CreateMon(mon, src->species, src->level, 0, TRUE, src->personality, OT_ID_PRESET, src->otId);' in pokemon,'Source fixed creation call')
    need('value = src->abilityNum;\n    SetMonData(mon, MON_DATA_ABILITY_NUM, &value);' in pokemon,'Explicit ability override')
    need('if (hasFixedPersonality)\n        personality = fixedPersonality;\n    else\n        personality = Random32();' in pokemon,'No random fixed PID')
    need('else if (otIdType == OT_ID_PRESET)' in pokemon and 'if (fixedIV < USE_RANDOM_IVS)' in pokemon and number('USE_RANDOM_IVS')==32,'No random fixed OT/IV')
    for p in ['include/cereader_tool.h','include/trainer_tower.h','include/random.h','src/random.c','src/battle_ai_script_commands.c',
              'data/scripts/trainer_tower.inc','asm/macros/trainer_tower.inc','src/data/items.json','src/easy_chat.c']:
        read(p)
    # All nine lobby stock definitions and three of the four prizes are inherited.
    # Metal Coat is the only inventory addition; held mechanics are already source-authenticated elsewhere.
    item_path='src/data/items.json';source_items=json.loads(read(item_path))['items'];symbol='ITEM_METAL_COAT'
    i=next(i for i in source_items if i['itemId']==symbol)
    m=one(item_path,r'"itemId":\s*"'+symbol+r'"');r=matchref(item_path,symbol,m)
    meta=shared_item(symbol)
    metal=dict(item=meta)|dict(description=i['description_english'].replace('\\n','\n'), healHp=0, cureStatus=0, effectBytes=[], useSupport=10)
    lobby='data/maps/TrainerTower_Lobby/scripts.inc'
    stock=one(lobby,r'^TrainerTower_Lobby_Mart_Items::\n(?:\t.2byte ITEM_\w+\n)+')
    stock_symbols=re.findall(r'ITEM_\w+',stock[0]);need(stock_symbols[-1]=='ITEM_NONE','Terminated lobby stock')
    need(stock_symbols[:-1]==['ITEM_ULTRA_BALL','ITEM_GREAT_BALL','ITEM_FULL_RESTORE','ITEM_MAX_POTION','ITEM_HYPER_POTION',
        'ITEM_REVIVE','ITEM_FULL_HEAL','ITEM_ESCAPE_ROPE','ITEM_MAX_REPEL'],'Original nine lobby items')
    shop=dict(id='TrainerTower_Lobby_Mart_Items',items=[ident(s) for s in stock_symbols[:-1]])
    item_document=dict(additions=[metal],shop=shop,
        prizes=[dict(course=c['kind'],item=ident(c['prizeItem']),sourceItemId=c['sourcePrizeItemId'],quantity=1) for c in courses])
    local=dict(localSetId=1,externalEReaderEnabled=False,courses=courses,templates=templates)
    outputs={'catalog.json':dict(local=local),
        'items.json':item_document}
    return outputs,raw

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('reference',type=Path);p.add_argument('output',type=Path)
    p.add_argument('--dependency-root',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    docs,raw=generate(a.reference,a.dependency_root or a.output)
    files={BASE+n:(json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False, default=item_json)+'\n').encode() for n,d in docs.items()}
    files.update({RAW+n:data for n,data in raw.items()})
    lock=dict(sourceCommit=COMMIT,profile=PROFILE,
        files=[dict(path=p) for p,b in sorted(files.items())])
    for name,data in files.items():
        dest=a.output/name
        if a.check:need(dest.read_bytes()==data,'Regeneration differs: '+name)
        else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
    print(json.dumps(dict(checked=a.check,courses=4,floors=32,usedMembers=74,sourceInputs=len(raw),files=len(files)),indent=2, default=item_json))
if __name__=='__main__':main()
