#!/usr/bin/env python3
"""Offline Route3 Pokemon metadata and offline normal/shiny battle pixels.

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
PROFILE = 'firered-route3-pokemon-v1'
NEW_SPECIES = ('Ekans', 'NidoranF', 'Nidorina', 'NidoranM', 'Nidorino', 'Jigglypuff', 'Magikarp', 'Gyarados')
ART_SPECIES = (('Ekans', 23), ('NidoranF', 29), ('Nidorina', 30), ('NidoranM', 32),
               ('Nidorino', 33), ('Jigglypuff', 39), ('Magikarp', 129), ('Gyarados', 130))


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/route3-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


def metadata(lock, sources):
    text = {p: b.decode('utf-8') for p, b in sources.items() if not p.endswith(('.png', '.wav'))}
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
        m = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z ♀♂-]+)"\),$', text[path])
        return dict(id=ident(symbol), name=m[1], source=ref(path, symbol, m))

    species, names, deferred, move_symbols = [], [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for name_ in NEW_SPECIES:
        symbol = 'SPECIES_' + source_symbol(name_)
        fields, source = block('src/data/pokemon/species_info.h', symbol)
        if fields['genderRatio'] not in ('PERCENT_FEMALE(50)', 'PERCENT_FEMALE(75)', 'MON_MALE', 'MON_FEMALE') or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW', 'GROWTH_FAST', 'GROWTH_SLOW'):
            raise ValueError('Unsupported Route3 species profile')
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
                moves.append(dict(level=int(m[1]), move=ident(m[2]), source=ref(learnpath, pointer, m)))
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
                evolution = dict(level=int(level[1]), target=ident(level[2]), source=ref(evolution_path, symbol, m))
            elif item:
                evolution = dict(level=0, target=ident(item[2]), source=ref(evolution_path, symbol, m),
                                 method='item', parameter=item[1])
            else:
                raise ValueError('Unsupported evolution declaration: ' + m[1])
            if level and evolution['level'] <= 20:
                if evolution['target']['value'] not in {'pret:SPECIES_' + source_symbol(n) for n in NEW_SPECIES}:
                    raise ValueError('Missing bounded natural evolution target')
                evolutions.append(evolution)
            else:
                deferred.append(dict(species=ident(symbol), evolution=evolution, executable=False,
                    reason='Moon Stone evolution is deferred; no item executor or target imported' if item else
                           'Target lies above current natural level 20 boundary'))
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol),
            baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)},
            type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']),
            evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}, growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow', 'GROWTH_FAST': 'fast', 'GROWTH_SLOW': 'slow'}[fields['growthRate']],
            genderRatio={'PERCENT_FEMALE(50)': 127, 'PERCENT_FEMALE(75)': 191, 'MON_MALE': 0, 'MON_FEMALE': 254}[fields['genderRatio']], ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']),
            levelUpMoves=moves, evolutions=evolutions, source=source, learnsetSource=ref(learnpath, pointer, learn)))
        names.append(name('src/data/text/species_names.h', symbol))
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(dict(id=ident(symbol), sourceMoveId=number('include/constants/moves.h', symbol),
            pp=int(f['pp']), power=int(f['power']), type=f['type'], accuracy=int(f['accuracy']), effect=f['effect'],
            secondaryEffectChance=int(f['secondaryEffectChance']), target=f['target'], priority=int(f['priority']),
            flags=[] if f['flags'] == '0' else f['flags'].split(' | '), battleEffectImplemented=False, source=source))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    for path, symbol in [('include/constants/pokemon.h', 'EVO_LEVEL'), ('include/constants/pokemon.h', 'EVO_ITEM'),
                         ('include/constants/items.h', 'ITEM_MOON_STONE'),
                         *[('include/constants/abilities.h', s) for s in (
                             'ABILITY_INTIMIDATE', 'ABILITY_SHED_SKIN', 'ABILITY_SWIFT_SWIM',
                             'ABILITY_POISON_POINT', 'ABILITY_CUTE_CHARM')]]:
        m = constant(path, symbol)
        constants.append(dict(symbol=symbol, value=int(m[1]), source=ref(path, symbol, m)))
    encoding_path = 'charmap.txt'
    english = one(r"\A.*\Z", text[encoding_path])
    encoding_source = ref(encoding_path, 'English name encoding, gender glyphs, spaces and EOS', english)
    encoded_names = []
    for row in names + move_names:
        values = []
        for char in row['name'] + '$':
            m = one(r"^'" + re.escape(char) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$", text[encoding_path])
            values.append(int(m[1], 16))
        encoded_names.append(dict(symbol=row['source']['symbol'], name=row['name'], bytes=values,
                                  source=row['source'], encodingSource=encoding_source))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'],
        species=species, moves=moves, speciesNames=names, moveNames=move_names, constants=constants,
        encodedNames=encoded_names, deferredEvolutions=deferred, itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False,
        tradeEvolutionExecutorImplemented=False,
        limitations=['Adds eight exact Route3 and pre-MtMoon-center species over the unchanged 32-species Route22 catalog.',
                     'Complete learnsets are source metadata, not a claim that every later move executes.',
                     'NidoranF/NidoranM level16 and Magikarp level20 targets are present; three Moon Stone targets and Arbok22 are deferred.',
                     'Battle, level/item/trade evolution and gift execution are integrated independently; this importer does not execute rules.',
                     'Original English names include Nidoran gender glyphs and EOS; no native save encoding is claimed.'])


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Eight Route3 species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


DEX_DEPENDENCY = 'content/generated/pokedex-menu/pokedex-menu.json'
LEGACY_SPECIES = ('BULBASAUR', 'IVYSAUR', 'VENUSAUR', 'CHARMANDER', 'CHARMELEON', 'CHARIZARD',
    'SQUIRTLE', 'WARTORTLE', 'BLASTOISE', 'PIDGEY', 'PIDGEOTTO', 'PIDGEOT', 'RATTATA', 'RATICATE',
    'WEEDLE', 'KAKUNA', 'BEEDRILL', 'CATERPIE', 'METAPOD', 'BUTTERFREE', 'PIKACHU', 'RAICHU',
    'GEODUDE', 'GRAVELER', 'GOLEM', 'SANDSHREW', 'SANDSLASH', 'ONIX', 'STEELIX', 'SPEAROW', 'FEAROW', 'MANKEY')


def supplement(lock, sources):
    return pokemon.supplement(lock, sources, profile=PROFILE, species=ART_SPECIES,
        weight_species=LEGACY_SPECIES + tuple(source_symbol(name) for name in NEW_SPECIES),
        natural_level_limit=20, cry_source_newline=False)


def scope(lock, sources):
    """Trace the bounded additions to actual FireRed slots, initial trainers and gift.

    These are requirements/evidence only. Encounter reachability and execution are
    validated by the separately existing region/application profile.
    """
    reference = lambda path, symbol, needle=None: pokemon.reference(lock, sources, path, symbol, needle)
    wild = json.loads(sources['src/data/wild_encounters.json'], object_pairs_hook=unique_pairs)
    encounters = [e for g in wild['wild_encounter_groups'] for e in g.get('encounters', [])]
    lands = []
    for map_name in ('Route3', 'Route4'):
        entry = next(e for e in encounters if e['base_label'] == 's' + map_name + '_FireRed')
        lands.append(dict(map=entry['map'], table=entry['base_label'], landMons=entry['land_mons'],
            enabledByThisCatalog=False, currentRegion=map_name == 'Route3',
            reason='Route3 grass; actual collision/reachability belongs to the region profile' if map_name == 'Route3' else
                   'Route4 eastern grass lies beyond MtMoon; catalog availability does not open it',
            source=reference('src/data/wild_encounters.json', entry['base_label'])))
    trainer_text = sources['src/data/trainers.h'].decode()
    parties_text = sources['src/data/trainer_parties.h'].decode()
    scripts_text = sources['data/scripts/trainers.inc'].decode()
    trainers = []
    for map_name in ('Route3', 'Route4'):
        map_path = f'data/maps/{map_name}/map.json'
        map_data = json.loads(sources[map_path], object_pairs_hook=unique_pairs)
        for event in map_data['object_events']:
            if event.get('trainer_type') != 'TRAINER_TYPE_NORMAL':
                continue
            script = event['script']
            match = re.search(r'^' + script + r'::\n\ttrainerbattle_single (TRAINER_\w+),[^\n]*', scripts_text, re.M)
            if match is None:
                raise ValueError('Missing initial road trainer')
            trainer = match[1]
            record = re.search(r'^    \[' + trainer + r'\] = \{\n(.*?)^    \},', trainer_text, re.M | re.S)
            party = re.search(r'\.party = NO_ITEM_DEFAULT_MOVES\((\w+)\)', record[1])[1]
            party_match = re.search(r'^static const struct TrainerMonNoItemDefaultMoves ' + party + r'\[\] = \{\n(.*?)^\};', parties_text, re.M | re.S)
            rows = re.findall(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n    \},', party_match[1])
            if not rows or party_match[1] != ''.join(f'    {{\n        .iv = {iv},\n        .lvl = {level},\n        .species = {species},\n    }},\n' for iv, level, species in rows):
                raise ValueError('Unsupported road trainer party shape')
            trainers.append(dict(trainer=trainer, script=script, map='MAP_' + map_name.upper(),
                currentRegion=map_name == 'Route3', party=[dict(iv=int(iv), level=int(level), species=species) for iv, level, species in rows],
                scriptSource=reference('data/scripts/trainers.inc', script, match[0]),
                partySource=reference('src/data/trainer_parties.h', party, party_match[0]),
                trainerSource=reference('src/data/trainers.h', trainer, record[0])))
    gift_path = 'data/maps/Route4_PokemonCenter_1F/scripts.inc'
    gift_text = sources[gift_path].decode()
    required = ('.equ MAGIKARP_PRICE, 500', '\tgivemon SPECIES_MAGIKARP, 5', '\tsetflag FLAG_BOUGHT_MAGIKARP')
    if any(gift_text.count(line) != 1 for line in required):
        raise ValueError('Magikarp seller source differs')
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, landTables=lands,
        roadTrainers=trainers, gift=dict(species='SPECIES_MAGIKARP', level=5, price=500,
            flag='FLAG_BOUGHT_MAGIKARP', partyAndPcDelivery=True, payOnlyAfterSuccessfulDelivery=True,
            source=reference(gift_path, 'Route4_PokemonCenter_1F_EventScript_TryBuyMagikarp')),
        exclusions=['Route4 eastern grass and Lass Crissy until MtMoon traversal', 'Surf and fishing',
                    'Rematches', 'MtMoon fossils and encounters', 'Moon Stone and trade evolution',
                    'Natural growth above level20'], effectsEnabledByThisCatalog=False)


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs.update(supplement(lock, sources))
    outputs['scope.json'] = canonical(scope(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        inputCount=len(lock['files']), addedSpeciesCount=8, composedSpeciesCount=40,
        artSpeciesCount=8, textureCount=48, rgbaCount=48, upstreamScriptsExecuted=False,
        romDownloaded=False, publiclyUploaded=False, runtimeShadersUsed=False,
        outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    protected = (ROOT / 'content' / kind / name
                 for name in ('route1-pokemon', 'old-man-tutorial', 'forest-pokemon', 'pewter-pokemon', 'pokedex-menu', 'capture-dex', 'forest-capture-dex', 'route22-pokemon')
                 for kind in ('raw', 'generated'))
    return import_region(raw, out, recipe=load_lock(lock_path), convert=convert,
                         repository=repository, protected=protected)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/route3-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/route3-pokemon.lock.json')
    args = parser.parse_args()
    if args.reference_repo is None and not args.raw.exists():
        args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
