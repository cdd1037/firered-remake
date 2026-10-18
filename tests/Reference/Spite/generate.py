"""Pinned Spite and generic-wave callbacks, with a bounded authored host transport."""
import argparse,hashlib,json,re,subprocess,sys,tempfile
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser();p.add_argument('reference',nargs='?',type=Path);p.add_argument('--repo','--root',dest='repo',type=Path,required=True);p.add_argument('--scanline-source',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args();a.reference=a.reference or a.repo/'content/raw/pokefirered/037335f4';here=Path(__file__).resolve().parent
if a.output.exists():raise FileExistsError(a.output)
sys.path[:0]=[str(a.repo/'tests/Reference'),str(a.repo/'tests/Reference/Recovery')]
from reference_c import verify_reference,compile_c
from source_extract import function,array,macros,COMMIT
verify_reference(a.reference)
names={'src/battle_anim_ghost.c':['AnimTask_SpiteTargetShadow','AnimTask_SpiteTargetShadow_Step1','AnimTask_SpiteTargetShadow_Step2','AnimTask_SpiteTargetShadow_Step3'],'src/scanline_effect.c':['ScanlineEffect_Stop','ScanlineEffect_Clear','ScanlineEffect_SetParams','ScanlineEffect_InitHBlankDmaTransfer','CopyValue16Bit','CopyValue32Bit','TaskFunc_UpdateWavePerFrame','GenerateWave','ScanlineEffect_InitWave'],'src/battle_anim_mons.c':['CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet'],'src/blend_palette.c':['BlendPalette']}
paths=list(names)+['src/trig.c','include/gba/types.h','include/gba/io_reg.h','include/constants/battle_anim.h'];pins=json.loads((here/'pinned-inputs.json').read_text());sources={}
for path in paths:
 file=a.scanline_source if path=='src/scanline_effect.c' and a.scanline_source else a.reference/path;data=file.read_bytes()
 if hashlib.sha256(data).hexdigest()!=pins[path]:raise ValueError('Pinned input mismatch '+path)
 sources[path]=data.decode()
functions=[];extracted=[]
for path,ns in names.items():
 for name in ns:
  f=function(sources[path],name);functions.append(f);start=sources[path].index(f.rstrip());extracted.append(dict(path=path,name=name,startLine=sources[path][:start].count('\n')+1,sha256=hashlib.sha256(f.encode()).hexdigest()))
types=sources['include/gba/types.h'];types=types[types.index('struct OamData'):types.index('#define ST_OAM_HFLIP')]
wave=sources['src/scanline_effect.c'];wave=wave[wave.index('#define tStartLine'):wave.index('static void TaskFunc_UpdateWavePerFrame')]
constants=macros(list(sources.values()),['ANIM_TAG_BENT_SPOON','REG_OFFSET_DISPCNT','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_BG0HOFS','REG_ADDR_BG0HOFS','DISPCNT_BG1_ON','DISPCNT_BG2_ON','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDCNT_TGT1_BG1','BLDCNT_TGT1_BG2','BLDALPHA_BLEND'])
# REG_BASE is a host memory address; hardware transport is deliberately substituted.
constants=re.sub(r'^#define REG_BASE[^\n]*\n','',constants,flags=re.M)
c=(here/'prefix.c.in').read_text().replace('@TYPES@',types).replace('@MACROS@',constants+wave).replace('@SINE@',array(sources['src/trig.c'],'gSineTable')).replace('@PROTOTYPES@','\n'.join(f[:f.index('{')].strip()+';' for f in functions)).replace('@FUNCTIONS@','\n'.join(functions))
build=(a.build_dir or Path(tempfile.mkdtemp(prefix='spite-oracle-'))).resolve();build.mkdir(parents=True,exist_ok=True);(build/'reference.c').write_text(c)
for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(build/'reference.c'),'-o',str(build/opt)])
# Both BG destinations, target corners, logical versus sprite invisibility,
# clipped start row, affine copy, and genuine allocation failures. All frames.
selection=[(0,0,0,1,0,88,0,0,0),(0,1,0,1,0,48,0,0,0),(1,2,0,1,0,96,0,0,0),(1,3,0,1,0,40,0,0,0),(1,1,1,1,0,48,1,0,0),(1,0,0,1,1,88,1,0,0),(0,1,0,1,0,20,0,0,0),(0,0,0,0,0,88,0,0,0),(0,1,0,1,0,48,0,255,0),(0,1,0,1,0,48,0,15,0)]
cases=[]
for doubles,target,logical,free,affine,y,sprite,palette,unused in selection:
 args=[doubles,target,logical,free,affine,y,sprite,palette];runs=[subprocess.check_output([str(build/o),*map(str,args)]) for o in ['O0','O2']];assert runs[0]==runs[1],args
 rows=[json.loads(line) for line in runs[0].splitlines()];assert rows and not rows[-1]['alive'] and not rows[-1]['waveAlive'];cases.append(dict(id=f'spite-{len(cases):02}',doubles=bool(doubles),target=target,logical=bool(logical),spriteInvisible=bool(sprite),free=free,affine=bool(affine),y=y,paletteFailure=palette,rows=rows))
result=dict(schema='firered-spite-wave-v1',commit=COMMIT,sources={path:dict(sha256=pins[path],url='https://github.com/pret/pokefirered/blob/'+COMMIT+'/'+path) for path in paths},extractedFunctions=extracted,optimizationParity=['O0','O2'],semantics={'frame0':'Post-native visual initializer, before first OAM/task/VBlank pass. Row1 runs setup palette copy. Wave priority0 precedes visual priority2 on subsequent passes.','scanline':'Native retained banks, source-buffer swap and deferred-stop callback are extracted unchanged. Host DMA exposes each transferred 161-word frame; no CPU cycle claim.','palette':'Target normal/faded and copied palette all16 colors are retained. Source slot15 leaks its allocation and is explicitly rejected by managed invocation admission; slot255 and sprite capacity failures finish naturally.','material':'Clone uses original sprite fields; native matrix index is checked through fixed host matrix fields by managed separate ownership tests.'},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),terminalFrames=sorted({c['rows'][-1]['frame'] for c in cases}))))
