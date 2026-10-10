#!/usr/bin/env python3
"""Bounded, authenticated Tower/Fuji metadata; does not execute upstream code or enable uses."""
import argparse,base64,hashlib,importlib.util,json,pathlib,re,subprocess,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-pokemon-tower-items-v1';LOCK='8605005350d51d4646abb8573071ef73a2549fb2f2d01a3f2be810b4943ea94a'
CODEC='b03954e07eb38a9b2dee1f98cc8ed9f92a1f35b32fe9197a16795db4e527795a'
ITEMS=('ELIXIR','CLEANSE_TAG','X_ACCURACY','SOOTHE_BELL','POKE_FLUTE')
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def unique(rows):
 result={}
 for k,v in rows:
  if k in result:raise ValueError('Duplicate JSON key '+k)
  result[k]=v
 return result
def parse(b):return json.loads(b,object_pairs_hook=unique)
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe relative path')
 f=pathlib.Path(root).absolute()/p
 if any(x.is_symlink() for x in (f,*f.parents)):raise ValueError('Symlink rejected')
 return f
def read(root,p,h,n=None):
 f=safe(root,p)
 if f.stat().st_size>4*1024*1024:raise ValueError('Bounded file exceeded')
 b=f.read_bytes()
 if sha(b)!=h or n is not None and len(b)!=n:raise ValueError('Pinned bytes differ: '+p)
 return b

def outputs(lock,data,project):
 text={p:b.decode() for p,b in data.items() if not p.endswith('.png')};entries={e['path']:e for e in lock['files']};rules=[]
 def match(p,pattern):
  found=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(found)!=1:raise ValueError('Nonunique source match '+p+' '+pattern)
  return found[0]
 def ref(p,s,m=None):return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1,sha256=entries[p]['sha256'])
 def rule(p,s,pattern):
  m=match(p,pattern);r=ref(p,s,m);rules.append(r);return r
 def function(p,s):return rule(p,s,r'^(?:static )?[^\n;]*\b'+re.escape(s)+r'\([^;\n]*\)\n\{.*?^\}')
 def block(p,s):return rule(p,s,r'^'+re.escape(s)+r'::\s*\n(.*?)(?=^\w+::|\Z)')
 def ident(s):return dict(value='pret:'+s)
 def number(p,s):
  m=match(p,r'^#define '+re.escape(s)+r'[ \t]+(0x[0-9A-Fa-f]+|\d+|\(FLAG_HIDDEN_ITEMS_START \+\s+\d+\))[ \t]*(?://[^\n]*)?$');rules.append(ref(p,s,m))
  return number(p,'FLAG_HIDDEN_ITEMS_START')+int(re.search(r'\+\s+(\d+)',m[1])[1]) if m[1].startswith('(') else int(m[1],0)
 codecpath=safe(project,'scripts/import-lab-battle-ui.py');read(project,'scripts/import-lab-battle-ui.py',CODEC)
 spec=importlib.util.spec_from_file_location('tower_item_codec',codecpath);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 declarations=parse(data['src/data/items.json'])['items'];items=[];icons=[];result={}
 for short in ITEMS:
  symbol='ITEM_'+short;i=next(x for x in declarations if x['itemId']==symbol);p='src/data/items.json'
  m=match(p,r'^    \{\n      "english": "[^\n]*",\n      "itemId": "'+symbol+r'",.*?^    \}');source=ref(p,symbol,m)
  definition=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=number('include/constants/items.h',symbol),name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=source)
  description=i['description_english'].replace('\\n','\n');wrapped=dict(item=definition,description=description,declarationDescription=description,descriptionSource=source)
  effects=[];effectsource=None
  if short in ('ELIXIR','X_ACCURACY'):
   p='src/data/pokemon/item_effects.h';camel='Elixir' if short=='ELIXIR' else 'XAccuracy';m=match(p,r'^static const u8 sItemEffect_'+camel+r'\[(\d+)\] = \{\n(.*?)^\};');actual=' '.join(re.sub(r'//[^\n]*','',m[2]).split());effectsource=ref(p,'sItemEffect_'+camel,m)
   if short=='ELIXIR':
    assert actual=='[4] = ITEM4_HEAL_PP_ALL, [6] = 10,';effects=[0,0,0,0,number('include/constants/item_effects.h','ITEM4_HEAL_PP_ALL'),0,10]
   else:
    assert actual=='[2] = 1 << 4, [5] = ITEM5_FRIENDSHIP_LOW | ITEM5_FRIENDSHIP_MID, STAT_BOOST_FRIENDSHIP_CHANGE,'
    macro=match(p,r'^#define STAT_BOOST_FRIENDSHIP_CHANGE\s+.*?mid \*/');assert re.findall(r'= (\d+)',macro[0])==['1','1'];rules.append(ref(p,'STAT_BOOST_FRIENDSHIP_CHANGE',macro))
    effects=[0,0,16,0,0,number('include/constants/item_effects.h','ITEM5_FRIENDSHIP_LOW')|number('include/constants/item_effects.h','ITEM5_FRIENDSHIP_MID'),1,1]
   assert len(effects)==int(m[1])
  items.append(dict(metadata=wrapped,effectBytes=effects,effectSource=effectsource))
  p='src/data/item_icon_table.h';m=match(p,r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,m);symbols=m.groups();paths=[]
  for asset in symbols:
   p='src/data/graphics/items.h';m=match(p,r'^const u32 '+asset+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,asset,m));paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image,palette=paths;w,h,indices,_=codec.indexed_png(data[image]);rgba=codec.rgba(indices,codec.palette(data[palette]));png=codec.png(w,h,rgba);assert(w,h)==(24,24)
  pngpath=symbol.lower()+'.png';result[pngpath]=png;icons.append(dict(item=ident(symbol),width=w,height=h,rgbaBase64=base64.b64encode(rgba).decode(),rgbaSha256=sha(rgba),pngPath=pngpath,pngSha256=sha(png),source=ref(image,symbols[0]),paletteSource=ref(palette,symbols[1]),bindingSource=binding))
 pickups=[]
 for floor in range(1,8):
  p=f'data/maps/PokemonTower_{floor}F/map.json';mapdata=parse(data[p]);mapsource=ref(p,mapdata['id'])
  for index,a in enumerate(mapdata['object_events']):
   if a['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
   s=a['script'];sp='data/scripts/item_ball_scripts.inc';m=match(sp,r'^'+s+r'::\s*\n(.*?)(?=^\w+::|\Z)');award=re.fullmatch(r'\s*finditem (ITEM_\w+)\s+end\s*',m[1]);assert award,s
   pickups.append(dict(map=mapdata['id'],kind='visible',localId=index+1,eventIndex=None,x=a['x'],y=a['y'],elevation=a['elevation'],flag=a['flag'],sourceFlagId=number('include/constants/flags.h',a['flag']),initiallyHidden=False,underfoot=False,requiresItemfinder=False,allowsOrdinaryInteraction=True,item=ident(award[1]),quantity=1,script=s,scriptCommand='finditem',mapSource=mapsource,scriptSource=ref(sp,s,m)))
  for index,e in enumerate(mapdata['bg_events']):
   if e['type']!='hidden_item':continue
   pickups.append(dict(map=mapdata['id'],kind='hidden',localId=None,eventIndex=index,x=e['x'],y=e['y'],elevation=e['elevation'],flag=e['flag'],sourceFlagId=number('include/constants/flags.h',e['flag']),initiallyHidden=False,underfoot=e['underfoot'],requiresItemfinder=e['underfoot'],allowsOrdinaryInteraction=not e['underfoot'],item=ident(e['item']),quantity=e['quantity'],script=None,scriptCommand=None,mapSource=mapsource,scriptSource=None))
 assert len(pickups)==10 and sum(p['kind']=='visible' for p in pickups)==8 and sum(p['allowsOrdinaryInteraction'] for p in pickups)==9
 assert all(p['quantity']==1 for p in pickups)
 p='data/maps/LavenderTown_VolunteerPokemonHouse/map.json';house=parse(data[p]);fuji=house['object_events'][0];assert (fuji['graphics_id'],fuji['x'],fuji['y'],fuji['elevation'],fuji['flag'])==('OBJ_EVENT_GFX_MR_FUJI',3,3,3,'FLAG_HIDE_POKEHOUSE_FUJI')
 rewardsource=block('data/maps/LavenderTown_VolunteerPokemonHouse/scripts.inc',fuji['script'])
 reward=dict(item=ident('ITEM_POKE_FLUTE'),quantity=1,map=house['id'],localId=1,x=3,y=3,elevation=3,actorHideFlag=fuji['flag'],sourceActorHideFlagId=number('include/constants/flags.h',fuji['flag']),obtainedFlag='FLAG_GOT_POKE_FLUTE',sourceObtainedFlagId=number('include/constants/flags.h','FLAG_GOT_POKE_FLUTE'),fanfare='MUS_OBTAIN_KEY_ITEM',sourceOrder=['goto_if_set FLAG_GOT_POKE_FLUTE','msgbox IdLikeYouToHaveThis','checkitemspace ITEM_POKE_FLUTE','goto_if_eq VAR_RESULT FALSE NoRoomForPokeFlute','setflag FLAG_GOT_POKE_FLUTE','giveitem_msg ITEM_POKE_FLUTE 1 MUS_OBTAIN_KEY_ITEM','msgbox ExplainPokeFlute'],mapSource=ref(p,house['id']),scriptSource=rewardsource)
 flute_sources=[function('src/item_use.c',s) for s in ('FieldUseFunc_PokeFlute','Task_PlayPokeFlute','Task_DisplayPokeFluteMessage','BattleUseFunc_PokeFlute')]
 flute_sources+=[function('src/battle_main.c','HandleAction_UseItem'),rule('src/battle_script_commands.c','VARIOUS_CHECK_POKEFLUTE',r'^    case VARIOUS_CHECK_POKEFLUTE:.*?(?=^    case VARIOUS_WAIT_FANFARE:)'),block('data/battle_scripts_2.s','BattleScript_UsePokeFlute'),block('data/battle_scripts_2.s','BattleScript_PokeFluteWakeUp'),block('data/battle_scripts_2.s','BattleScript_PokeFluteEnd'),function('src/pokemon.c','ExecuteTableBasedItemEffect'),function('src/pokemon.c','PokemonUseItemEffects')]
 assert number('include/constants/party_menu.h','AILMENT_FNT')==7
 flute=dict(item=ident('ITEM_POKE_FLUTE'),consumed=False,registrable=False,fieldUsesAwakeningOnEntirePlayerParty=True,fieldChecksSoundproof=False,battleTargetsBothParties=True,battleChecksSoundproof=True,battleClearsActiveNightmare=True,sleepStatusMask=7,nightmareStatusMask=1<<27,battleSleepingPartyStatusWrite=0,battleConsumesAction=True,fanfare='MUS_POKE_FLUTE',fanfareOnlyWhenWaking=True,fieldNoEffectMessage='gText_PlayedPokeFluteCatchy',battleNoEffectMessage='STRINGID_POKEFLUTECATCHY',executionImplemented=False,sources=flute_sources)
 bell=next(p for p in pickups if p['underfoot']);assert (bell['map'],bell['x'],bell['y'],bell['item'])==('MAP_POKEMON_TOWER_7F',11,4,ident('ITEM_SOOTHE_BELL'))
 underfoot=dict(item=bell['item'],requiredItem=ident('ITEM_ITEMFINDER'),sourceRequiredItemId=number('include/constants/items.h','ITEM_ITEMFINDER'),map=bell['map'],x=11,y=4,elevation=3,flag=bell['flag'],sourceFlagId=bell['sourceFlagId'],blocksOrdinaryInteraction=True,requiresExactPlayerTile=True,forcedQuantity=1,occupyingActorLocalId=1,occupyingActorHideFlag='FLAG_HIDE_TOWER_FUJI',sourceOccupyingActorHideFlagId=number('include/constants/flags.h','FLAG_HIDE_TOWER_FUJI'),hasExplicitRescueFlagCheck=False,setPickupFlagOnlyAfterSuccessfulAdd=True,executionImplemented=False,sources=[function('src/field_control_avatar.c','GetInteractedBackgroundEventScript'),function('src/itemfinder.c','HiddenItemIsWithinRangeOfPlayer'),function('src/itemfinder.c','SetUnderfootHiddenItem'),block('data/scripts/itemfinder.inc','EventScript_ItemfinderDigUpUnderfootItem'),block('data/scripts/itemfinder.inc','EventScript_DigUpItemPutInPocket'),block('data/scripts/itemfinder.inc','EventScript_DigUpItemBagIsFull'),block('data/scripts/obtain_item.inc','EventScript_TryPickUpHiddenItem'),block('data/maps/PokemonTower_7F/scripts.inc','PokemonTower_7F_EventScript_MrFuji')])
 notes=[dict(item=ident('ITEM_CLEANSE_TAG'),status='metadata-only',rules=['Lead party held item reduces ordinary encounter rate to rate*2/3 using integer arithmetic.','Cooldown also changes: minSteps += minSteps/3; encRate -= encRate/3. Preserve order and truncation.'],sources=[function('src/wild_encounter.c','ApplyCleanseTagEncounterRateMod'),function('src/wild_encounter.c','IsLeadMonHoldingCleanseTag'),function('src/wild_encounter.c','HandleWildEncounterCooldown')]),dict(item=ident('ITEM_SOOTHE_BELL'),status='metadata-only',rules=['Positive friendship delta is multiplied by150/100 with integer truncation before Luxury Ball and matching met-location bonuses.','Special pickup requires Itemfinder; no adjacent-A pickup registration.'],sources=[function('src/pokemon.c','AdjustFriendship')]),dict(item=ident('ITEM_ELIXIR'),status='metadata-only',rules=['Original effect bytes [0,0,0,0,8,0,10] preserve10 PP restoration for all moves; no use handler is enabled.'],sources=[items[0]['effectSource']]),dict(item=ident('ITEM_X_ACCURACY'),status='metadata-only',rules=['Original effect bytes [0,0,16,0,0,96,1,1] preserve accuracy stage and friendship effect data; no battle use handler is enabled.'],sources=[items[2]['effectSource']])]
 for s in ('AddBagItem','RemoveBagItem'):function('src/item.c',s)
 limitations=['All56 predecessor item entries, five shop stocks and limits are preserved exactly when merged; five additions produce61 metadata entries; inherited Big Mushroom remains unchanged.','This increment supplies source metadata, pickup/reward contracts and icons only. It does not enable an Application, field-use, battle-use, save or generic inventory profile.','All10 pickups remain authenticated; OrdinaryPickups deliberately returns only eight visible and one ordinary hidden event. Soothe Bell requires Itemfinder on the vacated Fuji tile.','Fuji reward is a separate one-time home conversation, after capacity succeeds; no flute is awarded by Tower rescue alone. Original source sets obtained flag after checkitemspace and before giveitem_msg.','Poke Flute is reusable, cannot be registered/given/sold/tossed, and is not an ordinary single-target medicine. Its field and battle behavior differs; both remain contracts.','Elixir, X Accuracy, Cleanse Tag and Soothe Bell mechanics remain metadata-only; Big Mushroom item use and later mushroom tutor service are not enabled.','Existing inherited item uses and machine compatibility records are not changed. Route12/16 Snorlax events are outside this Tower/Fuji increment. Itemfinder is a source dependency, not a newly granted item.']
 result['pokemon-tower-items.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],rulesSources=rules,items=items,icons=icons,limitations=limitations))
 result['pokemon-tower-pickups.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,pickups=pickups,limitations=limitations))
 result['pokemon-tower-item-contracts.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,fluteReward=reward,pokeFlute=flute,sootheBell=underfoot,itemUseNotes=notes,limitations=limitations))
 return result

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--dependency-project-root',required=True,type=pathlib.Path);ap.add_argument('--reference-root',type=pathlib.Path);ap.add_argument('--check',action='store_true');args=ap.parse_args()
 lock=parse(read(ROOT,'scripts/pokemon-tower-items.lock.json',LOCK));assert(lock['commit'],lock['tree'],lock['profile'],len(lock['files']))==(COMMIT,TREE,PROFILE,44);data={}
 for e in lock['files']:
  if args.reference_root:
   b=subprocess.check_output(['git','-C',str(args.reference_root),'show',COMMIT+':'+e['path']]);assert(len(b),sha(b))==(e['byteLength'],e['sha256'])
   if not args.check:
    dest=safe(ROOT,'content/raw/pokemon-tower-items/'+e['path']);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
  b=read(ROOT,'content/raw/pokemon-tower-items/'+e['path'],e['sha256'],e['byteLength']);assert hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()==e['gitBlobSha1'];data[e['path']]=b
 result=outputs(lock,data,args.dependency_project_root)
 manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,tree=TREE,lockSha256=LOCK,inputCount=len(data),outputs=[dict(path=p,sha256=sha(b),byteLength=len(b)) for p,b in sorted(result.items())]);result['manifest.json']=canonical(manifest)
 for p,b in result.items():
  dest=safe(ROOT,'content/generated/pokemon-tower-items/'+p)
  if args.check:
   if dest.read_bytes()!=b:raise ValueError('Generated bytes differ '+p)
  else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 print(json.dumps(dict(passed=True,sourceFiles=len(data),outputs=len(result),addedItems=5,totalMetadataItems=61,pickups=10,ordinaryPickups=9,executableUsesAdded=0,check=args.check)))
if __name__=='__main__':main()
