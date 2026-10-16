#!/usr/bin/env python3
"""Convert pinned local FireRed Safari fishing and held-item definitions; never execute source code."""
import argparse
import json
from pathlib import Path
import re
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT

MAPS = {f'MAP_SAFARI_ZONE_{name.upper()}': f'map.safari-zone-{name}' for name in ('center', 'east', 'north', 'west')}
RODS = (('old', 0, (70, 30)), ('good', 2, (60, 20, 20)), ('super', 5, (40, 40, 15, 4, 1)))


def convert(raw):
    raw = Path(raw)
    source = json.loads((raw / 'src/data/wild_encounters.json').read_text(encoding='utf-8'))
    tables = []
    species = set()
    for symbol, map_id in MAPS.items():
        matches = [e for group in source['wild_encounter_groups'] for e in group['encounters']
                   if e['map'] == symbol and e['base_label'].endswith('_FireRed')]
        if len(matches) != 1 or len(matches[0]['fishing_mons']['mons']) != 10:
            raise ValueError('Missing exact FireRed Safari fishing table: ' + symbol)
        fish = matches[0]['fishing_mons']
        for rod, start, weights in RODS:
            slots = []
            for i, weight in enumerate(weights, start):
                mon = fish['mons'][i]
                species.add(mon['species'])
                slots.append(dict(index=i, species=dict(value='pret:' + mon['species']),
                                  minimumLevel=mon['min_level'], maximumLevel=mon['max_level'], weight=weight))
            tables.append(dict(rod=rod, table=dict(map=dict(value=map_id), rawEncounterRate=fish['encounter_rate'], slots=slots)))
    info = (raw / 'src/data/pokemon/species_info.h').read_text(encoding='utf-8')
    held = []
    for name in sorted(species):
        match = re.search(r'^    \[' + re.escape(name) + r'\] =\s*\n    \{\n(.*?)^    \},', info, re.M | re.S)
        if match is None:
            raise ValueError('Missing species: ' + name)
        pair = [re.search(r'\.item' + field + r' = (ITEM_\w+),', match[1]) for field in ('Common', 'Rare')]
        if any(item is None for item in pair):
            raise ValueError('Missing held items: ' + name)
        held.append(dict(species=dict(value='pret:' + name), common=pair[0][1], rare=pair[1][1]))
    return dict(schemaVersion=1, tables=tables, heldItems=held)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated/maps/regions/safari/fishing.json')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    document = convert(args.raw)
    payload = (json.dumps(document, sort_keys=True, separators=(',', ':')) + '\n').encode()
    if args.check:
        if not args.out.is_file() or args.out.read_bytes() != payload:
            raise ValueError('Generated Safari fishing data differs')
    else:
        if args.out.resolve().is_relative_to(args.raw.resolve()):
            raise ValueError('Refusing source overwrite')
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_bytes(payload)
    print(json.dumps(dict(tables=len(document['tables']), species=len(document['heldItems']), checked=args.check)))


if __name__ == '__main__':
    main()
