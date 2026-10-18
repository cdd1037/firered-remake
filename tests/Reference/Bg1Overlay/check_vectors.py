#!/usr/bin/env python3
"""Regenerate bounded source oracles outside the repository and compare fixtures."""
import argparse,hashlib,shutil,subprocess,sys
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--repository',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
source=Path(__file__).resolve().parent;root=a.repository.resolve();out=a.output.resolve()
if out.exists():raise SystemExit('Use a fresh external output directory.')
if out.is_relative_to(root):raise SystemExit('Reference regeneration must stay outside the repository.')
shutil.copytree(source,out);raw=root/'content/raw/pokefirered/037335f4'
def run(*args):subprocess.run([sys.executable,*map(str,args)],check=True)
run(out/'overlay/generate.py',raw);run(out/'mist/generate.py',raw);run(out/'material/generate.py',raw,raw)
run(out/'glare/generate_glare_vectors.py','--repository',root)
run(out/'validate.py')
for generated,fixture in [('overlay/vectors.json','task'),('mist/vectors.json','mist'),('glare/glare-source-vectors.json','glare'),('material/vectors.json','material')]:
 expected=root/f'tests/Game.Presentation.Tests/Fixtures/overlay-{fixture}-source-vectors.json';candidate=out/generated
 if candidate.read_bytes()!=expected.read_bytes():raise SystemExit('Source fixture differs: '+str(expected))
 print(f'{fixture}: '+hashlib.sha256(candidate.read_bytes()).hexdigest())
