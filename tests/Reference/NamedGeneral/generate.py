#!/usr/bin/env python3
"""Extract only the newly used callbacks. Never execute the terminal undefined read.

The Doom source has four coordinates but enters phase 1 a fifth time. Native
vectors stop immediately before that read. A separate directly-entered phase-5
case proves cleanup; it is not represented as a source transition across UB.
"""
import argparse,hashlib,io,json,struct,subprocess,sys,tempfile
from PIL import Image
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser();p.add_argument('--repo',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.reference=a.reference or a.repo/'content/raw/pokefirered/037335f4'
if a.output.exists():raise FileExistsError(a.output)
here=Path(__file__).resolve().parent;sys.path[:0]=[str(a.repo/'tests/Reference'),str(a.repo/'tests/Reference/Recovery')]
from reference_c import verify_reference,compile_c
from source_extract import function,array,macros,enum_containing,COMMIT
verify_reference(a.reference);pins=json.loads((here/'pinned-inputs.json').read_text());sources={}
for path,digest in pins.items():
 data=(a.reference/path).read_bytes()
 if hashlib.sha256(data).hexdigest()!=digest:raise ValueError('Pinned input mismatch '+path)
 if path.endswith(('.c','.h','.s')):sources[path]=data.decode()
fx=sources['src/battle_anim_effects_3.c'];utility=sources['src/battle_anim_utility_funcs.c']
functions=[function(fx,'AnimTask_DoomDesireLightBeam'),function(utility,'AnimTask_SetAnimTargetToBattlerTarget')]
arrays=[array(fx,'sDoomDesireLightBeamCoordTable'),array(fx,'sDoomDesireLightBeamDelayTable')]
constants=macros(list(sources.values()),['BLDCNT_TGT2_ALL','BLDCNT_EFFECT_BLEND','BLDCNT_TGT1_BG1','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','B_POSITION_PLAYER_RIGHT','B_POSITION_OPPONENT_RIGHT','B_SIDE_OPPONENT','BG_PLTT_ID','PLTT_SIZE_4BPP'])+enum_containing(sources['include/battle_anim.h'],'BG_ANIM_SCREEN_SIZE')
c=(here/'prefix.c.in').read_text().replace('@MACROS@',constants).replace('@FUNCTIONS@','\n'.join(arrays+functions))
# Independent source PNG/tilemap decoder, with a fixed RGB555 back layer.
# No product resource generator or managed compositor supplies expected pixels.
with Image.open(io.BytesIO((a.reference/'graphics/battle_anims/masks/light_beam.png').read_bytes())) as im:
 assert im.mode=='P';tiles=[list(im.crop((x,y,x+8,y+8)).tobytes()) for y in range(0,im.height,8) for x in range(0,im.width,8)];colors=im.getpalette()[:48]
tiles+= [[0]*64]*(256-len(tiles));palette=[sum((colors[i*3+c]//8)<<(c*5) for c in range(3)) for i in range(16)];tilemap=struct.unpack('<1024H',(a.reference/'graphics/battle_anims/masks/light_beam.bin').read_bytes())
def pixel_samples(row):
 output=[];indices=[];under=7|(11<<5)|(17<<10)
 for y in range(112):
  for x in range(240):
   color=under;index=0
   if row['overlay']:
    sx=(x+row['x'])&255;sy=(y+row['y'])&255;entry=tilemap[(sy//8)*32+sx//8];tx=sx&7;ty=sy&7
    if entry&1024:tx=7-tx
    if entry&2048:ty=7-ty
    index=tiles[entry&1023][ty*8+tx]
    if index:
     assert entry>>12==8;color=palette[index];color=sum(min(31,(((color>>shift)&31)*row['alpha'][0]+((under>>shift)&31)*row['alpha'][1])>>4)<<shift for shift in (0,5,10))
   output.append([(color&31)*255//31,((color>>5)&31)*255//31,((color>>10)&31)*255//31,255]);indices.append(index)
 points={(x,y) for y in (0,1,8,24,48,72,96,111) for x in (0,1,8,24,48,80,112,144,176,208,238,239)}
 for y in (5,28,55,84,110):
  for x in range(1,240):
   if (indices[y*240+x]!=0)!=(indices[y*240+x-1]!=0):
    for dx in (-1,0,1):
     if 0<=x+dx<240:points.add((x+dx,y))
 return [dict(x=x,y=y,index=indices[y*240+x],rgba=output[y*240+x]) for x,y in sorted(points,key=lambda p:(p[1],p[0]))]

with tempfile.TemporaryDirectory(prefix='general-oracle-') as tmp:
 root=Path(tmp);source=root/'reference.c';source.write_text(c)
 for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv',str(source),'-o',str(root/opt)])
 cases=[]
 for doubles,target in [(0,0),(0,1),(1,0),(1,1),(1,2),(1,3)]:
  runs=[subprocess.check_output([str(root/opt),str(doubles),str(target)],text=True) for opt in ['O0','O2']]
  if runs[0]!=runs[1]:raise ValueError('Native optimization output differs')
  rows=[json.loads(line) for line in runs[0].splitlines()];assert rows[-2]['undefinedNext'] and not rows[-1]['active'] and rows[-1]['cleanupOnly']
  for row in rows:
   if row['frame'] in {0,1,2,4,16,44,80,115,116}:row['pixelSamples']=pixel_samples(row)
  cases.append(dict(doubles=bool(doubles),target=target,rows=rows))
 result=dict(schema='named-general-native-v1',commit=COMMIT,sources=pins,functions=[dict(name=name,sha256=hashlib.sha256(body.encode()).hexdigest()) for name,body in zip(['AnimTask_DoomDesireLightBeam','AnimTask_SetAnimTargetToBattlerTarget'],functions)],optimizationParity=['O0','O2'],semantics={'terminal':'Four defined positions only. Native sequence stops before phase1/index4. Final row directly invokes source phase5 cleanup, independently of the undefined transition.','frame0':'Immediate source visual task callback, before later task ticks.','target':'Before each case the original synchronous setter receives all four initial targets; it selects the supplied committed target and releases its visual count.'},cases=cases)
 a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),undefinedFrames=sorted({c['rows'][-2]['frame'] for c in cases}))))
