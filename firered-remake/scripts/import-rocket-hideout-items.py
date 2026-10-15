#!/usr/bin/env python3
"""Bounded source-pinned item/machine/pickup importer. Never executes source repository code."""
from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse,base64,copy,importlib.util,json,pathlib,re,subprocess,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
import sys
sys.path.insert(0, str(ROOT / "scripts"))
from machine_catalog import load_catalog, machine_reference, compatibility_reference, admission_view, admission_species
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-rocket-hideout-items-v1'
DEPENDENCIES=(
    'scripts/import-lab-battle-ui.py',
)
ITEMS=('LIFT_KEY','SILPH_SCOPE','BLACK_GLASSES','X_SPEED','CALCIUM','NET_BALL','NEST_BALL','TM12','TM21','TM49')
TEACHING={'TM12':('TAUNT','Taunt'),'TM21':('FRUSTRATION','Frustration'),'TM49':('SNATCH','Snatch')}
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False, default=item_json)+'\n').encode()
def unique(rows):
 r={}
 for k,v in rows:
  if k in r:raise ValueError('Duplicate JSON key '+k)
  r[k]=v
 return r
def parse(b):return json.loads(b,object_pairs_hook=unique)
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe relative path')
 f=pathlib.Path(root).absolute()/p
 if any(x.is_symlink() for x in (f,*f.parents)):raise ValueError('Symlink rejected')
 return f
def read(root, p):
 f=safe(root,p)
 if f.stat().st_size>4*1024*1024:raise ValueError('Bounded file exceeded')
 b=f.read_bytes()
 return b

def outputs(lock,data,project):
 text={p:b.decode() for p,b in data.items() if not p.endswith('.png')}; entries={e['path']:e for e in lock['files']}; rules=[]
 def match(p,pattern):
  matches=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(matches)!=1:raise ValueError('Nonunique source match '+p+' '+pattern)
  return matches[0]
 def ref(p,s,m=None):return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,endLine=max(1,text.get(p,'').count('\n')) if m is None else text[p].count('\n',0,m.end())+1)
 def ident(s):return dict(value='pret:'+s)
 def number(p,s):
  m=match(p,r'^#define '+s+r'[ \t]+(0x[0-9A-Fa-f]+|\d+|\(FLAG_HIDDEN_ITEMS_START \+\s+\d+\))[ \t]*(?://[^\n]*)?$');rules.append(ref(p,s,m))
  return number(p,'FLAG_HIDDEN_ITEMS_START')+int(re.search(r'\+\s+(\d+)',m[1])[1]) if m[1].startswith('(') else int(m[1],0)
 target='content/generated/rocket-hideout-items/rocket-hideout-machines.json'
 machines=[admission_view(project,target,'/machines/'+str(index)) for index in range(18)]
 species=[species[5:] for species in admission_species(project,target,'/machines/0')]
 codec_path=safe(project,'scripts/import-lab-battle-ui.py');read(project,'scripts/import-lab-battle-ui.py')
 spec=importlib.util.spec_from_file_location('rocket_item_codec',codec_path);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 items=[];icons=[];result={};declarations=parse(data['src/data/items.json'])['items']
 effectp='src/data/pokemon/item_effects.h';constants='include/constants/item_effects.h'
 # Exact source constants/macros are authenticated above. Resolve only these two supported declaration shapes.
 def effect(symbol):
  short={'CALCIUM':'Calcium','X_SPEED':'XSpeed'}.get(symbol)
  if short is None:return [],None
  m=match(effectp,r'^static const u8 sItemEffect_'+short+r'\[(\d+)\] = \{\n(.*?)^\};')
  if symbol=='CALCIUM':
   expected='[5] = ITEM5_EV_SPATK | ITEM5_FRIENDSHIP_ALL, [6] = ITEM6_ADD_EV, VITAMIN_FRIENDSHIP_CHANGE(7),'
   actual=' '.join(m[2].split());assert actual==expected
   values=[0]*10;values[5]=number(constants,'ITEM5_EV_SPATK')|number(constants,'ITEM5_FRIENDSHIP_LOW')|number(constants,'ITEM5_FRIENDSHIP_MID')|number(constants,'ITEM5_FRIENDSHIP_HIGH');values[6]=number(constants,'ITEM6_ADD_EV')
   macro=match(effectp,r'^#define VITAMIN_FRIENDSHIP_CHANGE\(i\).*?high \*/');changes=re.findall(r'= (\d+)',macro[0]);assert changes==['5','3','2'];values[7:]=map(int,changes);rules.append(ref(effectp,'VITAMIN_FRIENDSHIP_CHANGE',macro))
  else:
   actual=' '.join(re.sub(r'//[^\n]*','',m[2]).split());assert actual=='[1] = 1, [5] = ITEM5_FRIENDSHIP_LOW | ITEM5_FRIENDSHIP_MID, STAT_BOOST_FRIENDSHIP_CHANGE,'
   values=[0,1,0,0,0,number(constants,'ITEM5_FRIENDSHIP_LOW')|number(constants,'ITEM5_FRIENDSHIP_MID'),1,1]
   macro=match(effectp,r'^#define STAT_BOOST_FRIENDSHIP_CHANGE\s+.*?mid \*/');assert re.findall('= (\\d+)', macro[0]) == ['1', '1'];rules.append(ref(effectp,'STAT_BOOST_FRIENDSHIP_CHANGE',macro))
  assert len(values)==int(m[1]);return values,ref(effectp,'sItemEffect_'+short,m)
 mappingp='src/data/party_menu.h';m=match(mappingp,r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};');mapping=re.findall(r'\bMOVE_\w+\b',m[1]);assert len(mapping)==58;rules.append(ref(mappingp,'sTMHMMoves',m))
 for short in ITEMS:
  symbol='ITEM_'+short;i=next(x for x in declarations if x['itemId']==symbol);p='src/data/items.json'
  m=match(p,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}');source=ref(p,symbol,m)
  definition=shared_item(symbol)
  description=i['description_english'].replace('\\n','\n')
  if short in TEACHING:
   move,camel=TEACHING[short];assert mapping[int(short[2:])-1]=='MOVE_'+move
   p='src/move_descriptions.c';m=match(p,r'^const u8 gMoveDescription_'+camel+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1])
  wrapped=dict(item=definition,description=description)
  effects,effect_source=effect(short);items.append(dict(metadata=wrapped, effectBytes=effects))
  if short in TEACHING:
   p='src/data/battle_moves.h';m=match(p,r'^    \[MOVE_'+move+r'\] =\n    \{(.*?)^    \},');pp=int(re.search(r'\.pp = (\d+),',m[1])[1]);rules.append(ref(p,'MOVE_'+move,m))
  p='src/data/item_icon_table.h';m=match(p,r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,m);icon_symbols=m.groups();paths=[]
  for asset in icon_symbols:
   p='src/data/graphics/items.h';m=match(p,r'^const u32 '+asset+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,asset,m));paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image,palette=paths;w,h,indices,_=codec.indexed_png(data[image]);rgba=codec.rgba(indices,codec.palette(data[palette]));png=codec.png(w,h,rgba);assert(w,h)==(24,24)
  pngpath=symbol.lower()+'.png';result[pngpath]=png;icons.append(dict(item=ident(symbol),width=w,height=h,pngPath=pngpath,source=ref(image,icon_symbols[0]),paletteSource=ref(palette,icon_symbols[1]),bindingSource=binding))
 pickups=[]
 for floor in range(1,5):
  name='RocketHideout_B'+str(floor)+'F';p='data/maps/'+name+'/map.json';mapdata=parse(data[p]);mapsource=ref(p,'MAP_ROCKET_HIDEOUT_B'+str(floor)+'F')
  for index,actor in enumerate(mapdata['object_events']):
   if actor['graphics_id']!='OBJ_EVENT_GFX_ITEM_BALL':continue
   script=actor['script'];sp='data/maps/RocketHideout_B4F/scripts.inc' if script.endswith(('LiftKey','SilphScope')) else 'data/scripts/item_ball_scripts.inc'
   m=match(sp,r'^'+script+r'::\s*\n(.*?)(?=^\w+::|\Z)');award=re.search(r'^\s*(finditem|giveitem) (ITEM_\w+)(?:, (\d+))?\s*$',m[1],re.M);assert award,script
   initial=actor['flag'] in ('FLAG_HIDE_LIFT_KEY','FLAG_HIDE_SILPH_SCOPE');flag=actor['flag']
   pickups.append(dict(map=mapdata['id'],kind='visible',localId=index+1,eventIndex=None,x=actor['x'],y=actor['y'],elevation=actor['elevation'],flag=flag,sourceFlagId=number('include/constants/flags.h',flag),initiallyHidden=initial,underfoot=False,item=ident(award[2]),quantity=int(award[3] or 1),script=script,scriptCommand=award[1],mapSource=mapsource,scriptSource=ref(sp,script,m)))
  for index,e in enumerate(mapdata['bg_events']):
   if e['type']!='hidden_item':continue
   pickups.append(dict(map=mapdata['id'],kind='hidden',localId=None,eventIndex=index,x=e['x'],y=e['y'],elevation=e['elevation'],flag=e['flag'],sourceFlagId=number('include/constants/flags.h',e['flag']),initiallyHidden=False,underfoot=e['underfoot'],item=ident(e['item']),quantity=e['quantity'],script=None,scriptCommand=None,mapSource=mapsource,scriptSource=None))
 if len(pickups)!=18 or sum(p['kind']=='visible' for p in pickups)!=14 or any(p['quantity']!=1 for p in pickups):raise ValueError('Pickup scope differs')
 for flag in ('FLAG_HIDE_LIFT_KEY','FLAG_HIDE_SILPH_SCOPE'):
  p='data/event_scripts.s';m=match(p,r'^\s*setflag '+flag+r'\s*$');rules.append(ref(p,flag,m))
 for p,s,pattern in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','RemoveBagItem',r'^bool8 RemoveBagItem\(.*?^\}'),('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}'),('src/pokemon.c','CanMonLearnTMHM',r'^u32 CanMonLearnTMHM\([^;]*?\n\{.*?^\}'),('src/battle_script_commands.c','Cmd_handleballthrow',r'^static void Cmd_handleballthrow\(void\)\n\{.*?^\}')]:rules.append(ref(p,s,match(p,pattern)))
 limitations=['56 items preserve all46 predecessor items and five shop stocks exactly. No new shop stock.','18 source machines have99 compatibility cells; the preceding15x98 cells are identical.','TM12 Taunt, TM21 Frustration and TM49 Snatch remain inventory/compatibility metadata; teaching is rejected until battle execution is admitted.','Calcium and X Speed retain original effect bytes but their use is unavailable. PP Up and Max Ether remain inherited unavailable uses.','Lift Key/Silph Scope are key items; original progression flags and atomic award integration belong to Application. BlackGlasses held battle admission belongs to the Rocket battle profile.','Net/Nest Ball capture requires the separate source multiplier and capture integration. Game Corner coins and slot games are excluded.']
 result['rocket-hideout-items.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],items=items,icons=icons,limitations=limitations))
 result['rocket-hideout-machines.json']=canonical(dict(schemaVersion=1,profile='firered-rocket-hideout-machines-v1',sourceCommit=COMMIT,inputs=lock['files'],machines=machines,icons=icons,limitations=limitations))
 result['rocket-hideout-pickups.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,pickups=pickups,limitations=limitations))
 return result

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--dependency-project-root',required=True,type=pathlib.Path);ap.add_argument('--reference-root',type=pathlib.Path);ap.add_argument('--check',action='store_true');args=ap.parse_args()
 lock=parse(read(ROOT,'scripts/rocket-hideout-items.lock.json'));assert(lock['commit'], lock['profile'], len(lock['files'])) == (COMMIT, PROFILE, 41);data={}
 for e in lock['files']:
  if args.reference_root:
   b=subprocess.check_output(['git','-C',str(args.reference_root),'show',COMMIT+':'+e['path']])
   dest=safe(ROOT,'content/raw/pokefirered/037335f4/'+e['path']);dest.parent.mkdir(parents=True,exist_ok=True)
   if not args.check:dest.write_bytes(b)
  b=read(ROOT,'content/raw/pokefirered/037335f4/'+e['path']);data[e['path']]=b
 result=outputs(lock,data,args.dependency_project_root)
 manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(data),outputs=[dict(path=p) for p,b in sorted(result.items())]);result['manifest.json']=canonical(manifest)
 for p,b in result.items():
  dest=safe(ROOT,'content/generated/rocket-hideout-items/'+p)
  if args.check:
   if dest.read_bytes()!=b:raise ValueError('Generated bytes differ '+p)
  else:dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 print(json.dumps(dict(passed=True,sourceFiles=len(data),outputs=len(result),items=10,totalItems=56,machines=18,species=99,pickups=18,check=args.check), default=item_json))
if __name__=='__main__':main()
