#!/usr/bin/env python3
"""Import FireRed's complete 15-row prize shelf, five items and 50-machine/174-owner teaching delta.
REFERENCE DEPENDENCY OUTPUT [--check]. Only pinned Git blobs and existing dependencies are read.
No upstream code is run; source art is local-only and only six new icon variants are generated.
"""
import argparse,  io, json, re, subprocess
from pathlib import Path
from PIL import Image
from machine_catalog import compatibility_additions, load_catalog, machine_definitions

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/game-corner-prizes/'
RAW = 'content/raw/pokefirered/037335f4/'
PROFILE = 'firered-game-corner-prizes-v1'
ROOM = 'data/maps/CeladonCity_GameCorner_PrizeRoom/'
SHARED = {
 'src/data/items.json':'content/raw/pokefirered/037335f4/src/data/items.json',
 'include/constants/items.h':'content/raw/pokefirered/037335f4/include/constants/items.h',
 'src/data/pokemon/item_effects.h':'content/raw/pokefirered/037335f4/src/data/pokemon/item_effects.h',
 'include/constants/item_effects.h':'content/raw/pokefirered/037335f4/include/constants/item_effects.h',
 'src/data/item_icon_table.h':'content/raw/pokefirered/037335f4/src/data/item_icon_table.h',
 'src/data/graphics/items.h':'content/raw/pokefirered/037335f4/src/data/graphics/items.h',
 
 'include/constants/moves.h':'content/raw/pokefirered/037335f4/include/constants/moves.h',
 'src/data/battle_moves.h':'content/raw/pokefirered/037335f4/src/data/battle_moves.h',
 'src/data/party_menu.h':'content/raw/pokefirered/037335f4/src/data/party_menu.h',
 'asm/macros/event.inc':'content/raw/pokefirered/037335f4/asm/macros/event.inc',
 'src/script_menu.c':'content/raw/pokefirered/037335f4/src/script_menu.c',
 'include/constants/menu.h':'content/raw/pokefirered/037335f4/include/constants/menu.h',
 'include/constants/flags.h':'content/raw/pokefirered/037335f4/include/constants/flags.h',
 'src/strings.c':'content/raw/pokefirered/037335f4/src/strings.c',
 'src/script_pokemon_util.c':'content/raw/pokefirered/037335f4/src/script_pokemon_util.c',
 'src/scrcmd.c':'content/raw/pokefirered/037335f4/src/scrcmd.c',
 'graphics/items/icons/tm_hm.png':'content/raw/pokefirered/037335f4/graphics/items/icons/tm_hm.png',
 **{ROOM+p:'content/raw/pokefirered/037335f4/'+ROOM+p for p in ['scripts.inc','map.json','text.inc']},
}
DEPENDENCIES = (
    'scripts/import-config/machine-admissions.json',
    'scripts/import-config/machine-catalog.json',
)

def need(value, message):
    if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode()

def generate(reference, dependency):
    inputs,raw,cache,dependencies={}, {}, {}, []
    def read(path):
        if path in cache:return cache[path]
        data=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference)
        local=SHARED.get(path,RAW+path)
        if path in SHARED:need((dependency/local).read_bytes()==data,'Shared source bytes: '+path)
        else:raw[local]=data
        inputs[path]=dict(path=path,localPath=local,shared=path in SHARED)
        cache[path]=data;return data
    def text(path):return read(path).decode()
    def one(path,pattern):
        found=list(re.finditer(pattern,text(path),re.M|re.S));need(len(found)==1,'Exact declaration '+path+': '+pattern);return found[0]
    def source(path,symbol,m):return dict(path=path,symbol=symbol,startLine=text(path).count('\n',0,m.start())+1,
        endLine=text(path).count('\n',0,m.end())+1)
    def number(path,symbol):return int(one(path,r'^#define '+symbol+r'\s+(0x[0-9A-Fa-f]+|\d+)\b[^\n]*')[1],0)
    dependencies.extend(dict(path=p) for p in DEPENDENCIES)
    inherited=compatibility_additions(dependency, BASE+'machines.json', 'inherited')
    machines=machine_definitions(dependency, BASE+'machines.json', 'additions')
    need(len(inherited)==45 and len(machines)==5 and all(len(m['species'])==174 for m in machines), 'Exact admitted machine scope')
    ip='src/data/items.json';items=json.loads(text(ip))['items']
    def metadata(symbol):
        i=next(x for x in items if x['itemId']==symbol)
        block=one(ip,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}')
        sr=source(ip,symbol,block)
        m=dict(id=dict(value='pret:'+symbol),sourceItemSymbol=symbol,sourceItemId=number('include/constants/items.h',symbol),
            name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),
            holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],
            battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=sr)
        return i,m
    icon_path='src/data/item_icon_table.h';graphics='src/data/graphics/items.h'
    icons,generated,aliases=[],{},[]
    def icon(symbol,stem,alias=None):
        row=one(icon_path,r'^    \['+symbol+r'\]\s*= \{(\w+), (\w+)\},$')
        if alias:
            other=one(icon_path,r'^    \[ITEM_'+alias+r'\]\s*= \{'+row[1]+r', '+row[2]+r'\},$')
            aliases.append(dict(item=dict(value='pret:'+symbol),reusedItem=dict(value='pret:ITEM_'+alias),
                source=source(icon_path,symbol,row),reusedSource=source(icon_path,'ITEM_'+alias,other)));return
        image=one(graphics,r'^const u32 '+row[1]+r'\[\] = INCBIN_U32\("([^"\n]+)\.4bpp.lz"\);')[1]+'.png'
        palette=one(graphics,r'^const u32 '+row[2]+r'\[\]\s*= INCBIN_U32\("([^"\n]+)\.gbapal.lz"\);')[1]+'.pal'
        im=Image.open(io.BytesIO(read(image)));need(im.mode=='P' and im.size==(24,24) and max(im.tobytes())<16,'Icon indices')
        lines=text(palette).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Palette')
        colors=[tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
        rgba=bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0))
        stream=io.BytesIO();Image.frombytes('RGBA',(24,24),rgba).save(stream,format='PNG',compress_level=9);png=stream.getvalue()
        entry = dict(item=dict(value='pret:' + symbol), width=24, height=24, texturePath=BASE + stem + '.png', sourcePaths=[image, palette], bindingSource=source(icon_path, symbol, row))
        icons.append(entry);generated[entry['texturePath']] = png
    ordinary=[]
    for symbol,effect,param in [('ITEM_SMOKE_BALL','CAN_ALWAYS_RUN',0),('ITEM_MIRACLE_SEED','GRASS_POWER',10),('ITEM_CHARCOAL','FIRE_POWER',10),('ITEM_MYSTIC_WATER','WATER_POWER',10),('ITEM_YELLOW_FLUTE','NONE',0)]:
        i,m=metadata(symbol);need(i['holdEffect']=='HOLD_EFFECT_'+effect and i['holdEffectParam']==param and i['fieldUseFunc']=='FieldUseFunc_OakStopsYou','Item semantics')
        effects=[];effect_source=None
        if symbol=='ITEM_YELLOW_FLUTE':
            ep='src/data/pokemon/item_effects.h';es='sItemEffect_YellowFlute'
            row=one(ep,r'^static const u8 '+es+r'\[6\] = \{\n    \[3\] = ITEM3_CONFUSION,\n\};')
            need(number('include/constants/item_effects.h','ITEM3_CONFUSION')==1 and i['battleUsage']==1 and i['battleUseFunc']=='BattleUseFunc_Medicine','Yellow Flute effect')
            effects = [0, 0, 0, 1, 0, 0];effect_source=source(ep,es,row)
        else:need(i['battleUsage']==0 and i['battleUseFunc']=='NULL','Held-only item')
        addition={k:m[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}
        addition.update(description=i['description_english'].replace('\\n','\n'),healHp=0,cureStatus=0,effectBytes=effects,useSupport=10,effectSource=effect_source,sourceMetadata=m)
        ordinary.append(addition);icon(symbol,symbol[5:].lower().replace('_','-'))
    tm_rows=[('TM13','ICE_BEAM','IceBeam',58,10,'ICE',4000,'TM14'),('TM23','IRON_TAIL','IronTail',231,15,'STEEL',3500,'TM47'),
        ('TM24','THUNDERBOLT','Thunderbolt',85,15,'ELECTRIC',4000,'TM25'),('TM30','SHADOW_BALL','ShadowBall',247,15,'GHOST',4500,None),('TM35','FLAMETHROWER','Flamethrower',53,15,'FIRE',4000,'TM38')]
    table='src/data/party_menu.h';entries=re.findall(r'MOVE_(\w+)',one(table,r'^static const u16 sTMHMMoves\[\] =\s*\{(.*?)^\};')[1])
    for index,(tm,move,name,n,pp,typ,coins,alias) in enumerate(tm_rows):
        symbol='ITEM_'+tm
        need(machines[index]['item']=='pret:'+symbol, 'Admitted prize machine')
        need(number('include/constants/moves.h','MOVE_'+move)==n and entries[int(tm[2:])-1]==move,'Source TM mapping')
        bp='src/data/battle_moves.h';battle=one(bp,r'^    \[MOVE_'+move+r'\]\s*=\s*\{(.*?)\},')
        need(re.search(r'\.pp\s*=\s*'+str(pp)+r'\s*,',battle[1]) and re.search(r'\.type\s*=\s*TYPE_'+typ+r'\s*,',battle[1]),'Move PP/type')
        icon(symbol,'tm30',alias)
    path=ROOM+'scripts.inc';prefix='CeladonCity_GameCorner_PrizeRoom_EventScript_'
    def script(label):return one(path,r'^'+prefix+label+r'::\n.*?\tend\n')
    def fire(body):
        return re.sub(r'\t\.ifdef FIRERED\n(.*?)\t\.else\n\t\.ifdef LEAFGREEN\n.*?\t\.endif\n\t\.endif\n',lambda m:m[1],body,flags=re.S)
    rows=[]
    for index,(label,name,n,coins,level) in enumerate([('Abra','ABRA',63,180,9),('Clefairy','CLEFAIRY',35,500,8),('DratiniPinsir','DRATINI',147,2800,18),('ScytherDratini','SCYTHER',123,5500,25),('Porygon','PORYGON',137,9999,26)]):
        body=script(label);given=script('Give'+name.title())
        need(re.search(r'setvar VAR_TEMP_1, SPECIES_'+name+r'\n',fire(body[0])) and re.search(r'setvar VAR_TEMP_2, '+str(coins)+r'\n',fire(body[0])) and
            '\tgivemon VAR_TEMP_1, '+str(level)+'\n' in fire(given[0]),'FireRed mon prize')
        rows.append(dict(index=index,reward='pret:SPECIES_'+name,sourceRewardId=n,coins=coins,level=level,heldItem='ITEM_NONE',move=None,
            source=source(path,prefix+label,body),giveSource=source(path,prefix+'Give'+name.title(),given)))
    mons=rows;tmprizes=[];itemprizes=[]
    for index,(tm,move,name,n,pp,typ,coins,alias) in enumerate(tm_rows):
        body=script(tm);need('\tsetvar VAR_TEMP_1, ITEM_'+tm+'\n' in body[0] and '\tsetvar VAR_TEMP_2, '+str(coins)+'\n' in body[0] and '\tbuffermovename STR_VAR_2, MOVE_'+move+'\n' in body[0],'TM price script')
        tmprizes.append(dict(index=index,reward='pret:ITEM_'+tm,sourceRewardId=number('include/constants/items.h','ITEM_'+tm),coins=coins,level=0,heldItem=None,move='pret:MOVE_'+move,source=source(path,prefix+tm,body),giveSource=None))
    for index,(label,item,coins) in enumerate([('SmokeBall','SMOKE_BALL',800),('MiracleSeed','MIRACLE_SEED',1000),('Charcoal','CHARCOAL',1000),('MysticWater','MYSTIC_WATER',1000),('YellowFlute','YELLOW_FLUTE',1600)]):
        body=script(label);need('\tsetvar VAR_TEMP_1, ITEM_'+item+'\n' in body[0] and '\tsetvar VAR_TEMP_2, '+str(coins)+'\n' in body[0],'Item price script')
        itemprizes.append(dict(index=index,reward='pret:ITEM_'+item,sourceRewardId=number('include/constants/items.h','ITEM_'+item),coins=coins,level=0,heldItem=None,move=None,source=source(path,prefix+label,body),giveSource=None))
    map_data=json.loads(text(ROOM+'map.json'));counters=[]
    for kind,clerk,suffix,menu,prizes in [('pokemon','Mons','PokemonPrizes','POKEMON_PRIZES',mons),('machines','TMs','TMPrizes','TMPRIZES',tmprizes),('items','Items','BattleItemPrizes','BATTLE_ITEM_PRIZES',itemprizes)]:
        clerk_label=prefix+'PrizeClerk'+clerk;actor=next((i+1,o) for i,o in enumerate(map_data['object_events']) if o['script']==clerk_label)
        body=script('PrizeClerk'+clerk);need('goto_if_unset FLAG_GOT_COIN_CASE' in body[0] and 'goto_if_questlog EventScript_ReleaseEnd' in body[0],'Source counter guards')
        menu_symbol='MULTICHOICE_GAME_CORNER_'+menu;menu_id=number('include/constants/menu.h',menu_symbol);mp='src/script_menu.c';ms='sMultichoiceList_GameCorner'+suffix
        block=one(mp,r'^static const struct MenuAction '+ms+r'\[\] = \{\n(.*?)^\};');menu_body=block[1]
        if kind=='pokemon':menu_body=re.sub(r'#if defined\(FIRERED\)\n(.*?)#elif defined\(LEAFGREEN\)\n.*?#endif\n',lambda m:m[1],menu_body,flags=re.S)
        labels=re.findall(r'\{ (\w+) \}',menu_body);need(len(labels)==6 and labels[-1]=='gText_NoThanks_2','Six original menu entries')
        one(mp,r'^    \['+menu_symbol+r'\]\s*= MULTICHOICE\('+ms+r'\),$')
        menu_text=[]
        for label in labels:
            line=one('src/strings.c',r'^const u8 '+label+r'\[\] = _\("(.*?)"\);');menu_text.append(json.loads('"'+line[1]+'"'))
        counters.append(dict(kind=kind,actorLocalId=actor[0],x=actor[1]['x'],y=actor[1]['y'],elevation=actor[1]['elevation'],
            clerkScript=clerk_label,menuSymbol=menu_symbol,menuId=menu_id,menuLabels=menu_text,prizes=prizes,source=source(path,clerk_label,body),menuSource=source(mp,ms,block)))
    one('asm/macros/event.inc',r'^\t\.macro giveitem item:req, amount=1\n.*?\t\.endm')
    received=script('CheckReceivedMon')[0]
    for value,label in [(0,'ReceivedMonParty'),(1,'ReceivedMonPC'),(2,'PartyFull')]:
        need('\tgoto_if_eq VAR_RESULT, '+str(value)+', '+prefix+label+'\n' in received,'Original gift result binding')
    for label in ['ChoosePrizeMon','ChoosePrizeTM','ChoosePrizeItem']:
        choice=script(label)[0]
        for cancel in [5,127]:need('\tcase '+str(cancel)+', '+prefix+'EndPrizeExchange\n' in choice,'Original cancel values')
    need('\tsetflag ' not in text(path),'Repeatable prize script has no receipt flag')
    macro=one('asm/macros/event.inc',r'^\t\.macro givemon species:req, level:req, item=ITEM_NONE\n.*?\t\.endm')
    gift=one('src/script_pokemon_util.c',r'^u8 ScriptGiveMon\(.*?^\}')
    need('CreateMon(mon, species, level, 32, 0, 0, OT_ID_PLAYER_ID, 0);' in gift[0] and 'sentToPc = GiveMonToPlayer(mon);' in gift[0],'Random source gift')
    source_rows=[dict(source=source(path,prefix+label,script(label))) for label in ['ConfirmPrizeMon','TryGivePrize','CheckReceivedMon','ReceivedMonParty','ReceivedMonPC','PartyFull','BagFull','EndPrizeExchange']]
    for label in ['ReceivedMonParty','ReceivedMonPC']:need('\tremovecoins VAR_TEMP_2\n' in script(label)[0],'Successful gift deduction')
    need('removecoins' not in script('PartyFull')[0],'Full storage no coin deduction')
    order=script('TryGivePrize')[0];need(order.index('checkcoins')<order.index('checkitemspace')<order.index('removecoins')<order.index('giveitem'),'Item capacity before payment')
    text(ROOM+'text.inc')
    facts=dict(sourceCommit=COMMIT,profile=PROFILE,mapId='map.celadon-city-game-corner-prize-room',sourceMapId=map_data['id'],
        mapSection=map_data['region_map_section'],coinCaseFlag='FLAG_GOT_COIN_CASE',coinCaseFlagId=number('include/constants/flags.h','FLAG_GOT_COIN_CASE'),
        counters=counters,cancelValues=[5,127],quantityPerItemExchange=1,monResultParty=0,monResultPc=1,monResultNoRoom=2,
        itemCapacityCheckedBeforeDeduction=True,monDeductedOnlyAfterReceipt=True,rewardsRepeatable=True,
        giftMacroSource=source('asm/macros/event.inc','givemon',macro),giftFunctionSource=source('src/script_pokemon_util.c','ScriptGiveMon',gift),controlSources=source_rows)
    items_doc=dict(sourceCommit=COMMIT,profile=PROFILE,previousItemCount=132,itemCount=137,additions=ordinary,
        shopsAdded=[],icons=icons[:5])
    machine_doc=dict(sourceCommit=COMMIT,profile='firered-game-corner-teaching-v1',previousSpeciesCount=170,speciesCount=174,
        previousMachineCount=45,machineCount=50,inherited=inherited,
        additions=machines,iconAliases=aliases,icons=icons[5:])
    evidence=dict(sourceCommit=COMMIT,inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=dependencies)
    files={BASE+'items.json':encoded(items_doc),BASE+'machines.json':encoded(machine_doc),BASE+'prizes.json':encoded(facts),BASE+'source-input-hashes.json':encoded(evidence),**generated,**raw}
    return files,items_doc,machine_doc,facts

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    for arg in ['reference','dependency','output']:parser.add_argument(arg,type=Path)
    parser.add_argument('--check',action='store_true');args=parser.parse_args()
    files,items,machines,facts=generate(args.reference,args.dependency)
    for path,data in files.items():
        target=args.output/path
        if args.check:need(target.read_bytes()==data,'Regeneration differs: '+path)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    catalog=load_catalog(args.dependency)
    print(json.dumps(dict(checked=args.check,prizes=len(facts),compatible=[sum(catalog.compatibility(m['item'], species)['canLearn'] for species in m['species']) for m in machines['additions']]),indent=2))
