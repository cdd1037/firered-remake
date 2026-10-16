#!/usr/bin/env python3
"""Discover complete reviewed move-background scripts without duplicating sprite materials."""
import argparse, importlib.util, json, re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def module(name,file):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
elemental=module('background_elemental','import-source-elemental-animations.py')
physical=module('background_physical','import-source-physical-animations.py')
BACKGROUND_SYMBOLS={'BG_DARK','BG_PSYCHIC'}
BACKGROUND_OPS={'fadetobg','restorebg','waitbgfadein','waitbgfadeout','setarg'}
def build(reference):
 source=elemental.read(reference,'data/battle_anim_scripts.s').decode()
 ids={n:int(i) for n,i in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',elemental.read(reference,'include/constants/moves.h').decode(),re.M)}
 backgrounds={n:int(i) for n,i in re.findall(r'^#define (BG_\w+)\s+(\d+)\s*$',elemental.read(reference,'include/constants/battle_anim.h').decode(),re.M)}
 ops=elemental.hit.OPS|BACKGROUND_OPS|{'call','return','splitbgprio','panse','waitplaysewithpan','splitbgprio_foes','createsoundtask','waitsound'}
 def commands(label,end):
  match=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not match:raise ValueError('Missing source label '+label)
  rows=[]
  for offset,line in enumerate(source[match.end():].splitlines()):
   line=line.split('@',1)[0].strip()
   if not line:continue
   if line.endswith(':'):raise ValueError('Unterminated source label '+label)
   op,*tail=line.split(None,1);args=[x.strip() for x in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in ops:raise ValueError('Uncovered source command '+op)
   rows.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
   if op==end:return rows
  raise ValueError('Unterminated source label '+label)
 sprites=set(elemental.TEMPLATES)|elemental.SHARED_SPRITES|physical.SPRITES
 tasks=elemental.TASKS|physical.TASKS|{'AnimTask_SetPsychicBackground'}
 sounds=elemental.SOUNDS|{'SE_M_VITAL_THROW'}
 gfx=elemental.GFX|{'ANIM_TAG_IMPACT'}
 scripts=[];subs={};symbols={}
 for name in re.findall(r'^Move_(\w+):\n',source,re.M):
  if name not in ids:continue
  try:
   root=commands('Move_'+name,'end');local={label:commands(label,'return') for label in dict.fromkeys(c['args'][0] for c in root if c['op']=='call')}
  except ValueError:continue
  if any(c['op']=='call' for sub in local.values() for c in sub):continue
  flat=root+[c for sub in local.values() for c in sub]
  if not any(c['op']=='fadetobg' for c in flat):continue
  if any(c['op']=='fadetobg' and (len(c['args'])!=1 or c['args'][0] not in BACKGROUND_SYMBOLS) or c['op'] in BACKGROUND_OPS-{'fadetobg','setarg'} and c['args'] or c['op']=='createsprite' and c['args'][0] not in sprites or c['op']=='createvisualtask' and c['args'][0] not in tasks or c['op']=='loadspritegfx' and c['args'][0] not in gfx or c['op'] in {'playsewithpan','loopsewithpan','panse','waitplaysewithpan'} and c['args'][0] not in sounds for c in flat):continue
  if any(c['op']=='createsoundtask' and (c['args'][0]!='SoundTask_LoopSEAdjustPanning' or c['args'][1] not in sounds) for c in flat):continue
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=root,soundEffects=elemental.sound_effects(reference,flat)));subs.update(local)
  symbols.update({c['args'][0]:backgrounds[c['args'][0]] for c in flat if c['op']=='fadetobg'})
 if not scripts:raise ValueError('No complete background script dependencies')
 return (json.dumps(dict(schemaVersion=1,scripts=sorted(scripts,key=lambda r:r['moveId']),subroutines=subs,backgroundSymbols=symbols),indent=2)+'\n').encode()
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data=build(a.reference)
 if a.check:
  if a.output.read_bytes()!=data:raise SystemExit('Background scripts differ')
 else:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_bytes(data)
 print(json.dumps(dict(passed=True,moves=[r['moveId'] for r in json.loads(data)['scripts']])))
