#!/usr/bin/env python3
"""Run extracted load/delay scheduling over canonical source command discovery."""
import argparse,hashlib,importlib.util,json,subprocess,tempfile,types
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root,reference,out,importer=None):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 extract=module('source_extract',root/'tests/Reference/CallerPrelude/source_extract.py')
 importer_path=root/'scripts/imports/battle/import-source-elemental-animations.py';imp=types.ModuleType('petal_paw_schedule_importer');imp.__file__=str(importer_path);exec(compile((importer or importer_path).read_text(),str(importer or importer_path),'exec'),imp.__dict__)
 source=(reference/'src/battle_anim.c').read_text();pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text())
 for path in ['src/battle_anim.c','data/battle_anim_scripts.s']:
  if hashlib.sha256((reference/path).read_bytes()).hexdigest()!=pins[path]:raise ValueError('Pinned scheduling source changed: '+path)
 names=['WaitAnimFrameCount','RunAnimScriptCommand','Cmd_loadspritegfx','Cmd_delay'];functions=[extract.function(source,n) for n in names];scripts,subs=imp.discover_scripts(reference);roots={r['moveId']:r for r in scripts};cases=[]
 for move in [230,274]:
  commands=[]
  for command in roots[move]['commands']:
   if command['op']=='call':commands.extend(c for c in subs[command['args'][0]] if c['op']!='return')
   else:commands.append(command)
  data=[];templates=[]
  for command in commands:
   op=command['op']
   if op in ['end','waitforvisualfinish']:break
   if op=='loadspritegfx':data.extend([0,0,0])
   elif op=='delay':data.extend([4,int(command['args'][0])])
   elif op=='createsprite':data.append(2);templates.append(command['args'][0])
   else:data.append(3)
  data.append(8)
  prefix=(root/'tests/Reference/CallerPrelude/schedule-prefix.c.in').read_text();code=prefix.replace('@PROTOTYPES@','\n'.join(f[:f.index('{')].strip()+';' for f in functions)).replace('@FUNCTIONS@','\n'.join(functions)).replace('@SCRIPT@',','.join(map(str,data)))
  path=out/f'schedule-{move}.c';path.write_text(code);outputs=[]
  for opt in ['O0','O2']:
   exe=out/f'schedule-{move}-{opt}';helper.compile_c(['cc','-std=c11','-'+opt,str(path),'-o',str(exe)]);outputs.append(subprocess.check_output([str(exe)]))
  if outputs[0]!=outputs[1]:raise ValueError('Native scheduling parity failed')
  rows=[json.loads(row) for row in outputs[0].splitlines()];births=[r['frame'] for r in rows for _ in range(r['birth'])];assert len(births)==len(templates)
  cases.append(dict(move=move,births=[dict(frame=frame,template=name) for frame,name in zip(births,templates)]))
 return dict(schema='firered-petal-paw-schedule-source-v1',sourceSha256={p:pins[p] for p in ['src/battle_anim.c','data/battle_anim_scripts.s']},sourceFunctions=names,optimizationParity=['O0','O2'],semantics='Canonical importer alone parses source scripts. Its single-level calls/returns are flattened without inserting waits. Original native load/delay scheduler computes birth frames. Non-yielding audio/palette commands are no-ops; each root stops after its final births at end/wait. Complete-runtime tests independently verify every sprite and palette owner drains.',cases=cases)

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--importer',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='petal-paw-schedule-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root.resolve(),a.reference or a.root/'content/raw/pokefirered/037335f4',out,a.importer);a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(r['cases']))
