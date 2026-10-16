#!/usr/bin/env python3
"""Compile final early field encounters, source boundary, and teleporter art."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
import argparse
import json
import tempfile
from bill_teleporter import compile_teleporter
from generated_output_compare import generated_output_matches
from map_runtime import encode
from project_paths import PROJECT_ROOT
from world_source import WorldSource
from source_constants import world_constants
from shared_constants import publish_constants

LAND_MAPS = {'MAP_ROUTE2', 'MAP_VIRIDIAN_FOREST', 'MAP_ROUTE22', 'MAP_ROUTE3', 'MAP_ROUTE4',
             'MAP_MT_MOON_1F', 'MAP_MT_MOON_B1F', 'MAP_MT_MOON_B2F', 'MAP_ROUTE24', 'MAP_ROUTE25',
             'MAP_ROUTE5', 'MAP_ROUTE6'}


def land_headers(src):
    group = next(g for g in src.json('src/data/wild_encounters.json')['wild_encounter_groups']
                 if g['label'] == 'gWildMonHeaders')
    rows = [dict(map=e['map'], land_mons=e['land_mons']) for e in group['encounters']
            if e['map'] in LAND_MAPS and e['base_label'].endswith('_FireRed')]
    if {e['map'] for e in rows} != LAND_MAPS or len(rows) != len(LAND_MAPS):
        raise ValueError('Early land headers differ')
    if any(len(e['land_mons']['mons']) != 12 for e in rows): raise ValueError('Land slots differ')
    return rows


def compile_features(raw, generated):
    src = WorldSource(raw)
    publish_constants(generated, world_constants(src))
    outputs = {'data/encounters/early-land.json': land_headers(src),
               'maps/regions/bill/animations.json': compile_teleporter(src, generated / 'maps/regions/bill/animation')}
    for name, value in outputs.items():
        path = generated / name; path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(encode(value))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    if args.check:
        with tempfile.TemporaryDirectory() as temporary:
            stage = Path(temporary)
            outputs = compile_features(args.raw, stage)
            for path in stage.rglob('*'):
                if path.relative_to(stage).as_posix() == 'data/constants.json':
                    publish_constants(args.out, {'values': json.loads(path.read_bytes())}, check=True)
                    continue
                if path.is_file() and not generated_output_matches(args.out / path.relative_to(stage), path.read_bytes()):
                    raise ValueError('Early field output differs: ' + str(path.relative_to(stage)))
    else:
        outputs = compile_features(args.raw, args.out)
    print(json.dumps({'outputs': sorted(outputs), 'checked': args.check}))


if __name__ == '__main__': main()
