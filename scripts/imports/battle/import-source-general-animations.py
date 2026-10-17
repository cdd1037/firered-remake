#!/usr/bin/env python3
"""Import named source general animations and their exact material dependencies."""
import argparse,importlib.util,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
spec=importlib.util.spec_from_file_location('general_elemental',ROOT/'scripts/imports/battle/import-source-elemental-animations.py');shared=importlib.util.module_from_spec(spec);spec.loader.exec_module(shared)
def branched_program(source,label):
 # Compile one general root and its physical local labels. Keep executable PCs;
 # source return comparison aliases use the shared signed argument register7.
 start=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
 if not start:raise ValueError('Missing source label '+label)
 tail=source[start.end():];end=re.search(r'^General_[A-Za-z0-9_]+:',tail,re.M)
 if end:tail=tail[:end.start()]
 result=[];labels={label:0}
 allowed={'createvisualtask','goto','monbg','clearmonbg','playsewithpan','waitplaysewithpan','waitforvisualfinish','end','jumpargeq'}
 for i,line in enumerate(tail.splitlines()):
  line=line.split('@',1)[0].strip()
  if not line:continue
  if line.endswith(':'):
   name=line[:-1]
   if name in labels:raise ValueError('Duplicate general label '+name)
   labels[name]=len(result);continue
  op,*rest=line.split(None,1);args=[v.strip() for v in rest[0].split(',')] if rest else []
  if op=='jumpreteq':op='jumpargeq';args=['7',*args]
  if op not in allowed:raise ValueError('Uncovered branched general command '+op)
  result.append(dict(op=op,args=args,line=source[:start.end()].count('\n')+i+1))
 active=set();done=set()
 def visit(pc):
  if pc in active:raise ValueError('Cyclic general source program')
  if pc in done:return
  if pc<0 or pc>=len(result):raise ValueError('General source program falls off')
  active.add(pc);row=result[pc];op=row['op'];args=row['args'];nexts=[]
  if op in {'goto','jumpargeq'}:
   if len(args)!=(1 if op=='goto' else 3) or args[-1] not in labels:raise ValueError('Missing general branch target')
   nexts.append(labels[args[-1]])
  if op not in {'goto','end'}:nexts.append(pc+1)
  for target in nexts:visit(target)
  active.remove(pc);done.add(pc)
 visit(0)
 if len(done)!=len(result):raise ValueError('Unreachable general instructions')
 return result,labels

def build(reference):
 read=lambda p:shared.read(reference,p)
 source=read('data/battle_anim_scripts.s').decode()
 def commands(label,terminal):
  match=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not match:raise ValueError('Missing source label '+label)
  result=[]
  for i,line in enumerate(source[match.end():].splitlines()):
   line=line.split('@',1)[0].strip()
   if not line:continue
   if line.endswith(':'):raise ValueError('Unterminated general source label')
   op,*tail=line.split(None,1);args=[s.strip() for s in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in {'loadspritegfx','delay','playsewithpan','createvisualtask','waitforvisualfinish','call','createsprite','return','end'}:raise ValueError('Uncovered source general command '+op)
   result.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+i+1))
   if op==terminal:return result
  raise ValueError('Missing general terminator')
 roots={name:commands(name,'end') for name in ['General_HeldItemEffect','General_WishHeal']}
 castform,castform_labels=branched_program(source,'General_CastformChange')
 roots['General_CastformChange']=castform
 subs={name:commands(name,'return') for name in ['GrantingStarsEffect','HealingEffect']}
 def dependency_commands(rows):
  flat=list(rows)
  for row in rows:
   if row['op']=='call':
    name=row['args'][0]
    if name not in subs:raise ValueError('Uncovered general subroutine '+name)
    flat+=subs[name]
  return flat
 recipes=[]
 for file,name,callback in [('battle_anim_effects_1.c','gGrantingStarsSpriteTemplate','AnimGrantingStars'),('battle_anim_effects_2.c','gThinRingExpandingSpriteTemplate','AnimSpriteOnMonPos')]:
  code=read('src/'+file).decode()
  recipes.append(shared.recipe(code,name,callback))
 rows=read('graphics/battle_anims/sprites/sparkle_2.pal').decode().splitlines()
 if rows[:3]!=['JASC-PAL','0100','16']:raise ValueError('Unexpected sparkle palette')
 palette=[int(v) for row in rows[3:] for v in row.split()]
 blobs={'general-sparkle-2.rgb555':shared.hit.rgb555(read('graphics/battle_anims/sprites/sparkle_1.png'),(32,256),palette),'general-thin-ring.rgb555':shared.hit.rgb555(read('graphics/battle_anims/sprites/thin_ring.png'),(64,64))}
 assets=[dict(id='sparkle-2',path='general-sparkle-2.rgb555'),dict(id='thin-ring',path='general-thin-ring.rgb555')]
 data=dict(schemaVersion=1,generalScripts=[dict(label=name,commands=rows,soundEffects=shared.sound_effects(reference,dependency_commands(rows))) for name,rows in roots.items()],subroutines=subs,spriteRecipes=recipes,assets=assets)
 for row in data['generalScripts']:
  if row['label']=='General_CastformChange':row['labels']=castform_labels
 return (json.dumps(data,indent=2)+'\n').encode(),blobs
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data,blobs=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/name,blob) for name,blob in blobs.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('General runtime output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,generalAnimations=len(json.loads(data)['generalScripts']))))
