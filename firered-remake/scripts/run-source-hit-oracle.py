#!/usr/bin/env python3
"""Re-extract authenticated source callback bodies; no reference build scripts/ROM."""
import argparse,hashlib,importlib.util,json,re,subprocess
from pathlib import Path
root=Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('hit_importer',root/'scripts/import-source-hit-animations.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
p=argparse.ArgumentParser();p.add_argument('output',type=Path);p.add_argument('--reference',type=Path,default=Path((root/'.tools/reference-root').read_text().strip()));a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
raw=imp.authenticated(a.reference)
functions={
'src/battle_anim.c':['WaitAnimFrameCount','RunAnimScriptCommand','Cmd_delay','Cmd_waitforvisualfinish','Cmd_end','Cmd_clearmonbg','Task_ClearMonBg','BattleAnimAdjustPanning'],
'src/battle_anim_mon_movement.c':['DoHorizontalLunge','ReverseHorizontalLungeDirection','AnimTask_ShakeMon','AnimTask_ShakeMon_Step'],
'src/battle_anim_normal.c':['AnimHitSplatBasic'],
'src/battle_anim_mons.c':['TranslateSpriteLinearById','RunStoredCallbackWhenAffineAnimEnds','SetAnimSpriteInitialXOffset','InitSpritePosToAnimTarget','InitSpritePosToAnimAttacker'],
'src/sprite.c':['BeginAffineAnim','ContinueAffineAnim','AffineAnimDelay','AffineAnimCmd_end','AffineAnimCmd_frame','AffineAnimStateRestartAnim','AffineAnimStateStartAnim','ApplyAffineAnimFrameAbsolute','DecrementAffineAnimDelayCounter','ApplyAffineAnimFrameRelativeAndUpdateMatrix','ConvertScaleParam','GetAffineAnimFrame','ApplyAffineAnimFrame','StartSpriteAffineAnim']}
bodies=[];identities=[]
for path,names in functions.items():
 text=raw[path].decode()
 for name in names:
  match=re.search(r'^((?:static )?\w+ '+name+r'\([^;]*?\))\n\{',text,re.M);assert match,name
  start=match.start();pos=match.end();depth=1
  while depth:
   if text[pos]=='{':depth+=1
   if text[pos]=='}':depth-=1
   pos+=1
  body=text[start:pos];bodies.append((match[1],body));identities.append(dict(path=path,name=name,line=text.count('\n',0,start)+1,sha256=hashlib.sha256(body.encode()).hexdigest()))
constants=[]
text=raw['src/battle_anim_normal.c'].decode()
for name in ['sAffineAnim_HitSplat_'+str(i) for i in range(4)]+['sAffineAnims_HitSplat']:
 match=re.search(r'^static const union AffineAnimCmd [^\n]*'+name+r'\[\] =\n\{.*?^\};',text,re.M|re.S);assert match,name
 constants.append(match[0]);identities.append(dict(path='src/battle_anim_normal.c',name=name,line=text.count('\n',0,match.start())+1,sha256=hashlib.sha256(match[0].encode()).hexdigest()))
manifest=json.loads(imp.build(a.reference)['source-hit-animations']['manifest.json']);ops=['loadspritegfx','monbg','setalpha','playsewithpan','createsprite','createvisualtask','delay','waitforvisualfinish','clearmonbg','blendoff','end']
arrays=['struct Command {int args[5];};'];names=[]
for row in manifest['scripts']:
 bytecode=[];args=[];name=row['label'];names.append(name)
 for c in row['commands']:
  op=c['op'];v=c['args'];bytecode.append(ops.index(op));av=[]
  if op=='setalpha':av=list(map(int,v))
  elif op=='playsewithpan':av=[1 if v[0]=='SE_M_DOUBLE_SLAP' else 2]
  elif op=='createsprite':av=[int(v[0]=='gBasicHitSplatSpriteTemplate')]+[1 if x=='ANIM_TARGET' else int(x) for x in v[3:]]
  elif op=='createvisualtask':av=[1 if x=='ANIM_TARGET' else int(x) for x in v[2:]]
  args.append(av)
  if op in ['delay','clearmonbg']:bytecode.append(1 if v[0]=='ANIM_TARGET' else int(v[0]));args.append([])
 arrays.append('static const u8 '+name+'[]={'+','.join(map(str,bytecode))+'};')
 arrays.append('static const struct Command '+name+'_args[]={'+','.join('{{'+(','.join(map(str,v)) or '0')+'}}' for v in args)+'};')
arrays.append('static const u8 *scripts[]={'+','.join(names)+'};');arrays.append('static const struct Command *commands[]={'+','.join(x+'_args' for x in names)+'};')
source=(root/'tests/SourceHitAnimation.Oracle/host.h').read_text()+'\n'+'\n'.join(sig+';' for sig,body in bodies)+'\n'+'\n'.join(body for sig,body in bodies)+'\n'+'\n'.join(constants)+'\n'+'\n'.join(arrays)+'\n'+(root/'tests/SourceHitAnimation.Oracle/driver.c').read_text();(out/'oracle.c').write_text(source)
result=subprocess.run(['cc','-std=c99','-O0','-Wall','-Wextra','-Werror',out/'oracle.c','-o',out/'oracle'],capture_output=True,text=True);(out/'compile.log').write_text(result.stdout+result.stderr)
if result.returncode:raise SystemExit(result.stderr)
result=subprocess.run([out/'oracle'],capture_output=True,text=True,timeout=10);(out/'source-c.csv').write_text(result.stdout);assert result.returncode==0,result.stderr
(out/'provenance.json').write_text(json.dumps(dict(commit=imp.COMMIT,functions=identities[:len(bodies)],constantArrays=identities[len(bodies):],hostSha256=hashlib.sha256(source.encode()).hexdigest(),rows=len(result.stdout.splitlines())-1,originalRuntimeCompared=False),indent=2)+'\n');print('PASS independently compiled source C callbacks:',len(bodies),'functions + 5 arrays,',len(result.stdout.splitlines())-1,'frames')
