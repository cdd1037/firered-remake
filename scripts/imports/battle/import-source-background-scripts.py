#!/usr/bin/env python3
"""Discover complete reviewed move-background scripts without duplicating sprite materials."""
import argparse, importlib.util, json, re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def module(name,file):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
elemental=module('background_elemental','import-source-elemental-animations.py')
physical=module('background_physical','import-source-physical-animations.py')
BACKGROUND_SYMBOLS={'BG_IMPACT_PLAYER','BG_IMPACT_OPPONENT','BG_DARK','BG_PSYCHIC','BG_THUNDER','BG_COSMIC','BG_SKY','BG_ICE','BG_HIGHSPEED_PLAYER','BG_HIGHSPEED_OPPONENT'}
BACKGROUND_OPS={'fadetobg','restorebg','waitbgfadein','waitbgfadeout','setarg'}
def build(reference):
 source=elemental.read(reference,'data/battle_anim_scripts.s').decode()
 ids={n:int(i) for n,i in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',elemental.read(reference,'include/constants/moves.h').decode(),re.M)}
 backgrounds={n:int(i) for n,i in re.findall(r'^#define (BG_\w+)\s+(\d+)\s*$',elemental.read(reference,'include/constants/battle_anim.h').decode(),re.M)}
 tag_source=elemental.read(reference,'include/constants/battle_anim.h').decode();tag_start=int(re.search(r'^#define ANIM_SPRITES_START\s+(\d+)\b',tag_source,re.M)[1]);tags={name:tag_start+int(offset) for name,offset in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',tag_source,re.M)}
 ops=elemental.hit.OPS|BACKGROUND_OPS|{'call','return','splitbgprio','panse','waitplaysewithpan','splitbgprio_foes','createsoundtask','waitsound','jumpifcontest','goto','jumpargeq','jumprettrue','jumpretfalse'}
 def commands(label,end):
  def block(start):
   match=re.search(r'^'+re.escape(start)+r':\n',source,re.M)
   if not match:raise ValueError('Missing source label '+start)
   rows=[];labels={start:0}
   for offset,line in enumerate(source[match.end():].splitlines()):
    line=line.split('@',1)[0].strip()
    if not line:continue
    if line.endswith(':'):
     if line.startswith('Move_'):raise ValueError('Unterminated source label '+start)
     labels[line[:-1]]=len(rows);continue
    op,*tail=line.split(None,1);args=[x.strip() for x in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
    if op not in ops:raise ValueError('Uncovered source command '+op)
    if op in {'jumprettrue','jumpretfalse'}:
     if len(args)!=1:raise ValueError('Malformed return comparison')
     args=['7','1' if op=='jumprettrue' else '0',args[0]];op='jumpargeq'
    rows.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
    if op in {end,'goto'}:return elemental.fold_firered_contest_branches(reference,source,rows,labels)
   raise ValueError('Unterminated source label '+start)
  rows,labels=block(label);cursor=0
  while cursor<len(rows):
   c=rows[cursor];cursor+=1
   if c['op'] not in {'goto','jumpargeq'}:continue
   if len(c['args'])!=(1 if c['op']=='goto' else 3):raise ValueError('Malformed source branch')
   target=c['args'][-1]
   if target in labels:continue
   more,local=block(target);base=len(rows);rows.extend(more)
   for name,pc in local.items():
    if name in labels:raise ValueError('Overlapping source branch region')
    labels[name]=base+pc
   if len(rows)>512:raise ValueError('Source branch program exceeds bound')
  visiting=set();visited=set()
  def visit(pc):
   if pc in visiting:raise ValueError('Cyclic source branch program')
   if pc in visited:return
   if not 0<=pc<len(rows):raise ValueError('Source branch falls off program')
   visiting.add(pc);c=rows[pc]
   if c['op']=='goto':visit(labels[c['args'][0]])
   elif c['op']=='jumpargeq':visit(labels[c['args'][2]]);visit(pc+1)
   elif c['op'] not in {'end','return'}:visit(pc+1)
   visiting.remove(pc);visited.add(pc)
  visit(0)
  # Only actual runtime branches need labels; old straight-line rows stay identical.
  used={c['args'][-1] for c in rows if c['op'] in {'goto','jumpargeq'}}
  return rows,{name:labels[name] for name in sorted(used)}
 sprites=set(elemental.TEMPLATES)|elemental.SHARED_SPRITES|physical.SPRITES|{'gAirWaveCrescentSpriteTemplate'}
 tasks=elemental.TASKS|physical.TASKS|{'AnimTask_SetPsychicBackground','AnimTask_StartSlidingBg','AnimTask_ShakeTargetInPattern','AnimTask_BlendNonAttackerPalettes','SoundTask_PlaySE1WithPanning','SoundTask_PlaySE2WithPanning','AnimTask_GetAttackerSide'}
 sounds=elemental.SOUNDS|{'SE_M_LEER','SE_M_VITAL_THROW','SE_M_COSMIC_POWER','SE_M_JUMP_KICK','SE_M_RAZOR_WIND','SE_M_BLIZZARD','SE_M_BLIZZARD2'}
 gfx=elemental.GFX|{'ANIM_TAG_FANG_ATTACK','ANIM_TAG_IMPACT','ANIM_TAG_AIR_WAVE_2'}
 scripts=[];subs={};symbols={};task_affine_programs={}
 for name in re.findall(r'^Move_(\w+):\n',source,re.M):
  if name not in ids:continue
  try:
   root,root_labels=commands('Move_'+name,'end');programs={label:commands(label,'return') for label in dict.fromkeys(c['args'][0] for c in root if c['op']=='call')};local={name:p[0] for name,p in programs.items()}
   if any(p[1] for p in programs.values()):raise ValueError('Subroutine branches await shared metadata binding')
  except ValueError:continue
  if any(c['op']=='call' for sub in local.values() for c in sub):continue
  flat=root+[c for sub in local.values() for c in sub]
  if not any(c['op']=='fadetobg' for c in flat):continue
  if any(c['op']=='fadetobg' and (len(c['args'])!=1 or c['args'][0] not in BACKGROUND_SYMBOLS) or c['op'] in BACKGROUND_OPS-{'fadetobg','setarg'} and c['args'] or c['op']=='createsprite' and c['args'][0] not in sprites or c['op']=='createvisualtask' and c['args'][0] not in tasks or c['op']=='loadspritegfx' and c['args'][0] not in gfx or c['op'] in {'playsewithpan','loopsewithpan','panse','waitplaysewithpan'} and c['args'][0] not in sounds for c in flat):continue
  if any(c['op']=='createsoundtask' and (c['args'][0]!='SoundTask_LoopSEAdjustPanning' or c['args'][1] not in sounds) for c in flat):continue
  row=dict(moveId=ids[name],label='Move_'+name,commands=root,soundEffects=elemental.sound_effects(reference,flat),spriteTags={n:tags[n] for n in sorted({c['args'][0] for c in flat if c['op']=='loadspritegfx'})})
  if root_labels:row['labels']=root_labels
  if any(c['op']=='createvisualtask' and c['args'][0]=='AnimTask_MeditateStretchAttacker' for c in flat):
   program_name='sAffineAnim_MeditateStretchAttacker'
   psychic=elemental.read(reference,'src/battle_anim_psychic.c').decode()
   init=re.search(r'void AnimTask_MeditateStretchAttacker\(u8 taskId\)\s*\{(.*?)^\}',psychic,re.S|re.M)
   step=re.search(r'static void AnimTask_MeditateStretchAttacker_Step\(u8 taskId\)\s*\{(.*?)^\}',psychic,re.S|re.M)
   if init is None or step is None or 'PrepareAffineAnimInTaskData(task, spriteId, '+program_name+');' not in init[1] or re.sub(r'\s+','',step[1])!='if(!RunAffineAnimFromTaskData(&gTasks[taskId]))DestroyAnimVisualTask(taskId);':raise ValueError('Meditate task affine handoff differs')
   task_affine_programs[program_name]=physical.task_affine_program(psychic,program_name)
  scripts.append(row);subs.update(local)
  symbols.update({c['args'][0]:backgrounds[c['args'][0]] for c in flat if c['op']=='fadetobg'})
 if not scripts:raise ValueError('No complete background script dependencies')
 return (json.dumps(dict(schemaVersion=1,scripts=sorted(scripts,key=lambda r:r['moveId']),subroutines=subs,backgroundSymbols=symbols,**(dict(taskAffinePrograms=task_affine_programs) if task_affine_programs else {})),indent=2)+'\n').encode()
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data=build(a.reference)
 if a.check:
  if a.output.read_bytes()!=data:raise SystemExit('Background scripts differ')
 else:a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_bytes(data)
 print(json.dumps(dict(passed=True,moves=[r['moveId'] for r in json.loads(data)['scripts']])))
