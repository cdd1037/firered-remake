"""Pinned original punch callbacks; safe initialization is separately labelled."""
import argparse,hashlib,json,subprocess,sys,tempfile
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--repo',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args();here=Path(__file__).resolve().parent
if a.output.exists():raise FileExistsError(a.output)
sys.path[:0]=[str(a.repo/'tests/Reference'),str(a.repo/'tests/Reference/CallerPrelude')]
from reference_c import verify_reference,compile_c
from source_extract import function,identity,COMMIT,macros
verify_reference(a.reference)
names={'src/battle_anim_mons.c':['GetBattlerSpriteSubpriority','AnimTask_AttackerPunchWithTrace','AnimTask_AttackerPunchWithTrace_Step','CreateBattlerTrace','AnimBattlerTrace','CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet'],'src/blend_palette.c':['BlendPalette']}
paths=list(names)+['include/gba/types.h','include/constants/battle.h','include/constants/battle_anim.h'];pins=json.loads((here/'pinned-inputs.json').read_text());sources={path:(a.reference/path).read_text() for path in paths}
for path in paths:
 if hashlib.sha256((a.reference/path).read_bytes()).hexdigest()!=pins[path]:raise ValueError('Pinned input mismatch '+path)
functions=[];extracted=[]
for path,ns in names.items():
 for name in ns:
  f=function(sources[path],name);functions.append(f);start=sources[path].index(f.rstrip());extracted.append(dict(path=path,name=name,startLine=sources[path][:start].count('\n')+1,sha256=hashlib.sha256(f.encode()).hexdigest()))
text=sources['include/gba/types.h'];types=text[text.index('struct OamData'):text.index('#define ST_OAM_HFLIP')]
mono=sources['src/battle_anim_mons.c'];taskmacros=mono[mono.index('#define tBattlerSpriteId data[0]',mono.index('// Task and sprite data for AnimTask_AttackerPunchWithTrace')):mono.index('void AnimTask_AttackerPunchWithTrace(u8 taskId)')]
c=(here/'prefix.c.in').read_text().replace('@TYPES@',types).replace('@MACROS@',taskmacros+macros(list(sources.values()),['ANIM_TAG_BENT_SPOON','B_POSITION_PLAYER_LEFT','B_POSITION_PLAYER_RIGHT','B_POSITION_OPPONENT_LEFT'])).replace('@PROTOTYPES@','\n'.join(f[:f.index('{')].strip()+';' for f in functions)).replace('@FUNCTIONS@','\n'.join(functions))
build=(a.build_dir or Path(tempfile.mkdtemp(prefix='punch-trace-oracle-'))).resolve();build.mkdir(parents=True,exist_ok=True);(build/'reference.c').write_text(c)
for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(build/'reference.c'),'-o',str(build/opt)])
# Explicit semantic selection. Every callback row is retained, no frame sampling.
selection=[(0,0,8,3,0,0),(0,1,8,3,1,0),(1,0,8,3,1,0),(1,1,8,3,0,1),(1,2,8,3,1,0),(1,3,8,3,0,1),(1,0,0,3,1,0),(1,1,1,3,0,1),(0,0,8,32760,0,0),(0,1,8,-32760,1,1)]
cases=[]
for doubles,actor,free,x,hidden,style in selection:
 args=[doubles,actor,free,x,hidden,style];runs=[subprocess.check_output([str(build/o),*map(str,args)]) for o in ['O0','O2']];assert runs[0]==runs[1],args
 rows=[json.loads(line) for line in runs[0].splitlines()];assert not rows[-1]['alive'];cases.append(dict(id=f'punch-{len(cases):02}',doubles=bool(doubles),actor=actor,free=free,initialX=x,hidden=bool(hidden),style=style,rows=rows))
result=dict(schema='firered-punch-trace-policy-v1',commit=COMMIT,sources=identity(a.reference,paths),extractedFunctions=extracted,optimizationParity=['O0','O2'],semantics={'native':'All eight functions are extracted unchanged. Callback/task execution and 16-bit arithmetic are native.','policyBoundary':'After the native initializer, the harness explicitly restores intended x2 and initializes destination normal/faded palette from the actor then blends. This models the approved safe policy, NOT native initializer equivalence. nativeInitialX and nativePalette preserve the historical result as separate evidence.','frame0':'Post-policy initializer. Subsequent frames advance existing clones in numeric sprite order, then the source task. Task-created clones first appear in next-frame OAM.','ids':'Actor source ID is deliberately nonidentity (20,6,47,39). Native free cells 8..15 map by ascending rank to managed free cells. IDs are handles only after policy initialization.','palette':'Palette bank ownership is source-shaped; managed immutable actor-normal tint is compared per opaque RGB555 pixel, not as invented native indices.','capacity':'0/1/8 free cells exercise natural failed allocation and same-cell reuse. Raw clones are not visual-count owners.'},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),terminalFrames=sorted({c['rows'][-1]['frame'] for c in cases})),indent=2))
