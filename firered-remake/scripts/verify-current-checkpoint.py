#!/usr/bin/env python3
"""Selected final integration gates, with exact source identity and no native claim.

Run after sourcing scripts/env.sh. This deliberately does not mean every test
project, all historical recorded-audio checks, or the complete game was tested.
"""
import argparse
import datetime
import hashlib
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
if subprocess.run(['git', 'diff', '--quiet', 'HEAD', '--', 'src'], cwd=ROOT).returncode:
    parser.error('Commit product edits before validating a fixed checkpoint')
untracked = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', 'src'], cwd=ROOT, text=True).splitlines()
if any(Path(path).suffix in ('.cs', '.csproj') for path in untracked):
    parser.error('Commit new product sources before validating a fixed checkpoint')

def source_hashes():
    return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted((ROOT / 'src').rglob('*')) if p.is_file()
            and p.suffix in ('.cs', '.csproj') and not {'bin', 'obj', '.godot'}.intersection(p.parts)}

before = source_hashes()
out.mkdir(parents=True)
report = {'commit': commit, 'startedUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
          'scope': 'Selected current integration gates; not all test projects, native input, hearing, or complete-game acceptance.',
          'sourceSha256': before, 'gates': [], 'passed': False}
route_run = 'checkpoint-' + args.run_name
gates = [
    ('solution-build', ['flock', '/tmp/firered-remake-build.lock', './dev', 'build'], {}),
    ('six-foundation-runners', ['./dev', 'test'], {}),
    ('command-retention', ['./dev', 'test-command-retention'], {}),
    ('save-recovery', ['python3', 'tests/Game.SaveRecoveryAudit.Tests/run.py', '--commit', commit,
                       '--output', str(out / 'save-recovery')], {}),
    ('pokedex-composition', ['./dev', 'test-pokedex-composition'], {}),
    ('runtime-map-state-parity', ['python3', 'tests/Game.RuntimeMapIntegration.Tests/run.py'], {}),
    ('menu-pointer-controls', ['python3', 'tests/Game.MenuPointerControls.Tests/verify_godot.py'], {}),
    ('player-entry', ['python3', 'tests/Game.PlayerEntryUi.Tests/verify_godot.py'], {}),
    ('player-entry-save-safety', ['python3', 'tests/Game.PlayerEntrySafetyAudit.Tests/verify_godot.py'], {}),
    ('route22-default-synth', ['python3', 'tests/Game.Route22Ui.Tests/verify_godot.py', route_run],
     {'ROUTE22_UI_AUDIO': 'synth', 'FIRERED_COMPACT_MAPS': '1'}),
]
try:
    for name, command, extra in gates:
        if source_hashes() != before:
            raise RuntimeError('Product source changed before ' + name)
        print('Running ' + name, flush=True)
        log = out / (name + '.log')
        with log.open('w', encoding='utf-8') as stream:
            result = subprocess.run(command, cwd=ROOT, env={**os.environ, **extra},
                                    text=True, stdout=stream, stderr=subprocess.STDOUT)
        report['gates'].append({'name': name, 'command': command, 'environmentOverrides': extra,
                                'exitCode': result.returncode, 'log': str(log.relative_to(ROOT)),
                                'logSha256': hashlib.sha256(log.read_bytes()).hexdigest()})
        (out / 'result.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        if result.returncode:
            raise RuntimeError(name + ' failed; inspect ' + str(log))
    if source_hashes() != before:
        raise RuntimeError('Product source changed during the selected gates')
    report.update(passed=True, sourceStable=True,
                  route22Report=str(Path('tests/Game.Route22Ui.Tests/evidence') / route_run / 'godot.json'))
except Exception as error:
    report['error'] = str(error)
finally:
    report['endedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    (out / 'result.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
print(json.dumps({'passed': report['passed'], 'commit': commit, 'completedGates': len(report['gates']),
                  'result': str(out / 'result.json'), 'error': report.get('error')}))
sys.exit(0 if report['passed'] else 1)
