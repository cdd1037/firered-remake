#!/usr/bin/env python3
"""Rebuild Route 8/7 FireRed encounters from pinned raw inputs, without prior outputs."""
import argparse
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT
from pokemon_import_io import canonical, read_source, safe, unique_pairs

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
WILD = 'src/data/wild_encounters.json'
SPECIES = 'src/data/pokemon/species_info.h'
POKEMON = 'src/pokemon.c'
PATHS = (WILD, SPECIES, POKEMON)
MAPS = (('Route8', 'MAP_ROUTE8', 'map.route-8'), ('Route7', 'MAP_ROUTE7', 'map.route-7'))


def exactly_one(rows, description):
    if len(rows) != 1:
        raise ValueError('Expected one ' + description)
    return rows[0]


def convert(sources):
    wild = json.loads(sources[WILD], object_pairs_hook=unique_pairs)
    tables, species = [], []
    for name, source_map, map_id in MAPS:
        group, row = exactly_one([
            (group, row) for group in wild['wild_encounter_groups']
            if group.get('for_maps') is True
            for row in group.get('encounters', [])
            if row.get('map') == source_map and row.get('base_label') == 's' + name + '_FireRed'
        ], name + ' FireRed encounter table')
        weights = exactly_one([field['encounter_rates'] for field in group['fields']
                               if field['type'] == 'land_mons'], 'land slot weights')
        land = row['land_mons']
        if len(weights) != 12 or len(land['mons']) != len(weights):
            raise ValueError('Land slots and weights must have twelve entries')
        if any(type(w) is not int or w <= 0 for w in weights) or sum(weights) != 100:
            raise ValueError('Invalid land slot weights')
        rate = land['encounter_rate']
        if type(rate) is not int or not 0 < rate <= 255:
            raise ValueError('Invalid land encounter rate')
        slots = []
        for index, (mon, weight) in enumerate(zip(land['mons'], weights)):
            symbol, low, high = mon['species'], mon['min_level'], mon['max_level']
            if not re.fullmatch(r'SPECIES_[A-Z0-9_]+', symbol):
                raise ValueError('Invalid encounter species: ' + symbol)
            if type(low) is not int or type(high) is not int or not 1 <= low <= high <= 100:
                raise ValueError('Invalid encounter levels')
            if symbol not in species:
                species.append(symbol)
            slots.append(dict(index=index, species=dict(value='pret:' + symbol),
                              minimumLevel=low, maximumLevel=high, weight=weight))
        tables.append(dict(map=dict(value=map_id), rawEncounterRate=rate, slots=slots))

    info = sources[SPECIES].decode('utf-8')
    held = []
    for symbol in species:
        block = exactly_one(re.findall(r'^    \[' + re.escape(symbol) + r'\] =\s*\n    \{\n(.*?)^    \},',
                                       info, re.M | re.S), symbol + ' species definition')
        items = [exactly_one(re.findall(r'^\s*\.' + field + r'\s*=\s*(ITEM_[A-Z0-9_]+),\s*$', block, re.M),
                             symbol + ' ' + field) for field in ('itemCommon', 'itemRare')]
        held.append(dict(species='pret:' + symbol, common=items[0], rare=items[1]))
    method = exactly_one(re.findall(r'^void SetWildMonHeldItem\(void\)\n\{(.*?)^\}',
                                   sources[POKEMON].decode('utf-8'), re.M | re.S), 'wild held-item function')
    # Runtime implements this draw and its common/rare thresholds. Do not silently
    # publish metadata for a different source algorithm under the existing schema.
    draw = re.search(r'u16 rnd = Random\(\) % 100;', method)
    if (draw is None or draw.start() > method.find('itemCommon')
            or 'if (rnd > 44)' not in method or 'if (rnd <= 94)' not in method):
        raise ValueError('Unsupported wild held-item draw policy')
    return dict(schemaVersion=1, tables=tables, heldItemDrawBound=100, heldItems=held)


def run(raw, output, repository=None, check=False):
    raw, output = safe(raw), safe(output)
    if output == raw or output.is_relative_to(raw) or raw.is_relative_to(output):
        raise ValueError('Raw/output overlap')
    sources = {path: read_source(raw, path, commit=COMMIT, repository=repository) for path in PATHS}
    document = convert(sources)
    data = canonical(document)
    if check:
        # Existing compact formatting is not part of the runtime data contract.
        if json.loads(output.read_bytes(), object_pairs_hook=unique_pairs) != document:
            raise ValueError('Generated Celadon approach encounters differ: ' + str(output))
    else:
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_bytes(data)
    return dict(tables=len(document['tables']), slots=sum(len(t['slots']) for t in document['tables']), checked=check)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--reference-repo', type=Path, help='Read fixed-revision Git blobs without executing upstream code')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated/data/encounters/celadon-approach.json')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    print(json.dumps(run(args.raw, args.out, args.reference_repo, args.check)))


if __name__ == '__main__':
    main()
