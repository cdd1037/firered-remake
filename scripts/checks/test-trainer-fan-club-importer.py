#!/usr/bin/env python3
"""Verify current source semantics and required fan-club resources."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import re
from pathlib import Path


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root',type=Path,required=True)
    parser.add_argument('--dependency-root',type=Path,required=True)
    parser.add_argument('--reference-root',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    root=args.project_root.resolve();package=root/'content/generated/maps/regions/trainer-fan-club'
    checks=[]
    def need(value,name):
        if not value:raise AssertionError(name)
        checks.append(name)
    raw = root / 'content/raw/pokefirered/037335f4'
    world = json.loads((root / 'content/generated/maps/world.json').read_bytes())
    entry = next(row for row in world['maps'] if row['stableId'] == 'map.saffron-city-pokemon-trainer-fan-club')
    room = json.loads((root / 'content/generated' / entry['path']).read_bytes())
    original = json.loads((raw / 'data/maps/SaffronCity_PokemonTrainerFanClub/map.json').read_bytes())
    need(room['events'] == original, 'room actors, flags and warps match original events')
    story = json.loads((root / 'content/generated/data/story.json').read_bytes())['Compilation']
    for actor in original['object_events']:
        label = actor['script']
        need(label in story['Programs'], 'compiled actor script:' + label)
        source_text = (raw / story['Programs'][label]['SourcePath']).read_text()
        need(re.search(r'^' + re.escape(label) + r'::?(?:\s|$)', source_text, re.M) is not None,
             'compiled actor script has its source definition:' + label)
    need('SaffronCity_PokemonTrainerFanClub_MapScripts' in story['MapHeaders'], 'compiled room script header')
    constants=json.loads((root/'content/generated/data/constants.json').read_bytes())
    need(constants['VAR_FANCLUB_FAN_COUNTER']==0x4038 and constants['VAR_FANCLUB_LOSE_FAN_TIMER']==0x4039,
         'native helper-only source variables retained')
    expression=constants['(NUM_TRAINER_FAN_CLUB_MEMBERS - 1)']
    need(expression==7 and expression==constants['NUM_TRAINER_FAN_CLUB_MEMBERS']-1, 'exact source expression value retained')
    report=dict(checkCount=len(checks),failures=0,checks=checks,scope='Current fan-club map events, compiled actor scripts and shared constants; gameplay rules are implemented in Domain')
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report))

if __name__=='__main__':main()
