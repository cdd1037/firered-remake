#!/usr/bin/env python3
"""Offline authenticated one-map Celadon Gym source candidate. Does not activate gameplay."""
import sys as _constant_sys
from pathlib import Path as _ConstantPath
_constant_scripts = str(_ConstantPath(__file__).resolve().parent)
if _constant_scripts not in _constant_sys.path:
    _constant_sys.path.insert(0, _constant_scripts)
from shared_constants import package_documents
import argparse,importlib.util,json,pathlib,re,shutil,tempfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-celadon-gym-region-import-v1'
MAPS=[('CeladonCity_Gym','map.celadon-city-gym','celadon-city-gym')]
TILESETS={'gTileset_Building':'primary/building','gTileset_CeladonGym':'secondary/celadon_gym'}
def canonical(v):return (json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,relative):
 p=pathlib.PurePosixPath(relative)
 if p.is_absolute() or '\\' in relative or any(x in ('','.','..') for x in relative.split('/')):raise ValueError('Unsafe relative path')
 q=pathlib.Path(root)/relative
 if any(x.is_symlink() for x in [q,*q.parents]):raise ValueError('Symlink rejected')
 return q

def module(path, name):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def read_lock(path=None):
 b=(path or safe(ROOT,'scripts/celadon-gym-region.lock.json')).read_bytes()
 d=json.loads(b)
 if (d['schemaVersion'], d['repository'], d['commit'], d['profile'], d['gameVersion'], d['revision'], d['completeGame'], d['maps']) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0, False, ['CeladonCity_Gym']):raise ValueError('Gym source identity differs')
 if len(d['files'])!=99 or len({e['path'] for e in d['files']})!=99:raise ValueError('Gym source closure differs')
 for e in d['files']:safe(ROOT,e['path'])
 return d

def protect(target,project,allowed_stem,kind):
 target=pathlib.Path(target).absolute();safe(target,'sentinel')
 for root in {ROOT.resolve(),pathlib.Path(project).resolve()}:
  for p in [root/'scripts',root/'src',root/'tests',root/'.git',root/'scripts/import-config']:
   if target.resolve().is_relative_to(p) or p.is_relative_to(target.resolve()):raise ValueError('Protected project path overlap')
  for k in ('raw','generated'):
   base=root/'content'/k
   if base.exists():
    for p in base.iterdir():
     if k==kind and p.name==allowed_stem:continue
     if target.resolve().is_relative_to(p.resolve()) or p.resolve().is_relative_to(target.resolve()):raise ValueError('Existing profile overlap')

def extract_dependencies(src,scripts,world):
 blocks={b['label']:b for b in scripts['blocks']}
 def block(label):return blocks[label]
 gym=world['events'];city=src.json('data/maps/CeladonCity/map.json')
 cut=[dict(map=gym['id'],localId=i,object=o) for i,o in enumerate(gym['object_events'],1) if o['graphics_id']=='OBJ_EVENT_GFX_CUT_TREE']
 citytrees=[dict(map=city['id'],localId=i,object=o) for i,o in enumerate(city['object_events'],1) if o.get('script')=='EventScript_CutTree']
 trainers=[]
 for i,o in enumerate(gym['object_events'],1):
  b=blocks.get(o['script']);lines='\n'.join(x['text'] for x in b['lines']) if b else ''
  m=re.search(r'trainerbattle_single (TRAINER_\w+)',lines)
  if m:trainers.append(dict(localId=i,trainer=m[1],object=o,battleScript=b))
 required=dict(badge='FLAG_BADGE02_GET',partyMove='MOVE_CUT',fieldEffect='FLDEFF_USE_CUT_ON_TREE',temporaryFlags=['FLAG_TEMP_12','FLAG_TEMP_13','FLAG_TEMP_14'])
 return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,runtimeIntegrated=False,completeGame=False,trainers=trainers,cutTrees=cut,outdoorCutTrees=citytrees,cutPrerequisites=required,
  cutScript=block('EventScript_CutTree'),cutEffectScript=block('EventScript_FldEffCut'),cutCompletion=block('EventScript_CutTreeDown'),
  erikaVictory=block('CeladonCity_Gym_EventScript_DefeatedErika'),gymTrainerFlags=block('EventScript_SetCeladonGymTrainers'),
  tm19Reward=block('CeladonCity_Gym_EventScript_GiveTM19'),tm19NoRoom=block('CeladonCity_Gym_EventScript_NoRoomForTM19'),
  entry=dict(sourceMap=city['id'],sourceWarpId=6,source=city['warp_events'][6],destinationMap=gym['id'],destinationWarpId=1,destination=gym['warp_events'][1]),
  badgeRewards=dict(flags=['FLAG_DEFEATED_ERIKA','FLAG_BADGE04_GET'],clearedFlag='FLAG_HIDE_FAME_CHECKER_ERIKA_JOURNALS',obedienceLevel=50,fieldPermission='MOVE_STRENGTH',tm='ITEM_TM19',tmFlag='FLAG_GOT_TM19_FROM_ERIKA'),
  sourceAudio=sorted({gym['music'],'MUS_ENCOUNTER_GYM_LEADER'}),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),
  status='Source preparation only; map, battle, save and reward integration are not enabled.')

def run(project,raw,out,reference=None):
 project,raw,out=map(pathlib.Path,(project,raw,out));protect(raw,project,'celadon-gym-region','raw');protect(out,project,'celadon-gym-region','generated')
 if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):raise ValueError('Source/output overlap')
 shared=module(safe(project, 'scripts/import-route22-region.py'), '_celadon_gym_renderer')
 for key in ('ROOT','COMMIT','TREE','PROFILE','MAPS','TILESETS','read_lock'):setattr(shared,key,globals()[key])
 if reference:shared.materialize(raw,pathlib.Path(reference),source_package='celadon-gym-region')
 src=shared.Source(raw);inventory=module(safe(ROOT, 'scripts/celadon-gym-script-inventory.py'), '_celadon_gym_inventory')
 converter=module(safe(project, 'scripts/export-runtime-maps.py'), '_celadon_gym_compact')
 out.parent.mkdir(parents=True,exist_ok=True)
 with tempfile.TemporaryDirectory(prefix='.celadon-gym-stage-',dir=out.parent) as tmp:
  stage=pathlib.Path(tmp);world=shared.import_map(src,*MAPS[0][:2],stage/MAPS[0][2]);worlds=[world]
  scripts=inventory.script_inventory(src,worlds);constants=shared.constants_inventory(src,worlds,scripts)
  npcs=shared.npc_inventory(src,worlds,stage/'npc');connections,warps=shared.connections_and_warps(src,worlds)
  catalog=dict(schemaVersion=1,profile=PROFILE,commit=COMMIT,runtimeIntegrated=False,maps=[dict(sourceName=MAPS[0][0],stableId=MAPS[0][1],directory=MAPS[0][2],sourceMapSymbol=world['events']['id'])],connections=connections,warps=warps)
  for p,doc in package_documents(out.parent, constants, {'celadon-gym-catalog.json':catalog,'scripts.json':scripts,'npc-source.json':npcs,'dependencies.json':extract_dependencies(src,scripts,world)}).items():safe(stage,p).write_bytes(canonical(doc))
  rich=safe(stage,'celadon-city-gym/runtime-map.json').read_bytes();safe(stage,'celadon-city-gym/runtime-map.json').write_bytes(converter.encode(converter.normalize_runtime(json.loads(rich))))
  outputs=[dict(path=p.relative_to(stage).as_posix()) for p in sorted(stage.rglob('*')) if p.is_file()]
  manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(src.entries),mapCount=1,cellCount=260,borderCellCount=4,warpCount=len(warps),npcGraphicsCount=len(npcs['graphics']),scriptBlockCount=len(scripts['blocks']),unresolvedLabelCount=len(scripts['unresolvedLabelReferences']),outputs=outputs)
  b=canonical(manifest)
  safe(stage,'manifest.json').write_bytes(b)
  for p in sorted(stage.rglob('*')):
   if p.is_file():safe(out,p.relative_to(stage).as_posix())
  for p in sorted(stage.rglob('*')):
   if p.is_file():q=safe(out,p.relative_to(stage).as_posix());q.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(p,q)
 return {k:v for k,v in manifest.items() if k!='outputs'}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/celadon-gym-region');a=p.parse_args();print(json.dumps(run(a.project_root,a.raw,a.out,a.reference_repo),sort_keys=True))
