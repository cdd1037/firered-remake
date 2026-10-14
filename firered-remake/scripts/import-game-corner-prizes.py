#!/usr/bin/env python3
"""Import FireRed's complete 15-row prize shelf, five items and 50-machine/174-owner teaching delta.
REFERENCE DEPENDENCY OUTPUT [--check]. Only pinned Git blobs and existing dependencies are read.
No upstream code is run; source art is local-only and only six new icon variants are generated.
"""
import argparse, hashlib, io, json, re, subprocess
from pathlib import Path
from PIL import Image

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE = 'content/generated/game-corner-prizes/'
RAW = 'content/raw/game-corner-prizes/'
PROFILE = 'firered-game-corner-prizes-v1'
ROOM = 'data/maps/CeladonCity_GameCorner_PrizeRoom/'
SHARED = {
 'src/data/items.json':'content/raw/bill-items/src/data/items.json',
 'include/constants/items.h':'content/raw/bill-items/include/constants/items.h',
 'src/data/pokemon/item_effects.h':'content/raw/bill-items/src/data/pokemon/item_effects.h',
 'include/constants/item_effects.h':'content/raw/bill-items/include/constants/item_effects.h',
 'src/data/item_icon_table.h':'content/raw/bill-items/src/data/item_icon_table.h',
 'src/data/graphics/items.h':'content/raw/bill-items/src/data/graphics/items.h',
 'src/data/pokemon/tmhm_learnsets.h':'content/raw/bill-machines/src/data/pokemon/tmhm_learnsets.h',
 'src/move_descriptions.c':'content/raw/bill-machines/src/move_descriptions.c',
 'include/constants/moves.h':'content/raw/bill-machines/include/constants/moves.h',
 'src/data/battle_moves.h':'content/raw/bill-machines/src/data/battle_moves.h',
 'src/data/party_menu.h':'content/raw/bill-machines/src/data/party_menu.h',
 'asm/macros/event.inc':'content/raw/bill-machines/asm/macros/event.inc',
 'src/script_menu.c':'content/raw/bill-region/src/script_menu.c',
 'include/constants/menu.h':'content/raw/bill-region/include/constants/menu.h',
 'include/constants/flags.h':'content/raw/bill-region/include/constants/flags.h',
 'src/strings.c':'content/raw/capture-dex/src/strings.c',
 'src/script_pokemon_util.c':'content/raw/regional-service-maps/src/script_pokemon_util.c',
 'src/scrcmd.c':'content/raw/regional-service-maps/src/scrcmd.c',
 'graphics/items/icons/tm_hm.png':'content/raw/bill-machines/graphics/items/icons/tm_hm.png',
 **{ROOM+p:'content/raw/regional-service-maps/'+ROOM+p for p in ['scripts.inc','map.json','text.inc']},
}
DEPENDENCIES = {
 'scripts/department-machine-stock.contract.json':'b61c04443d4b89d5cf029288fd3c88259c5c1f8bc9cbe2a3ac244ec2f1be8088',
 'content/generated/stone-evolution-species/machines.json':'333dc352f5e9fc491c05a333250142cb5c5e24ed4c525240fc864629621fb2ab',
 'content/generated/department-rooftop-items/machines.json':'9bbf60e6f2d74f19f7fd9492310e17619c353854f7cbbdb2f411aae8b5542b8a',
}

def sha(data): return hashlib.sha256(data).hexdigest()
def need(value, message):
    if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode()
def fingerprint(value): return sha(json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode())

def generate(reference, dependency):
    need(subprocess.check_output(['git','rev-parse',COMMIT+'^{tree}'],cwd=reference).decode().strip()==TREE,'Pinned tree')
    inputs,raw,cache,dependencies={}, {}, {}, []
    def read(path):
        if path in cache:return cache[path]
        data=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference)
        local=SHARED.get(path,RAW+path)
        if path in SHARED:need((dependency/local).read_bytes()==data,'Shared source bytes: '+path)
        else:raw[local]=data
        inputs[path]=dict(path=path,localPath=local,shared=path in SHARED,byteLength=len(data),sha256=sha(data),
            gitBlobSha1=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest())
        cache[path]=data;return data
    def text(path):return read(path).decode()
    def one(path,pattern):
        found=list(re.finditer(pattern,text(path),re.M|re.S));need(len(found)==1,'Exact declaration '+path+': '+pattern);return found[0]
    def source(path,symbol,m):return dict(path=path,symbol=symbol,startLine=text(path).count('\n',0,m.start())+1,
        endLine=text(path).count('\n',0,m.end())+1,sha256=inputs[path]['sha256'])
    def number(path,symbol):return int(one(path,r'^#define '+symbol+r'\s+(0x[0-9A-Fa-f]+|\d+)\b[^\n]*')[1],0)
    def dependent(path):
        data=(dependency/path).read_bytes();need(sha(data)==DEPENDENCIES[path],'Dependency bytes '+path)
        dependencies.append(dict(path=path,byteLength=len(data),sha256=sha(data)));return json.loads(data)
    dept,stone,roof=[dependent(p) for p in DEPENDENCIES]
    previous=dept['ownedSpecies']+[dict(symbol=c['species']['value'][5:],sourceSpeciesId=c['sourceSpeciesId']) for c in stone['additions'][0]['compatibility']]
    extra=[dict(symbol='SPECIES_'+name,sourceSpeciesId=n) for name,n in [('DRATINI',147),('DRAGONAIR',148),('DRAGONITE',149),('PORYGON',137)]]
    owned=previous+extra
    need(len(previous)==170 and len(owned)==174 and len({s['symbol'] for s in owned})==174,'Exact owner scope')
    old_ids=sorted([m['item']['value'][5:] for m in stone['additions']]+[m['machine']['item']['sourceItemSymbol'] for m in roof['additions']])
    need(len(old_ids)==len(set(old_ids))==45,'Exact inherited machine scope')
    ip='src/data/items.json';items=json.loads(text(ip))['items']
    def metadata(symbol,machine=False):
        i=next(x for x in items if x['itemId']==symbol)
        block=one(ip,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}')
        sr=source(ip,symbol,block)
        if machine:sr['startLine']+=1;sr['endLine']-=1
        m=dict(id=dict(value='pret:'+symbol),sourceItemSymbol=symbol,sourceItemId=number('include/constants/items.h',symbol),
            name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),
            holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],
            battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=sr)
        return i,m
    def compatibility(item, scope):
        path='src/data/pokemon/tmhm_learnsets.h';cells=[]
        for s in scope:
            row=one(path,r'^    \['+s['symbol']+r'\]\s*=\s*TMHM_LEARNSET\((.*?)\),')
            cells.append(dict(species=dict(value='pret:'+s['symbol']),sourceSpeciesId=s['sourceSpeciesId'],
                canLearn=bool(re.search(r'TMHM\('+item[5:]+r'_',row[1])),source=source(path,s['symbol'],row)))
        return cells
    inherited=[dict(item=dict(value='pret:'+item),compatibility=compatibility(item,extra)) for item in old_ids]
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
        entry=dict(item=dict(value='pret:'+symbol),width=24,height=24,rgbaPath=BASE+stem+'.rgba',pngPath=BASE+stem+'.png',
            rgbaSha256=sha(rgba),pngSha256=sha(png),sourcePaths=[image,palette],bindingSource=source(icon_path,symbol,row))
        icons.append(entry);generated[entry['rgbaPath']]=rgba;generated[entry['pngPath']]=png
    ordinary=[]
    for symbol,effect,param in [('ITEM_SMOKE_BALL','CAN_ALWAYS_RUN',0),('ITEM_MIRACLE_SEED','GRASS_POWER',10),('ITEM_CHARCOAL','FIRE_POWER',10),('ITEM_MYSTIC_WATER','WATER_POWER',10),('ITEM_YELLOW_FLUTE','NONE',0)]:
        i,m=metadata(symbol);need(i['holdEffect']=='HOLD_EFFECT_'+effect and i['holdEffectParam']==param and i['fieldUseFunc']=='FieldUseFunc_OakStopsYou','Item semantics')
        effects=[];effect_source=None
        if symbol=='ITEM_YELLOW_FLUTE':
            ep='src/data/pokemon/item_effects.h';es='sItemEffect_YellowFlute'
            row=one(ep,r'^static const u8 '+es+r'\[6\] = \{\n    \[3\] = ITEM3_CONFUSION,\n\};')
            need(number('include/constants/item_effects.h','ITEM3_CONFUSION')==1 and i['battleUsage']==1 and i['battleUseFunc']=='BattleUseFunc_Medicine','Yellow Flute effect')
            effects=[0,0,0,1,0,0];effect_source=source(ep,es,row)
        else:need(i['battleUsage']==0 and i['battleUseFunc']=='NULL','Held-only item')
        addition={k:m[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}
        addition.update(description=i['description_english'].replace('\\n','\n'),healHp=0,cureStatus=0,effectBytes=effects,useSupport=10,effectSource=effect_source,sourceMetadata=m)
        ordinary.append(addition);icon(symbol,symbol[5:].lower().replace('_','-'))
    machines=[]
    tm_rows=[('TM13','ICE_BEAM','IceBeam',58,10,'ICE',4000,'TM14'),('TM23','IRON_TAIL','IronTail',231,15,'STEEL',3500,'TM47'),
        ('TM24','THUNDERBOLT','Thunderbolt',85,15,'ELECTRIC',4000,'TM25'),('TM30','SHADOW_BALL','ShadowBall',247,15,'GHOST',4500,None),('TM35','FLAMETHROWER','Flamethrower',53,15,'FIRE',4000,'TM38')]
    table='src/data/party_menu.h';entries=re.findall(r'MOVE_(\w+)',one(table,r'^static const u16 sTMHMMoves\[\] =\s*\{(.*?)^\};')[1])
    for tm,move,name,n,pp,typ,coins,alias in tm_rows:
        symbol='ITEM_'+tm;i,m=metadata(symbol,True)
        dp='src/move_descriptions.c';ds='gMoveDescription_'+name;desc=one(dp,r'^const u8 '+ds+r'\[\] = _\("(.*?)"\);')
        need(number('include/constants/moves.h','MOVE_'+move)==n and entries[int(tm[2:])-1]==move,'Source TM mapping')
        bp='src/data/battle_moves.h';battle=one(bp,r'^    \[MOVE_'+move+r'\]\s*=\s*\{(.*?)\},')
        need(re.search(r'\.pp\s*=\s*'+str(pp)+r'\s*,',battle[1]) and re.search(r'\.type\s*=\s*TYPE_'+typ+r'\s*,',battle[1]),'Move PP/type')
        machines.append(dict(machine=dict(item=m,description=json.loads('"'+desc[1]+'"'),declarationDescription=i['description_english'].replace('\\n','\n'),descriptionSource=source(dp,ds,desc)),
            move=dict(value='pret:MOVE_'+move),sourceMoveId=n,movePp=pp,compatibility=compatibility(symbol,owned)))
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
    facts=dict(sourceCommit=COMMIT,sourceTree=TREE,profile=PROFILE,mapId='map.celadon-city-game-corner-prize-room',sourceMapId=map_data['id'],
        mapSection=map_data['region_map_section'],coinCaseFlag='FLAG_GOT_COIN_CASE',coinCaseFlagId=number('include/constants/flags.h','FLAG_GOT_COIN_CASE'),
        counters=counters,cancelValues=[5,127],quantityPerItemExchange=1,monResultParty=0,monResultPc=1,monResultNoRoom=2,
        itemCapacityCheckedBeforeDeduction=True,monDeductedOnlyAfterReceipt=True,rewardsRepeatable=True,
        giftMacroSource=source('asm/macros/event.inc','givemon',macro),giftFunctionSource=source('src/script_pokemon_util.c','ScriptGiveMon',gift),controlSources=source_rows)
    items_doc=dict(sourceCommit=COMMIT,profile=PROFILE,previousItemCount=132,itemCount=137,additions=ordinary,
        additionsSemanticSha256=fingerprint(ordinary),shopsAdded=[],icons=icons[:5])
    machine_doc=dict(sourceCommit=COMMIT,profile='firered-game-corner-teaching-v1',previousSpeciesCount=170,speciesCount=174,
        previousMachineCount=45,machineCount=50,inherited=inherited,inheritedSemanticSha256=fingerprint(inherited),
        additions=machines,additionsSemanticSha256=fingerprint(machines),iconAliases=aliases,icons=icons[5:])
    evidence=dict(sourceCommit=COMMIT,sourceTree=TREE,inputs=[inputs[p] for p in sorted(inputs)],dependencyInputs=dependencies)
    files={BASE+'items.json':encoded(items_doc),BASE+'machines.json':encoded(machine_doc),BASE+'prizes.json':encoded(facts),BASE+'source-input-hashes.json':encoded(evidence),**generated,**raw}
    files['scripts/game-corner-prizes.lock.json']=encoded(dict(sourceCommit=COMMIT,sourceTree=TREE,profile=PROFILE,importerSha256=sha(Path(__file__).read_bytes()),
        files=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(files.items())]))
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
    print(json.dumps(dict(checked=args.check,items=items['additionsSemanticSha256'],machines=machines['additionsSemanticSha256'],
        inherited=machines['inheritedSemanticSha256'],prizes=fingerprint(facts),compatible=[sum(c['canLearn'] for c in m['compatibility']) for m in machines['additions']],
        hashes={p:sha(b) for p,b in files.items()}),indent=2))
