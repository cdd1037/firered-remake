#!/usr/bin/env python3
"""Minimal offline Tower metadata overlay; reuse authenticated Rocket source bytes."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import json
import pathlib
import re
from import_canonical_pokemon import Sources
from pokemon_import_io import safe, load_recipe, read_sources, write_files

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-pokemon-tower-pokemon-v1'
SOURCE_PATHS = (
    'charmap.txt', 'include/constants/abilities.h', 'include/constants/items.h',
    'include/constants/moves.h', 'include/constants/pokemon.h', 'include/constants/species.h',
    'src/data/battle_moves.h', 'src/data/items.json', 'src/data/pokemon/evolution.h',
    'src/data/pokemon/level_up_learnset_pointers.h', 'src/data/pokemon/level_up_learnsets.h',
    'src/data/pokemon/species_info.h', 'src/data/text/move_names.h', 'src/data/text/species_names.h')
NEW_MOVES = ('MOVE_DREAM_EATER', 'MOVE_NIGHT_SHADE', 'MOVE_SHADOW_BALL', 'MOVE_SHADOW_PUNCH', 'MOVE_SPITE')


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False) + '\n').encode()


def generate(project):
    recipe = load_recipe(safe(project, 'scripts/recipes/pokemon/rocket-hideout-pokemon.lock.json'), commit=COMMIT)
    inventory = {r['path']: r for r in recipe['files']}
    entries = [inventory[p] for p in sorted(SOURCE_PATHS)]
    raw = read_sources(dict(commit=COMMIT, files=entries), safe(project, 'content/raw/pokefirered/037335f4'))
    texts = {path: data.decode('utf-8') for path, data in raw.items()}

    src = Sources(texts, {})
    one, ref, constant, block, name = src.one, src.match_ref, src.constant, src.block, src.name
    def ident(symbol):
        return dict(value='pret:' + symbol)

    species, names, deferred, held = [], [], [], []
    required = {'MOVE_BONEMERANG'}
    for short in ('GASTLY', 'HAUNTER'):
        symbol = 'SPECIES_' + short
        definition, fields = src.species_definition(symbol)
        assert fields['genderRatio'] == 'PERCENT_FEMALE(50)' and fields['growthRate'] == 'GROWTH_MEDIUM_SLOW'
        assert fields['types'] == '{TYPE_GHOST, TYPE_POISON}' and fields['abilities'] == '{ABILITY_LEVITATE, ABILITY_NONE}'
        moves = definition['levelUpMoves']
        required.update(row['move']['value'][5:] for row in moves)
        assert len(moves) == (11 if short == 'GASTLY' else 13)
        evo_path = 'src/data/pokemon/evolution.h'
        evolution = one(evo_path, r'^    \[' + symbol + r'\]\s*= \{\{(EVO_\w+), (\w+), (SPECIES_\w+)\}\},$')
        evolutions = []
        if short == 'GASTLY':
            assert evolution.groups() == ('EVO_LEVEL', '25', 'SPECIES_HAUNTER')
            evolutions.append(dict(level=25, target=ident(evolution[3])))
        else:
            assert evolution.groups() == ('EVO_TRADE', '0', 'SPECIES_GENGAR')
            deferred.append(dict(species=ident(symbol), method=evolution[1], parameter=evolution[2], target=ident(evolution[3]), executable=False, reason='Gengar is excluded; trade evolution and communication are not implemented.'))
        species.append(dict(definition, evolutions=evolutions))
        names.append(name('src/data/text/species_names.h', symbol))
        held.append(dict(species=symbol, common=fields['itemCommon'], rare=fields['itemRare']))

    move_definitions = []
    for symbol in sorted(required):
        f, source = block('src/data/battle_moves.h', symbol)
        move_definitions.append(ident(symbol))
    moves = [m for m in move_definitions if m['value'][5:] in NEW_MOVES]
    move_names = [name('src/data/text/move_names.h', s) for s in NEW_MOVES]
    encoding = ref('charmap.txt', 'English name encoding, gender glyphs, spaces and EOS', one('charmap.txt', r'\A.*\Z'))
    encoded = []
    for row in names + move_names:
        codes = [int(one('charmap.txt', r"^'" + re.escape(char) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1], 16)
                 for char in row['name'] + '$']
        encoded.append(dict(symbol=row['source']['symbol'], name=row['name'], bytes=codes, source=row['source'], encodingSource=encoding))
    symbols = [('include/constants/species.h', 'SPECIES_' + s) for s in ('GASTLY', 'HAUNTER', 'GENGAR')]
    symbols += [('include/constants/moves.h', s) for s in sorted(required)]
    symbols += [('include/constants/abilities.h', 'ABILITY_LEVITATE')]
    symbols += [('include/constants/items.h', s) for s in ('ITEM_CLEANSE_TAG', 'ITEM_SOOTHE_BELL', 'ITEM_SPELL_TAG', 'ITEM_THICK_CLUB')]
    constants = [constant(p, s) for p, s in symbols]
    doc = dict(schemaVersion=1, species=[dict(id=row['id'], evolutions=row['evolutions']) for row in species], moves=moves, speciesNames=names, moveNames=[dict(id=row['id'], name=row['name']) for row in move_names], constants=[dict(symbol=row['symbol'], value=row['value']) for row in constants], encodedNames=encoded, deferredEvolutions=deferred, requiredMoves=[ident(s) for s in sorted(required)], naturalLevelLimit=None)
    fixtures = []
    for s in species:
        for level in (13, 16, 20, 21, 23, 25, 30, 48, 64, 100):
            learned = []
            for row in s['levelUpMoves']:
                move = row['move']['value']
                if row['level'] <= level and move not in learned:
                    learned = (learned + [move])[-4:]
            fixtures.append(dict(species=s['id']['value'], level=level, moves=learned))
    fixture = dict(schemaVersion=1, sourceFiles=entries, expectedSpeciesDelta=2, expectedMoveDelta=5, expectedCampaignSpecies=101, expectedCampaignMoves=236, naturalMoves=fixtures, excludedSpecies='pret:SPECIES_GENGAR', requiredMoveDefinitions=move_definitions)
    item_source = json.loads(texts['src/data/items.json'])
    items = item_source if isinstance(item_source, list) else item_source['items']
    audit = dict(schemaVersion=1, newSpecies=[92, 93], newMoves=list(NEW_MOVES), metadataAlreadyPresent=sorted(required - set(NEW_MOVES)), newAbilities=[], ability='ABILITY_LEVITATE', wildHeldItems=held, heldItemAdmissions=['ITEM_CLEANSE_TAG', 'ITEM_SOOTHE_BELL', 'ITEM_SPELL_TAG', 'ITEM_THICK_CLUB'], constants=constants, itemDefinitions=[i for i in items if i['itemId'] in {'ITEM_CLEANSE_TAG', 'ITEM_SOOTHE_BELL', 'ITEM_SPELL_TAG', 'ITEM_THICK_CLUB'}], itemSource=dict(path='src/data/items.json'))
    return {'content/generated/data/pokemon/pokemon-tower.json': runtime_json_bytes(doc, sort_keys=True),
            'tests/fixtures/pokemon-tower/catalog-source-fixtures.json': canonical(fixture),
            'docs/pokemon-tower-catalog-delta.json': canonical(audit)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dependency-project-root', type=pathlib.Path, required=True)
    parser.add_argument('--target-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    files = generate(args.dependency_project_root)
    if args.check:
        for relative, data in files.items():
            if safe(args.target_root, relative).read_bytes() != data:
                raise ValueError('Generated bytes differ: ' + relative)
    else:
        write_files(args.target_root, files)
    for relative, data in files.items():
        print(len(data), relative)


if __name__ == '__main__':
    main()
