#!/usr/bin/env python3
"""Offline forest Pokemon metadata and offline normal/shiny battle pixels.

Only immutable local Git objects or local raw input files are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
"""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse
import pokemon_import as pokemon
from pokemon_import import source_symbol, source_stem, learn_rows
from pokemon_import_io import canonical, unique_pairs, safe, load_recipe, read_sources, import_region
import json
import os
import pathlib
import re
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-forest-pokemon-v1'
NEW_SPECIES = ('Caterpie', 'Metapod', 'Butterfree', 'Pikachu', 'Raichu')
ART_SPECIES = (('Caterpie', 10), ('Metapod', 11), ('Butterfree', 12), ('Weedle', 13),
               ('Kakuna', 14), ('Beedrill', 15), ('Pikachu', 25), ('Raichu', 26))


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/forest-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


def metadata(lock, sources):
    text = {p: b.decode('utf-8') for p, b in sources.items() if not p.endswith('.png')}
    entries = {e['path']: e for e in lock['files']}

    def one(pattern, value):
        matches = list(re.finditer(pattern, value, re.M | re.S))
        if len(matches) != 1:
            raise ValueError('Expected exactly one source match: ' + pattern)
        return matches[0]

    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text[path].count('\n', 0, match.start()) + 1,
                    endLine=text[path].count('\n', 0, match.end()) + 1)

    def block(path, symbol):
        match = one(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', text[path])
        fields = {}
        for line in match[1].splitlines():
            field = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not field or field[1] in fields:
                raise ValueError('Unexpected source field: ' + line)
            fields[field[1]] = field[2]
        return fields, ref(path, symbol, match)

    def ident(symbol):
        return dict(value='pret:' + symbol)

    def constant(path, symbol):
        return one(r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$', text[path])

    def number(path, symbol):
        return int(constant(path, symbol)[1])

    def name(path, symbol):
        m = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z -]+)"\),$', text[path])
        return dict(id=ident(symbol), name=m[1], source=ref(path, symbol, m))

    species, names, move_symbols = [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for name_ in NEW_SPECIES:
        symbol = 'SPECIES_' + name_.upper()
        fields, source = block('src/data/pokemon/species_info.h', symbol)
        if fields['genderRatio'] != 'PERCENT_FEMALE(50)' or fields['growthRate'] != 'GROWTH_MEDIUM_FAST':
            raise ValueError('Unsupported forest family profile')
        types = one(r'^\{(TYPE_\w+), (TYPE_\w+)\}$', fields['types'])
        abilities = one(r'^\{(ABILITY_\w+), (ABILITY_\w+)\}$', fields['abilities'])
        learnpath = 'src/data/pokemon/level_up_learnsets.h'
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', text['src/data/pokemon/level_up_learnset_pointers.h'])[1]
        learn = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', text[learnpath])
        if not re.fullmatch(r'(?:    LEVEL_UP_MOVE\(\d+, MOVE_\w+\),\n)+    LEVEL_UP_END\n', learn[1]):
            raise ValueError('Unsupported or incomplete source learnset')
        moves = []
        for m in re.finditer(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', text[learnpath]):
            if learn.start() < m.start() < learn.end():
                moves.append(dict(level=int(m[1]), move=ident(m[2])))
                move_symbols.add(m[2])
        evolution_path = 'src/data/pokemon/evolution.h'
        rows = list(re.finditer(r'^    \[' + symbol + r'\]\s*= (.*),$', text[evolution_path], re.M))
        evolutions = []
        if len(rows) > 1:
            raise ValueError('Duplicate species evolution row')
        if rows:
            m = rows[0]
            level = re.fullmatch(r'\{\{EVO_LEVEL,\s+(\d+), (SPECIES_\w+)\}\}', m[1])
            item = re.fullmatch(r'\{\{EVO_ITEM, (ITEM_\w+), (SPECIES_\w+)\}\}', m[1])
            if level:
                evolution = dict(level=int(level[1]), target=ident(level[2]))
            elif item and symbol == 'SPECIES_PIKACHU' and (item[1], item[2]) == ('ITEM_THUNDER_STONE', 'SPECIES_RAICHU'):
                evolution = dict(level=0, target=ident(item[2]), method='item', parameter=item[1])
            else:
                raise ValueError('Unsupported evolution declaration: ' + m[1])
            if evolution['target']['value'] not in {'pret:SPECIES_' + n.upper() for n in NEW_SPECIES}:
                raise ValueError('Unimported forward evolution target')
            evolutions.append(evolution)
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol), baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)}, type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']), evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}, growthRate='mediumFast', genderRatio=127, ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']), levelUpMoves=moves, evolutions=evolutions))
        names.append(name('src/data/text/species_names.h', symbol))
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(ident(symbol))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    for path, symbol in [('include/constants/pokemon.h', 'EVO_LEVEL'), ('include/constants/pokemon.h', 'EVO_ITEM'),
                         ('include/constants/items.h', 'ITEM_THUNDER_STONE')]:
        m = constant(path, symbol)
        constants.append(dict(symbol=symbol, value=int(m[1]), source=ref(path, symbol, m)))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'],
        species=[dict(id=row['id'], evolutions=row['evolutions']) for row in species], moves=moves, speciesNames=names, moveNames=move_names, constants=constants,
        itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False,
        limitations=['Adds only Caterpie/Metapod/Butterfree and Pikachu/Raichu to the authenticated original seventeen species.',
                     'Complete forward evolution metadata is retained; no level/item evolution executor, Thunder Stone use or battle effects are implemented.',
                     'Pichu, breeding, eggs, TM/HM and tutor moves are outside this forward regional family scope.',
                     'Forest encounters, trainers and campaign integration are not certified by this content increment.'])


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Eight forward-family species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        inputCount=len(lock['files']), addedSpeciesCount=5, composedSpeciesCount=22,
        artSpeciesCount=8, textureCount=32, rgbaCount=32, upstreamScriptsExecuted=False,
        romDownloaded=False, publiclyUploaded=False, runtimeShadersUsed=False,
        outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    return import_region(raw, out, recipe=load_lock(lock_path), convert=convert,
                         repository=repository)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/forest-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/forest-pokemon.lock.json')
    args = parser.parse_args()
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
