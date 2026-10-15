#!/usr/bin/env python3
"""Offline pewter Pokemon metadata and offline normal/shiny battle pixels.

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
PROFILE = 'firered-pewter-pokemon-v1'
NEW_SPECIES = ('Geodude', 'Graveler', 'Golem', 'Sandshrew', 'Sandslash', 'Onix', 'Steelix')
ART_SPECIES = (('Geodude', 74), ('Graveler', 75), ('Golem', 76), ('Sandshrew', 27),
               ('Sandslash', 28), ('Onix', 95), ('Steelix', 208))


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/pewter-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


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
        if fields['genderRatio'] != 'PERCENT_FEMALE(50)' or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW'):
            raise ValueError('Unsupported pewter family profile')
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
            trade = re.fullmatch(r'\{\{EVO_TRADE, 0, (SPECIES_\w+)\}\}', m[1])
            trade_item = re.fullmatch(r'\{\{EVO_TRADE_ITEM, (ITEM_\w+), (SPECIES_\w+)\}\}', m[1])
            if level:
                evolution = dict(level=int(level[1]), target=ident(level[2]))
            elif trade and symbol == 'SPECIES_GRAVELER' and trade[1] == 'SPECIES_GOLEM':
                evolution = dict(level=0, target=ident(trade[1]), method='trade')
            elif trade_item and symbol == 'SPECIES_ONIX' and (trade_item[1], trade_item[2]) == ('ITEM_METAL_COAT', 'SPECIES_STEELIX'):
                evolution = dict(level=0, target=ident(trade_item[2]), method='tradeItem', parameter=trade_item[1])
            else:
                raise ValueError('Unsupported evolution declaration: ' + m[1])
            if evolution['target']['value'] not in {'pret:SPECIES_' + n.upper() for n in NEW_SPECIES}:
                raise ValueError('Unimported forward evolution target')
            evolutions.append(evolution)
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol), baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)}, type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']), evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}, growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow'}[fields['growthRate']], genderRatio=127, ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']), levelUpMoves=moves, evolutions=evolutions))
        names.append(name('src/data/text/species_names.h', symbol))
    # Brock's original Onix custom moves require this one non-level-up move.
    # The existing existing move/name/constants source files already contain it.
    move_symbols.add('MOVE_ROCK_TOMB')
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(ident(symbol))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    for path, symbol in [('include/constants/pokemon.h', 'EVO_LEVEL'), ('include/constants/pokemon.h', 'EVO_TRADE'),
                         ('include/constants/pokemon.h', 'EVO_TRADE_ITEM'), ('include/constants/items.h', 'ITEM_METAL_COAT')]:
        m = constant(path, symbol)
        constants.append(dict(symbol=symbol, value=int(m[1]), source=ref(path, symbol, m)))
    encoding_path = 'charmap.txt'
    english = one(r"^'A'[ \t]+= BB.*?^'\$'[ \t]+= FF", text[encoding_path])
    encoding_source = ref(encoding_path, 'English A-Z and EOS', english)
    encoded_names = []
    for row in names + move_names:
        values = []
        for char in row['name'] + '$':
            m = one(r"^'" + re.escape(char) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$", text[encoding_path])
            values.append(int(m[1], 16))
        encoded_names.append(dict(symbol=row['source']['symbol'], name=row['name'], bytes=values,
                                  source=row['source'], encodingSource=encoding_source))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'],
        species=[dict(id=row['id'], evolutions=row['evolutions']) for row in species], moves=moves, speciesNames=names, moveNames=move_names, constants=constants,
        encodedNames=encoded_names, itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False,
        tradeEvolutionExecutorImplemented=False,
        limitations=['Adds Geodude/Graveler/Golem, Sandshrew/Sandslash and Onix/Steelix to the unchanged twenty-two species.',
                     'Steelix is imported solely to preserve source evolution target closure; no campaign availability is enabled.',
                     'Complete level/trade/trade-item metadata is retained; this increment implements no evolution executor or battle effects.',
                     'Source trade-item code assigns its target before the National Dex conditional; that conditional gates held-item removal. Reachability is not certified.',
                     'Canonical names contain original English charmap bytes and EOS, not padded native save storage.',
                     'Rock Tomb is included for Brock\'s source custom moves only; breeding, eggs, general TM/HM and tutor coverage remain outside this content increment.'])


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Seven forward-family species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        inputCount=len(lock['files']), addedSpeciesCount=7, composedSpeciesCount=29,
        artSpeciesCount=7, textureCount=28, rgbaCount=28, upstreamScriptsExecuted=False,
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
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/pewter-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/pewter-pokemon.lock.json')
    args = parser.parse_args()
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
