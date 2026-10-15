#!/usr/bin/env python3
"""Offline RockTunnel Pokemon metadata and source RGBA art.
No network or upstream execution.
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
PROFILE = 'firered-rocktunnel-pokemon-v1'
TEACHING_MOVES = ('MOVE_AERIAL_ACE',)
NEW_SPECIES = ('Cubone', 'Electrode', 'Hypno', 'Lickitung', 'Machoke', 'Marowak', 'Primeape', 'Rhyhorn', 'Seadra', 'Seaking', 'Tentacruel', 'Venomoth', 'Venonat', 'Weezing')
ART_SPECIES = (('Cubone', 104), ('Electrode', 101), ('Hypno', 97), ('Lickitung', 108), ('Machoke', 67), ('Marowak', 105), ('Primeape', 57), ('Rhyhorn', 111), ('Seadra', 117), ('Seaking', 119), ('Tentacruel', 73), ('Venomoth', 49), ('Venonat', 48), ('Weezing', 110))
TRAINERS = ('TRAINER_PICNICKER_ALICIA', 'TRAINER_HIKER_JEREMY', 'TRAINER_HIKER_ALAN', 'TRAINER_CAMPER_CHRIS', 'TRAINER_BUG_CATCHER_BRENT', 'TRAINER_BUG_CATCHER_CONNER', 'TRAINER_HIKER_BRICE', 'TRAINER_PICNICKER_CAITLIN', 'TRAINER_CAMPER_DREW', 'TRAINER_PICNICKER_CAROL', 'TRAINER_HIKER_CLARK', 'TRAINER_POKEMANIAC_HERMAN', 'TRAINER_HIKER_TRENT', 'TRAINER_POKEMANIAC_MARK', 'TRAINER_PICNICKER_HEIDI', 'TRAINER_PICNICKER_DANA', 'TRAINER_PICNICKER_ARIANA', 'TRAINER_PICNICKER_LEAH', 'TRAINER_HIKER_LUCAS', 'TRAINER_HIKER_OLIVER', 'TRAINER_HIKER_LENNY', 'TRAINER_POKEMANIAC_ASHTON', 'TRAINER_POKEMANIAC_COOPER', 'TRAINER_HIKER_DUDLEY', 'TRAINER_PICNICKER_SOFIA', 'TRAINER_HIKER_ALLEN', 'TRAINER_HIKER_ERIC', 'TRAINER_POKEMANIAC_STEVE', 'TRAINER_PICNICKER_MARTHA', 'TRAINER_POKEMANIAC_WINSTON')
LEGACY_SPECIES = (
    'ABRA', 'ARBOK', 'BEEDRILL', 'BELLSPROUT', 'BLASTOISE', 'BULBASAUR', 'BUTTERFREE', 'CATERPIE',
    'CHARIZARD', 'CHARMANDER', 'CHARMELEON', 'CLEFABLE', 'CLEFAIRY', 'DIGLETT', 'DROWZEE',
    'DUGTRIO', 'EKANS', 'FEAROW', 'GEODUDE', 'GLOOM', 'GOLBAT', 'GOLDEEN', 'GOLEM', 'GRAVELER',
    'GRIMER', 'GROWLITHE', 'GYARADOS', 'HORSEA', 'IVYSAUR', 'JIGGLYPUFF', 'KADABRA', 'KAKUNA',
    'KOFFING', 'MACHOP', 'MAGIKARP', 'MAGNEMITE', 'MAGNETON', 'MANKEY', 'MEOWTH', 'METAPOD',
    'NIDOKING', 'NIDOQUEEN', 'NIDORAN_F', 'NIDORAN_M', 'NIDORINA', 'NIDORINO', 'ODDISH', 'ONIX',
    'PARAS', 'PARASECT', 'PERSIAN', 'PIDGEOT', 'PIDGEOTTO', 'PIDGEY', 'PIKACHU', 'POLIWAG',
    'POLIWHIRL', 'PONYTA', 'RAICHU', 'RATICATE', 'RATTATA', 'SANDSHREW', 'SANDSLASH', 'SHELLDER',
    'SLOWPOKE', 'SPEAROW', 'SQUIRTLE', 'STARMIE', 'STARYU', 'STEELIX', 'TENTACOOL', 'VENUSAUR',
    'VOLTORB', 'VULPIX', 'WARTORTLE', 'WEEDLE', 'WEEPINBELL', 'WIGGLYTUFF', 'ZUBAT',
)


def load_lock(path=None):
    return load_recipe(path or ROOT / 'scripts/rocktunnel-pokemon.lock.json', commit=COMMIT, profile=PROFILE)


def metadata(lock, sources):
    text = {p: b.decode('utf-8') for p, b in sources.items() if not p.endswith(('.png', '.wav'))}
    entries = {e['path']: e for e in lock['files']}
    def one(pattern, value):
        matches = list(re.finditer(pattern, value, re.M | re.S))
        if len(matches) != 1: raise ValueError('Expected exactly one source match: ' + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text[path].count('\n', 0, match.start()) + 1,
            endLine=text[path].count('\n', 0, match.end()) + 1)
    def block(path, symbol):
        match = one(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', text[path])
        fields = {}
        for line in match[1].splitlines():
            field = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not field or field[1] in fields: raise ValueError('Unexpected source field: ' + line)
            fields[field[1]] = field[2]
        return fields, ref(path, symbol, match)
    def ident(symbol): return dict(value='pret:' + symbol)
    def constant(path, symbol): return one(r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$', text[path])
    def number(path, symbol): return int(constant(path, symbol)[1])
    def name(path, symbol):
        m = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z ♀♂-]+)"\),$', text[path])
        return dict(id=ident(symbol), name=m[1], source=ref(path, symbol, m))
    species, names, deferred, move_symbols = [], [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for name_ in NEW_SPECIES:
        symbol = 'SPECIES_' + source_symbol(name_)
        fields, source = block('src/data/pokemon/species_info.h', symbol)
        if fields['genderRatio'] not in ('PERCENT_FEMALE(25)', 'PERCENT_FEMALE(50)', 'PERCENT_FEMALE(75)', 'MON_MALE', 'MON_FEMALE', 'MON_GENDERLESS') or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW', 'GROWTH_FAST', 'GROWTH_SLOW'):
            raise ValueError('Unsupported RockTunnel species profile')
        types = one(r'^\{(TYPE_\w+), (TYPE_\w+)\}$', fields['types'])
        abilities = one(r'^\{(ABILITY_\w+), (ABILITY_\w+)\}$', fields['abilities'])
        learnpath = 'src/data/pokemon/level_up_learnsets.h'
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', text['src/data/pokemon/level_up_learnset_pointers.h'])[1]
        learn = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', text[learnpath])
        moves = []
        for m in learn_rows(text[learnpath], learn):
            moves.append(dict(level=int(m[1]), move=ident(m[2])))
            move_symbols.add(m[2])
        evolution_path = 'src/data/pokemon/evolution.h'
        rows = list(re.finditer(r'^    \[' + symbol + r'\]\s*= (\{\{.*?\}\}),$', text[evolution_path], re.M | re.S))
        evolutions = []
        if len(rows) > 1: raise ValueError('Duplicate evolution row')
        m = rows[0] if rows else None
        declarations = re.findall(r'\{(EVO_\w+),\s+([A-Z_0-9]+), (SPECIES_\w+)\}', m[1]) if m else []
        if m and (not declarations or re.sub(r'\s+', '', m[1]) != '{' + ','.join('{' + ','.join(d) + '}' for d in declarations) + '}'):
            raise ValueError('Unsupported or incomplete evolution declarations')
        expected_evolution = {'SPECIES_CUBONE': [('EVO_LEVEL', '28', 'SPECIES_MAROWAK')], 'SPECIES_ELECTRODE': [], 'SPECIES_HYPNO': [], 'SPECIES_LICKITUNG': [], 'SPECIES_MACHOKE': [('EVO_TRADE', '0', 'SPECIES_MACHAMP')], 'SPECIES_MAROWAK': [], 'SPECIES_PRIMEAPE': [], 'SPECIES_RHYHORN': [('EVO_LEVEL', '42', 'SPECIES_RHYDON')], 'SPECIES_SEADRA': [('EVO_TRADE_ITEM', 'ITEM_DRAGON_SCALE', 'SPECIES_KINGDRA')], 'SPECIES_SEAKING': [], 'SPECIES_TENTACRUEL': [], 'SPECIES_VENOMOTH': [], 'SPECIES_VENONAT': [('EVO_LEVEL', '31', 'SPECIES_VENOMOTH')], 'SPECIES_WEEZING': []}
        if declarations != expected_evolution[symbol]: raise ValueError('Unexpected RockTunnel evolution')
        for method, parameter, target in declarations:
            if method == 'EVO_LEVEL' and target in {'SPECIES_' + source_symbol(n) for n in NEW_SPECIES}:
                evolutions.append(dict(level=int(parameter), target=ident(target)))
            else:
                deferred.append(dict(species=ident(symbol), method=method, parameter=parameter, target=ident(target), executable=False, reason='Evolution target is not imported in this profile; retained source metadata, not a battle level cap.'))
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol), baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)}, type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']), evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}, growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow', 'GROWTH_FAST': 'fast', 'GROWTH_SLOW': 'slow'}[fields['growthRate']], genderRatio={'PERCENT_FEMALE(25)': 63, 'PERCENT_FEMALE(50)': 127, 'PERCENT_FEMALE(75)': 191, 'MON_MALE': 0, 'MON_FEMALE': 254, 'MON_GENDERLESS': 255}[fields['genderRatio']], ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']), levelUpMoves=moves, evolutions=evolutions))
        names.append(name('src/data/text/species_names.h', symbol))
    # Source trainer parties require definitions independently of these six complete learnsets.
    move_symbols.update(TEACHING_MOVES)
    move_symbols.update(m for t in trainer_parties(lock, sources) for mon in t['party'] for m in mon['moves'])
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(ident(symbol))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    abilities = sorted({s[k] for s in species for k in ('ability1', 'ability2')} - {'ABILITY_NONE'})
    for path, symbol in [('include/constants/pokemon.h', 'EVO_LEVEL'), ('include/constants/pokemon.h', 'EVO_TRADE'),
                         ('include/constants/items.h', 'ITEM_DRAGON_SCALE'), ('include/constants/pokemon.h', 'EVO_TRADE_ITEM'),
                         *[('include/constants/abilities.h', s) for s in abilities]]:
        m = constant(path, symbol)
        constants.append(dict(symbol=symbol, value=int(m[1]), source=ref(path, symbol, m)))
    encoding_path = 'charmap.txt'
    english = one(r'\A.*\Z', text[encoding_path])
    encoding_source = ref(encoding_path, 'English name encoding, gender glyphs, spaces and EOS', english)
    encoded_names = []
    for row in names + move_names:
        values = []
        for char in row['name'] + '$':
            m = one(r"^'" + re.escape(char) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$", text[encoding_path])
            values.append(int(m[1], 16))
        encoded_names.append(dict(symbol=row['source']['symbol'], name=row['name'], bytes=values,
            source=row['source'], encodingSource=encoding_source))
    evolution_path = 'src/data/pokemon/evolution.h'
    overrides = []
    for symbol, level, target in [('SPECIES_DROWZEE', 26, 'SPECIES_HYPNO'), ('SPECIES_GOLDEEN', 33, 'SPECIES_SEAKING'), ('SPECIES_HORSEA', 32, 'SPECIES_SEADRA'), ('SPECIES_KOFFING', 35, 'SPECIES_WEEZING'), ('SPECIES_MACHOP', 28, 'SPECIES_MACHOKE'), ('SPECIES_MANKEY', 28, 'SPECIES_PRIMEAPE'), ('SPECIES_TENTACOOL', 30, 'SPECIES_TENTACRUEL'), ('SPECIES_VOLTORB', 30, 'SPECIES_ELECTRODE')]:
        m = one(r'^    \[' + symbol + r'\]\s*= \{\{EVO_LEVEL, ' + str(level) + ', ' + target + r'\}\},$', text[evolution_path])
        overrides.append(dict(species=ident(symbol), evolution=dict(level=level, target=ident(target))))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'], species=[dict(id=row['id'], evolutions=row['evolutions']) for row in species],
        moves=moves, speciesNames=names, moveNames=move_names, constants=constants, encodedNames=encoded_names,
        deferredEvolutions=deferred, evolutionOverrides=overrides, naturalLevelLimit=None,
        itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False, tradeEvolutionExecutorImplemented=False,
        limitations=['Fourteen RockTunnel dependency species over the unchanged 79-species Route11 catalog; 93 species total.',
          'Normal level evolution closure through level35 includes eight legacy overlays plus Cubone28 and Venonat31; later and trade targets remain deferred.',
          'Complete learnsets and required trainer/machine definitions do not enable battle effects.',
          'No global or natural battle level cap is introduced; cry playback requires separate verification.'])


def trainer_parties(lock, sources):
    reference = lambda p, s, needle=None: pokemon.reference(lock, sources, p, s, needle)
    text = {p: b.decode() for p,b in sources.items() if p.endswith(('.h','.inc'))}
    def learnset(symbol):
        pointer = re.search(r'^    \[' + symbol + r'\] = (\w+),$', text['src/data/pokemon/level_up_learnset_pointers.h'], re.M)[1]
        block = re.search(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', text['src/data/pokemon/level_up_learnsets.h'], re.M|re.S)
        return [(int(m[1]), m[2]) for m in learn_rows(text['src/data/pokemon/level_up_learnsets.h'], block)]
    trainers=[]
    for symbol in TRAINERS:
        record = re.search(r'^    \[' + symbol + r'\] = \{\n(.*?)^    \},', text['src/data/trainers.h'], re.M|re.S)
        shape, party_symbol = re.search(r'\.party = (NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)', record[1]).groups()
        shape_type='TrainerMonNoItemCustomMoves' if shape=='NO_ITEM_CUSTOM_MOVES' else 'TrainerMonNoItemDefaultMoves'
        party = re.search(r'^static const struct '+shape_type+' '+party_symbol+r'\[\] = \{\n(.*?)^\};',text['src/data/trainer_parties.h'],re.M|re.S)
        rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{(MOVE_\w+(?:, MOVE_\w+){3})\},\n)?    \},\n',party[1]))
        if not rows or ''.join(m[0] for m in rows)!=party[1]: raise ValueError('Unsupported RockTunnel trainer party declaration')
        mons=[]
        for row in rows:
            iv,level,species=int(row[1]),int(row[2]),row[3]
            if shape=='NO_ITEM_CUSTOM_MOVES':
                if row[4] is None: raise ValueError('Missing custom moves')
                slots=row[4].split(', '); moves=[m for m in slots if m!='MOVE_NONE']
                if slots!=moves+['MOVE_NONE']*(4-len(moves)): raise ValueError('Nontrailing empty custom move slot')
            else:
                if row[4] is not None: raise ValueError('Unexpected custom moves')
                moves=[]
                for n,m in learnset(species):
                    if n<=level and m not in moves:
                        moves.append(m)
                        if len(moves)>4: moves.pop(0)
                slots=moves+['MOVE_NONE']*(4-len(moves))
            mons.append(dict(iv=iv,level=level,species=species,moves=moves,sourceMoveSlots=slots))
        trainers.append(dict(trainer=symbol,sourceTrainerId=int(re.search(r'^#define '+symbol+r'\s+(\d+)\b',text['include/constants/opponents.h'],re.M)[1]),
            partyFormat=shape,party=mons,trainerSource=reference('src/data/trainers.h',symbol,record[0]),
            partySource=reference('src/data/trainer_parties.h',party_symbol,party[0])))
    if len(trainers)!=30 or sum(len(t['party']) for t in trainers)!=72: raise ValueError('RockTunnel team inventory differs')
    return trainers






def supplement(lock, sources):
    return pokemon.supplement(lock, sources, profile=PROFILE, species=ART_SPECIES,
        weight_species=LEGACY_SPECIES + tuple(source_symbol(name) for name in NEW_SPECIES),
        natural_level_limit=None)


LEGACY_CATALOGS = ('content/generated/route11-pokemon/catalog.json', 'content/generated/ssanne-pokemon/catalog.json', 'content/generated/vermilion-pokemon/catalog.json', 'content/generated/bill-pokemon/catalog.json', 'content/generated/route1-pokemon/catalog.json', 'content/generated/old-man-tutorial/catalog.json', 'content/generated/forest-pokemon/catalog.json', 'content/generated/pewter-pokemon/catalog.json', 'content/generated/route22-pokemon/catalog.json', 'content/generated/route3-pokemon/catalog.json', 'content/generated/mtmoon-pokemon/catalog.json', 'content/generated/cerulean-pokemon/catalog.json')


def art(lock, sources):
    return pokemon.battle_art(lock, sources, profile=PROFILE, species=ART_SPECIES,
        limitation='Fourteen RockTunnel dependency species, one static front/back frame each, normal and shiny offline RGBA/PNG.')


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs.update(supplement(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        inputCount=len(lock['files']), addedSpeciesCount=14, composedSpeciesCount=93,
        artSpeciesCount=14, textureCount=84, rgbaCount=84, upstreamScriptsExecuted=False,
        romDownloaded=False, publiclyUploaded=False, runtimeShadersUsed=False,
        outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    return import_region(raw, out, recipe=load_lock(lock_path), convert=convert,
                         repository=repository)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, help='Explicit raw cache rebuilds offline unless --reference-repo is also supplied')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/rocktunnel-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/rocktunnel-pokemon.lock.json')
    args = parser.parse_args()
    explicit_raw = args.raw is not None
    args.raw = args.raw or ROOT / 'content/raw/pokefirered/037335f4'
    if args.reference_repo is None and not explicit_raw:
        configured = os.environ.get('FIRERED_ORIGINAL_REFERENCE')
        if configured: args.reference_repo = pathlib.Path(configured)
        elif not args.raw.exists(): args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))

if __name__ == '__main__': main()
