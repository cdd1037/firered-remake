#!/usr/bin/env python3
"""Rebuild the pinned original VS SEEKER rules catalog from authenticated local Git objects.

No upstream program is executed. Generated catalog/raw evidence stays out of Git exports.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-vs-seeker-v1'


def need(value, message):
    if not value:
        raise ValueError(message)


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, separators=(',', ':')) + '\n').encode()


def read_sources(git_dir, lock_path):
    raw_lock = lock_path.read_bytes()
    lock = json.loads(raw_lock)
    need((lock['commit'], lock['profile']) == (COMMIT, PROFILE), 'vs_seeker_lock_identity')
    def git(*args):
        return subprocess.check_output(['git', '--git-dir=' + str(git_dir), *args])
    sources = {}
    for entry in lock['files']:
        path = entry['path']
        need(path not in sources and not Path(path).is_absolute() and '..' not in Path(path).parts, 'vs_seeker_source_path')
        raw = git('show', COMMIT + ':' + path)
        sources[path] = raw.decode()
    return lock, sources


def constants(source):
    return {m[1]: int(m[2], 0) for m in re.finditer(r'^#define\s+(\w+)\s+(0x[0-9a-fA-F]+|\d+)\s*(?://[^\n]*)?$', source, re.M)}


def extract( sources):
    source = sources['src/vs_seeker.c']
    trainer_ids = constants(sources['include/constants/opponents.h'])
    graphics = constants(sources['include/constants/event_objects.h'])
    movements = constants(sources['include/constants/event_object_movement.h'])
    table = re.search(r'sRematches\[\] = \{(.*?)\n\};', source, re.S)[1]
    rematches = []
    for m in re.finditer(r'\{\s*\{([^}]+)\},\s*MAP\((\w+)\)\s*\}', table):
        parties = [v.strip() for v in m[1].split(',') if v.strip()]
        need(2 <= len(parties) <= 6, 'vs_seeker_party_count')
        parties += ['TRAINER_NONE'] * (6 - len(parties))
        need(parties[0] in trainer_ids and all(p == 'SKIP' or p in trainer_ids for p in parties), 'vs_seeker_trainer_id')
        rematches.append(dict(index=len(rematches), sourceMap=m[2], trainers=parties,
                              trainerSourceIds=[65535 if p == 'SKIP' else trainer_ids[p] for p in parties]))
    need(len(rematches) == 221, 'vs_seeker_rematch_count')
    need(len(set(r['trainers'][0] for r in rematches)) == len(rematches), 'vs_seeker_duplicate_base')
    # Parse the original graphics switch rather than deriving behavior from trainer classes.
    body = source[source.index('static u8 GetRunningBehaviorFromGraphicsId(u8 graphicsId)\n{'):]
    body = body[:body.index('\n}\n')]
    behavior = {}
    pending = []
    default = None
    for line in body.splitlines():
        c = re.search(r'case (OBJ_EVENT_GFX_\w+):', line)
        r = re.search(r'return (MOVEMENT_TYPE_\w+);', line)
        if c:
            pending.append(c[1])
        if r:
            if pending:
                for symbol in pending:
                    behavior[symbol] = movements[r[1]]
                pending = []
            else:
                default = movements[r[1]]
    need(len(behavior) == 25 and default == 77, 'vs_seeker_graphics_behavior')
    maps = [json.loads(t) for p, t in sources.items() if p.endswith('/map.json')]
    scripts = sources['data/scripts/trainers.inc']
    first_commands = {m[1]: (m[2], m[3]) for m in re.finditer(r'^(\w+)::\s*\n\s*(trainerbattle_\w+)\s+(TRAINER_\w+)', scripts, re.M)}
    bindings = []
    bases = {r['trainers'][0] for r in rematches}
    for map_data in maps:
        for i, actor in enumerate(map_data['object_events']):
            if actor.get('trainer_type') not in ('TRAINER_TYPE_NORMAL', 'TRAINER_TYPE_BURIED'):
                continue
            command = first_commands.get(actor['script'])
            if command is None or command[1] not in bases:
                continue
            bindings.append(dict(sourceMap=map_data['id'], templateIndex=i, localId=i + 1, sourceLocalId=actor.get('local_id'),
                                 trainerSymbol=command[1], scriptLabel=actor['script'], battleMacro=command[0],
                                 trainerType=actor['trainer_type'], graphicsSymbol=actor['graphics_id'], graphicsSourceId=graphics[actor['graphics_id']],
                                 x=actor['x'], y=actor['y'], movementSymbol=actor['movement_type'], flag=actor['flag']))
    need({b['trainerSymbol'] for b in bindings} == bases, 'vs_seeker_binding_coverage')
    woman_map = next(m for m in maps if m['id'] == 'MAP_VERMILION_CITY_POKEMON_CENTER_1F')
    index, woman = next((i, a) for i, a in enumerate(woman_map['object_events']) if a['script'].endswith('_VSSeekerWoman'))
    acquisition = dict(sourceMap=woman_map['id'], templateIndex=index, localId=index + 1, x=woman['x'], y=woman['y'],
                       scriptLabel=woman['script'], sourcePath='data/event_scripts.s', ownershipFlag='FLAG_GOT_VS_SEEKER',
                       itemSymbol='ITEM_VS_SEEKER', flagSetBeforeGiveItem=True, initialChargeSteps=0)
    events = sources['data/event_scripts.s']
    need(re.search(r'setflag FLAG_GOT_VS_SEEKER\s*\n\s*giveitem ITEM_VS_SEEKER', events) is not None, 'vs_seeker_acquisition_order')
    # Roster requirements are source metadata only: battle adapters still own party construction.
    trainers_source = sources['src/data/trainers.h']
    roster_requirements = []
    all_trainers = sorted({p for r in rematches for p in r['trainers'] if p not in ('SKIP', 'TRAINER_NONE')}, key=trainer_ids.get)
    for symbol in all_trainers:
        m = re.search(r'\[' + symbol + r'\]\s*=\s*\{(.*?)\n    \},', trainers_source, re.S)
        need(m is not None, 'vs_seeker_trainer_metadata:' + symbol)
        body = m[1]
        party = re.search(r'\.party = (\w+)\((\w+)\)', body)
        double = re.search(r'\.doubleBattle = (TRUE|FALSE)', body)
        need(party and double, 'vs_seeker_party_metadata:' + symbol)
        roster_requirements.append(dict(trainerSymbol=symbol, sourceId=trainer_ids[symbol], partyMacro=party[1], partySymbol=party[2],
                                        doubleBattle=double[1] == 'TRUE'))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
                rematches=rematches, actorBindings=bindings, acquisition=acquisition,
                runningMovementByGraphics={str(graphics[k]): v for k, v in sorted(behavior.items())},
                defaultRunningMovement=default, rosterRequirements=roster_requirements,
                progressFlags=['', 'FLAG_GOT_VS_SEEKER', 'FLAG_WORLD_MAP_CELADON_CITY', 'FLAG_WORLD_MAP_FUCHSIA_CITY', 'FLAG_SYS_GAME_CLEAR', 'FLAG_SYS_CAN_LINK_WITH_RS'],
                excludedMaps=['MAP_VIRIDIAN_FOREST', 'MAP_MT_EMBER_EXTERIOR', 'MAP_THREE_ISLAND_BERRY_FOREST', 'MAP_SIX_ISLAND_PATTERN_BUSH'])


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--git-dir', required=True, type=Path)
    p.add_argument('--output', required=True, type=Path)
    p.add_argument('--lock', type=Path, default=Path(__file__).with_name('vs-seeker.lock.json'))
    args = p.parse_args()
    lock, sources = read_sources(args.git_dir, args.lock)
    catalog = extract( sources)
    raw = json_bytes(catalog)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'catalog.json').write_bytes(raw)
    print(json.dumps(dict(profile=PROFILE, rematches=len(catalog['rematches']), actorBindings=len(catalog['actorBindings']),
                         rosterRequirements=len(catalog['rosterRequirements']), bytes=len(raw))))


if __name__ == '__main__':
    main()
