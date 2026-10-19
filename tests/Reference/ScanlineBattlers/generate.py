#!/usr/bin/env python3
"""Extract pinned four-root callbacks. Hardware/VBlank queue is tested by shared scanline vectors."""
import argparse,hashlib,importlib.util,json,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference,out,full=False):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('source_extract',root/'tests/Reference/source_extract.py');here=Path(__file__).parent;pins=json.loads((here/'pinned-inputs.json').read_text());src={p:(reference/p).read_text() for p in pins}
 for p,h in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=h:raise ValueError('Pinned input differs: '+p)
 if '#define Q_8_8(n) ((s16)((n) * 256))' not in src['include/global.h']:raise ValueError('Source sine conversion differs')
 names={'src/battle_anim_ground.c':['AnimTask_DigDownMovement','AnimTask_DigBounceMovement','AnimTask_DigDisappear','AnimTask_DigUpMovement','AnimTask_DigSetVisibleUnderground','AnimTask_DigRiseUpFromHole','SetDigScanlineEffect','AnimDirtPlumeParticle','AnimDirtPlumeParticle_Step','AnimDigDirtMound'],'src/battle_anim_effects_2.c':['AnimTask_SketchDrawMon','AnimTask_SketchDrawMon_Step','AnimPencil','AnimPencil_Step'],'src/battle_anim_dragon.c':['AnimDragonDanceOrb','AnimDragonDanceOrb_Step','AnimTask_DragonDanceWaver','AnimTask_DragonDanceWaver_Step','UpdateDragonDanceScanlineEffect'],'src/battle_anim_effects_3.c':['AnimTask_AcidArmor','AnimTask_AcidArmor_Step'],'src/battle_anim_mons.c':['WaitAnimForDuration','InitAnimArcTranslation','TranslateAnimHorizontalArc','InitAnimLinearTranslation','AnimTranslateLinear','AnimTask_BlendPalInAndOutByTag','AnimTask_BlendMonInAndOutSetup','AnimTask_BlendMonInAndOut_Step'],'src/trig.c':['Sin','Cos']}
 funcs=[x.function(src[p],n) for p,ns in names.items() for n in ns];prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in funcs)
 c=(here/'harness.c.in').read_text().replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',x.array(src['src/trig.c'],'gSineTable')).replace('@FUNCTIONS@','\n'.join(funcs));source=out/'scanline-battlers.c';source.write_text(c);exes=[]
 for opt in ['O0','O2']:
  exe=out/('scanline-battlers-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(source),'-o',str(exe)]);exes.append(exe)
 cases=[]
 def add(kind,d,a,t,args=(),h=64,w=64,y=0,e=0,seed=0,bank=0,hidden=0,label='authored'):
  args=list(args)+[2000+i for i in range(len(args),8)];argv=[kind,d,a,t,h,w,y,e,seed,bank,*args,hidden];outputs=[subprocess.check_output([str(exe),*map(str,argv)],text=True) for exe in exes]
  if outputs[0]!=outputs[1]:raise ValueError('Optimization mismatch '+str(argv))
  rows=[json.loads(line) for line in outputs[0].splitlines()]
  if rows[-1]['alive']:raise ValueError('Incomplete life')
  # Lossless deltas retain every buffer word at every selected callback step.
  previous=[(b*4093+i*17)&65535 if seed else 0 for b in range(2) for i in range(960)]
  for r in rows:
   values=r.pop('buffers');r['writes']=[[i,v] for i,v in enumerate(values) if previous[i]!=v];previous=values
  cases.append(dict(kind=kind,doubles=bool(d),actor=a,target=t,height=h,width=w,yOffset=y,elevation=e,seeded=bool(seed),writeBuffer=bank,hidden=bool(hidden),arguments=args,branch=label,rows=rows))
 layouts=[(0,0,1),(0,1,0),(1,2,3),(1,3,2)] if not full else [(d,a,t) for d in [0,1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a!=t]
 for d,a,t in layouts:
  for mode in [0,1]:add(0,d,a,t,[mode],hidden=mode);add(1,d,a,t,[mode],hidden=1)
  add(2,d,a,t);add(3,d,a,t);add(4,d,a,t,[0]);add(7,d,a,t)
  for angle in [0,43,85,128,170,213]:add(8,d,a,t,[angle])
  for side in [0,1]:add(5,d,a,t,[0,side,12,4,-16,18]);add(6,d,a,t,[0,side,48])
 # Real native picture bounds and retained-bank handoffs, not a Cartesian matrix.
 for kind in [2,3,4]:add(kind,1,3,0,[1] if kind==4 else [],h=8,w=16,y=7,e=40,seed=1,bank=1,label='short-picture-elevated-retained-bank')
 add(4,0,0,1,[1],h=32,w=48,y=20,e=41,seed=1,label='byte-y-wrap')
 add(7,0,1,0,[],h=8,w=16,y=7,e=20,label='short-pencil')
 add(6,0,0,1,[0,1,0],label='zero-wait-installs-then-destroys')
 add(5,0,1,0,[1,1,0,-4,0,1],label='arc-single-tick-zero-amplitude')
 add(5,0,0,1,[0,0,32767,-32768,-16,1],label='signed-destination-wrap')
 for peak,delay,cycles in [(14,0,3),(1,1,1),(16,3,2)]:add(9,0,0,1,[10000,19456,peak,delay,cycles],label='tagged-pulse-excludes-index-zero')
 return dict(schema='firered-scanline-battlers-source-v1',profile='full' if full else 'representative',sourceSha256=pins,optimizationParity=['O0','O2'],semantics='Original pinned task and sprite callbacks; static dummy sprite tables do not overwrite native tile offsets. Every callback frame, both 960-word banks (lossless delta), retained args, task words, BG and alpha writes, visibility and audio are observed. Between callback rows only source stop/flip semantics are modeled; GPU queued row-zero precedence belongs to the shared scanline engine oracle. No source script is executed.',cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full',action='store_true');a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='scanline-battlers-reference-'));out.mkdir(parents=True,exist_ok=True);data=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.full);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(data['cases']),rows=sum(len(c['rows']) for c in data['cases']))))
