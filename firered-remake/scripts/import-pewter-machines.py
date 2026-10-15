#!/usr/bin/env python3
"""Read only authenticated local Git blobs/cache for Brock's TM39. Never run upstream code."""
import argparse
import base64
import importlib.util
import sys
import json
import os
import pathlib
import re
import subprocess

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
import sys
sys.path.insert(0, str(ROOT / "scripts"))
from machine_catalog import load_catalog, machine_reference, compatibility_reference
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-pewter-machines-v1'
SPECIES = ('BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE '
           'CATERPIE METAPOD BUTTERFREE WEEDLE KAKUNA BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE '
           'PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX').split()


def encoded(value):
    return (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode()

def pairs(values):
    result = {}
    for k, v in values:
        if k in result:
            raise ValueError('Duplicate JSON member: ' + k)
        result[k] = v
    return result

def safe(path):
    path = pathlib.Path(path).absolute()
    for p in [path, *path.parents]:
        if p.is_symlink():
            raise ValueError('Symlink path rejected: ' + str(p))
    return path

def directory(path):
    path = safe(path)
    for p in [path, *path.parents]:
        if p.exists() and not p.is_dir():
            raise ValueError('Non-directory ancestor: ' + str(p))
    return path

def read_lock():
    data = safe(ROOT / 'scripts/pewter-machines.lock.json').read_bytes()
    lock = json.loads(data, object_pairs_hook=pairs)
    if lock['commit'] != COMMIT or lock['profile'] != PROFILE or len(lock['files']) != 23:
        raise ValueError('Incomplete or incorrect source identity')
    return lock


def build(lock, data):
    texts = {p: b.decode('utf-8') for p, b in data.items() if not p.endswith('.png')}
    inputs = {e['path']: e for e in lock['files']}
    rules = []
    def match(path, pattern):
        values = list(re.finditer(pattern, texts[path], re.M | re.S))
        if len(values) != 1:
            raise ValueError('Non-unique source pattern: ' + path + ':' + pattern)
        return values[0]
    def ref(path, symbol, m):
        return dict(path=path, symbol=symbol, startLine=texts[path][:m.start()].count('\n') + 1,
                    endLine=texts[path][:m.end()].count('\n') + 1)
    def constant(path, symbol):
        m = match(path, r'^#define ' + re.escape(symbol) + r'[ \t]+(\d+)[ \t]*$')
        rules.append(ref(path, symbol, m))
        return int(m.group(1))
    def block(path, symbol, pattern):
        r = ref(path, symbol, match(path, pattern)); rules.append(r); return r
    ids_path = 'include/constants/items.h'
    source_tm = constant(ids_path, 'ITEM_TM39')
    source_case = constant(ids_path, 'ITEM_TM_CASE')
    first_tm = constant(ids_path, 'ITEM_TM01')
    slots = constant('include/constants/global.h', 'BAG_TMHM_COUNT')
    key_slots = constant('include/constants/global.h', 'BAG_KEYITEMS_COUNT')
    move_id = constant('include/constants/moves.h', 'MOVE_ROCK_TOMB')
    if (source_tm,source_case,slots,key_slots,move_id,source_tm-first_tm) != (327,364,58,30,317,38):
        raise ValueError('Machine constants differ')
    item_data = json.loads(texts['src/data/items.json'], object_pairs_hook=pairs)['items']
    definition_template = texts['src/data/items.json.txt']
    if '.description = gMoveDescription_{{ item.moveId }},' not in definition_template:
        raise ValueError('TM description dispatch differs')
    block('src/data/items.json.txt', 'gItems', r'^const struct Item gItems\[\] = \{.*?^\};')
    def item(symbol, source_id):
        candidates = [x for x in item_data if x['itemId'] == symbol]
        if len(candidates) != 1:
            raise ValueError('Non-unique machine source item')
        i = candidates[0]
        m = match('src/data/items.json', r'^    \{\n      "english": "' + i['english'] + r'",.*?^    \}')
        location = ref('src/data/items.json', symbol, m)
        declaration = i['description_english'].replace('\\n', '\n')
        description = declaration
        if symbol == 'ITEM_TM39':
            if i['moveId'] != 'RockTomb':
                raise ValueError('TM39 move mapping differs')
            d = match('src/move_descriptions.c', r'^const u8 gMoveDescription_RockTomb\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
            description = json.loads(d.group(1))
        value = dict(id={'value': 'pret:' + symbol}, sourceItemSymbol=symbol, sourceItemId=source_id,
            name=i['english'], price=i['price'], importance=i['importance'], registrability=i['registrability'],
            pocket=i['pocket'], type=str(i['type']), holdEffect=i['holdEffect'], holdEffectParam=i['holdEffectParam'],
            fieldUseFunction=i['fieldUseFunc'], battleUsage=i['battleUsage'], battleUseFunction=i['battleUseFunc'],
            secondaryId=i['secondaryId'], canToss=i['importance']==0, canSell=i['price']!=0,
            canGive=i['importance']==0, source=location)
        return dict(item=value, description=description)
    tm = item('ITEM_TM39',source_tm); case = item('ITEM_TM_CASE',source_case)
    map_block = match('src/data/party_menu.h', r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};')
    mapped_moves = re.findall(r'\bMOVE_\w+\b',map_block.group(1))
    if len(mapped_moves) != 58 or mapped_moves[38] != 'MOVE_ROCK_TOMB':
        raise ValueError('TM39 party-menu move dispatch differs')
    rules.append(ref('src/data/party_menu.h','sTMHMMoves',map_block))
    move = match('src/data/battle_moves.h', r'^    \[MOVE_ROCK_TOMB\] =\n    \{(.*?)^    \},')
    pp = re.search(r'\.pp = (\d+),', move.group(1))
    if pp is None or int(pp.group(1)) != 10:
        raise ValueError('Rock Tomb PP differs')
    rules.append(ref('src/data/battle_moves.h','MOVE_ROCK_TOMB',move))
    shared = load_catalog(ROOT)
    tm = dict(item='pret:ITEM_TM39')
    compatibility = [compatibility_reference(shared, 'pret:ITEM_TM39', 'pret:SPECIES_' + symbol) for symbol in SPECIES]
    for path, symbol, pattern in [
        ('src/item.c','CheckBagHasSpace',r'^bool8 CheckBagHasSpace\(.*?^\}'),
        ('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),
        ('src/item.c','RemoveBagItem',r'^bool8 RemoveBagItem\(.*?^\}'),
        ('src/party_menu.c','CanMonLearnTMTutor',r'^static u8 CanMonLearnTMTutor\([^;]*?\n\{.*?^\}'),
        ('src/party_menu.c','ItemUseCB_TMHM',r'^void ItemUseCB_TMHM\([^;]*?\n\{.*?^\}'),
        ('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}'),
        ('src/party_menu.c','CB2_UseTMHMAfterForgettingMove',r'^static void CB2_UseTMHMAfterForgettingMove\([^;]*?\n\{.*?^\}'),
        ('src/pokemon.c','CanMonLearnTMHM',r'^u32 CanMonLearnTMHM\([^;]*?\n\{.*?^\}'),
        ('src/pokemon.c','SetMonMoveSlot',r'^void SetMonMoveSlot\([^;]*?\n\{.*?^\}'),
        ('src/pokemon.c','RemoveMonPPBonus',r'^void RemoveMonPPBonus\([^;]*?\n\{.*?^\}'),
        ('src/pokemon.c','AdjustFriendship',r'^void AdjustFriendship\([^;]*?\n\{.*?^\}'),
        ('src/pokemon.c','FRIENDSHIP_EVENT_LEARN_TMHM',r'^    \[FRIENDSHIP_EVENT_LEARN_TMHM\][^\n]*'),
        ('data/maps/PewterCity_Gym/scripts.inc','PewterCity_Gym_EventScript_GiveTM39',r'^PewterCity_Gym_EventScript_GiveTM39::.*?^\tend$'),
        ('asm/macros/event.inc','giveitem_msg',r'^\s*\.macro giveitem_msg[^\n]*\n.*?\s*\.endm')]:
        block(path,symbol,pattern)
    spec = importlib.util.spec_from_file_location('pewter_machine_codec', ROOT/'scripts/import-lab-battle-ui.py')
    codec = importlib.util.module_from_spec(spec); spec.loader.exec_module(codec)
    icons, icon_outputs = [], {}
    for symbol in ('ITEM_TM39','ITEM_TM_CASE'):
        table = 'src/data/item_icon_table.h'
        m = match(table, r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},')
        paths = []
        for binding, ext in ((m[1],'.png'),(m[2],'.pal')):
            gm = match('src/data/graphics/items.h', r'^const u32 '+binding+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);')
            path = gm[1].replace('.4bpp.lz',ext).replace('.gbapal.lz',ext)
            if path not in data: raise ValueError('Icon binding outside locked scope')
            paths.append(path)
        image_path, palette_path = paths
        w,h,indices,_ = codec.indexed_png(data[image_path])
        if (w,h)!=(24,24): raise ValueError('Icon dimensions differ')
        rgba = codec.rgba(indices,codec.palette(data[palette_path])); png = codec.png(w,h,rgba)
        png_path = symbol.lower()+'.png'; icon_outputs[png_path] = png
        def whole(path, sym): return dict(path=path,symbol=sym,startLine=1,
            endLine=max(1,texts[path].count('\n')) if path in texts else 1)
        icons.append(dict(item={'value':'pret:'+symbol},width=w,height=h,
            pngPath=png_path,source=whole(image_path,m[1]),
            paletteSource=whole(palette_path,m[2]),bindingSource=ref(table,symbol,m)))
    for p,sym in [('tools/gbagfx/gfx.c','WriteGbaPalette'),('tools/gbagfx/convert_png.c','ConvertBitDepth')]:
        rules.append(dict(path=p,symbol=sym,startLine=1,endLine=texts[p].count('\n')))
    catalog = dict(sourceCommit=COMMIT,profile=PROFILE,completeItemCatalog=False,machineSlots=slots,keyItemSlots=key_slots,
        maximumStack=999,move={'value':'pret:MOVE_ROCK_TOMB'},sourceMoveId=move_id,movePp=10,
        tm39=tm,tmCase=case,compatibility=compatibility)
    return dict(schemaVersion=1,icons=icons,inputs=lock['files'],limitations=[
        'Only TM39 and TM CASE storage/teaching are implemented; metadata does not add a general TM/HM service.',
        'Other pockets and non-CASE Key Item membership remain the existing validator responsibility.',
        'CheckBagHasSpace checks the machine pocket; AddBagItem may fail if the first CASE cannot fit in Key Items.',
        'Source can partially insert CASE before failing in a full TM pocket. This cannot occur with the one-TM profile; atomic failure is retained.',
        'Source Brock giveitem_msg sets its flag without checking AddBagItem result. Key-item slot exhaustion cannot occur in the current three-key-item campaign profile.',
        'Teaches consume one TM, reset only the learned slot PP/PP Ups and apply source TM friendship at the supplied map section.',
        'Ownership, EVs, stat-calculation EV cache, status and history remain unchanged; no UI, RNG or quest log is implemented.',
        'No imported move is an HM. Generic HM forgetting constraints are outside this bounded profile.',
        'Local source verification does not imply ROM, native-window, audio or original-frame parity.'
    ],catalog=catalog), icon_outputs

def run(reference, raw, out):
    raw = directory(raw); out = directory(out)
    if raw == out or raw in out.parents or out in raw.parents:
        raise ValueError('Raw/output overlap')
    lock = read_lock()
    data = {}
    for entry in lock['files']:
        path = safe(raw / entry['path'])
        directory(path.parent)
        if path.exists() and not path.is_file():
            raise ValueError('Non-file raw path')
        b = subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+entry['path']]) if reference else path.read_bytes()
        data[entry['path']] = b
    parsed, icon_outputs = build(lock,data)
    document = encoded(parsed)
    manifest = dict(sourceCommit=COMMIT,profile=PROFILE,inputCount=len(lock['files']),speciesCount=29)
    manifest['icons'] = [dict(path=p) for p,b in icon_outputs.items()]
    outputs = {'pewter-machines.json':document,'manifest.json':encoded(manifest),**icon_outputs}
    targets = [(raw / p,b) for p,b in data.items()] if reference else []
    targets.extend((out / p,b) for p,b in outputs.items())
    for target,_ in targets:
        safe(target); directory(target.parent)
        safe(target.with_name(target.name+'.tmp'))
        for candidate in (target,target.with_name(target.name+'.tmp')):
            if candidate.exists() and not candidate.is_file():
                raise ValueError('Non-file output path')
    for target,b in targets:
        target.parent.mkdir(parents=True,exist_ok=True)
        temp=target.with_name(target.name+'.tmp');temp.write_bytes(b);os.replace(temp,target)
    return manifest

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path,default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/pewter-machines')
    args=parser.parse_args()
    print(json.dumps(run(args.reference_repo,args.raw,args.out)))
