"""Explicit pinned-source Memento callbacks; authored host transport, no product calls."""
import argparse, hashlib, json, os, subprocess, sys, tempfile
from pathlib import Path
sys.dont_write_bytecode = True
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--repo',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
if a.output.exists():raise FileExistsError(a.output)
here=Path(__file__).resolve().parent
sys.path[:0]=[str(a.repo/'tests/Reference')]
from reference_c import verify_reference, compile_c, COMMIT
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function, macros
verify_reference(a.reference)
names={'src/battle_anim_dark.c':['AnimTask_MoveAttackerMementoShadow','AnimTask_MoveAttackerMementoShadow_Step','AnimTask_MoveTargetMementoShadow','AnimTask_MoveTargetMementoShadow_Step','DoMementoShadowEffect','SetAllBattlersSpritePriority','AnimTask_InitMementoShadow','AnimTask_MementoHandleBg'], 'src/scanline_effect.c':['ScanlineEffect_Clear','ScanlineEffect_SetParams','ScanlineEffect_InitHBlankDmaTransfer','CopyValue16Bit','CopyValue32Bit']}
pins=json.loads((here/'pinned-inputs.json').read_text());sources={}
for path,pin in pins.items():
 data=(a.reference/path).read_bytes()
 if hashlib.sha256(data).hexdigest()!=pin:raise ValueError('Pinned source mismatch '+path)
 sources[path]=data.decode()
functions=[];extracted=[]
for path,ns in names.items():
 for name in ns:
  raw=function(sources[path],name);code=raw.replace('(task->data[7] - task->data[6]) << 8','(task->data[7] - task->data[6]) * 256').replace('task->data[6] << 8','task->data[6] * 256')
  functions.append(code);extracted.append(dict(path=path,name=name,startLine=sources[path][:sources[path].index(raw.rstrip())].count('\n')+1,sha256=hashlib.sha256(raw.encode()).hexdigest()))
mac=macros(list(sources.values()),['REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_WININ','REG_OFFSET_WINOUT','BLDCNT_TGT2_ALL','BLDCNT_EFFECT_BLEND','BLDCNT_TGT1_BG1','BLDCNT_TGT1_BG2','BLDALPHA_BLEND','WINOUT_WIN01_BG1','WINOUT_WIN01_BG2','WINOUT_WIN01_BG0','WINOUT_WIN01_BG3','WINOUT_WIN01_BG_ALL','WINOUT_WIN01_OBJ','WINOUT_WIN01_CLR','WINOUT_WINOBJ_BG_ALL','WINOUT_WINOBJ_OBJ','WINOUT_WINOBJ_CLR','WININ_WIN0_BG_ALL','WININ_WIN0_OBJ','WININ_WIN0_CLR','WININ_WIN1_BG_ALL','WININ_WIN1_OBJ','WININ_WIN1_CLR'])
code=(here/'host.c.in').read_text().replace('@MACROS@',mac).replace('@PROTOTYPES@','\n'.join(f[:f.index('{')].strip()+';' for f in functions)).replace('@FUNCTIONS@','\n'.join(functions))
build=(a.build_dir or Path(tempfile.mkdtemp(prefix='memento-reference-'))).resolve();build.mkdir(parents=True,exist_ok=True);(build/'reference.c').write_text(code)
for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(build/'reference.c'),'-o',str(build/opt)])
selection=[(mode,doubles,slot,48,8,0,0) for mode in [0,1] for doubles in [0,1] for slot in range(4 if doubles else 2)]
selection += [(0,1,2,64,0,0,1),(1,1,3,64,0,24,1),(1,0,1,64,0,36,0)]
cases=[]
for mode,doubles,slot,height,offset,elevation,hidden in selection:
 args=[mode,doubles,slot,height,offset,elevation,hidden]
 runs=[subprocess.check_output([str((build/opt).with_suffix('.exe') if os.name=='nt' else build/opt),*map(str,args)]) for opt in ['O0','O2']]
 if runs[0]!=runs[1]:raise AssertionError('Optimization mismatch '+str(args))
 rows=[json.loads(line) for line in runs[0].splitlines()]
 if not rows or rows[-1]['alive']:raise AssertionError('Incomplete task '+str(args))
 cases.append(dict(id='memento-'+str(len(cases)),mode=mode,doubles=bool(doubles),slot=slot,height=height,offset=offset,elevation=elevation,hiddenPartner=bool(hidden),rows=rows))
# The source zero-height branch is not reached by normal eight-pixel task steps.
# Exercise it explicitly rather than silently claiming ordinary roots cover it.
helper=[]
for top,bottom,sourceTop,sourceBottom,base,bank in [(0,0,-7,79,-18,0),(-12,-12,19,111,-56,1),(-7,39,17,79,65520,0),(-64,71,-11,71,27,1),(5,13,19,111,-64,0)]:
 args=[2,top,bottom,sourceTop,sourceBottom,base,bank]
 runs=[subprocess.check_output([str((build/o).with_suffix('.exe') if os.name=='nt' else build/o),*map(str,args)]) for o in ['O0','O2']]
 if runs[0]!=runs[1]:raise AssertionError('Helper optimization mismatch')
 helper.append(dict(args=args[1:],row=json.loads(runs[0])))
result=dict(schema='firered-memento-v1',commit=COMMIT,sources=pins,extractedFunctions=extracted,optimizationParity=['O0','O2'],semantics={'transport':'Native two-bank scanline setup, seed, bank swap and deferred stop are extracted. Host SetGpuReg stores values immediately; task vectors intentionally start with no queued BG writes. Root pixel tests separately exercise the shared explicit VBlank model. CPU/VCOUNT line-zero cycle timing is not claimed.', 'portableSignedArithmetic':'Two signed left-shifts by eight are replaced by multiplication by256 within proven int32 bounds. Signed right-shift remains the arithmetic shift used by both supported host compilers and ARM source target.', 'phase':'Row0 is the native initializer. Each later row runs one callback then source VBlank. Target initializer performs setup state0 immediately; its later four setup states remain distinct.', 'retained':'Both banks begin with per-index sentinels. Sparse writes reconstruct all1920 words. The first112-row fills leave retained bank tails intact.'},cases=cases,helper=helper)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),helpers=len(helper))))
