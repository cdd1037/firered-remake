#!/usr/bin/env python3
"""Regenerate the bounded stage12-to-department/stone/rooftop resources from pinned local pret Git data.
This executes project-authored importers only, never upstream scripts or gameplay.
The complete stage12 resource base and reviewed tools/import-regional-service-maps
must already exist. No networking, archive restore, or backup replay is performed.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

STEPS = [('import-npc-trade-elyssa.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-npc-trade-haden.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-npc-trades.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-npc-trade-owned-rules.py', ['{reference}', '{project}', '{project}']),
 ('import-npc-trade-mail.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-good-rod-service.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-seafoam-rules.py', ['{reference}', '{project}', '{project}']),
 ('import-seafoam-items.py', ['{reference}', '{project}', '{project}']),
 ('import-seafoam-resources.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-seafoam-presentation.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-move-tutors.py', ['--source-git', '{reference}']),
 ('import-move-reminder.py', ['--source-git', '{reference}']),
 ('import-move-reminder-house.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-move-deleter.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-move-deleter-audio.py',
  ['--reference', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-original-tutor-rooms.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-poke-doll-items.py', ['{reference}', '{project}', '{project}']),
 ('import-copycat-house.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-super-rod-species.py', ['{reference}', '{project}', '{project}']),
 ('import-qwilfish-presentation.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-super-rod-service.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-cape-brink-tutor.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-ordinary-item-supplement.py', ['{reference}', '{project}', '{project}']),
 ('import-department-machine-stock.py', ['--source', '{reference}', '--root', '{project}']),
 ('import-department-fourth-floor.py', ['{reference}', '{project}', '{project}']),
 ('import-stone-evolution-species.py', ['{reference}', '{project}', '{project}']),
 ('import-stone-evolution-presentation.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-department-rooftop-items.py', ['{reference}', '{project}', '{project}']),
 ('import-vending-sound.py',
  ['--reference', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-game-corner-coins.py', ['{reference}', '{project}', '{project}']),
 ('import-game-corner-species.py', ['{reference}', '{project}', '{project}']),
 ('import-game-corner-prizes.py', ['{reference}', '{project}', '{project}']),
 ('import-game-corner-presentation.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-route16-fly-house.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-field-fly.py', ['{reference}', '{project}', '{project}']),
 ('import-fly-map.py', ['--reference-repo', '{reference}']),
 ('import-field-fly-sound.py', ['--reference', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('import-field-fly-travel.py', ['--reference-root', '{reference}']),
 ('import-route5-daycare.py', ['--reference-root', '{reference}', '--dependency-root', '{project}']),
 ('import-four-island-daycare.py', ['--reference-root', '{reference}', '--dependency-root', '{project}']),
 ('import-breeding-species.py', ['{reference}', '{project}', '{project}']),
 ('import-breeding-presentation.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}'])]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--report-root', type=Path, required=True)
    args = parser.parse_args()
    project, reference, report = args.project_root.resolve(), args.reference_root.resolve(), args.report_root.resolve()
    for name, digest in {
        '_regional_service_shared.py': '06652d76f26abf13cd25b5e8a80c72f6e134314f62786cce79eee45d84303a0b',
        '_reviewed_runtime_converter.py': '9300c36ac56fd7172477fe6032a76f104e6b698974ca2a56c0081d3231f49cd8',
    }.items():
        path = project / 'tools/import-regional-service-maps' / name
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise SystemExit('Required authored converter missing or changed: ' + str(path))
    report.mkdir(parents=True, exist_ok=True)
    rows = []
    for index, (script, arguments) in enumerate(STEPS, 1):
        command = [sys.executable, str(project / 'scripts' / script),
                   *[x.format(project=str(project), reference=str(reference)) for x in arguments]]
        log = report / f'{index:02}-{script}.log'
        start = time.monotonic()
        with log.open('w') as stream:
            result = subprocess.run(command, cwd=project, stdout=stream, stderr=subprocess.STDOUT)
        rows.append(dict(script=script, exitCode=result.returncode, seconds=round(time.monotonic()-start, 3), log=log.name))
        (report / 'result.json').write_text(json.dumps(rows, indent=2) + '\n')
        print(script, result.returncode, flush=True)
        if result.returncode:
            print(log.read_text()[-4000:], file=sys.stderr)
            return result.returncode
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
