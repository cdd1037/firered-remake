#!/usr/bin/env python3
import argparse,json,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
from source_extract import function,identity,verify,COMMIT
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);a=p.parse_args();r=a.reference;out=Path(__file__).resolve().parent
paths=['src/battle_anim.c','data/battle_anim_scripts.s'];verify(r,paths);source=(r/paths[0]).read_text();script=(r/paths[1]).read_text();names=['WaitAnimFrameCount','RunAnimScriptCommand','Cmd_loadspritegfx','Cmd_delay']
funcs=[function(source,n) for n in names]
def block(name):
 lines=[]
 for line in script.split(name+':\n',1)[1].splitlines():
  line=line.split('@')[0].strip()
  if not line:continue
  if ':' in line:break
  lines.append(line)
  if line in ['return','end']:break
 return lines
def expanded(name):
 result=[]
 for line in block(name):
  if line.startswith('call '):
   target=line.split()[1]
   if target=='HealingEffect':break
   result+=expanded(target)
  elif line not in ['return','end']:result.append(line)
 return result
cases=[]
for move,name,template in [(156,'Move_REST','gSleepLetterZSpriteTemplate'),(234,'Move_MORNING_SUN','gGreenStarSpriteTemplate'),(62,'Move_AURORA_BEAM','gAuroraBeamRingSpriteTemplate')]:
 lines=expanded(name);last=max(i for i,l in enumerate(lines) if l.startswith('createsprite '+template));lines=lines[:last+1];data=[]
 # Aurora's BG fade is independently covered by the shared transition oracle;
 # this schedule authenticates its relative emission train from first ring.
 if move==62:lines=lines[next(i for i,l in enumerate(lines) if l.startswith('createsprite '+template)):]
 for line in lines:
  if line.startswith('loadspritegfx '):data += [0,0,0]
  elif line.startswith('delay '):data += [4,int(line.split()[1])]
  elif line.startswith('createsprite '+template):data += [2]
  else:data += [3]
 data+=[8]
 text=(out/'schedule-prefix.c.in').read_text().replace('@PROTOTYPES@','\n'.join(f[:f.index('{')].strip()+';' for f in funcs)).replace('@FUNCTIONS@','\n'.join(funcs)).replace('@SCRIPT@',','.join(map(str,data)))
 (out/'schedule.c').write_text(text)
 raw=[]
 for opt in ['O0','O2']:
  subprocess.run(['cc','-std=c11','-'+opt,str(out/'schedule.c'),'-o',str(out/('schedule-'+opt))],check=True);raw.append(subprocess.check_output([str(out/('schedule-'+opt))]))
 assert raw[0]==raw[1];rows=[json.loads(v) for v in raw[0].splitlines()];births=[row['frame'] for row in rows if row['birth']];cases.append(dict(move=move,commands=lines,rows=rows,birthFrames=births,origin='first ring emission' if move==62 else 'first root script callback'))
result=dict(schema='firered-recovery-schedule-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,optimization_parity=['O0','O2'],semantics='Verbatim source wait and load scheduler. Non-yielding task/audio/setarg/call/return commands are flattened to no-op instructions; creation only records its callback frame. Stop immediately after final authored family creation; unrelated visual waits and HealingEffect are excluded. Aurora origin is its first ring after completed background fade.',cases=cases)
(out/'schedule-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n');print('schedule',[(c['move'],c['birthFrames']) for c in cases])
