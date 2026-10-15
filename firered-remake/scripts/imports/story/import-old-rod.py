#!/usr/bin/env python3
"""Offline import of pinned FireRed Old Rod rules and ten early-Kanto fishing tables."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-old-rod-v1'
PATHS = ('src/field_player_avatar.c', 'src/item_use.c', 'src/wild_encounter.c',
 'src/data/wild_encounters.json', 'src/pokemon.c', 'src/data/pokemon/species_info.h',
 'src/main.c', 'src/random.c', 'src/metatile_behavior.c', 'include/global.fieldmap.h',
 'include/constants/metatile_behaviors.h', 'include/random.h', 'src/event_object_movement.c', 'src/strings.c')
MAPS = (('pallet-town','PalletTown'),('viridian-city','ViridianCity'),('cerulean-city','CeruleanCity'),
 ('vermilion-city','VermilionCity'),('route-4','Route4'),('route-6','Route6'),('route-22','Route22'),
 ('route-24','Route24'),('route-25','Route25'),('ssanne-exterior','SSAnneExterior'))
def canonical(value): return (json.dumps(value,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,path):
 if pathlib.PurePosixPath(path).is_absolute() or '\\' in path or any(x in ('','.','..') for x in path.split('/')): raise ValueError('Unsafe path')
 p=pathlib.Path(root)/path
 if any(q.is_symlink() for q in (p,*p.parents)): raise ValueError('Symlink rejected')
 return p

def convert(sources):
 groups=json.loads(sources['src/data/wild_encounters.json'])['wild_encounter_groups']
 group=next(x for x in groups if x['label']=='gWildMonHeaders')
 field=next(x for x in group['fields'] if x['type']=='fishing_mons')
 if field['groups']['old_rod']!=[0,1] or field['encounter_rates']!=[70,30,60,20,20,40,40,15,4,1]: raise ValueError('Source fishing slot weights differ')
 for name in ('Route1','Route2','Route3','Route5'):
  rows=[x for x in group['encounters'] if x['base_label']=='s'+name+'_FireRed']
  if len(rows)!=1 or 'fishing_mons' in rows[0]: raise ValueError('Verified no-fishing map differs')
 tables=[]
 for stable,name in MAPS:
  rows=[x for x in group['encounters'] if x['base_label']=='s'+name+'_FireRed']
  if len(rows)!=1: raise ValueError('Missing or duplicated FireRed fishing table')
  table=rows[0]['fishing_mons']; slots=table['mons'][:2]
  if len(slots)!=2 or any(x['species']!='SPECIES_MAGIKARP' or x['min_level']!=5 or x['max_level']!=(10 if stable=='pallet-town' else 5) for x in slots): raise ValueError('Bounded Old Rod source species/levels differ')
  tables.append(dict(map=dict(value='map.'+stable),rawEncounterRate=table['encounter_rate'],slots=[dict(index=i,species=dict(value='pret:'+m['species']),minimumLevel=m['min_level'],maximumLevel=m['max_level'],weight=field['encounter_rates'][i]) for i,m in enumerate(slots)]))
 avatar=sources['src/field_player_avatar.c'].decode()
 skip=re.search(r'^static bool8 Fishing7\(struct Task \*task\)\n\{(.*?)^\}',avatar,re.M|re.S)
 if not skip or 'task->tStep += 3;' not in skip[1] or 'gMain.newKeys' in skip[1]: raise ValueError('Source skips reel window through Fishing7')
 magikarp=re.search(r'^    \[SPECIES_MAGIKARP\] =\s*\n    \{\n(.*?)^    \},',sources['src/data/pokemon/species_info.h'].decode(),re.M|re.S)
 if not magikarp or '.itemCommon = ITEM_NONE,' not in magikarp[1] or '.itemRare = ITEM_NONE,' not in magikarp[1]: raise ValueError('Magikarp held items differ')
 texts={}
 for symbol in ('gText_PokemonOnHook','gText_NotEvenANibble'):
  matches=re.findall(r'^const u8 '+symbol+r'\[\] = _\("([^"]*)"\);$',sources['src/strings.c'].decode(),re.M)
  if len(matches)!=1 or not matches[0].endswith('{PAUSE_UNTIL_PRESS}'): raise ValueError('Old Rod result text control differs')
  texts[symbol]=matches[0]
 return dict(texts=texts,schemaVersion=1,tables=tables,castDelayFrames=60,dotDelayFrames=20,firstRoundMinimumDots=4,firstRoundMaximumDots=10,biteDrawBound=2,biteSuccessRoll=0,reelReactionReachable=False,inFlightCancelSupported=False,minimumRoundDrawBound=1,heldItemDrawBound=100,heldItem='ITEM_NONE')

def run(repo,raw,out,check=False):
 raw,out=pathlib.Path(raw).absolute(),pathlib.Path(out).absolute()
 def overlap(a,b): return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 if overlap(raw,out): raise ValueError('Raw/generated paths overlap')
 if repo:
  repo=pathlib.Path(repo).absolute()
  if overlap(repo,raw) or overlap(repo,out): raise ValueError('Reference/output paths overlap')
 lb=safe(ROOT,'scripts/recipes/config/old-rod.json').read_bytes()
 lock=json.loads(lb)
 if (lock['commit'], lock['profile'], lock['repository']) != (COMMIT, PROFILE, 'pret/pokefirered') or tuple((x['path'] for x in lock['files'])) != PATHS: raise ValueError('Source identity differs')
 sources={}
 for entry in lock['files']:
  b=subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+entry['path']]) if repo else safe(raw,entry['path']).read_bytes()
  sources[entry['path']]=b
 data=canonical(convert(sources))
 if check:
  if out.read_bytes()!=data: raise ValueError('Existing output differs')
 else:
  material=[(out,data)]+([(safe(raw,p),b) for p,b in sources.items()] if repo else [])
  for f,_ in material:
   if f.exists() and not f.is_file(): raise ValueError('Nonregular output')
   if any(x.exists() and not x.is_dir() for x in f.parents): raise ValueError('Non-directory parent')
  for f,b in material: f.parent.mkdir(parents=True,exist_ok=True);f.write_bytes(b)
 print(json.dumps(dict(profile=PROFILE,tables=10,slots=20,checked=check)))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/data/services/old-rod.json');p.add_argument('--check',action='store_true');a=p.parse_args();run(a.reference_repo,a.raw,a.out,a.check)
