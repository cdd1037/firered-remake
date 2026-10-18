#!/usr/bin/env python3
"""Regenerate all candidates outside a repository and reject byte differences."""
import argparse,shutil,subprocess,sys
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--supplement',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--fixtures',type=Path);a=p.parse_args();here=Path(__file__).resolve().parent;out=a.output.resolve()
if out.exists():raise SystemExit('Choose a new external output directory.')
if any(part in {'.git','content','src','tests'} for part in out.parts):raise SystemExit('Use an external scratch output, not a repository directory.')
out.mkdir(parents=True)
for path in here.iterdir():
 if path.suffix=='.py' or path.name.endswith('.c.in') or path.name=='pinned-inputs.json':shutil.copy2(path,out/path.name)
for family in ['bg','engine','motion','material','pan','schedule']:
 command=[sys.executable,'-B',str(out/f'generate_{family}.py'),str(a.reference.resolve())]
 if family=='material':command.append(str(a.supplement.resolve()))
 subprocess.run(command,check=True)
 expected=(a.fixtures/f'weather-{family}-source-vectors.json') if a.fixtures else (here/f'{family}-vectors.json')
 if (out/f'{family}-vectors.json').read_bytes()!=expected.read_bytes():raise SystemExit('Reference differs: '+expected.name)
subprocess.run([sys.executable,'-B',str(out/'validate.py')],check=True)
print('Six source fixtures regenerate byte-identically.')
