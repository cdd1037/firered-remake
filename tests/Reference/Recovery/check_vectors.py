#!/usr/bin/env python3
"""Rebuild bounded native source fixtures outside the repository."""
import argparse,shutil,subprocess,sys
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--fixtures',type=Path);a=p.parse_args();here=Path(__file__).resolve().parent;out=a.output.resolve()
if out.exists():raise SystemExit('Choose a new external output directory.')
if any(part in {'.git','content','src','tests'} for part in out.parts):raise SystemExit('Use external scratch output.')
out.mkdir(parents=True)
for source in here.iterdir():
 if source.suffix=='.py' or source.name.endswith('.c.in') or source.name=='pinned-inputs.json':shutil.copy2(source,out/source.name)
for family in ['motion','tasks','schedule','material']:
 subprocess.run([sys.executable,'-B',str(out/f'generate_{family}.py'),str(a.reference.resolve())],check=True)
 expected=a.fixtures/f'recovery-{family}-source-vectors.json' if a.fixtures else here/f'{family}-vectors.json'
 if (out/f'{family}-vectors.json').read_bytes()!=expected.read_bytes():raise SystemExit('Source fixture differs: '+expected.name)
subprocess.run([sys.executable,'-B',str(out/'validate.py')],check=True);print('Four source fixtures reproduce byte-identically.')
