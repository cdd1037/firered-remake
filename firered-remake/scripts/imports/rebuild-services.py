#!/usr/bin/env python3
"""Regenerate the service, item, evolution and side-activity resources from pinned local pret Git data.
This executes project-authored importers only, never upstream scripts or gameplay.
The dependent region resources and tools/import-regional-service-maps must already exist. No networking, archive restore, or backup replay is performed.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
from pathlib import Path
import subprocess
import sys
import time

STEPS = [('imports/story/import-npc-trade-elyssa.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-npc-trade-haden.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-npc-trades.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-npc-trade-owned-rules.py', ['{reference}', '{project}', '{project}']),
 ('imports/story/import-npc-trade-mail.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-good-rod-service.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-seafoam-rules.py', ['{reference}', '{project}', '{project}']),
 ('imports/items/import-seafoam-items.py', ['{reference}', '{project}', '{project}']),
 ('imports/story/import-seafoam-resources.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/presentation/import-seafoam-presentation.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-move-tutors.py', ['--source-git', '{reference}']),
 ('imports/story/import-move-reminder.py', ['--source-git', '{reference}']),
 ('imports/story/import-move-reminder-house.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-move-deleter.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/audio/import-move-deleter-audio.py',
  ['--reference', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-original-tutor-rooms.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/items/import-poke-doll-items.py', ['{reference}', '{project}', '{project}']),
 ('imports/story/import-copycat-house.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/pokemon/import-super-rod-species.py', ['{reference}', '{project}', '{project}']),
 ('imports/presentation/import-qwilfish-presentation.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-super-rod-service.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-cape-brink-tutor.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/items/import-ordinary-item-supplement.py', ['{reference}', '{project}', '{project}']),
 ('imports/items/import-department-machine-stock.py', ['--source', '{reference}', '--root', '{project}']),
 ('imports/items/import-department-fourth-floor.py', ['{reference}', '{project}', '{project}']),
 ('imports/pokemon/import-stone-evolution-species.py', ['{reference}', '{project}', '{project}']),
 ('imports/pokemon/import-stone-evolution-presentation.py',
  ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/items/import-department-rooftop-items.py', ['{reference}', '{project}', '{project}']),
 ('imports/audio/import-vending-sound.py',
  ['--reference', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-game-corner-coins.py', ['{reference}', '{project}', '{project}']),
 ('imports/pokemon/import-game-corner-species.py', ['{reference}', '{project}', '{project}']),
 ('imports/story/import-game-corner-prizes.py', ['{reference}', '{project}', '{project}']),
 ('imports/presentation/import-game-corner-presentation.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-route16-fly-house.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-field-fly.py', ['{reference}', '{project}', '{project}']),
 ('imports/presentation/import-fly-map.py', ['--reference-repo', '{reference}']),
 ('imports/audio/import-field-fly-sound.py', ['--reference', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-field-fly-travel.py', ['--reference-root', '{reference}']),
 ('imports/story/import-route5-daycare.py', ['--reference-root', '{reference}', '--dependency-root', '{project}']),
 ('imports/story/import-four-island-daycare.py', ['--reference-root', '{reference}', '--dependency-root', '{project}']),
 ('imports/pokemon/import-breeding-species.py', ['{reference}', '{project}', '{project}']),
 ('imports/presentation/import-breeding-presentation.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/pokemon/import-ordinary-evolution-species.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/pokemon/import-ordinary-evolution-presentation.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/story/import-field-rock-smash.py', ['--reference-repo', '{reference}']),
 ('imports/items/import-original-machine-catalog.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/presentation/import-original-tm-acquisition.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/presentation/import-memorial-pillar-presentation.py', ['--reference-root', '{reference}', '--dependency-root', '{project}', '--project-root', '{project}']),
 ('imports/items/import-slot-machine.py', ['{reference}', '{project}']),
 ('imports/items/import-slot-machine-presentation.py', ['{reference}', '{project}', '--dependency', '{project}']),
 ('imports/story/import-fame-checker.py', ['--git-dir','{reference}/.git','--output','{project}/content/generated/data/services/fame-checker.json']),
 ('imports/presentation/import-fame-checker-presentation.py', ['{reference}','{project}']),
 ('imports/trainers/import-vs-seeker.py', ['--git-dir','{reference}/.git','--output','{project}/content/generated/data/services/vs-seeker.json']),
 ('imports/trainers/import-vs-seeker-parties.py', ['--source-root','{reference}','--dependency-root','{project}','--rules-root','{project}','--output-root','{project}']),
 ('imports/trainers/import-vs-seeker-presentation.py', ['{reference}','{project}','{project}']),
 ('imports/story/import-teachy-tv.py', ['--git-dir','{reference}/.git','--output','{project}/content/generated/data/services/teachy-tv.json','--raw-output','{project}/content/raw/pokefirered/037335f4']),
 ('imports/presentation/import-teachy-tv-art.py', ['--git-dir','{reference}/.git','--output','{project}/content/generated/ui/teachy-tv-art']),
 ('imports/audio/import-teachy-tv-audio.py', ['{reference}','{project}','--dependency','{project}']),
 ('imports/story/import-mail-composer.py', ['--reference-root','{reference}','--dependency-root','{project}','--project-root','{project}']),
 ('imports/presentation/import-mail-presentation.py', ['--reference-root','{reference}','--dependency-root','{project}','--project-root','{project}']),
 ('imports/story/import-hall-pc.py', ['{reference}', '{project}'])]

# Machine producers emit direct shared references. Compile the one raw source
# catalog first; the final pass only validates/canonicalizes those same compact
# views and never expands a predecessor catalog.
STEPS = [('imports/items/import-item-catalog.py', ['--project-root', '{project}'])] + [('imports/items/import-machine-catalog.py', ['--project-root', '{project}', '--references-only'])] + STEPS + [
    ('imports/items/import-machine-catalog.py', ['--project-root', '{project}', '--references-only']),
    ('imports/items/import-machine-catalog.py', ['--project-root', '{project}', '--references-only', '--check']),
    ('imports/items/import-current-items.py', ['--project-root', '{project}']),
    ('imports/items/import-source-battle-items.py', ['--project-root', '{project}']),
    ('imports/pokemon/import-game-pokemon.py', ['--root', '{project}']),
]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=_PROJECT_ROOT)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--report-root', type=Path, required=True)
    args = parser.parse_args()
    project, reference, report = args.project_root.resolve(), args.reference_root.resolve(), args.report_root.resolve()
    for relative in ('tools/import-regional-service-maps/_regional_service_shared.py', 'scripts/imports/maps/export-runtime-maps.py'):
        path = project / relative
        if not path.is_file():
            raise SystemExit('Required converter missing: ' + str(path))
    report.mkdir(parents=True, exist_ok=True)
    rows = []
    for index, (script, arguments) in enumerate(STEPS, 1):
        command = [sys.executable, str(project / 'scripts' / script),
                   *[x.format(project=str(project), reference=str(reference)) for x in arguments]]
        log = report / f'{index:02}-{Path(script).stem}.log'
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
