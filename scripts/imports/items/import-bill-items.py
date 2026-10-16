#!/usr/bin/env python3
"""Authenticated Bill quest rewards, northern pickups and seen-only computer pictures, including the seen-bitfield implementation, from pinned Git blobs or cache. No upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from item_effects import read_effects_text
from item_source import ItemSource
from image_io import indexed_png, png as encode_png
from presentation_pixels import palette as decode_palette, rgba as decode_rgba
from item_catalog import item_definition as shared_item, item_json
import argparse, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
sys.path.insert(0, str(ROOT/'scripts/lib'))
from item_icon_exports import output_materials
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-bill-items-v1'
def encoded(v): return (json.dumps(v,ensure_ascii=False,indent=2, default=item_json)+'\n').encode()
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
 b=safe(path or ROOT/'scripts/recipes/items/bill-items.lock.json').read_bytes()
 d=json.loads(b,object_pairs_hook=pairs)
 if (d['schemaVersion'], d['repository'], d['commit'], d['profile']) != (1, 'pret/pokefirered', COMMIT, PROFILE): raise ValueError('Source identity differs')
 return d
def read_sources(lock,raw,repo=None):
 data={}
 if repo is not None:
  safe(repo)
  if git(repo, 'rev-parse', 'HEAD').decode().strip() != COMMIT: raise ValueError('Reference revision differs')
 for e in lock['files']:
  if repo is None: b=safe(raw,e['path']).read_bytes()
  else:
   b=git(repo,'cat-file','blob',COMMIT + ':' + e['path'])
  data[e['path']]=b
 return data

ITEMS = 'NUGGET FAME_CHECKER SS_TICKET ELIXIR ETHER PECHA_BERRY ORAN_BERRY BLUK_BERRY BERRY_POUCH'.split()
DEX_SPECIES = 'EEVEE FLAREON JOLTEON VAPOREON'.split()

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
 all_items=json.loads(text['src/data/items.json'],object_pairs_hook=pairs)['items']
 item_source=ItemSource(data)
 source_effects=read_effects_text(text['include/constants/item_effects.h'],text['src/data/pokemon/item_effects.h'])
 effects={'ETHER':'Ether','ELIXIR':'Elixir','PECHA_BERRY':'PechaBerry','ORAN_BERRY':'OranBerry'}
 defs=[];icons=[];outputs={}
 for name in ITEMS:
  symbol='ITEM_'+name;chosen=[i for i in all_items if i['itemId']==symbol]
  if len(chosen)!=1:raise ValueError('Nonunique item')
  i=chosen[0];p='src/data/items.json';m=match(p,r'^    \{\n      "english": "'+re.escape(i['english'])+r'",.*?^    \}');source=ref(p,symbol,m)
  definition=shared_item(symbol)
  declaration=i['description_english'].replace('\\n','\n');effect_bytes=source_effects[symbol] if name in effects else []
  if name in effects:
   p='src/data/pokemon/item_effects.h';em=match(p,r'^    \['+symbol+r' - ITEM_POTION\][ \t]*= sItemEffect_'+effects[name]+',');rules.append(ref(p,symbol+' effect binding',em))
  defs.append(dict(metadata=dict(item=definition,description=declaration), effectBytes=effect_bytes))
  icon,icon_png=item_source.icon(symbol)
  icons.append(icon);outputs[icon['pngPath']]=icon_png
 pickups=item_source.pickups(['Route24','Route25'])
 if len(pickups)!=7 or sum(x['kind']=='hidden' for x in pickups)!=5 or any(x['quantity']!=1 for x in pickups):raise ValueError('Pickup scope differs')
 rewards=[]
 for map_name,label,name,needle in [
  ('CeruleanCity','CeruleanCity_EventScript_Rival','FAME_CHECKER','giveitem ITEM_FAME_CHECKER'),
  ('Route24','Route24_EventScript_BattleRocket','NUGGET','giveitem_msg Route24_Text_ReceivedNuggetFromMysteryTrainer, ITEM_NUGGET'),
  ('Route25_SeaCottage','Route25_SeaCottage_EventScript_BillGiveSSTicket','SS_TICKET','giveitem_msg Route25_SeaCottage_Text_ReceivedSSTicketFromBill, ITEM_SS_TICKET, 1, MUS_OBTAIN_KEY_ITEM')]:
  p='data/maps/'+map_name+'/scripts.inc';m=match(p,r'^'+label+r'::\n(.*?)(?=^\w+::|\Z)')
  if '\t'+needle+'\n' not in m[1]:raise ValueError('Reward source differs')
  rewards.append(dict(item=ident('ITEM_'+name),script=label,source=ref(p,label,m)))
 dex=[]
 p='include/constants/pokedex.h';m=match(p,r'enum\s*\{\s*NATIONAL_DEX_NONE,(.*?)^\};');national=['NONE']+re.findall(r'\bNATIONAL_DEX_(\w+)\s*,',m[1]);rules.append(ref(p,'NationalDex',m))
 for species in DEX_SPECIES:
  p='data/maps/Route25_SeaCottage/scripts.inc';label='Route25_SeaCottage_EventScript_View'+species.title();m=match(p,r'^'+label+r'::\n(.*?)(?=^\w+::|\Z)');script_source=ref(p,label,m)
  expected='\tshowmonpic SPECIES_'+species+', 10, 3\n\twaitbuttonpress\n\thidemonpic\n\tsetvar VAR_0x8004, SPECIES_'+species+'\n\tspecial SetSeenMon\n\tgoto Route25_SeaCottage_EventScript_BillsMonList\n\tend\n'
  if m[1].strip()!=expected.strip():raise ValueError('Bill species-view sequence differs')
  p='src/data/text/species_names.h';m=match(p,r'^    \[SPECIES_'+species+r'\] = _\("([^"]+)"\),');display=m[1];name_source=ref(p,'SPECIES_'+species,m)
  p='src/data/pokemon_graphics/front_pic_table.h';m=match(p,r'^    SPECIES_SPRITE\('+species+r', (\w+)\),');front_symbol=m[1];rules.append(ref(p,'SPECIES_'+species,m))
  p='src/data/pokemon_graphics/palette_table.h';m=match(p,r'^    SPECIES_PAL\('+species+r', (\w+)\),');palette_symbol=m[1];rules.append(ref(p,'SPECIES_'+species,m))
  paths=[]
  for sym in [front_symbol,palette_symbol]:
   p='src/data/graphics/pokemon.h';m=match(p,r'^const u32 '+sym+r'\[\] = INCBIN_U32\("([^"]+)"\);');paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'));rules.append(ref(p,sym,m))
  image,palette=paths;w,h,idx,_=indexed_png(data[image]);rgba=decode_rgba(idx,decode_palette(data[palette]))
  if (w,h)!=(64,64):raise ValueError('Bill species front dimensions differ')
  png=encode_png(w,h,rgba);png_name='dex_'+species.lower()+'.png';outputs[png_name]=png
  dex.append(dict(species=ident('SPECIES_'+species),sourceSpeciesId=constant('include/constants/species.h','SPECIES_'+species),nationalDex=national.index(species),name=display,x=10,y=3,width=w,height=h,marksSeen=True,marksOwned=False,pngPath=png_name,source=ref(image,front_symbol),paletteSource=ref(palette,palette_symbol),scriptSource=script_source,nameSource=name_source))
 for p,s,pat in [('src/field_specials.c','SetSeenMon',r'^void SetSeenMon\(void\)\n\{.*?^\}'),('src/pokedex.c','GetSetPokedexFlag',r'^s8 GetSetPokedexFlag\([^;]*?\n\{.*?^\}'),('src/pokedex_screen.c','DexScreen_GetSetPokedexFlag',r'^s8 DexScreen_GetSetPokedexFlag\([^;]*?\n\{.*?^\}'),('include/pokedex.h','Pokedex flag modes',r'enum\s*\{\s*FLAG_GET_SEEN,.*?FLAG_SET_CAUGHT\s*\};'),('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','CheckBagHasSpace',r'^bool8 CheckBagHasSpace\(.*?^\}'),('src/pokemon.c','PokemonUseItemEffects',r'^bool8 PokemonUseItemEffects\([^;]*?\n\{.*?^\}'),('data/maps/Route25_SeaCottage/scripts.inc','Route25_SeaCottage_EventScript_BillsMonList',r'^Route25_SeaCottage_EventScript_BillsMonList::.*?^\tend$')]:rules.append(ref(p,s,match(p,pat)))
 for p in ['include/constants/item_effects.h','src/data/items.json.txt','tools/gbagfx/gfx.c','tools/gbagfx/convert_png.c']:rules.append(ref(p,'bounded source declaration and conversion'))
 seen=match('src/field_specials.c',r'^void SetSeenMon\(void\)\n\{.*?^\}')
 if 'GetSetPokedexFlag(SpeciesToNationalPokedexNum(gSpecialVar_0x8004), 2);' not in seen[0]:raise ValueError('SetSeenMon source mode differs')
 modes=match('include/pokedex.h',r'enum\s*\{\s*FLAG_GET_SEEN,.*?FLAG_SET_CAUGHT\s*\};')
 if re.findall(r'FLAG_\w+',modes[0])!=['FLAG_GET_SEEN','FLAG_GET_CAUGHT','FLAG_SET_SEEN','FLAG_SET_CAUGHT']:raise ValueError('Pokedex source enum differs')
 screen=match('src/pokedex_screen.c',r'case FLAG_SET_SEEN:(.*?)break;')
 if 'owned' in screen[1] or 'pokedex.seen[index] |= mask;' not in screen[1]:raise ValueError('Seen-only mutation differs')
 doc=dict(schemaVersion=1,items=defs,icons=icons,pickups=pickups,rewards=rewards,dexViews=dex)
 outputs['bill-items.json']=runtime_json_bytes(doc, default=item_json)
 outputs['manifest.json']=encoded(dict(schemaVersion=1,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
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
   if protected.name=='bill-items': continue
   p=protected.absolute()
   if any(q==p or q in p.parents or p in q.parents for q in [raw,out]):raise ValueError('Legacy/dependency output overlap')
 lock=lock_read(lock_path);data=read_sources(lock,raw,repo);outputs=convert(lock,data)
 materials=output_materials(out,outputs)
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
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/items/bill');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/items/bill-items.lock.json');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True, default=item_json))
