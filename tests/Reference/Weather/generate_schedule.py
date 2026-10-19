#!/usr/bin/env python3
"""Exact bounded source script wait/creation scheduling and crescent callbacks."""
import argparse,json,re,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function,array,macros,identity,COMMIT,verify
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);a=p.parse_args();r=a.reference;out=Path(__file__).resolve().parent
paths=['src/battle_anim.c','src/battle_anim_rock.c','src/sprite.c','include/sprite.h','include/gba/types.h','include/constants/battle.h','include/constants/battle_anim.h','data/battle_anim_scripts.s'];verify(r,paths, Path(__file__).resolve().parent / 'pinned-inputs.json');s={p:(r/p).read_text() for p in paths}
h=s['include/sprite.h'];types=h[h.index('enum\n{\n    SUBSPRITES_OFF'):h.index('struct Sprite;')];h=s['include/gba/types.h'];types+='\n'+h[h.index('struct PlttData'):h.index('struct BgAffineSrcData')]
funcs={'src/battle_anim.c':['WaitAnimFrameCount','RunAnimScriptCommand','Cmd_loadspritegfx','Cmd_createsprite','Cmd_delay','Cmd_waitforvisualfinish'],'src/sprite.c':['CreateSpriteAndAnimate','AnimateSprites','SetSubspriteTables'],'src/battle_anim_rock.c':['AnimFlyingSandCrescent']}
bodies=[function(s[p],n, signed_byte=False) for p,ns in funcs.items() for n in ns];protos='\n'.join(f[:f.index('{')].strip()+';' for f in bodies)
def root(name):
 text=s['data/battle_anim_scripts.s'].split(name+':\n',1)[1];lines=[]
 for line in text.splitlines():
  line=line.split('@')[0].strip()
  if not line:continue
  if ':' in line:raise ValueError('Unexpected root boundary')
  lines.append(line)
  if line=='end':return lines
 raise ValueError('Missing end')
def word(x):return [x&255,(x>>8)&255]
def encode(lines):
 b=[]
 for line in lines:
  op,*tail=line.split(None,1);args=[v.strip() for v in tail[0].split(',')] if tail else []
  if op=='loadspritegfx':assert args==['ANIM_TAG_FLYING_DIRT'];b += [0,0,0]
  elif op=='createvisualtask':b += [3,0,0,0,0,int(args[1]),len(args)-2]+[0]*(2*(len(args)-2))
  elif op=='createsprite':
   assert args[:3]==['gFlyingSandCrescentSpriteTemplate','ANIM_ATTACKER','40'];b += [2,1,0,0,0,40,len(args)-3]+sum([word(int(v)) for v in args[3:]],[])
  elif op=='delay':b += [4,int(args[0])]
  elif op=='playsewithpan':b += [25,0,0,0]
  elif op=='panse':b += [27,0,0,0,0,0,0]
  elif op=='end':b += [8]
  else:raise ValueError(op)
 return b
roots={201:root('Move_SANDSTORM'),257:root('Move_HEAT_WAVE')};arrays='\n'.join(array(s['src/battle_anim_rock.c'],n) for n in ['sFlyingSandSubsprites','sFlyingSandSubspriteTable']);scripts='\n'.join('static const u8 '+('sandScript' if k==201 else 'heatScript')+'[]={'+','.join(map(str,encode(v)))+'};' for k,v in roots.items())
c=(out/'schedule-prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',macros(list(s.values()),['B_SIDE_PLAYER','ANIMSPRITE_IS_TARGET'])).replace('@PROTOTYPES@',protos).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(bodies)).replace('@SCRIPTS@',scripts);(out/'schedule.c').write_text(c)
for opt in ['O0','O2']:subprocess.run(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(out/'schedule.c'),'-o',str(out/('schedule-'+opt))],check=True)
cases=[]
for move,commands in roots.items():
 for actor in range(4):
  raw=[subprocess.check_output([str(out/('schedule-'+opt)),str(move),str(actor)]) for opt in ['O0','O2']];assert raw[0]==raw[1];rows=[json.loads(x) for x in raw[0].splitlines()];summary=rows.pop();cases.append(dict(move=move,attacker=actor,commands=commands,rows=rows,**summary))
result=dict(schema='firered-weather-script-schedule-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=funcs,optimization_parity=['O0','O2'],semantics={'frameZero':'First callback1 RunAnimScriptCommand invocation; controller setup before this point is excluded.','frameOrder':'Script callback, then source AnimateSprites including creation-frame callback.','visibleOamCount':'Every admitted crescent is source-visible (invisible flag unset); offscreen allocated sprites still count.','fieldIntersectCount':'Logical 64x16 compound rectangle intersects 240x112 field; this is a geometric diagnostic, not opaque-pixel counting.','stubs':['graphics loads and palette bookkeeping do not change waits','task/audio instructions preserve encoded byte widths but only advance the script pointer; callbacks are covered by existing task fixtures','end stops script command execution without waiting for unrelated BG/audio tasks; all created crescents continue until destruction','CreateSpriteAt initializes selected fields and records allocation instance; static frame-program AnimateSprite has no callback effect','T2_READ_32 resolves the one encoded template id to a native pointer without imposing a 32-bit host pointer ABI']},cases=cases)
(out/'schedule-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('schedule',[(c['move'],c['attacker'],c['birthFrames'],c['destructionFrames'],c['peakRawCount'],c['peakVisibleOamCount']) for c in cases])
