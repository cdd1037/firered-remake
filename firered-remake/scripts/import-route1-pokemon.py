#!/usr/bin/env python3
"""Extract bounded Route 1 Pokemon data from verified text; never execute upstream code."""
import argparse
from decimal import Decimal
import importlib.util
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-route1-pokemon-v1'
SPECIES = [('BULBASAUR', 1), ('IVYSAUR', 2), ('VENUSAUR', 3), ('CHARMANDER', 4),
           ('CHARMELEON', 5), ('CHARIZARD', 6), ('SQUIRTLE', 7), ('WARTORTLE', 8),
           ('BLASTOISE', 9), ('PIDGEY', 16), ('PIDGEOTTO', 17), ('PIDGEOT', 18),
           ('RATTATA', 19), ('RATICATE', 20)]
spec = importlib.util.spec_from_file_location('route1_pokemon_fetch', ROOT / 'scripts/fetch-route1-pokemon.py')
fetcher = importlib.util.module_from_spec(spec)
spec.loader.exec_module(fetcher)


def import_catalog(raw, out, lock_path=None):
    # This verifies the reviewed lock, fixed tree and every source byte before extraction.
    lock, verified = fetcher.verify_sources(raw, lock_path=lock_path)
    if lock['commit'] != COMMIT or lock['profile'] != PROFILE or lock['speciesSymbols'] != ['SPECIES_' + n for n, _ in SPECIES]:
        raise ValueError('Unknown source identity or species scope')
    sources = {p: b.decode('utf-8') for p, b in verified.items()}
    meta = {e['path']: e for e in lock['files']}

    def one(pattern, text):
        matches = list(re.finditer(pattern, text, re.M | re.S))
        if len(matches) != 1:
            raise ValueError(f'Expected one match for {pattern}; found {len(matches)}')
        return matches[0]

    def ref(path, symbol, start, end):
        return dict(path=path, symbol=symbol, startLine=sources[path].count('\n', 0, start) + 1,
                    endLine=sources[path].count('\n', 0, end) + 1)

    def number(value):
        if not re.fullmatch(r'\d+', value):
            raise ValueError('Not an unsigned decimal: ' + value)
        return int(value)

    def block(path, symbol):
        match = one(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', sources[path])
        fields = {}
        for line in match[1].splitlines():
            field = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not field or field[1] in fields:
                raise ValueError('Unexpected/duplicate field: ' + line)
            fields[field[1]] = field[2]
        return fields, ref(path, symbol, match.start(), match.end())

    def pair(value, prefix):
        match = re.fullmatch(r'\{(' + prefix + r'\w+), (' + prefix + r'\w+)\}', value)
        if not match:
            raise ValueError('Unexpected source pair: ' + value)
        return match[1], match[2]

    def constant(path, symbol):
        return number(one(r'^#define ' + symbol + r'\s+(\d+)\s*$', sources[path])[1])

    def ident(symbol):
        return {'value': 'pret:' + symbol}

    def display_name(path, symbol, maximum):
        match = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z -]+)"\),$', sources[path])
        if len(match[1]) > maximum:
            raise ValueError('Name exceeds source limit: ' + symbol)
        return dict(id=ident(symbol), name=match[1], source=ref(path, symbol, match.start(), match.end()))

    species, species_names, move_symbols = [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    expected_fields = {'base' + s for s in stat_fields} | {'evYield_' + s for s in stat_fields} | {
        'types', 'catchRate', 'expYield', 'itemCommon', 'itemRare', 'genderRatio', 'eggCycles',
        'friendship', 'growthRate', 'eggGroups', 'abilities', 'safariZoneFleeRate', 'bodyColor', 'noFlip'}
    for name, expected_id in SPECIES:
        symbol = 'SPECIES_' + name
        fields, species_ref = block('src/data/pokemon/species_info.h', symbol)
        if set(fields) != expected_fields:
            raise ValueError('Unexpected species fields: ' + symbol)
        species_id = constant('include/constants/species.h', symbol)
        if species_id != expected_id:
            raise ValueError('Unexpected source species ID')
        base = {k: number(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)}
        yields = {k: number(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}
        gender = one(r'^PERCENT_FEMALE\((\d+(?:\.\d+)?)\)$', fields['genderRatio'])[1]
        ratio = min(254, int(Decimal(gender) * 255 / 100))
        growth = {'GROWTH_MEDIUM_SLOW': 'mediumSlow', 'GROWTH_MEDIUM_FAST': 'mediumFast'}.get(fields['growthRate'])
        if growth is None:
            raise ValueError('Unexpected growth curve')
        types = pair(fields['types'], 'TYPE_')
        abilities = pair(fields['abilities'], 'ABILITY_')
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', sources['src/data/pokemon/level_up_learnset_pointers.h'])[1]
        path = 'src/data/pokemon/level_up_learnsets.h'
        match = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', sources[path])
        lines = match[1].splitlines()
        if not lines or lines[-1].strip() != 'LEVEL_UP_END':
            raise ValueError('Missing learnset terminator')
        entries = []
        previous = 0
        for line in lines[:-1]:
            entry = re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', line)
            if not entry:
                raise ValueError('Unhandled learnset entry: ' + line)
            level, move = number(entry[1]), entry[2]
            if not 1 <= level <= 100 or level < previous:
                raise ValueError('Invalid learnset level/order')
            previous = level
            move_symbols.add(move)
            pos = sources[path].index(line, match.start(), match.end())
            entries.append(dict(level=level, move=ident(move), source=ref(path, pointer, pos, pos + len(line))))
        evolution_path = 'src/data/pokemon/evolution.h'
        evolutions = []
        matches = list(re.finditer(r'^    \[' + symbol + r'\]\s*= \{\{EVO_LEVEL, (\d+), (SPECIES_\w+)\}\},$', sources[evolution_path], re.M))
        mentions = re.findall(r'^    \[' + symbol + r'\]', sources[evolution_path], re.M)
        if len(matches) != len(mentions) or len(matches) > 1:
            raise ValueError('Unsupported evolution method')
        for entry in matches:
            if entry[2] not in lock['speciesSymbols']:
                raise ValueError('Evolution target outside bounded catalog')
            evolutions.append(dict(level=number(entry[1]), target=ident(entry[2]),
                                   source=ref(evolution_path, symbol, entry.start(), entry.end())))
        species.append(dict(id=ident(symbol), sourceSpeciesId=species_id, baseStats=base,
                            type1=types[0], type2=types[1], catchRate=number(fields['catchRate']),
                            experienceYield=number(fields['expYield']), evYield=yields,
                            growthRate=growth, genderRatio=ratio, ability1=abilities[0], ability2=abilities[1],
                            friendship=number(fields['friendship']), levelUpMoves=entries, evolutions=evolutions,
                            source=species_ref, learnsetSource=ref(path, pointer, match.start(), match.end())))
        species_names.append(display_name('src/data/text/species_names.h', symbol, 10))
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        fields, source = block('src/data/battle_moves.h', symbol)
        if set(fields) != {'effect', 'power', 'type', 'accuracy', 'pp', 'secondaryEffectChance', 'target', 'priority', 'flags'}:
            raise ValueError('Unexpected move fields')
        if not re.fullmatch(r'-?\d+', fields['priority']):
            raise ValueError('Unexpected priority expression')
        flags = [] if fields['flags'] == '0' else fields['flags'].split(' | ')
        if any(not re.fullmatch(r'FLAG_\w+', flag) for flag in flags):
            raise ValueError('Unexpected move flags')
        moves.append(dict(id=ident(symbol), sourceMoveId=constant('include/constants/moves.h', symbol),
                          pp=number(fields['pp']), power=number(fields['power']), type=fields['type'],
                          accuracy=number(fields['accuracy']), effect=fields['effect'],
                          secondaryEffectChance=number(fields['secondaryEffectChance']), target=fields['target'],
                          priority=int(fields['priority']), flags=flags, battleEffectImplemented=False, source=source))
        move_names.append(display_name('src/data/text/move_names.h', symbol, 12))
    limitations = [
        'Only the nine Kanto starter-family species plus Pidgey, Pidgeotto, Pidgeot, Rattata and Raticate are imported.',
        'Complete level-up moves and level evolutions are reference data; every BattleEffectImplemented flag is false.',
        'This catalog adds no battle, ability, capture, wild-generation, held-item, evolution or experience-award executor.',
        'Names are the pinned canonical English source text. Translation cannot change stable IDs or trainer nameHash.',
        'No TM/HM, egg, tutor, breeding, wild held-item, original-save encoding or complete-species profile is enabled.']
    document = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'],
                    limitations=limitations, species=species, moves=moves, speciesNames=species_names, moveNames=move_names)
    data = (json.dumps(document, indent=2, ensure_ascii=False) + '\n').encode('utf-8')
    manifest = dict(schemaVersion=1, repository=lock['repository'], commit=COMMIT, profile=PROFILE,
                     inputs=lock['files'],
                    outputs=[dict(path='content/generated/route1-pokemon/catalog.json')],
                    counts=dict(species=len(species), moves=len(moves),
                                levelUpEntries=sum(len(s['levelUpMoves']) for s in species),
                                evolutions=sum(len(s['evolutions']) for s in species)),
                    upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False)
    # Reject reference/raw overlaps and symlink destinations before either output is changed.
    raw_root, out_root = raw.resolve(), out.resolve()
    if raw_root == out_root or raw_root in out_root.parents or out_root in raw_root.parents:
        raise ValueError('Raw and generated output must not overlap')
    targets = [fetcher.safe_path(out, name) for name in ['catalog.json', 'manifest.json']]
    out.mkdir(parents=True, exist_ok=True)
    targets[0].write_bytes(data)
    targets[1].write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8', newline='\n')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/route1-pokemon')
    parser.add_argument('--lock', type=pathlib.Path)
    args = parser.parse_args()
    result = import_catalog(args.raw, args.output, args.lock)
    print(json.dumps(dict(outputs=result['outputs'], counts=result['counts']), sort_keys=True))
