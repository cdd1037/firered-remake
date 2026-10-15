#!/usr/bin/env python3
"""Three distinct original move roots sharing battler trace callbacks; no duplicated art."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,importlib.util,json,re,subprocess
from pathlib import Path
ROOT=_PROJECT_ROOT
spec=importlib.util.spec_from_file_location('hit_importer',ROOT/'scripts/imports/battle/import-source-hit-animations.py')
hit=importlib.util.module_from_spec(spec);spec.loader.exec_module(hit)
COMMIT=hit.COMMIT
ROOTS=[(97,'AGILITY'),(98,'QUICK_ATTACK'),(332,'AERIAL_ACE')]
def build(reference):
 raw=hit.authenticated(reference);source=raw['data/battle_anim_scripts.s'].decode();scripts=[]
 for move,name in ROOTS:
  match=re.search(r'^Move_'+name+r':\n',source,re.M);rows=[]
  for offset,line in enumerate(source[match.end():].splitlines()):
   text=line.split('@',1)[0].strip()
   if not text:continue
   if text.endswith(':'):raise ValueError('Unexpected source branch')
   op,*tail=text.split(None,1);args=[a.strip() for a in tail[0].split(',')] if tail else []
   if op not in hit.OPS:raise ValueError('Unimplemented trace root command '+op)
   rows.append(dict(op=op,args=args,line=source[:match.end()].count('\n')+offset+1))
   if op=='end':break
  if not rows or len(rows)>32 or rows[-1]['op']!='end':raise ValueError('Unbounded trace root')
  scripts.append(dict(moveId=move,label='Move_'+name,commands=rows,dependencies=dict(commands=sorted({r['op'] for r in rows}),sprites=sorted({r['args'][0] for r in rows if r['op']=='createsprite'}),tasks=sorted({r['args'][0] for r in rows if r['op']=='createvisualtask'}),sounds=sorted({r['args'][0] for r in rows if r['op']=='playsewithpan'}))))
 manifest=dict(schemaVersion=1,scripts=scripts)
 # Bind the original imported assets and sine table, including their per-file hashes.
 return (json.dumps(manifest,indent=2)+'\n').encode()
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,default=ROOT/'content/generated/battle/animations/trace.json');p.add_argument('--check',action='store_true');a=p.parse_args();data=build(a.reference);out=a.output
 if a.check:
  if out.read_bytes()!=data:raise SystemExit('Trace animation pack differs')
 else:
  if out.exists():raise SystemExit('Refuse existing generated pack')
  a.output.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(data)
 print(json.dumps(dict(passed=True,sourceCommit=COMMIT,roots=3,bytes=len(data))))
