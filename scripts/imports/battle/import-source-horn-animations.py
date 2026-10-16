#!/usr/bin/env python3
"""Import complete horn-hit scripts and indexed source material."""
import argparse,importlib.util,io,json,re,struct
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
def module(name,file):
 spec=importlib.util.spec_from_file_location(name,ROOT/'scripts/imports/battle'/file);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
shared=module('horn_elemental','import-source-elemental-animations.py')
physical=module('horn_physical','import-source-physical-animations.py')
def build(reference):
 read=lambda p:shared.read(reference,p)
 source=read('data/battle_anim_scripts.s').decode()
 engine=read('src/battle_anim.c').decode()
 body=re.search(r'static void Cmd_jumpifcontest\(void\)\s*\{([^}]+)\}',engine)
 if body is None or re.sub(r'\s+','',body[1])!='sBattleAnimScriptPtr+=5;':raise ValueError('Source engine contest-branch invariant differs')
 ids={n:int(i) for n,i in re.findall(r'^#define MOVE_(\w+)\s+(\d+)\s*$',read('include/constants/moves.h').decode(),re.M)}
 tags={n:10000+int(i) for n,i in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',read('include/constants/battle_anim.h').decode(),re.M)}
 sounds={n:int(i) for n,i in re.findall(r'^#define (SE_\w+)\s+(\d+)\b',read('include/constants/songs.h').decode(),re.M)}
 bg={n:int(i) for n,i in re.findall(r'^#define (BG_\w+)\s+(\d+)\s*$',read('include/constants/battle_anim.h').decode(),re.M)}
 ops={'loadspritegfx','createsprite','createvisualtask','playsewithpan','playse','waitforvisualfinish','delay','end','goto','choosetwoturnanim','jumpifcontest','fadetobg','waitbgfadeout','waitbgfadein','restorebg','setarg','setalpha'}
 def block(label):
  match=re.search(r'^'+re.escape(label)+r':\n',source,re.M)
  if not match:raise ValueError('Missing source label '+label)
  rows=[];labels={label:0};dead=[]
  for offset,line in enumerate(source[match.end():].splitlines()):
   line=line.split('@',1)[0].strip()
   if not line:continue
   if line.endswith(':'):
    if line.startswith('Move_'):raise ValueError('Unterminated source block')
    labels[line[:-1]]=len(rows);continue
   op,*tail=line.split(None,1);args=[x.strip() for x in re.split(r',\s*(?![^()]*\))',tail[0])] if tail else []
   if op not in ops:raise ValueError('Unsupported source opcode '+op)
   rows.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
   if op=='jumpifcontest':
    if len(args)!=1 or not re.search(r'^'+re.escape(args[0])+r':\n',source,re.M):raise ValueError('Missing dead source target')
    dead.append(args[0])
   if op in {'end','goto','choosetwoturnanim'}:return rows,labels,dead
  raise ValueError('Unterminated source block')
 scripts=[];symbols={}
 for name in ids:
  try:
   commands=[];labels={};dead=set();pending=['Move_'+name]
   while pending:
    label=pending.pop(0)
    if label in labels:continue
    rows,local,excluded=block(label);labels.update({n:len(commands)+i for n,i in local.items()});commands.extend(rows);dead.update(excluded)
    pending.extend(rows[-1]['args'] if rows[-1]['op'] in {'goto','choosetwoturnanim'} else [])
    if len(commands)>128:raise ValueError('Source script exceeds command bound')
  except ValueError:continue
  if not any(c['op']=='createsprite' and c['args'][0]=='gHornHitSpriteTemplate' for c in commands):continue
  if dead&labels.keys():raise ValueError('Contest-only target overlaps executable labels')
  if any(c['op']=='createsprite' and c['args'][0] not in physical.SPRITES|{'gHornHitSpriteTemplate'} or c['op']=='createvisualtask' and c['args'][0] not in physical.TASKS|{'AnimTask_StartSlidingBg'} or c['op']=='loadspritegfx' and c['args'][0] not in {'ANIM_TAG_IMPACT','ANIM_TAG_HORN_HIT'} or c['op']=='fadetobg' and c['args']!=['BG_DRILL'] for c in commands):continue
  cue_names={c['args'][0] for c in commands if c['op'] in {'playse','playsewithpan'}}
  if not cue_names<=sounds.keys():raise ValueError('Unknown sound operand')
  loaded={c['args'][0] for c in commands if c['op']=='loadspritegfx'}
  labels.pop('Move_'+name)
  # FireRed Cmd_jumpifcontest only advances its five-byte instruction. Fold it
  # after verifying the dead source target, and relocate executable labels.
  labels={n:sum(c['op']!='jumpifcontest' for c in commands[:pc]) for n,pc in labels.items()}
  commands=[c for c in commands if c['op']!='jumpifcontest']
  scripts.append(dict(moveId=ids[name],label='Move_'+name,commands=commands,labels=labels,soundEffects=shared.sound_effects(reference,commands),spriteTags={n:tags[n] for n in sorted(loaded)}))
  symbols.update({c['args'][0]:bg[c['args'][0]] for c in commands if c['op']=='fadetobg'})
 recipe=shared.recipe(read('src/battle_anim_effects_1.c').decode(),'gHornHitSpriteTemplate','AnimHornHit')
 with Image.open(io.BytesIO(read('graphics/battle_anims/sprites/horn_hit.png'))) as image:
  if image.mode!='P' or image.size!=(32,32):raise ValueError('Unexpected horn source sheet')
  palette=image.getpalette()[:48];indices=[v for y in range(0,32,8) for x in range(0,32,8) for v in image.crop((x,y,x+8,y+8)).tobytes()]
 blobs={'horn-hit.4bpp':bytes(indices[i]|indices[i+1]<<4 for i in range(0,len(indices),2)),'horn-hit.pal16':struct.pack('<16H',*[sum((palette[i*3+c]//8)<<(5*c) for c in range(3)) for i in range(16)])}
 if not scripts:raise ValueError('No complete horn source roots')
 data=dict(schemaVersion=1,scripts=sorted(scripts,key=lambda r:r['moveId']),subroutines={},backgroundSymbols=symbols,spriteRecipes=[recipe],assets=[],indexedAssets=[dict(id='horn-hit',width=32,height=32,indicesPath='horn-hit.4bpp',palettePath='horn-hit.pal16',paletteTag='ANIM_TAG_HORN_HIT')])
 return (json.dumps(data,indent=2)+'\n').encode(),blobs
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();data,blobs=build(a.reference)
 for path,blob in [(a.output,data)]+[(a.output.parent/name,blob) for name,blob in blobs.items()]:
  if a.check:
   if path.read_bytes()!=blob:raise SystemExit('Horn runtime output differs: '+str(path))
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 print(json.dumps(dict(passed=True,moves=[r['moveId'] for r in json.loads(data)['scripts']])))
