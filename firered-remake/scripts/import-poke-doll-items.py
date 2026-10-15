#!/usr/bin/env python3
"""Import one original Poke Doll; retain complete 4F source shelf as evidence only.
REFERENCE DEPENDENCY OUTPUT [--check]. Reads pinned Git blobs, never upstream programs.
"""
import argparse,  io, json, re, subprocess
from pathlib import Path
from PIL import Image
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/poke-doll-items/'
RAW = 'content/raw/pokefirered/037335f4/'
PROFILE = 'firered-poke-doll-inventory-v1'
SHARED = {
    'src/data/items.json': 'content/raw/pokefirered/037335f4/src/data/items.json',
    'include/constants/items.h': 'content/raw/pokefirered/037335f4/include/constants/items.h',
    'src/data/item_icon_table.h': 'content/raw/pokefirered/037335f4/src/data/item_icon_table.h',
    'src/data/graphics/items.h': 'content/raw/pokefirered/037335f4/src/data/graphics/items.h',
    'src/item_use.c': 'content/raw/pokefirered/037335f4/src/item_use.c',
}
DEPENDENCIES = (
    'content/generated/seafoam-items/items.json',
)
def need(value, message):
    if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value, indent=2, ensure_ascii=False) + '\n').encode()
def generate(reference, dependency):
    inputs, raw, cache = {}, {}, {}
    def read(path):
        if path in cache: return cache[path]
        data = subprocess.check_output(['git', 'show', COMMIT+':'+path], cwd=reference)
        local = SHARED.get(path, RAW+path)
        if path in SHARED: need((dependency/local).read_bytes() == data, 'Shared source bytes: '+path)
        else: raw[local] = data
        inputs[path] = dict(path=path, localPath=local, shared=path in SHARED)
        cache[path] = data
        return data
    def text(path): return read(path).decode()
    def one(path, pattern):
        matches = list(re.finditer(pattern, text(path), re.M|re.S))
        need(len(matches) == 1, 'Exact source declaration: '+pattern)
        return matches[0]
    def source(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text(path).count('\n', 0, match.start())+1, endLine=text(path).count('\n', 0, match.end())+1)
    dependencies = []
    for path in DEPENDENCIES:
        data = (dependency/path).read_bytes()
        dependencies.append(dict(path=path))
    path = 'src/data/items.json'; symbol = 'ITEM_POKE_DOLL'
    source_items = json.loads(text(path))['items']; item = next(i for i in source_items if i['itemId'] == symbol)
    block = one(path, r'^    \{\n      "english": "POK\\u00e9 DOLL",.*?^    \}')
    item_id = int(one('include/constants/items.h', r'^#define ITEM_POKE_DOLL\s+(\d+)\s*$')[1])
    need(item_id == 80 and item['price'] == 1000 and item['pocket'] == 'POCKET_ITEMS' and item['importance'] == 0 and item['type'] == 'ITEM_TYPE_BAG_MENU' and item['fieldUseFunc'] == 'FieldUseFunc_OakStopsYou' and item['battleUsage'] == 2 and item['battleUseFunc'] == 'BattleUseFunc_PokeDoll' and item['holdEffect'] == 'HOLD_EFFECT_NONE', 'Original Doll semantics')
    src = source(path, symbol, block); ident = dict(value='pret:'+symbol)
    metadata = dict(id=ident, sourceItemSymbol=symbol, sourceItemId=item_id, name=item['english'], price=item['price'], importance=item['importance'], registrability=item['registrability'], pocket=item['pocket'], type=item['type'], holdEffect=item['holdEffect'], holdEffectParam=item['holdEffectParam'], fieldUseFunction=item['fieldUseFunc'], battleUsage=item['battleUsage'], battleUseFunction=item['battleUseFunc'], secondaryId=item['secondaryId'], canToss=True, canSell=True, canGive=True, source=src)
    addition = {key:metadata[key] for key in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}
    addition.update(description=item['description_english'].replace('\\n', '\n'), healHp=0, cureStatus=0, effectBytes=[], useSupport=10, effectSource=None, sourceMetadata=metadata)
    use = 'src/item_use.c'
    battle = one(use, r'^void BattleUseFunc_PokeDoll\(u8 taskId\)\n\{.*?^\}')
    need('if (!(gBattleTypeFlags & BATTLE_TYPE_TRAINER))' in battle[0] and 'RemoveUsedItem();' in battle[0] and 'else\n        PrintNotTheTimeToUseThat(taskId, 0);' in battle[0], 'Trainer refusal before consumption')
    field = one(use, r'^void FieldUseFunc_OakStopsYou\(u8 taskId\)\n\{.*?^\}')
    shelf_path = 'data/maps/CeladonCity_DepartmentStore_4F/scripts.inc'
    shelf = one(shelf_path, r'^CeladonCity_DepartmentStore_4F_Items::\n(?:\t\.2byte [A-Z_]+\n)+')
    symbols = re.findall(r'\.2byte (ITEM_\w+)', shelf[0]); expected = ['ITEM_POKE_DOLL','ITEM_RETRO_MAIL','ITEM_FIRE_STONE','ITEM_THUNDER_STONE','ITEM_WATER_STONE','ITEM_LEAF_STONE','ITEM_NONE']
    need(symbols == expected, 'Complete original 4F shelf')
    shelf_items = []
    for sym in symbols[:-1]:
        original = next(i for i in source_items if i['itemId'] == sym)
        number = int(one('include/constants/items.h', r'^#define '+sym+r'\s+(\d+)\s*$')[1])
        shelf_items.append(dict(symbol=sym, sourceItemId=number, name=original['english'], price=original['price'], pocket=original['pocket'], type=original['type'], fieldUseFunction=original['fieldUseFunc']))
    clerk = one(shelf_path, r'^CeladonCity_DepartmentStore_4F_EventScript_Clerk::\n.*?\tend\n')
    need('\tpokemart CeladonCity_DepartmentStore_4F_Items\n' in clerk[0], 'Original clerk binding')
    icon_path = 'src/data/item_icon_table.h'; binding = one(icon_path, r'^    \[ITEM_POKE_DOLL\]\s*= \{gItemIcon_PokeDoll, gItemIconPalette_PokeDoll\},$')
    graphics = text('src/data/graphics/items.h')
    need('gItemIcon_PokeDoll[] = INCBIN_U32("graphics/items/icons/poke_doll.4bpp.lz")' in graphics and 'gItemIconPalette_PokeDoll[] = INCBIN_U32("graphics/items/icon_palettes/poke_doll.gbapal.lz")' in graphics, 'Original icon binding')
    image = 'graphics/items/icons/poke_doll.png'; palette = 'graphics/items/icon_palettes/poke_doll.pal'
    im = Image.open(io.BytesIO(read(image))); need(im.mode == 'P' and im.size == (24,24) and max(im.tobytes()) < 16, 'Original icon indices')
    lines = text(palette).splitlines(); need(lines[:3] == ['JASC-PAL','0100','16'] and len(lines) == 19, 'Original palette')
    colors = [tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
    rgba = bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0))
    stream = io.BytesIO(); Image.frombytes('RGBA',(24,24),rgba).save(stream,format='PNG',compress_level=9); png=stream.getvalue()
    icon = dict(item=ident, width=24, height=24, texturePath=BASE + 'poke-doll.png', sourcePaths=[image, palette], bindingSource=source(icon_path, symbol, binding))
    items = dict(sourceCommit=COMMIT,profile=PROFILE,previousItemCount=128,itemCount=129,additions=[addition],shopsAdded=[],icons=[icon])
    facts = dict(sourceCommit=COMMIT,executableShelf=False,stockId='CeladonCity_DepartmentStore_4F_Items',items=shelf_items,terminator='ITEM_NONE',source=source(shelf_path,'CeladonCity_DepartmentStore_4F_Items',shelf),clerkSource=source(shelf_path,'CeladonCity_DepartmentStore_4F_EventScript_Clerk',clerk),battleUseSource=source(use,'BattleUseFunc_PokeDoll',battle),nonBattleUseSource=source(use,'FieldUseFunc_OakStopsYou',field))
    evidence = dict(sourceCommit=COMMIT,inputs=[inputs[k] for k in sorted(inputs)],dependencyInputs=dependencies)
    files = {BASE + 'items.json': encoded(items), BASE + 'source-input-hashes.json': encoded(evidence), BASE + 'source-contract.json': encoded(facts), icon['texturePath']: png, **raw}
    lock = dict(sourceCommit=COMMIT,profile=PROFILE,files=[dict(path=p) for p,b in sorted(files.items())])
    files['scripts/poke-doll-items.lock.json'] = encoded(lock)
    return files, items

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for arg in ['reference','dependency','output']: parser.add_argument(arg,type=Path)
    parser.add_argument('--check',action='store_true'); args=parser.parse_args()
    files,items=generate(args.reference,args.dependency)
    for path,data in files.items():
        target=args.output/path
        if args.check: need(target.read_bytes()==data,'Regeneration differs: '+path)
        else: target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(data)
    print(json.dumps(dict(checked=args.check,itemCount=129),indent=2))
if __name__ == '__main__': main()
