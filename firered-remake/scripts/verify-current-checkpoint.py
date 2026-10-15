#!/usr/bin/env python3
"""Selected final integration gates, with Git revision and command results.

Run after sourcing scripts/env.sh. This deliberately does not mean every test
project, all historical recorded-audio checks, or the complete game was tested.
"""
import argparse
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('run_name')
args = parser.parse_args()
if not args.run_name or Path(args.run_name).name != args.run_name or args.run_name in ('.', '..'):
    parser.error('One new evidence directory name is required')
out = ROOT / 'evidence/checkpoint-validation' / args.run_name
if out.exists():
    parser.error('Refusing to replace prior evidence')
dotnet_root = os.environ.get('DOTNET_ROOT')
if not dotnet_root or not (Path(dotnet_root) / 'dotnet').is_file():
    parser.error('Source scripts/env.sh first; no installation is attempted')
commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
out.mkdir(parents=True)
report = {'commit': commit, 'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'scope': 'Selected current integration gates; not all test projects, native input, hearing, or complete-game acceptance.',
          'gates': [], 'passed': False}
gates = [
    ('solution-build', ['flock', '/tmp/firered-remake-build.lock', './dev', 'build'], {}),
    ('current-tests', ['./dev', 'test'], {}),
    ('runtime-map-format', [str(Path(dotnet_root) / 'dotnet'), 'run',
                            '--project', 'tests/Game.RuntimeMapData.Tests',
                            '-p:RestoreLockedMode=true', '--', str(ROOT)], {}),
]
try:
    for name, command, extra in gates:

        print('Running ' + name, flush=True)
        log = out / (name + '.log')
        with log.open('w', encoding='utf-8') as stream:
            result = subprocess.run(command, cwd=ROOT, env={**os.environ, **extra},
                                    text=True, stdout=stream, stderr=subprocess.STDOUT)
        report['gates'].append({'name': name, 'command': command, 'environmentOverrides': extra,
                                'exitCode': result.returncode, 'log': str(log.relative_to(ROOT))})
        (out / 'result.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        if result.returncode:
            raise RuntimeError(name + ' failed; inspect ' + str(log))

    report['passed'] = True
except Exception as error:
    report['error'] = str(error)
finally:
    report['endedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (out / 'result.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'passed': report['passed'], 'commit': commit, 'completedGates': len(report['gates']),
                  'result': str(out / 'result.json'), 'error': report.get('error')}))
sys.exit(0 if report['passed'] else 1)
