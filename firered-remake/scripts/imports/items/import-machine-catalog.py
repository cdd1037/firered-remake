#!/usr/bin/env python3
"""Compile shared TM/HM data from local source and validate regional machine views.

No upstream code is executed. The catalog has one definition per admitted item
and one source learnset per real species. Regional references retain their exact
ordered roster; they never define a cumulative compatibility delta.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse
from collections import Counter
from collections.abc import Mapping
import copy
import json
from pathlib import Path
import re

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
RAW_PATH = Path('content/raw/pokefirered/037335f4')
CATALOG_PATH = Path('content/generated/data/machines.json')
RECIPE_PATH = Path('scripts/recipes/config/machine-catalog.json')
MACHINES = tuple(f'TM{i:02}' for i in range(1, 51)) + tuple(f'HM{i:02}' for i in range(1, 9))
ADMITTED_MACHINES = MACHINES[:-1]
ITEM_IDS = tuple('pret:ITEM_' + name for name in MACHINES)
FULL_MACHINE_FIELDS = {'machine', 'move', 'sourceMoveId', 'movePp', 'compatibility'}
COMPATIBILITY_FIELDS = {'species', 'sourceSpeciesId', 'canLearn'}
SOURCE_PATHS = (
    'include/constants/items.h', 'include/constants/moves.h',
    'include/constants/species.h', 'src/data/items.json',
    'src/data/battle_moves.h', 'src/data/pokemon/tmhm_learnsets.h',
    'src/data/party_menu.h', 'src/move_descriptions.c', 'include/constants/global.h',
)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique_pairs(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON member: ' + key)
        result[key] = value
    return result


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=unique_pairs)


def encode(value):
    return (json.dumps(value, ensure_ascii=False, separators=(',', ':'), default=item_json) + '\n').encode()


def constants(text, prefix):
    rows = re.findall(r'^#define (' + prefix + r'\w+)\s+(\d+)\b', text, re.M)
    require(len(rows) == len(dict(rows)), 'Duplicate source constants: ' + prefix)
    return {name: int(number) for name, number in rows}


def source_ref(path, symbol, text, start, end):
    return dict(path=path, symbol=symbol, startLine=text.count('\n', 0, start) + 1,
                endLine=text.count('\n', 0, end) + 1)


def compile_catalog(raw, recipe):
    """Use only authoritative source files and the small presentation recipe."""
    require(recipe['sourceCommit'] == COMMIT and recipe['schemaVersion'] == 1,
            'Unsupported machine import recipe')
    texts = {path: (raw / path).read_text(encoding='utf-8') for path in SOURCE_PATHS}
    item_ids = constants(texts['include/constants/items.h'], 'ITEM_')
    move_ids = constants(texts['include/constants/moves.h'], 'MOVE_')
    species_ids = constants(texts['include/constants/species.h'], 'SPECIES_')
    mappings = dict(re.findall(r'^#define ITEM_([TH]M\d\d)_([A-Z0-9_]+) ITEM_[TH]M\d\d\s*$',
                               texts['include/constants/items.h'], re.M))
    require(set(mappings) == set(MACHINES), 'Expected exact 58 TM/HM aliases')
    menu = re.search(r'static const u16 sTMHMMoves\[\] =\s*\{(.*?)\};',
                     texts['src/data/party_menu.h'], re.S)
    require(menu is not None, 'Missing source TM/HM move dispatch')
    require(re.findall(r'\bMOVE_\w+\b', menu[1]) == ['MOVE_' + mappings[m] for m in MACHINES],
            'Item aliases and source TM/HM move dispatch disagree')
    require([item_ids['ITEM_' + m] for m in MACHINES] == list(range(289, 347)),
            'Non-contiguous TM/HM item IDs')
    require(len(recipe['itemDescriptionMachines']) == len(set(recipe['itemDescriptionMachines'])) and
            set(recipe['itemDescriptionMachines']) <= set(ADMITTED_MACHINES), 'Invalid item descriptions')
    item_path = 'src/data/items.json'
    item_text = texts[item_path]
    items = json.loads(item_text, object_pairs_hook=unique_pairs)['items']
    items = [i for i in items if i['itemId'] in {'ITEM_' + m for m in MACHINES}]
    require(len(items) == len({i['itemId'] for i in items}) == 58,
            'Duplicate/missing source machine item definitions')
    items = {i['itemId']: i for i in items}
    desc_path = 'src/move_descriptions.c'
    descriptions = texts[desc_path]
    definitions = []
    for name in ADMITTED_MACHINES:
        symbol = 'ITEM_' + name
        item = items[symbol]
        value = shared_item(symbol)
        description_match = re.search(
            r'^const u8 (gMoveDescription_' + re.escape(item['moveId']) +
            r')\[\] = _\(("(?:[^"\\]|\\.)*")\);', descriptions, re.M)
        require(description_match is not None, 'Missing move description: ' + name)
        declaration = item['description_english'].replace('\\n', '\n')
        if name in recipe['itemDescriptionMachines']:
            description = declaration
        else:
            description = json.loads(description_match[2])
        machine = dict(item=value, description=description)
        move = 'MOVE_' + mappings[name]
        battle = re.search(r'\[' + move + r'\]\s*=\s*\{(.*?)\},',
                           texts['src/data/battle_moves.h'], re.S)
        require(battle is not None, 'Missing battle move: ' + move)
        pp = re.search(r'\.pp\s*=\s*(\d+)', battle[1])
        require(pp is not None, 'Missing move PP: ' + move)
        definitions.append(dict(machine=machine, move=dict(value='pret:' + move),
                                sourceMoveId=move_ids[move], movePp=int(pp[1])))

    # Ignore SPECIES_NONE, the 25 historical Unown placeholders, and the egg slot.
    # Hoenn source IDs are internal IDs (277..411), not National Dex ordinals.
    real_species = {name: number for name, number in species_ids.items()
                    if name not in ('SPECIES_NONE', 'SPECIES_EGG') and
                    not name.startswith('SPECIES_OLD_UNOWN')}
    require(len(real_species) == 386, 'Expected 386 real source species')
    learnset_path = 'src/data/pokemon/tmhm_learnsets.h'
    learnsets = texts[learnset_path]
    parsed = {}
    for match in re.finditer(r'^    \[(SPECIES_\w+)\]\s*=\s*TMHM_LEARNSET\((.*?)\),',
                             learnsets, re.M | re.S):
        symbol, expression = match[1], match[2]
        require(symbol not in parsed, 'Duplicate learnset: ' + symbol)
        tokens = re.findall(r'TMHM\(([TH]M\d\d)_([A-Z0-9_]+)\)', expression)
        remainder = re.sub(r'TMHM\([TH]M\d\d_[A-Z0-9_]+\)', '', expression)
        require(not re.sub(r'[\s|0]', '', remainder), 'Unsupported learnset expression: ' + symbol)
        require(len(tokens) == len(set(tokens)), 'Duplicate learnset token: ' + symbol)
        require(all(mappings.get(name) == move for name, move in tokens),
                'Unknown/mismatched learnset machine: ' + symbol)
        parsed[symbol] = {name for name, _ in tokens}
    require(set(real_species) <= set(parsed), 'Missing real-species learnsets')
    compatibility = []
    for symbol, number in sorted(real_species.items(), key=lambda pair: pair[1]):
        choices = parsed[symbol]
        compatibility.append(dict(species=dict(value='pret:' + symbol), sourceSpeciesId=number,
                                  compatibleItems=[dict(value='pret:ITEM_' + name)
                                                   for name in MACHINES if name in choices]))
    current_order = recipe['currentMachineOrder']
    require(len(current_order) == 57 and set(current_order) == set(ITEM_IDS[:-1]), 'Invalid current machine order')
    case = next(row for row in json.loads(item_text)['items'] if row['itemId'] == 'ITEM_TM_CASE')
    global_constants = constants(texts['include/constants/global.h'], '')
    current = dict(order=[dict(value=item) for item in current_order],
                   tmCase=dict(item=shared_item('ITEM_TM_CASE'), description=case['description_english'].replace('\\n', '\n')),
                   machineSlots=global_constants['BAG_TMHM_COUNT'], keyItemSlots=global_constants['BAG_KEYITEMS_COUNT'], maximumStack=999)
    return dict(schemaVersion=1, machines=definitions,
                compatibility=compatibility, current=current)


class Catalog:
    def __init__(self, document):
        require(document['schemaVersion'] == 1,
                'Unsupported catalog')
        self.machines = {}
        for row in document['machines']:
            item = row['machine']['item']
            identity = item if isinstance(item, str) else item['id']['value']
            self.machines[identity] = dict(row, machine=dict(row['machine'], item=shared_item(identity)))
        self.species = {row['species']['value']: row for row in document['compatibility']}
        require(len(self.machines) == len(document['machines']) == 57, 'Duplicate/missing machines')
        require(len(self.species) == len(document['compatibility']) == 386, 'Duplicate/missing species')
        require(set(self.machines) == set(ITEM_IDS[:-1]), 'Unexpected admitted machine definitions')
        for row in self.machines.values():
            require(set(row['machine']) == {'item', 'description'}, 'Unexpected machine item fields')
        for row in self.species.values():
            require(set(row) == {'species', 'sourceSpeciesId', 'compatibleItems'},
                    'Unexpected compatibility fields')
            values = [item['value'] for item in row['compatibleItems']]
            require(len(values) == len(set(values)) and set(values) <= set(ITEM_IDS),
                    'Invalid compatible item IDs')

    def compatibility(self, item, species):
        require(item in ITEM_IDS, 'Unknown compatibility item: ' + str(item))
        require(species in self.species, 'Unknown compatibility species: ' + str(species))
        row = self.species[species]
        return dict(species=copy.deepcopy(row['species']), sourceSpeciesId=row['sourceSpeciesId'],
                    canLearn=item in {x['value'] for x in row['compatibleItems']})

    def machine(self, item, species):
        require(item in self.machines, 'Unknown admitted machine: ' + str(item))
        result = copy.deepcopy(self.machines[item])
        result['compatibility'] = [self.compatibility(item, value) for value in species]
        return result


def is_machine_id(value):
    return isinstance(value, str) and re.fullmatch(r'pret:ITEM_[TH]M\d\d', value) is not None


def machine_context(value, inherited):
    for name in ('item', 'tm39', 'machine'):
        child = value.get(name)
        if isinstance(child, dict):
            nested = child.get('item')
            item = child.get('value') or (nested.get('id', {}).get('value')
                                          if isinstance(nested, Mapping) else nested)
            if is_machine_id(item):
                inherited = item
    return inherited


def transform(value, catalog, path, expand=False, inherited=None, pointer='', counts=None):
    """Tool-side structural conversion; runtime uses typed machine converters only."""
    counts = counts if counts is not None else Counter()
    if isinstance(value, list):
        return [transform(row, catalog, path, expand, inherited, pointer + '/' + str(i), counts)
                for i, row in enumerate(value)]
    if not isinstance(value, dict):
        return value
    if FULL_MACHINE_FIELDS <= value.keys():
        require(set(value) == FULL_MACHINE_FIELDS, 'Unexpected machine fields at ' + path + pointer)
        item = value['machine']['item']['id']['value']
        species = [row['species']['value'] for row in value['compatibility']]
        require(value == catalog.machine(item, species), 'Conflicting machine at ' + path + pointer)
        counts['machineDefinitions'] += 1
        counts['compatibilityCells'] += len(species)
        return copy.deepcopy(value) if expand else dict(item=item, species=species)
    if COMPATIBILITY_FIELDS <= value.keys():
        require(set(value) == COMPATIBILITY_FIELDS, 'Unexpected compatibility fields at ' + path + pointer)
        require(inherited is not None, 'Missing item context at ' + path + pointer)
        species = value['species']['value']
        require(value == catalog.compatibility(inherited, species),
                'Conflicting compatibility at ' + path + pointer)
        counts['compatibilityCells'] += 1
        return copy.deepcopy(value) if expand else dict(item=inherited, species=species)
    if set(value) == {'item', 'species'} and is_machine_id(value['item']):
        item, species = value['item'], value['species']
        if isinstance(species, list):
            require(all(isinstance(s, str) for s in species), 'Invalid machine roster at ' + path + pointer)
            resolved = catalog.machine(item, species)
            counts['machineDefinitions'] += 1
            counts['compatibilityCells'] += len(species)
        else:
            require(isinstance(species, str), 'Invalid compatibility reference at ' + path + pointer)
            resolved = catalog.compatibility(item, species)
            counts['compatibilityCells'] += 1
        return resolved if expand else copy.deepcopy(value)
    inherited = machine_context(value, inherited)
    return {key: transform(row, catalog, path, expand,
                           'pret:ITEM_TM39' if key == 'tm39Compatibility' else inherited,
                           pointer + '/' + key, counts) for key, row in value.items()}


def generated_documents(project):
    for path in sorted((project / 'content/generated').rglob('*.json')):
        relative = path.relative_to(project).as_posix()
        if relative == CATALOG_PATH.as_posix():
            continue
        yield relative, read_json(path)


def process_documents(project, catalog):
    results = []
    for relative, value in generated_documents(project):
        counts = Counter()
        compact = transform(value, catalog, relative, counts=counts)
        if not counts:
            continue
        expanded = transform(compact, catalog, relative, expand=True)
        require(transform(value, catalog, relative, expand=True) == expanded,
                'Semantic roundtrip differs: ' + relative)
        results.append((relative, value, compact, expanded, counts))
    return results


def require_direct_references(results):
    for path, original, compact, _, _ in results:
        require(original == compact, 'Importer emitted expanded machine data: ' + path)


def run(project, check=False, references_only=True):
    document = compile_catalog(project / RAW_PATH, read_json(project / RECIPE_PATH))
    catalog = Catalog(document)
    results = process_documents(project, catalog)
    if references_only:
        require_direct_references(results)
    totals = dict(files=len(results), **sum((counts for *_, counts in results), Counter()))
    output = {CATALOG_PATH.as_posix(): encode(document)}
    for relative, _, compact, expanded, _ in results:
        output[relative] = encode(compact)
    if check:
        for relative, data in output.items():
            require((project / relative).read_bytes() == data, 'Regeneration differs: ' + relative)
    else:
        for relative, data in output.items():
            path = project / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    return dict(checked=check, sharedMachineDefinitions=57,
                species=386, compatibilityMachines=58, compatibilityValues=386 * 58,
                **totals, catalogBytes=len(output[CATALOG_PATH.as_posix()]),
                regionalBytes=sum(len(data) for path, data in output.items()
                                  if path != CATALOG_PATH.as_posix()))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    parser.add_argument('--references-only', action='store_true', default=True,
                        help='Reject any legacy full definition/cell; allow only direct typed references')
    args = parser.parse_args()
    print(json.dumps(run(args.project_root.resolve(), args.check, args.references_only), indent=2, default=item_json))


if __name__ == '__main__':
    main()
