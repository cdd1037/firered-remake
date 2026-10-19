#!/usr/bin/env python3
"""Explicitly generate native evidence in temporary staging; never run from tests."""
import argparse
import json
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--repository', type=Path, required=True)
p.add_argument('--supplement', type=Path)
p.add_argument('--output', type=Path, help='Fresh external staging directory; defaults to a temporary directory')
p.add_argument('--prepare', type=Path, help='Copy generated JSON into this local fixture directory after validation')
p.add_argument('--fixtures', type=Path, help='Optionally compare against an existing corpus of the same profile')
p.add_argument('--full', action='store_true', help='Generate the complete native corpus instead of semantic representatives')
a = p.parse_args()
here = Path(__file__).resolve().parent
root = a.repository.resolve()
raw = root / 'content/raw/pokefirered/037335f4'
supplement = a.supplement.resolve() if a.supplement else raw
out = a.output.resolve() if a.output else Path(tempfile.mkdtemp(prefix='overlay-reference-'))
if a.output and out.exists():
    raise SystemExit('Choose a fresh external staging directory.')
if out.is_relative_to(root):
    raise SystemExit('Native compilation and staging must stay outside the repository.')
if a.full and a.prepare and a.prepare.resolve().is_relative_to(root):
    raise SystemExit('Prepare the full corpus in an external temporary directory.')
shutil.copytree(here, out, dirs_exist_ok=not a.output,
                ignore=shutil.ignore_patterns('__pycache__', '*.pyc', '*vectors.json', 'verification.json'))
shutil.copy2(here.parent / 'source_extract.py', out / 'source_extract.py')

def run(script, *arguments):
    subprocess.run([sys.executable, '-B', str(script), *map(str, arguments)], check=True)

profile = ['--full'] if a.full else []
for family in ['overlay', 'mist']:
    run(out / family / 'generate.py', raw, '--output', out / family / 'vectors.json', '--build-dir', out / family / 'build', *profile)
run(out / 'material/generate.py', raw, supplement)
run(out / 'glare/generate_glare_vectors.py', '--repository', root, '--output', out / 'glare/glare-source-vectors.json', '--build-dir', out / 'glare/build', *profile)
run(out / 'generate_pattern_shake_vectors.py', '--repository', root, '--output', out / 'pattern')
run(out / 'validate.py')
files = {'overlay/vectors.json': 'overlay-task-source-vectors.json', 'mist/vectors.json': 'overlay-mist-source-vectors.json', 'glare/glare-source-vectors.json': 'overlay-glare-source-vectors.json', 'material/vectors.json': 'overlay-material-source-vectors.json', 'pattern/vectors.json': 'overlay-pattern-shake-source-vectors.json'}
for generated, name in files.items():
    candidate = out / generated
    if not candidate.is_file():
        raise FileNotFoundError('Native generator did not produce ' + str(candidate))
    if a.fixtures:
        expected = a.fixtures / name
        if not expected.is_file():
            raise FileNotFoundError(f'Missing {expected}. Prepare it explicitly with this command plus --prepare {a.fixtures} and without --fixtures.')
        if json.loads(candidate.read_text()) != json.loads(expected.read_text()):
            raise SystemExit('Source fixture differs: ' + str(expected))
if a.prepare:
    a.prepare.mkdir(parents=True, exist_ok=True)
    for generated, name in files.items():
        shutil.copy2(out / generated, a.prepare / name)
print(json.dumps({'profile': 'full' if a.full else 'representative', 'files': list(files.values()),
                  'staging': str(out), 'prepared': str(a.prepare) if a.prepare else None}))
