#!/usr/bin/env python3
"""Bounded Route11 thirteen-machine support from authenticated local Git blobs or cache. No upstream execution."""
import argparse, base64,  importlib.util, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-route11-machines-v1'
def encoded(v): return (json.dumps(v,ensure_ascii=False,indent=2)+'\n').encode()
def pairs(rows):
 d={}
 for k,v in rows:
  if k in d: raise ValueError('Duplicate JSON member: '+k)
  d[k]=v
 return d
def safe(root,p=None):
 if p is not None and (not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/'))): raise ValueError('Unsafe relative path')
 if '..' in pathlib.Path(root).parts: raise ValueError('Parent traversal root rejected')
 value=pathlib.Path(root).absolute() if p is None else pathlib.Path(root).absolute()/p
 for q in [value,*value.parents]:
  if q.is_symlink(): raise ValueError('Symlink path rejected')
 return value
def directory(p):
 p=safe(p)
 for q in [p,*p.parents]:
  if q.exists() and not q.is_dir(): raise ValueError('Non-directory path')
 return p
def git(repo,*args): return subprocess.check_output(['git','-C',str(repo),*args])
def lock_read(path=None):
 b=safe(path or ROOT/'scripts/route11-machines.lock.json').read_bytes()
 d=json.loads(b,object_pairs_hook=pairs)
 if (d['schemaVersion'], d['repository'], d['commit'], d['profile']) != (1, 'pret/pokefirered', COMMIT, PROFILE): raise ValueError('Source identity differs')
 return d
def read_sources(lock,raw,repo=None):
 data={}
 if repo is not None:
     safe(repo)
     if git(repo, 'rev-parse', 'HEAD').decode().strip() != COMMIT:
         raise ValueError('Reference revision differs')
 for e in lock['files']:
  if repo is None: b=safe(raw,e['path']).read_bytes()
  else:
   b=git(repo,'cat-file','blob',COMMIT + ':' + e['path'])
  data[e['path']]=b
 return data

SPECIES = ('BULBASAUR IVYSAUR VENUSAUR CHARMANDER CHARMELEON CHARIZARD SQUIRTLE WARTORTLE BLASTOISE '
 'CATERPIE METAPOD BUTTERFREE WEEDLE KAKUNA BEEDRILL PIDGEY PIDGEOTTO PIDGEOT RATTATA RATICATE '
 'PIKACHU RAICHU SANDSHREW SANDSLASH GEODUDE GRAVELER GOLEM ONIX STEELIX SPEAROW FEAROW MANKEY '
 'EKANS NIDORAN_F NIDORINA NIDORAN_M NIDORINO JIGGLYPUFF MAGIKARP GYARADOS '
 'ARBOK CLEFAIRY ZUBAT GOLBAT ODDISH PARAS PARASECT BELLSPROUT MAGNEMITE GRIMER VOLTORB KOFFING NIDOQUEEN NIDOKING CLEFABLE WIGGLYTUFF '
 'ABRA KADABRA MACHOP DROWZEE SHELLDER HORSEA GOLDEEN STARYU STARMIE SLOWPOKE GLOOM WEEPINBELL').split()
SPECIES += 'MEOWTH PERSIAN GROWLITHE PONYTA TENTACOOL DIGLETT DUGTRIO MAGNETON POLIWAG POLIWHIRL VULPIX'.split()
MACHINES = [('TM39','ROCK_TOMB',317,10),('TM05','ROAR',46,20),('TM09','BULLET_SEED',331,30),('TM46','THIEF',168,10),('TM03','WATER_PULSE',352,20),('TM45','ATTRACT',213,15),('TM43','SECRET_POWER',290,20),('TM28','DIG',91,10),('TM31','BRICK_BREAK',280,15),('TM44','REST',156,10),('HM01','CUT',15,30),('TM34','SHOCK_WAVE',351,20),('HM05','FLASH',148,20)]

def convert(lock,data):
 text={p:b.decode() for p,b in data.items() if not p.endswith('.png')}; inputs={e['path']:e for e in lock['files']};rules=[]
 def match(p,pat):
  ms=list(re.finditer(pat,text[p],re.M|re.S))
  if len(ms)!=1:raise ValueError('Nonunique source match: '+p+' '+pat)
  return ms[0]
 def ref(p,s,m=None):
  return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,
   endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 def constant(p,s):
  m=match(p,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');rules.append(ref(p,s,m));return int(m[1])
 def compatibility(tm):
  result=[];p='src/data/pokemon/tmhm_learnsets.h'
  for s in SPECIES:
   full='SPECIES_'+s;m=match(p,r'^    \['+full+r'\]\s*= TMHM_LEARNSET\((.*?)\),');tokens=re.findall(r'TMHM\((\w+)\)',m[1])
   remaining=re.sub(r'TMHM\(\w+\)|\||\s|0','',m[1])
   if remaining or len(tokens)!=len(set(tokens)) or not tokens and m[1].strip()!='0':raise ValueError('Unsupported TM compatibility syntax')
   result.append(dict(species=ident(full),sourceSpeciesId=constant('include/constants/species.h',full),canLearn=tm in tokens,source=ref(p,full,m)))
  return result
 all_items=json.loads(text['src/data/items.json'],object_pairs_hook=pairs)['items'];machines=[]
 p='src/data/party_menu.h';m=match(p,r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};');mapping=re.findall(r'\bMOVE_\w+\b',m[1]);rules.append(ref(p,'sTMHMMoves',m))
 for name,move,move_id,move_pp in MACHINES:
  symbol='ITEM_'+name;chosen=[i for i in all_items if i['itemId']==symbol]
  if len(chosen)!=1:raise ValueError('Nonunique machine item')
  i=chosen[0];p='src/data/items.json';m=match(p,r'^    \{\n      "english": "'+name+r'",.*?^    \}')
  source=ref(p,symbol,m);definition=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=constant('include/constants/items.h',symbol),name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=source)
  declaration=i['description_english'].replace('\\n','\n');p='src/move_descriptions.c';m=match(p,r'^const u8 gMoveDescription_'+i['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
  wrapped=dict(item=definition,description=json.loads(m[1]),declarationDescription=declaration,descriptionSource=ref(p,'gMoveDescription_'+i['moveId'],m))
  if len(mapping)!=58 or mapping[(50 if name.startswith('HM') else 0)+int(name[2:])-1]!='MOVE_'+move:raise ValueError('Source TM move map differs')
  p='src/data/battle_moves.h';m=match(p,r'^    \[MOVE_'+move+r'\] =\n    \{(.*?)^    \},');pp=int(re.search(r'\.pp = (\d+),',m[1])[1]);rules.append(ref(p,'MOVE_'+move,m))
  source_move_id=constant('include/constants/moves.h','MOVE_'+move)
  if (source_move_id,pp)!=(move_id,move_pp):raise ValueError('Source machine move differs')
  machines.append(dict(machine=wrapped,move=ident('MOVE_'+move),sourceMoveId=source_move_id,movePp=pp,compatibility=compatibility(name+'_'+move)))
 if constant('include/constants/global.h','BAG_TMHM_COUNT')!=58 or constant('include/constants/global.h','BAG_KEYITEMS_COUNT')!=30:raise ValueError('Pocket capacity differs')
 codec_path=ROOT/'scripts/import-lab-battle-ui.py'
 spec=importlib.util.spec_from_file_location('ssanne_machines_existing_png_codec',codec_path);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 icons=[];outputs={}
 for name in ['HM05']:
  p='src/data/item_icon_table.h';m=match(p,r'^    \[ITEM_'+name+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,'ITEM_'+name,m);symbols=m.groups();paths=[]
  for sym in symbols:
   p='src/data/graphics/items.h';g=match(p,r'^const u32 '+sym+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,sym,g));paths.append(g[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image,palette=paths;w,h,idx,_=codec.indexed_png(data[image]);rgba=codec.rgba(idx,codec.palette(data[palette]))
  if (w,h)!=(24,24):raise ValueError('Machine icon dimensions differ')
  png=codec.png(w,h,rgba);png_name='item_'+name.lower()+'.png';outputs[png_name]=png
  icons.append(dict(item=ident('ITEM_'+name),width=w,height=h,pngPath=png_name,source=ref(image,symbols[0]),paletteSource=ref(palette,symbols[1]),bindingSource=binding))
 for p,s,pat in [
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
  ('data/maps/CeruleanCity_Gym/scripts.inc','CeruleanCity_Gym_EventScript_GiveTM03',r'^CeruleanCity_Gym_EventScript_GiveTM03::.*?^\tend$'),
  ('data/maps/CeruleanCity/scripts.inc','CeruleanCity_EventScript_GruntDefeated',r'^CeruleanCity_EventScript_GruntDefeated::.*?^\tend$'),
  ('data/scripts/item_ball_scripts.inc','Route24_EventScript_ItemTM45',r'^Route24_EventScript_ItemTM45::\n\tfinditem ITEM_TM45\n\tend$'),
  ('data/scripts/item_ball_scripts.inc','Route25_EventScript_ItemTM43',r'^Route25_EventScript_ItemTM43::\n\tfinditem ITEM_TM43\n\tend$'),
  ('asm/macros/event.inc','giveitem_msg',r'^\s*\.macro giveitem_msg[^\n]*\n.*?\s*\.endm')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 doc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],rulesSources=rules,machines=machines,icons=icons,limitations=[
  'Thirteen machines across exactly 79 source species; all twelve Gym machines retain their original first 73 compatibility rows.',
  'Historical 70-species machine metadata and compatibility rows are preserved at composition, including source spans.',
  'Existing reward and pickup source references are preserved; ship application owns the new pickup, captain reward, flags and teaching selections.',
  'Successful teaching consumes one TM but never HM01; existing HM moves cannot be overwritten. Source friendship and selected move PP reset are preserved.',
  'Decline/cancel does not invoke a successful transaction; unsupported, incompatible and incomplete selections consume nothing.',
  'No source audio, native-window, playable-route or complete-game acceptance is claimed.'])
 outputs['route11-machines.json']=encoded(doc)
 outputs['manifest.json']=encoded(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
 return outputs

def run(raw,out,repo=None,lock_path=None):
 raw,out=directory(raw),directory(out)
 if raw==out or raw in out.parents or out in raw.parents:raise ValueError('Raw/output overlap')
 if repo is not None:
  r=safe(repo)
  if any(p==r or p in r.parents or r in p.parents for p in [raw,out]):raise ValueError('Reference/output overlap')
 for kind in ['raw','generated']:
  parent=ROOT/'content'/kind
  for protected in parent.iterdir():
   if protected.name=='route11-machines': continue
   p=protected.absolute()
   if any(q==p or q in p.parents or p in q.parents for q in [raw,out]):raise ValueError('Legacy/dependency output overlap')
 lock=lock_read(lock_path);data=read_sources(lock,raw,repo);outputs=convert(lock,data)
 materials={(out,p):b for p,b in outputs.items()}
 if repo is not None:materials.update({(raw,p):b for p,b in data.items()})
 targets=[]
 for (root,p),b in materials.items():
  target=safe(root,p);stage=safe(root,p+'.tmp');directory(target.parent)
  if any(q.exists() and not q.is_file() for q in [target,stage]):raise ValueError('Non-file target')
  targets.append((target,stage,b))
 for target,stage,b in targets:
  target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(b);stage.replace(target)
 return dict(inputCount=len(data),outputCount=len(outputs))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/route11-machines');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/route11-machines.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True))
