"""Extract pinned native Nightmare callbacks; no upstream script is executed."""
import argparse,hashlib,json,subprocess,sys,tempfile
from pathlib import Path
sys.dont_write_bytecode=True
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--repo',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args();here=Path(__file__).resolve().parent
if a.output.exists():raise FileExistsError(a.output)
sys.path.insert(0,str(a.repo/'tests/Reference'))
from reference_c import compile_c,verify_reference
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function,macros,identity,COMMIT
verify_reference(a.reference)
names={'src/battle_anim_ghost.c':['AnimTask_NightmareClone','AnimTask_NightmareClone_Step'],'src/battle_anim_mons.c':['CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet','TranslateSpriteLinearFixedPoint']}
paths=list(names)+['include/gba/types.h','include/gba/io_reg.h','include/constants/battle_anim.h'];pins=json.loads((here/'pinned-inputs.json').read_text());s={path:(a.reference/path).read_text() for path in paths}
for path in paths:
 if hashlib.sha256((a.reference/path).read_bytes()).hexdigest()!=pins[path]:raise ValueError('Pinned source mismatch '+path)
functions=[];extracted=[]
for path,ns in names.items():
 for name in ns:
  f=function(s[path],name);functions.append(f);at=s[path].index(f.rstrip());extracted.append(dict(path=path,name=name,startLine=s[path][:at].count('\n')+1,endLine=s[path][:at+len(f.rstrip())].count('\n')+1,sha256=hashlib.sha256(f.encode()).hexdigest()))
prototypes='\n'.join(f[:f.index('{')].strip()+';' for f in functions)
oam=s['include/gba/types.h'];types=oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
defs=macros(list(s.values()),['ST_OAM_OBJ_BLEND','ANIM_TARGET','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDALPHA_BLEND'])
c=(here/'prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',prototypes).replace('@FUNCTIONS@','\n'.join(functions))
build=(a.build_dir or Path(tempfile.mkdtemp(prefix='nightmare-oracle-'))).resolve();build.mkdir(parents=True,exist_ok=True);(build/'reference.c').write_text(c)
for opt in ['O0','O2']:compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(build/'reference.c'),'-o',str(build/opt)])
cases=[]
def add(d,actor,target,hidden,free,present,mapping):
 args=[d,actor,target,hidden,free,present,mapping];runs=[subprocess.check_output([str(build/opt),*map(str,args)]) for opt in ['O0','O2']];assert runs[0]==runs[1],args;rows=[json.loads(r) for r in runs[0].splitlines()];assert not rows[-1]['alive'];cases.append(dict(id=f'nightmare-{len(cases):03}',doubles=bool(d),actor=actor,target=target,hidden=bool(hidden),free=bool(free),present=bool(present),mapping=mapping,rows=rows))
for d in [0,1]:
 for actor in range(4 if d else 2):
  for mapping in [0,1]:
   for hidden in [0,1]:add(d,actor,actor^1,hidden,1,1,mapping)
  add(d,actor,actor^1,1,0,1,1);add(d,actor,actor^1,0,1,0,1)
result=dict(schema='firered-shadow-copy-source-v1',commit=COMMIT,sources=identity(a.reference,paths),extractedFunctions=extracted,optimizationParity=['O0','O2'],semantics={'frame0':'Native initializer; following frames invoke the live copy callback before the native visual task.','allocation':'All 64 source cells occupied except optional cell4. Native battler-to-sprite mapping is tested both identity and deliberately nonidentity. Numeric source sprite IDs do not become runtime slot identity.','body':'Static source centers with YDelta8, initial x2=3/y2=-2, true source subpriorities and priority2. Hidden present target remains cloneable and clone visibility is forced by the native helper.','hardware':'BLDCNT/BLDALPHA stored verbatim; no managed product used. DestroySprite requires usingSheet, records destruction, retains last fields for observation.','scope':'Nightmare only. Spite scanline source absent. Role Play opposite-party visual identity not supplied.'},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');report=dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),terminalFrames=sorted({c['rows'][-1]['frame'] for c in cases}),fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest());(build/'verification.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
