#!/usr/bin/env python3
"""Extract original load/delay scheduling for the two caller-prelude roots."""
import argparse,hashlib,json,subprocess,sys,tempfile
from pathlib import Path
sys.dont_write_bytecode=True
HERE=Path(__file__).resolve().parent
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--build-dir',type=Path);p.add_argument('--reference-helper',type=Path,default=HERE.parent/'reference_c.py');p.add_argument('--output',type=Path,default=HERE.parent.parent/'Game.Presentation.Tests/Fixtures/caller-prelude-schedule-source-vectors.json');a=p.parse_args()
if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
sys.path.insert(0,str(a.reference_helper.resolve().parent));from reference_c import compile_c
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function,identity,verify,COMMIT
r=a.reference;out=(a.build_dir or Path(tempfile.mkdtemp(prefix='caller-prelude-schedule-'))).resolve();out.mkdir(parents=True,exist_ok=True)
paths=['src/battle_anim.c','data/battle_anim_scripts.s'];verify(r,paths, Path(__file__).resolve().parent / 'pinned-inputs.json');source=(r/paths[0]).read_text();script=(r/paths[1]).read_text();names=['WaitAnimFrameCount','RunAnimScriptCommand','Cmd_loadspritegfx','Cmd_delay'];funcs=[function(source,n) for n in names]
cases=[]
for move,name in [(118,'Move_METRONOME'),(214,'Move_SLEEP_TALK')]:
 lines=[]
 for line in script.split(name+':\n',1)[1].splitlines():
  line=line.split('@')[0].strip()
  if not line:continue
  if line=='waitforvisualfinish':break
  if ':' in line or line in ['return','end']:raise ValueError('Unexpected prelude root boundary')
  lines.append(line)
 data=[];templates=[]
 for line in lines:
  if line.startswith('loadspritegfx '):data += [0,0,0]
  elif line.startswith('delay '):data += [4,int(line.split()[1])]
  elif line.startswith('createsprite '):data += [2];templates.append(line.split()[1].rstrip(','))
  else:data += [3]
 data+=[8]
 text=(HERE/'schedule-prefix.c.in').read_text().replace('@PROTOTYPES@','\n'.join(f[:f.index('{')].strip()+';' for f in funcs)).replace('@FUNCTIONS@','\n'.join(funcs)).replace('@SCRIPT@',','.join(map(str,data)))
 c=out/f'schedule-{move}.c';c.write_text(text);raw=[]
 for opt in ['O0','O2']:
  target=out/f'schedule-{move}-{opt}';compile_c(['cc','-std=c11','-'+opt,str(c),'-o',str(target)]);raw.append(subprocess.check_output([str(target)]))
 assert raw[0]==raw[1];rows=[json.loads(v) for v in raw[0].splitlines()];births=[row['frame'] for row in rows for _ in range(row['birth'])];assert len(births)==len(templates)
 cases.append(dict(move=move,commands=lines,rows=rows,births=[dict(frame=frame,template=template) for frame,template in zip(births,templates)]))
result=dict(schema='firered-caller-prelude-schedule-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,optimization_parity=['O0','O2'],semantics='Verbatim load and delay scheduler. Non-yielding task/audio commands are no-ops; createsprite records its script callback frame. Stop at final waitforvisualfinish, whose sprite lifetimes are covered by independent native callback vectors and complete-root presentation tests.',cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print(json.dumps(dict(cases=len(cases),births=[c['births'] for c in cases],sha256=hashlib.sha256(a.output.read_bytes()).hexdigest())))
