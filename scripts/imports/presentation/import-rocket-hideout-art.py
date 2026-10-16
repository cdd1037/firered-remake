#!/usr/bin/env python3
"""Decode two pinned original trainer portraits. No upstream code is executed."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse,importlib.util,json,pathlib,re,sys
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PORTRAITS=[('rocket-grunt-m-front','rocket_grunt_m','RocketGruntM','ROCKET_GRUNT_M'),('giovanni-front','leader_giovanni','LeaderGiovanni','LEADER_GIOVANNI')]
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(base,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe art path')
 q=pathlib.Path(base)/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Art symlink rejected')
 return q

def authenticated(root=ROOT,output_root=None):
 out=output_root or root;b=safe(out,'scripts/recipes/presentation/rocket-hideout-art.lock.json').read_bytes()
 lock=json.loads(b)
 if (lock['schemaVersion'], lock['repository'], lock['commit'], len(lock['files']), len(lock['inputs'])) != (1, 'pret/pokefirered', COMMIT, 7, 2):raise ValueError('Art source scope differs')
 data={}
 for row in lock['files']:
  b=safe(out,'content/raw/pokefirered/037335f4/'+row['path']).read_bytes()
  data[row['path']]=b
 for row in lock['inputs']:
  b=safe(root,row['path']).read_bytes()
 return lock,data

def build(root,data):
 graphics=data['src/data/graphics/trainers.h'].decode();tables=data['src/data/trainer_graphics/front_pic_tables.h'].decode();trainers=data['src/data/trainers.h'].decode()
 spec=importlib.util.spec_from_file_location('_celadon_gym_codec',root/'scripts/imports/presentation/import-lab-battle-ui.py');codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 output={};assets=[]
 for id,stem,symbol,picture in PORTRAITS:
  for part in ['gTrainerFrontPic_'+symbol,'graphics/trainers/front_pics/'+stem+'_front_pic.4bpp.lz','gTrainerPalette_'+symbol,'graphics/trainers/palettes/'+stem+'.gbapal.lz']:
   if part not in graphics:raise ValueError('Portrait graphics binding differs: '+part)
  if f'TRAINER_SPRITE({picture}, gTrainerFrontPic_{symbol}, 0x800)' not in tables or f'TRAINER_PAL({picture}, gTrainerPalette_{symbol})' not in tables:raise ValueError('Portrait table differs: '+id)
  expected={'ROCKET_GRUNT_M':['TEAM_ROCKET_GRUNT_'+str(i) for i in range(7,19)],'LEADER_GIOVANNI':['BOSS_GIOVANNI']}[picture]
  for trainer in expected:
   m=re.search(r'\[TRAINER_'+trainer+r'\] = \{(.*?)\n    \}',trainers,re.S)
   if not m or '.trainerPic = TRAINER_PIC_'+picture not in m[1]:raise ValueError('Portrait trainer binding differs: '+trainer)
  w,h,indices,_=codec.indexed_png(data['graphics/trainers/front_pics/'+stem+'_front_pic.png'])
  if (w,h)!=(64,64):raise ValueError('Portrait source size differs')
  rgba=codec.rgba(indices,codec.palette(data['graphics/trainers/palettes/'+stem+'.pal']));png=codec.png(w,h,rgba)
  output[id+'.png']=png;assets.append(dict(id=id,width=w,height=h,trainerPicture=picture))
 return assets,output

def run(root=ROOT,output_root=None,check=False):
 out=output_root or root;lock,data=authenticated(root,out);assets,output=build(root,data)
 output['manifest.json']=runtime_json_bytes(dict(schemaVersion=1,assets=assets,transparentIndex=0,trainerTopLeft=[144,8],outputs=[dict(path=p) for p,b in sorted(output.items())]))
 for relative,b in output.items():
  p=safe(out,'content/generated/sprites/trainers/rocket-hideout/'+relative)
  if check:
   if p.read_bytes()!=b:raise ValueError('Generated art differs: '+relative)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,assets=len(assets),sourceBlobs=len(data))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--output-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.output_root,a.check),indent=2))
