#!/usr/bin/env python3
"""Extract pinned Water Sport callbacks; run every frame at O0 and O2.

Native task traversal, raw slot allocation, sprite traversal, both arc callbacks,
and Random arithmetic execute unchanged. Static dummy sprite animation and battle
coordinate lookup are explicit host boundaries. No upstream scripts run and no
product outputs supply expected values.
"""
import argparse
import hashlib
import importlib.util
import json
import re
import subprocess
import tempfile
from pathlib import Path


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def build(root, reference, directory, full):
    helper = module('water_sport_reference', root / 'tests/Reference/reference_c.py')
    helper.verify_reference(reference)
    extract = module('water_sport_extract', root / 'tests/Reference/source_extract.py')
    here = Path(__file__).parent
    pins = json.loads((here / 'pinned-inputs.json').read_text())
    source = {p: (reference / p).read_text() for p in pins}
    for path, digest in pins.items():
        if hashlib.sha256((reference / path).read_bytes()).hexdigest() != digest:
            raise ValueError('Pinned source changed: ' + path)
    names = {
        'src/battle_anim_water.c': ['AnimTask_WaterSport', 'AnimTask_WaterSport_Step', 'CreateWaterSportDroplet', 'AnimWaterSportDroplet', 'AnimWaterSportDroplet_Step'],
        'src/battle_anim_mons.c': ['InitAnimArcTranslation', 'TranslateAnimHorizontalArc', 'InitAnimLinearTranslation', 'AnimTranslateLinear'],
        'src/sprite.c': ['CreateSprite', 'AnimateSprites'],
        'src/task.c': ['ResetTasks', 'CreateTask', 'InsertTask', 'DestroyTask', 'RunTasks', 'FindFirstActiveTask', 'TaskDummy', 'GetTaskCount'],
        'src/battle_anim.c': ['DestroyAnimVisualTask'],
        'src/random.c': ['Random'],
        'src/trig.c': ['Sin'],
    }
    functions, identities = [], []
    for path, selected in names.items():
        for name in selected:
            function = extract.function(source[path], name)
            functions.append(function)
            identities.append(dict(path=path, name=name, sha256=hashlib.sha256(function.encode()).hexdigest()))
    macros = extract.macros(source.values(), ['Q_8_8', 'RAND_MULT', 'ISO_RANDOMIZE1'])
    table = extract.array(source['src/trig.c'], 'gSineTable')
    prototypes = '\n'.join(f[:f.index('{')].strip() + ';' for f in functions)
    harness = (here / 'harness.c.in').read_text().replace('@DEFINES@', macros).replace('@TABLE@', table).replace('@PROTOTYPES@', prototypes).replace('@FUNCTIONS@', '\n'.join(functions))
    cpath = directory / 'water-sport.c'
    cpath.write_text(harness)
    executables = []
    for optimization in ['O0', 'O2']:
        executable = directory / ('water-sport-' + optimization)
        helper.compile_c(['cc', '-std=c11', '-' + optimization, '-fwrapv', str(cpath), '-o', str(executable)])
        executables.append(executable)
    cases = []

    def add(doubles, actor, seed, y=0, elevation=0, occupied=0, release=0, branch='authored'):
        args = [doubles, actor, seed, y, elevation, occupied, release]
        output = [subprocess.check_output([str(exe), *map(str, args)], text=True) for exe in executables]
        if output[0] != output[1]:
            raise ValueError('Native optimization mismatch: ' + str(args))
        rows = [json.loads(line) for line in output[0].splitlines()]
        if rows[-1]['alive'] or rows[-1]['sprites'] or rows[-1]['task'][8]:
            raise ValueError('Incomplete native lifetime: ' + str(args))
        if [r['frame'] for r in rows] != list(range(len(rows))):
            raise ValueError('Missing native frame')
        cases.append(dict(doubles=bool(doubles), actor=actor, seed=seed, yOffset=y, elevation=elevation,
                          occupied=occupied, release=release, branch=branch, rows=rows))

    layouts = [(0, 0), (0, 1), *((1, actor) for actor in range(4))]
    for doubles, actor in layouts:
        for seed in ([0, 1, 0xffffffff, 0x12345678] if full else [0, 0xffffffff]):
            add(doubles, actor, seed)
    for doubles, actor in [(0, 1), (1, 2)]:
        add(doubles, actor, 0x12345678, 20, 48, branch='picture-offset-and-elevation')
        add(doubles, actor, 123, occupied=63 - (4 if doubles else 2), branch='one-free-slot')
        add(doubles, actor, 123, occupied=64 - (4 if doubles else 2), release=35, branch='saturated-then-release')
    # Saturation must preserve source timer resets and avoid both RNG and children.
    for case in cases:
        rows = case['rows']
        if case['release']:
            if any(row['sprites'] or row['rng'] != case['seed'] or row['task'][0] != 0 for row in rows[:case['release']]):
                raise ValueError('Failed allocation consumed RNG or advanced the owner')
        if not any(row['task'][0] == 6 for row in rows) or not any(s['phase'] == 1 for row in rows for s in row['sprites']):
            raise ValueError('Missing drain or random-bounce coverage')
    return dict(schema='firered-water-sport-source-v1', profile='full' if full else 'representative',
                sourceSha256=pins, extractedFunctions=identities, optimizationParity=['O0', 'O2'],
                semantics='Frame0 initializes the original task. Every later frame runs ascending AnimateSprites, then linked-priority RunTasks. Raw static-orb births occur after the sprite scan. RNG is an explicitly seeded presentation stream using the extracted Random arithmetic. Source DestroySprite tombstones are observed only while live; subsequent allocations fully reset them.', cases=cases)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', type=Path, required=True)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--build-dir', type=Path)
    parser.add_argument('--full', action='store_true')
    args = parser.parse_args()
    if args.output.exists():
        raise FileExistsError(args.output)
    directory = args.build_dir or Path(tempfile.mkdtemp(prefix='water-sport-reference-'))
    directory.mkdir(parents=True, exist_ok=True)
    result = build(args.root.resolve(), args.reference or args.root.resolve() / 'content/raw/pokefirered/037335f4', directory, args.full)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(json.dumps(dict(output=str(args.output), cases=len(result['cases']), frames=sum(len(c['rows']) for c in result['cases']))))
