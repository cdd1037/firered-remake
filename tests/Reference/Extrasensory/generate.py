#!/usr/bin/env python3
"""Pinned legal-index callbacks and a separately labeled approved periodic-boundary corpus."""
import argparse,hashlib,importlib.util,json,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference,out,full=False):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 x=module('source_extract',root/'tests/Reference/source_extract.py');here=Path(__file__).parent;pins=json.loads((here/'pinned-inputs.json').read_text());src={p:(reference/p).read_text() for p in pins}
 for p,h in pins.items():
  if hashlib.sha256((reference/p).read_bytes()).hexdigest()!=h:raise ValueError('Pinned source differs: '+p)
 names={'src/battle_anim_psychic.c':['AnimTask_ExtrasensoryDistortion','AnimTask_ExtrasensoryDistortion_Step','AnimTask_TransparentCloneGrowAndShrink','AnimTask_TransparentCloneGrowAndShrink_Step'],'src/battle_anim_mons.c':['GetAnimBattlerSpriteId','CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet','SetSpriteRotScale','ShouldRotScaleSpeciesBeFlipped','SetBattlerSpriteYOffsetFromOtherYScale'],'src/sprite.c':['AllocOamMatrix','FreeOamMatrix','CalcCenterToCornerVec']}
 funcs=[x.function(src[p],n) for p,ns in names.items() for n in ns];prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in funcs);arrays=x.array(src['src/trig.c'],'gSineTable')+'\n'+x.array(src['src/sprite.c'],'sCenterToCornerVecTable');body='\n'.join(funcs)
 if body.count('gSineTable[sineIndex]')!=1:raise ValueError('Distortion sine load differs')
 h=(here/'harness.c.in').read_text().replace('@PROTOTYPES@',prototypes).replace('@ARRAYS@',arrays);exes={}
 for policy in [False,True]:
  c=out/('extrasensory-'+('periodic-policy' if policy else 'legal-source')+'.c');c.write_text(h.replace('@FUNCTIONS@',body.replace('gSineTable[sineIndex]','SamplePeriodicSine(sineIndex)') if policy else body).replace('@POLICY@','1' if policy else '0'))
  exes[policy]=[]
  for opt in ['O0','O2']:
   exe=c.with_suffix('.'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(c),'-o',str(exe)]);exes[policy].append(exe)
 cases=[]
 def add(kind,d,a,t,arg=0,y=0,e=0,seed=0,bank=0,hidden=0,absent=-1,failure=0,policy=False,label='authored'):
  args=[arg,*range(2001,2008)];argv=[kind,d,a,t,y,e,seed,bank,hidden,absent,failure,*args];outputs=[subprocess.check_output([str(exe),*map(str,argv)],text=True) for exe in exes[policy]]
  if outputs[0]!=outputs[1]:raise ValueError('Optimization mismatch '+str(argv))
  rows=[json.loads(line) for line in outputs[0].splitlines()]
  if rows[-1]['alive']:raise ValueError('Incomplete callback life')
  previous=[(b*4093+i*17)&65535 if seed else 0 for b in range(2) for i in range(960)]
  for row in rows:
   values=row.pop('buffers');row['writes']=[[i,v] for i,v in enumerate(values) if v!=previous[i]];previous=values
  cases.append(dict(kind=kind,doubles=bool(d),actor=a,target=t,yOffset=y,elevation=e,seeded=bool(seed),writeBuffer=bank,hidden=bool(hidden),absent=absent,failure=failure,policy=policy,branch=label,arguments=args,rows=rows))
 layouts=[(0,0,1),(0,1,0),(1,2,3),(1,3,2)] if not full else [(d,a,t) for d in [0,1] for a in range(4 if d else 2) for t in range(4 if d else 2) if a!=t]
 for d,a,t in layouts:
  for mode in [0,2]:add(0,d,a,t,mode)
  add(0,d,a,t,1,policy=True,label='approved-periodic-index-320')
  add(1,d,a,t)
 # Original source only: clamped top reduces stage1 to a legal final index318.
 add(0,0,0,1,1,e=9,seed=1,bank=1,label='legal-index-318-clamped-top')
 add(0,1,2,3,1,e=32,seed=1,label='legal-clamped-zero-y')
 add(0,0,0,1,1,e=8,seed=1,bank=1,policy=True,label='approved-boundary-final-index-320')
 add(0,0,0,1,2,e=41,seed=1,bank=1,label='byte-y-wrap-retains-all-other-rows')
 for selector in [1,2,3]:add(1,1,0,1,selector,y=20,e=48,seed=1,label='target-or-partner-copy-height-and-wrap')
 add(1,0,0,1,hidden=1,label='present-hidden-direct-copy-is-visible')
 add(1,1,0,1,2,hidden=1,label='hidden-partner-does-not-clone')
 add(1,0,0,1,absent=0,label='absent-source-releases-matrix')
 add(1,0,0,1,failure=1,label='matrix-full-no-object-allocation')
 add(1,0,0,1,failure=2,label='object-full-releases-matrix')
 return dict(schema='firered-extrasensory-source-v1',profile='full' if full else 'representative',sourceSha256=pins,optimizationParity=['O0','O2'],semantics='policy=false extracts unchanged pinned callbacks and rejects any out-of-range sine access before execution. policy=true substitutes only the one distortion sine read with the explicitly approved periodic sampler; these rows are policy expectations, not claimed original OOB behavior. Both profiles record every callback, both retained 960-word banks, stop/flip lifecycle, source clone visibility, native allocation bitmap, exact angle-zero matrices and source height correction. Sprite destruction observation stops at inUse; opaque task handles are not runtime contracts. Hardware queued row0 is owned by the shared scanline oracle.',cases=cases)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);p.add_argument('--full',action='store_true');a=p.parse_args()
 if a.output.exists():raise FileExistsError(a.output)
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='extrasensory-reference-'));out.mkdir(parents=True,exist_ok=True);r=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.full);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(r['cases']),sourceCases=sum(not c['policy'] for c in r['cases']),policyCases=sum(c['policy'] for c in r['cases']),rows=sum(len(c['rows']) for c in r['cases']))))
