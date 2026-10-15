#!/usr/bin/env python3
"""Import only Lemonade, three rooftop reward machines and their service facts.
REFERENCE DEPENDENCY OUTPUT [--check]. Reads shared inputs without copying them.
"""
import argparse,  io, json, re, subprocess
from pathlib import Path
from PIL import Image
from machine_catalog import load_catalog, machine_definitions

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/department-rooftop-items/'
RAW = 'content/raw/pokefirered/037335f4/'
PROFILE = 'firered-department-rooftop-items-v1'
SHARED = {'src/data/items.json': 'content/raw/pokefirered/037335f4/src/data/items.json', 'include/constants/items.h': 'content/raw/pokefirered/037335f4/include/constants/items.h', 'src/data/pokemon/item_effects.h': 'content/raw/pokefirered/037335f4/src/data/pokemon/item_effects.h', 'include/constants/item_effects.h': 'content/raw/pokefirered/037335f4/include/constants/item_effects.h', 'src/data/item_icon_table.h': 'content/raw/pokefirered/037335f4/src/data/item_icon_table.h', 'src/data/graphics/items.h': 'content/raw/pokefirered/037335f4/src/data/graphics/items.h', 'include/constants/moves.h': 'content/raw/pokefirered/037335f4/include/constants/moves.h', 'src/data/battle_moves.h': 'content/raw/pokefirered/037335f4/src/data/battle_moves.h', 'src/pokemon.c': 'content/raw/pokefirered/037335f4/src/pokemon.c', 'src/script_menu.c': 'content/raw/pokefirered/037335f4/src/script_menu.c', 'include/constants/menu.h': 'content/raw/pokefirered/037335f4/include/constants/menu.h', 'include/constants/flags.h': 'content/raw/pokefirered/037335f4/include/constants/flags.h', 'src/strings.c': 'content/raw/pokefirered/037335f4/src/strings.c', 'data/scripts/obtain_item.inc': 'content/raw/pokefirered/037335f4/data/scripts/obtain_item.inc', 'data/event_scripts.s': 'content/raw/pokefirered/037335f4/data/event_scripts.s', 'data/text/obtain_item.inc': 'content/raw/pokefirered/037335f4/data/text/obtain_item.inc', 'asm/macros/event.inc': 'content/raw/pokefirered/037335f4/asm/macros/event.inc', 'data/maps/CeladonCity_DepartmentStore_Roof/scripts.inc': 'content/raw/pokefirered/037335f4/data/maps/CeladonCity_DepartmentStore_Roof/scripts.inc', 'data/maps/CeladonCity_DepartmentStore_Roof/text.inc': 'content/raw/pokefirered/037335f4/data/maps/CeladonCity_DepartmentStore_Roof/text.inc', 'data/maps/CeladonCity_DepartmentStore_Roof/map.json': 'content/raw/pokefirered/037335f4/data/maps/CeladonCity_DepartmentStore_Roof/map.json', 'src/data/party_menu.h': 'content/raw/pokefirered/037335f4/src/data/party_menu.h'}

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
        matches = list(re.finditer(pattern, text(path), re.M | re.S))
        need(len(matches) == 1, 'Exact declaration: '+path+': '+pattern)
        return matches[0]
    def source(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text(path).count('\n', 0, match.start())+1,
            endLine=text(path).count('\n', 0, match.end())+1)
    def number(path, symbol): return int(one(path, r'^#define '+symbol+r'\s+(0x[0-9A-Fa-f]+|\d+)\b[^\n]*')[1], 0)
    items_path = 'src/data/items.json'
    source_items = json.loads(text(items_path))['items']
    def metadata(symbol):
        item = next(i for i in source_items if i['itemId'] == symbol)
        block = one(items_path, r'^    \{\n      "english": "'+re.escape(item['english'])+r'",.*?^    \}')
        row = dict(id=dict(value='pret:'+symbol), sourceItemSymbol=symbol,
            sourceItemId=number('include/constants/items.h', symbol), name=item['english'], price=item['price'],
            importance=item['importance'], registrability=item['registrability'], pocket=item['pocket'], type=str(item['type']),
            holdEffect=item['holdEffect'], holdEffectParam=item['holdEffectParam'], fieldUseFunction=item['fieldUseFunc'],
            battleUsage=item['battleUsage'], battleUseFunction=item['battleUseFunc'], secondaryId=item['secondaryId'],
            canToss=item['importance'] == 0, canSell=item['price'] != 0, canGive=item['importance'] == 0,
            source=source(items_path, symbol, block))
        return item, row
    drinks = []
    for symbol, name, price, hp in [('ITEM_FRESH_WATER','FreshWater',200,50), ('ITEM_SODA_POP','SodaPop',300,60), ('ITEM_LEMONADE','Lemonade',350,80)]:
        item, meta = metadata(symbol)
        ep = 'src/data/pokemon/item_effects.h'; effect_symbol = 'sItemEffect_'+name
        effect = one(ep, r'^static const u8 '+effect_symbol+r'\[7\] = \{\n(.*?)^\};')
        need(effect[1] == f'    [4] = ITEM4_HEAL_HP,\n    [6] = {hp}, // Amount of HP to recover\n', 'Drink healing bytes')
        flag = number('include/constants/item_effects.h', 'ITEM4_HEAL_HP')
        need(flag == 4 and item['price'] == price and item['holdEffectParam'] == hp and item['fieldUseFunc'] == 'FieldUseFunc_Medicine' and item['battleUsage'] == 1 and item['battleUseFunc'] == 'BattleUseFunc_Medicine', 'Drink semantics')
        addition = {key:meta[key] for key in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}
        addition.update(description=item['description_english'].replace('\\n','\n'), healHp=hp, cureStatus=0,
            effectBytes=[0,0,0,0,flag,0,hp], useSupport=1, effectSource=source(ep,effect_symbol,effect), sourceMetadata=meta)
        drinks.append(addition)
    dependency_paths = ['content/generated/first-sevii-rules/items.json',
        'scripts/import-config/machine-admissions.json', 'scripts/import-config/machine-catalog.json']
    dependencies = [dict(path=p) for p in dependency_paths]
    existing_drinks = json.loads((dependency/dependency_paths[0]).read_bytes())['additions']
    for drink in drinks[:2]: need(drink == next(d for d in existing_drinks if d['id'] == drink['id']), 'Reuse exact existing drink')
    machines = machine_definitions(dependency, BASE + 'machines.json', 'additions')
    need(len(machines) == 3 and all(len(m['species']) == 170 for m in machines), 'Current admitted machines and species')
    icon_aliases = []
    for index, (tm, move, move_id, pp, move_type, effect, alias) in enumerate([
        ('TM16','LIGHT_SCREEN',113,30,'PSYCHIC','LIGHT_SCREEN','TM04'),
        ('TM20','SAFEGUARD',219,25,'NORMAL','SAFEGUARD','TM05'),
        ('TM33','REFLECT',115,20,'PSYCHIC','REFLECT','TM04')]):
        symbol = 'ITEM_'+tm
        need(machines[index]['item'] == 'pret:'+symbol, 'Admitted rooftop reward')
        need(number('include/constants/moves.h','MOVE_'+move) == move_id, 'Source move ID')
        bp='src/data/battle_moves.h'; battle=one(bp,r'^    \[MOVE_'+move+r'\]\s*=\s*\{(.*?)\},')
        for key,expected in [('effect','EFFECT_'+effect),('power','0'),('type','TYPE_'+move_type),('accuracy','0'),('pp',str(pp))]:
            need(re.search(r'\.'+key+r'\s*=\s*'+expected+r'\s*,',battle[1]), 'Battle move '+key)
        # Verify actual source TM index binding, not only the English move name.
        table_path='src/data/party_menu.h'
        entries=re.findall(r'MOVE_(\w+)',one(table_path,r'^static const u16 sTMHMMoves\[\] =\s*\{(.*?)^\};')[1])
        need(entries[int(tm[2:])-1] == move, 'Source TM index')
        ip='src/data/item_icon_table.h'; binding=one(ip,r'^    \['+symbol+r'\]\s*= \{gItemIcon_TMHM, gItemIconPalette_'+move_type.title()+r'TMHM\},$')
        other=one(ip,r'^    \[ITEM_'+alias+r'\]\s*= \{gItemIcon_TMHM, gItemIconPalette_'+move_type.title()+r'TMHM\},$')
        icon_aliases.append(dict(item=dict(value='pret:'+symbol), reusedItem=dict(value='pret:ITEM_'+alias), source=source(ip,symbol,binding), reusedSource=source(ip,'ITEM_'+alias,other)))
    ip='src/data/item_icon_table.h'; binding=one(ip,r'^    \[ITEM_LEMONADE\]\s*= \{gItemIcon_Lemonade, gItemIconPalette_Lemonade\},$')
    graphics=text('src/data/graphics/items.h')
    need('gItemIcon_Lemonade[] = INCBIN_U32("graphics/items/icons/lemonade.4bpp.lz")' in graphics and 'gItemIconPalette_Lemonade[] = INCBIN_U32("graphics/items/icon_palettes/lemonade.gbapal.lz")' in graphics, 'Lemonade icon binding')
    image='graphics/items/icons/lemonade.png'; palette='graphics/items/icon_palettes/lemonade.pal'
    im=Image.open(io.BytesIO(read(image))); need(im.mode == 'P' and im.size == (24,24) and max(im.tobytes()) < 16,'Icon indices')
    lines=text(palette).splitlines(); need(lines[:3] == ['JASC-PAL','0100','16'] and len(lines) == 19,'Icon palette')
    colors=[tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
    rgba=bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0))
    stream=io.BytesIO(); Image.frombytes('RGBA',(24,24),rgba).save(stream,format='PNG',compress_level=9); png=stream.getvalue()
    icon = dict(item=dict(value='pret:ITEM_LEMONADE'), width=24, height=24, texturePath=BASE + 'lemonade.png', sourcePaths=[image, palette], bindingSource=source(ip, 'ITEM_LEMONADE', binding))
    document=dict(sourceCommit=COMMIT, profile=PROFILE, previousItemCount=131,itemCount=132,additions=[drinks[2]],
        shopsAdded=[],icons=[icon])
    machine_doc=dict(sourceCommit=COMMIT,profile=PROFILE,previousMachineCount=42,machineCount=45,speciesCount=170,
        additions=machines,iconAliases=icon_aliases)
    roof='data/maps/CeladonCity_DepartmentStore_Roof/'
    script=text(roof+'scripts.inc'); texts=text(roof+'text.inc'); map_data=json.loads(text(roof+'map.json'))
    menus=[]
    for symbol, suffix, menu_id in [('CELADON_VENDING_MACHINE','CeladonVendingMachine',26),('THIRSTY_GIRL_FRESH_WATER','ThirstyGirlFreshWater',32),('THIRSTY_GIRL_SODA_POP','ThirstyGirlSodaPop',33),('THIRSTY_GIRL_FRESH_WATER_SODA_POP','ThirstyGirlFreshWaterSodaPop',34),('THIRSTY_GIRL_LEMONADE','ThirstyGirlLemonade',35),('THIRSTY_GIRL_FRESH_WATER_LEMONADE','ThirstyGirlFreshWaterLemonade',36),('THIRSTY_GIRL_SODA_POP_LEMONADE','ThirstyGirlSodaPopLemonade',37),('THIRSTY_GIRL_FRESH_WATER_SODA_POP_LEMONADE','ThirstyGirlFreshWaterSodaPopLemonade',38)]:
        symbol='MULTICHOICE_'+symbol; list_symbol='sMultichoiceList_'+suffix
        need(number('include/constants/menu.h',symbol) == menu_id,'Menu ID')
        path='src/script_menu.c'; block=one(path,r'^static const struct MenuAction '+list_symbol+r'\[\] = \{\n(.*?)^\};')
        binding=one(path,r'^    \['+symbol+r'\]\s*= MULTICHOICE\('+list_symbol+r'\),$')
        rows=[]
        for label in re.findall(r'\{ (\w+) \}',block[1]):
            string=one('src/strings.c',r'^const u8 '+label+r'\[\] = _\("(.*?)"\);')
            rows.append(dict(symbol=label,text=json.loads('"'+string[1]+'"'),source=source('src/strings.c',label,string)))
        menus.append(dict(symbol=symbol,id=menu_id,rows=rows,source=source(path,list_symbol,block),bindingSource=source(path,symbol,binding)))
    flags=[]
    for tm,value in [('TM16',0x296),('TM20',0x295),('TM33',0x294)]:
        symbol='FLAG_GOT_'+tm+'_FROM_THIRSTY_GIRL'; need(number('include/constants/flags.h',symbol) == value,'Reward flag')
        flags.append(dict(symbol=symbol,value=value))
    dependencies_facts=[]
    for path,symbol,pattern in [(roof+'text.inc','Text_ReceivedItemFromLittleGirl',r'^Text_ReceivedItemFromLittleGirl::\n(?:    \.string [^\n]+\n)+'),('data/scripts/obtain_item.inc','EventScript_RestorePrevTextColor',r'^EventScript_RestorePrevTextColor::\n.*?\treturn\n'),('asm/macros/event.inc','putitemaway',r'^\t\.macro putitemaway item:req, amount=1\n.*?\t\.endm'),('data/event_scripts.s','Std_PutItemAway',r'^Std_PutItemAway::\n.*?\treturn\n'),('data/text/obtain_item.inc','Text_PutItemAway',r'^Text_PutItemAway::\n(?:\s*\.string [^\n]+\n)+')]:
        block=one(path,pattern); dependencies_facts.append(dict(source=source(path,symbol,block),body=block[0]))
    need(script.index('\tremoveitem VAR_0x8008') < script.index('\tcheckitemspace VAR_0x8009') < script.index('\tadditem VAR_0x8009'), 'Original consumed-before-capacity order')
    facts=dict(sourceCommit=COMMIT,  mapId='map.celadon-city-department-store-roof',
        roofSources=[dict(path=roof+p) for p in ['scripts.inc','text.inc','map.json']],
        objects=map_data['object_events'],backgroundEvents=map_data['bg_events'],menus=menus,flags=flags,
        drinks=[dict(item=d['id']['value'],sourceItemId=d['sourceItemId'],price=d['price'],healHp=d['healHp'],effectBytes=d['effectBytes']) for d in drinks],
        rewardMappings=[dict(drink=d['id']['value'],reward=m['item'],flag=f['symbol']) for d,m,f in zip(drinks,machines,flags)],
        giveDrinkOrder=['removeitem','checkitemspace','additem','fanfare','putitemaway','setflag'],consumesDrinkWhenRewardHasNoRoom=True,dependencies=dependencies_facts)
    evidence=dict(sourceCommit=COMMIT,inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=dependencies)
    files = {BASE + 'items.json': encoded(document), BASE + 'machines.json': encoded(machine_doc), BASE + 'source-contract.json': encoded(facts), BASE + 'source-input-hashes.json': encoded(evidence), icon['texturePath']: png, **raw}
    return files,document,machine_doc

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for arg in ['reference','dependency','output']: parser.add_argument(arg,type=Path)
    parser.add_argument('--check',action='store_true'); args=parser.parse_args()
    files,items,machines=generate(args.reference,args.dependency)
    for path,data in files.items():
        target=args.output/path
        if args.check: need(target.read_bytes() == data,'Regeneration differs: '+path)
        else: target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(data)
    catalog=load_catalog(args.dependency)
    print(json.dumps(dict(checked=args.check,
        compatibility=[sum(catalog.compatibility(m['item'], species)['canLearn'] for species in m['species']) for m in machines['additions']]),indent=2))
