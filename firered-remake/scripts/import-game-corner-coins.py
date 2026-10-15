#!/usr/bin/env python3
"""Import bounded Coin Case / ordinary Game Corner coins; reuse maps and source bytes.
REFERENCE DEPENDENCY OUTPUT [--check]. Never executes the reference repository.
"""
import argparse, base64,  io, json, re, subprocess
from pathlib import Path
from PIL import Image

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-game-corner-coins-v1'
BASE = 'content/generated/game-corner-coins/'
RAW = 'content/raw/pokefirered/037335f4/'
RESTAURANT = 'data/maps/CeladonCity_Restaurant/'
CORNER = 'data/maps/CeladonCity_GameCorner/'
SHARED = {path: 'content/raw/pokefirered/037335f4/'+path for paths in [
    ['src/data/items.json','include/constants/items.h','src/data/item_icon_table.h','src/data/graphics/items.h','src/item_use.c'],
    ['include/constants/menu.h','include/constants/flags.h','include/constants/vars.h','src/script_menu.c','data/scripts/obtain_item.inc','data/event_scripts.s','data/text/obtain_item.inc'],
    ['asm/macros/event.inc'],
    ['src/strings.c'],
    ['src/scrcmd.c'],
    [RESTAURANT+p for p in ['scripts.inc','text.inc','map.json']],
    [CORNER+p for p in ['scripts.inc','text.inc','map.json']],
] for path in paths}

def need(value, message):
    if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value, indent=2, ensure_ascii=False)+'\n').encode()

def generate(reference, dependency):
    inputs, cache, raw = {}, {}, {}
    def read(path):
        if path in cache: return cache[path]
        data = subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference)
        local = SHARED.get(path, RAW+path)
        if path in SHARED: need((dependency/local).read_bytes() == data,'Shared source bytes: '+path)
        else: raw[local] = data
        inputs[path] = dict(path=path,localPath=local,shared=path in SHARED)
        cache[path] = data
        return data
    def text(path): return read(path).decode()
    def one(path, pattern):
        matches = list(re.finditer(pattern,text(path),re.M|re.S))
        need(len(matches) == 1,'Exact source declaration: '+path+': '+pattern)
        return matches[0]
    def source(path,symbol,match=None):
        body = '' if path.endswith('.png') else text(path)
        return dict(path=path,symbol=symbol,startLine=1 if match is None else body.count('\n',0,match.start())+1,
            endLine=max(1,body.count('\n')) if match is None else body.count('\n',0,match.end())+1)
    def number(path,symbol): return int(one(path,r'^#define '+symbol+r'\s+(0x[0-9A-Fa-f]+|\d+)\b[^\n]*')[1],0)
    def declaration(path,symbol):
        match=one(path,r'^(?:static )?[^\n;]+\b'+symbol+r'\([^;\n]*\)\n\{.*?^\}')
        return dict(source=source(path,symbol,match),body=match[0])
    constants={}
    for path,symbol,value in [('include/constants/items.h','ITEM_COIN_CASE',260),
        ('include/constants/coins.h','MAX_COINS',9999),('include/constants/menu.h','MULTICHOICE_GAME_CORNER_COIN_PURCHASE_COUNTER',27),
        ('include/constants/flags.h','FLAG_GOT_COIN_CASE',0x243),('include/constants/flags.h','FLAG_GOT_10_COINS_FROM_GAMBLER',0x26e),
        ('include/constants/flags.h','FLAG_GOT_20_COINS_FROM_GAMBLER',0x26f),('include/constants/flags.h','FLAG_GOT_20_COINS_FROM_GAMBLER_2',0x270)]:
        need(number(path,symbol) == value,'Source constant: '+symbol);constants[symbol]=value
    for amount in [10,20,50,500]: constants[f'(MAX_COINS + 1) - {amount}']=constants['MAX_COINS']+1-amount
    path='src/data/items.json'; original=next(i for i in json.loads(text(path))['items'] if i['itemId']=='ITEM_COIN_CASE')
    match=one(path,r'^    \{\n      "english": "COIN CASE",.*?^    \}')
    ref=source(path,'ITEM_COIN_CASE',match)
    item=dict(id=dict(value='pret:ITEM_COIN_CASE'),sourceItemSymbol='ITEM_COIN_CASE',sourceItemId=260,
        name=original['english'],price=original['price'],importance=original['importance'],registrability=original['registrability'],
        pocket=original['pocket'],type=str(original['type']),holdEffect=original['holdEffect'],holdEffectParam=original['holdEffectParam'],
        fieldUseFunction=original['fieldUseFunc'],battleUsage=original['battleUsage'],battleUseFunction=original['battleUseFunc'],
        secondaryId=original['secondaryId'],canToss=original['importance']==0,canSell=original['price']!=0,canGive=original['importance']==0,source=ref)
    need(item['importance']==1 and item['registrability']==0 and item['price']==0 and item['pocket']=='POCKET_KEY_ITEMS' and
        item['fieldUseFunction']=='FieldUseFunc_CoinCase' and item['secondaryId']==0 and item['battleUsage']==0,'Coin Case semantics')
    desc=original['description_english'].replace('\\n','\n')
    path='src/data/item_icon_table.h'; binding=one(path,r'^    \[ITEM_COIN_CASE\]\s*= \{gItemIcon_CoinCase, gItemIconPalette_CoinCase\},$')
    binding_ref=source(path,'ITEM_COIN_CASE',binding); graphics=text('src/data/graphics/items.h')
    need('gItemIcon_CoinCase[] = INCBIN_U32("graphics/items/icons/coin_case.4bpp.lz")' in graphics and
        'gItemIconPalette_CoinCase[] = INCBIN_U32("graphics/items/icon_palettes/coin_case.gbapal.lz")' in graphics,'Coin Case graphics binding')
    image='graphics/items/icons/coin_case.png'; palette='graphics/items/icon_palettes/coin_case.pal'
    im=Image.open(io.BytesIO(read(image)));need(im.mode=='P' and im.size==(24,24) and max(im.tobytes())<16,'Icon indices')
    lines=text(palette).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Icon palette')
    colors=[tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
    rgba=bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0))
    stream=io.BytesIO();Image.frombytes('RGBA',(24,24),rgba).save(stream,format='PNG',compress_level=9);png=stream.getvalue()
    icon=dict(item=item['id'],width=24,height=24,
        pngPath='coin-case.png',source=source(image,'gItemIcon_CoinCase'),
        paletteSource=source(palette,'gItemIconPalette_CoinCase'),bindingSource=binding_ref)
    document=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,
        items=[dict(item=item,description=desc,declarationDescription=desc,descriptionSource=ref)],icons=[icon])
    # Preserve original source lines and ordered instructions for only the five ordinary services.
    blocks={}
    for path in [RESTAURANT+'scripts.inc',CORNER+'scripts.inc']:
        for block in re.finditer(r'^(\w+)::\n(.*?)(?=^\w+::|\Z)',text(path),re.M|re.S):
            blocks[block[1]]=dict(label=block[1],source=source(path,block[1],block),body=block[2])
    roots=['CeladonCity_Restaurant_EventScript_CoinCaseMan']+['CeladonCity_GameCorner_EventScript_'+s for s in ['CoinsClerk','Fisher','Scientist','Gentleman']]
    selected=set();pending=roots[:]
    while pending:
        label=pending.pop()
        if label in selected:continue
        selected.add(label)
        for target in re.findall(r'\bCeladonCity_(?:GameCorner|Restaurant)_EventScript_\w+',blocks[label]['body']):
            need(target in blocks,'Missing service label: '+target);pending.append(target)
    need(not any('SlotMachine' in label and not label.endswith('FaceSlotMachine') for label in selected),'Excluded slot script')
    services=[]
    for label,coins,flag in zip(roots,[0,0,10,20,20],['FLAG_GOT_COIN_CASE',None,'FLAG_GOT_10_COINS_FROM_GAMBLER','FLAG_GOT_20_COINS_FROM_GAMBLER','FLAG_GOT_20_COINS_FROM_GAMBLER_2']):
        folder=RESTAURANT if label==roots[0] else CORNER
        actors=json.loads(text(folder+'map.json'))['object_events']; matches=[(i+1,a) for i,a in enumerate(actors) if a['script']==label]
        need(len(matches)==1,'Exact service actor')
        services.append(dict(entryLabel=label,localObjectId=matches[0][0],actor=matches[0][1],coins=coins,flag=flag))
        text(folder+'text.inc')
    for suffix,amount,cost in [('Buy50Coins',50,1000),('Buy500Coins',500,10000)]:
        body=blocks['CeladonCity_GameCorner_EventScript_'+suffix]['body']
        tokens=['checkcoins VAR_TEMP_1',f'goto_if_ge VAR_TEMP_1, (MAX_COINS + 1) - {amount}',f'checkmoney {cost}',f'addcoins {amount}',f'removemoney {cost}']
        need(all(t in body for t in tokens) and [body.index(t) for t in tokens]==sorted(body.index(t) for t in tokens),'Original capacity/payment order')
    path='src/script_menu.c'; menu=one(path,r'^static const struct MenuAction sMultichoiceList_GameCornerCoinPurchaseCounter\[\] = \{\n(.*?)^\};')
    binding=one(path,r'^    \[MULTICHOICE_GAME_CORNER_COIN_PURCHASE_COUNTER\]\s*= MULTICHOICE\(sMultichoiceList_GameCornerCoinPurchaseCounter\),$')
    rows=[]
    for symbol in re.findall(r'\{ (\w+) \}',menu[1]):
        match=one('src/strings.c',r'^const u8 '+symbol+r'\[\] = _\("(.*?)"\);')
        rows.append(dict(symbol=symbol,text=json.loads('"'+match[1]+'"'),source=source('src/strings.c',symbol,match)))
    need(len(rows)==3,'Original coin menu rows')
    use=declaration('src/item_use.c','FieldUseFunc_CoinCase')
    match=one('src/strings.c',r'^const u8 gText_CoinCase\[\] = _\("(.*?)"\);')
    use_text=json.loads('"'+match[1]+'"');need(use_text=='Your COINS:\n{STR_VAR_1}{PAUSE_UNTIL_PRESS}','Bag coin text')
    implementations=[declaration('src/coins.c',s) for s in ['GetCoins','SetCoins','AddCoins','RemoveCoins','PrintCoinsString','ShowCoinsWindow','HideCoinsWindow']]
    implementations += [declaration('src/scrcmd.c','ScrCmd_'+s) for s in ['checkcoins','addcoins','showcoinsbox','updatecoinsbox','hidecoinsbox']]
    for path in ['asm/macros/event.inc','data/scripts/obtain_item.inc','data/event_scripts.s','data/text/obtain_item.inc','include/constants/vars.h']:read(path)
    facts=dict(sourceCommit=COMMIT,profile=PROFILE,mapsAdded=[],services=services,
        constants=constants,maximumCoins=9999,purchases=[dict(coins=50,price=1000),dict(coins=500,price=10000)],
        menu=dict(id=27,x=13,y=0,ignoreB=False,rows=rows,source=source('src/script_menu.c','sMultichoiceList_GameCornerCoinPurchaseCounter',menu),
            bindingSource=source('src/script_menu.c','MULTICHOICE_GAME_CORNER_COIN_PURCHASE_COUNTER',binding)),
        coinsBox=dict(x=0,y=5,tileX=1,tileY=6,width=8,height=3,amountY=12),
        bagUse=dict(text=use_text,textSource=source('src/strings.c','gText_CoinCase',match),function=use,digitCount=4,leftAligned=True),
        scripts=[blocks[s] for s in sorted(selected)],implementations=implementations,
        excluded=['slot-machines','hidden-coins','prize-redemption'])
    evidence=dict(sourceCommit=COMMIT,inputs=[inputs[p] for p in sorted(inputs)])
    files={BASE+'items.json':encoded(document),BASE+'source-contract.json':encoded(facts),
        BASE+'source-input-hashes.json':encoded(evidence),BASE+'coin-case.png':png,**raw}
    return files,document,facts

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for arg in ['reference','dependency','output']:parser.add_argument(arg,type=Path)
    parser.add_argument('--check',action='store_true');args=parser.parse_args()
    files,items,facts=generate(args.reference,args.dependency)
    for path,data in files.items():
        target=args.output/path
        if args.check:need(target.read_bytes()==data,'Regeneration differs: '+path)
        else:
            need(not target.is_symlink(),'Refuse symlink output: '+path)
            target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(checked=args.check,services=len(facts['services']),scriptBlocks=len(facts['scripts'])),indent=2))
