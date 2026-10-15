#!/usr/bin/env python3
"""Authenticated MtMoon Pokemon content; reconstructed source after workspace loss.
Requires restored local project import helpers. No network or upstream execution.
"""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)
from raw_source_paths import raw_metadata_directory
import argparse
import importlib.util
import json
import os
import pathlib
import re
import sys
sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-mtmoon-pokemon-v1'
TEACHING_MOVES = ('MOVE_ROAR', 'MOVE_BULLET_SEED', 'MOVE_THIEF')
NEW_SPECIES = ('Arbok', 'Clefairy', 'Zubat', 'Golbat', 'Oddish', 'Paras', 'Parasect', 'Bellsprout', 'Magnemite', 'Grimer', 'Voltorb', 'Koffing', 'Nidoqueen', 'Nidoking', 'Clefable', 'Wigglytuff')
ART_SPECIES = tuple(zip(NEW_SPECIES, (24, 35, 41, 42, 43, 46, 47, 69, 81, 88, 100, 109, 31, 34, 36, 40)))
def source_symbol(name): return name.upper()
def source_stem(name): return name.lower()
spec = importlib.util.spec_from_file_location('mtmoon_private_existing_codecs', ROOT / 'scripts/import-route3-pokemon.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
canonical, unique_pairs = base.canonical, base.unique_pairs
safe, preflight_directory, git, battle = base.safe, base.preflight_directory, base.git, base.battle
PATHS = frozenset(p for p in base.PATHS if not p.startswith('graphics/pokemon/') and p not in (
    'data/maps/Route3/map.json', 'data/maps/Route4_PokemonCenter_1F/scripts.inc')) | frozenset(
    f'graphics/pokemon/{source_stem(n)}/{p}' for n in NEW_SPECIES
    for p in ('front.png', 'back.png', 'normal.pal', 'shiny.pal', 'icon.png', 'footprint.png')) | frozenset(
    f'graphics/pokemon/icon_palettes/icon_palette_{i}.pal' for i in range(3)) | frozenset(
    f'data/maps/{m}/{p}' for m in ('MtMoon_1F', 'MtMoon_B1F', 'MtMoon_B2F') for p in ('map.json', 'scripts.inc')) | frozenset(
    ('src/battle_util.c', 'src/battle_script_commands.c', 'data/battle_scripts_1.s'))
base.PROFILE, base.NEW_SPECIES, base.ART_SPECIES = PROFILE, NEW_SPECIES, ART_SPECIES
base.LEGACY_SPECIES = base.LEGACY_SPECIES + ('EKANS', 'NIDORAN_F', 'NIDORINA', 'NIDORAN_M', 'NIDORINO', 'JIGGLYPUFF', 'MAGIKARP', 'GYARADOS')


def load_lock(path=None):
    path = path or ROOT / 'scripts/mtmoon-pokemon.lock.json'
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('MtMoon Pokemon source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('MtMoon Pokemon source dependency set differs')
    return lock


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT,
         inputCount=len(lock['files'])))


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is not None:
        safe(repository)
        if git(repository, 'rev-parse', 'HEAD').decode().strip() != COMMIT:
            raise ValueError('MtMoon Pokemon reference commit/tree differs')
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            data = git(repository, 'cat-file', 'blob', COMMIT + ':' + entry['path'])
        sources[entry['path']] = data
    return sources


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
        if fields['genderRatio'] not in ('PERCENT_FEMALE(50)', 'PERCENT_FEMALE(75)', 'MON_MALE', 'MON_FEMALE', 'MON_GENDERLESS') or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW', 'GROWTH_FAST', 'GROWTH_SLOW'):
            raise ValueError('Unsupported MtMoon species profile')
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
        if len(rows) > 1: raise ValueError('Duplicate evolution row')
        if rows:
            m = rows[0]
            declaration = re.fullmatch(r'\{\{(EVO_\w+),\s+([A-Z_0-9]+), (SPECIES_\w+)\}\}', m[1])
            if declaration is None: raise ValueError('Unsupported evolution declaration')
            method, parameter, target = declaration.groups()
            if method == 'EVO_LEVEL' and int(parameter) <= 25 and target in {'SPECIES_' + source_symbol(n) for n in NEW_SPECIES}:
                evolutions.append(dict(level=int(parameter), target=ident(target), source=ref(evolution_path, symbol, m)))
            elif method == 'EVO_ITEM' and parameter == 'ITEM_MOON_STONE' and target == 'SPECIES_CLEFABLE':
                evolutions.append(dict(level=0, target=ident(target), method='item', parameter=parameter, source=ref(evolution_path, symbol, m)))
            else:
                deferred.append(dict(species=ident(symbol), method=method, parameter=parameter, target=ident(target),
                    source=ref(evolution_path, symbol, m), executable=False,
                    reason='Unavailable trainer-only evolution, item evolution, friendship evolution, or above natural level25'))
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol),
            baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)},
            type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']),
            evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)},
            growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow', 'GROWTH_FAST': 'fast', 'GROWTH_SLOW': 'slow'}[fields['growthRate']],
            genderRatio={'PERCENT_FEMALE(50)': 127, 'PERCENT_FEMALE(75)': 191, 'MON_MALE': 0, 'MON_FEMALE': 254, 'MON_GENDERLESS': 255}[fields['genderRatio']],
            ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']),
            levelUpMoves=moves, evolutions=evolutions, source=source, learnsetSource=ref(learnpath, pointer, learn)))
        names.append(name('src/data/text/species_names.h', symbol))
    # TM05, TM09 and TM46 need definitions even when absent from local level-up learnsets.
    move_symbols.update(TEACHING_MOVES)
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(dict(id=ident(symbol), sourceMoveId=number('include/constants/moves.h', symbol),
            pp=int(f['pp']), power=int(f['power']), type=f['type'], accuracy=int(f['accuracy']), effect=f['effect'],
            secondaryEffectChance=int(f['secondaryEffectChance']), target=f['target'], priority=int(f['priority']),
            flags=[] if f['flags'] == '0' else f['flags'].split(' | '), battleEffectImplemented=False, source=source))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    abilities = sorted({s[k] for s in species for k in ('ability1', 'ability2')} - {'ABILITY_NONE'})
    for path, symbol in [('include/constants/pokemon.h', 'EVO_LEVEL'), ('include/constants/pokemon.h', 'EVO_ITEM'),
                         ('include/constants/pokemon.h', 'EVO_FRIENDSHIP'), ('include/constants/items.h', 'ITEM_MOON_STONE'),
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
    m = one(r'^    \[SPECIES_EKANS\]\s*= \{\{EVO_LEVEL,\s+22, SPECIES_ARBOK\}\},$', text[evolution_path])
    override = dict(species=ident('SPECIES_EKANS'), evolution=dict(level=22, target=ident('SPECIES_ARBOK'), source=ref(evolution_path, 'SPECIES_EKANS', m)))
    overrides = [override]
    for symbol, target in [('SPECIES_NIDORINA', 'SPECIES_NIDOQUEEN'), ('SPECIES_NIDORINO', 'SPECIES_NIDOKING'), ('SPECIES_JIGGLYPUFF', 'SPECIES_WIGGLYTUFF')]:
        m = one(r'^    \[' + symbol + r'\]\s*= \{\{EVO_ITEM, ITEM_MOON_STONE, ' + target + r'\}\},$', text[evolution_path])
        overrides.append(dict(species=ident(symbol), evolution=dict(level=0, target=ident(target), method='item',
            parameter='ITEM_MOON_STONE', source=ref(evolution_path, symbol, m))))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'], species=species,
        moves=moves, speciesNames=names, moveNames=move_names, constants=constants, encodedNames=encoded_names,
        deferredEvolutions=deferred, evolutionOverrides=overrides, naturalLevelLimit=25,
        itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False, tradeEvolutionExecutorImplemented=False,
        limitations=['Sixteen additions over the authenticated forty-species Route3 catalog; 56 species total.',
          'Only Ekans22, Zubat22 and Paras24 close new obtainable natural evolution targets.',
          'Four Moon Stone target references are restored; item execution remains separately integrated. Trainer-only and friendship evolution remain deferred.',
          'Complete learnsets and cries are metadata; battle execution and audible output need separate verification.'])

REQUIRED_NEW_MOVES = ('ABSORB', 'AERIAL_ACE', 'ASTONISH', 'BODY_SLAM', 'CHARGE', 'DOUBLE_SLAP', 'DRAGON_RAGE',
    'ENCORE', 'FOLLOW_ME', 'GROWTH', 'HELPING_HAND', 'LEECH_LIFE', 'MAGNITUDE', 'METAL_SOUND',
    'MINIMIZE', 'MUD_SPORT', 'POISON_GAS', 'RAGE', 'RAPID_SPIN', 'ROCK_THROW', 'SCREECH',
    'SELF_DESTRUCT', 'SMOG', 'SPORE', 'SWEET_SCENT', 'THRASH', 'WING_ATTACK')
NATURAL_MIN_LEVEL = dict(BULBASAUR=1, IVYSAUR=16, CHARMANDER=1, CHARMELEON=16, SQUIRTLE=1,
    WARTORTLE=16, CATERPIE=1, METAPOD=7, BUTTERFREE=10, WEEDLE=1, KAKUNA=7, BEEDRILL=10,
    PIDGEY=1, PIDGEOTTO=18, RATTATA=1, RATICATE=20, PIKACHU=1, SPEAROW=1, FEAROW=20,
    MANKEY=1, NIDORAN_F=1, NIDORINA=16, NIDORAN_M=1, NIDORINO=16, JIGGLYPUFF=1,
    MAGIKARP=1, GYARADOS=20, EKANS=1, ARBOK=22, GEODUDE=1, GRAVELER=25, CLEFAIRY=1,
    ZUBAT=1, GOLBAT=22, PARAS=1, PARASECT=24, NIDOQUEEN=16, NIDOKING=16, CLEFABLE=8, WIGGLYTUFF=3)

def scope(lock, sources):
    reference = lambda p, s, needle=None: battle.reference(lock, sources, p, s, needle)
    texts = {p: b.decode() for p, b in sources.items() if p.endswith(('.h', '.inc', '.c', '.s'))}
    wild = json.loads(sources['src/data/wild_encounters.json'], object_pairs_hook=unique_pairs)
    encounters = [e for g in wild['wild_encounter_groups'] for e in g.get('encounters', [])]
    lands = []
    for label in ('sMtMoon1F_FireRed', 'sMtMoonB1F_FireRed', 'sMtMoonB2F_FireRed', 'sRoute4_FireRed'):
        entry = next(e for e in encounters if e['base_label'] == label)
        lands.append(dict(map=entry['map'], table=label, landMons=entry['land_mons'],
            source=reference('src/data/wild_encounters.json', label), enabledByThisCatalog=False))
    learnpath = 'src/data/pokemon/level_up_learnsets.h'
    def learnset(symbol):
        pointer = re.search(r'^    \[' + symbol + r'\] = (\w+),$', texts['src/data/pokemon/level_up_learnset_pointers.h'], re.M)[1]
        block = re.search(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', texts[learnpath], re.M | re.S)
        return [(int(m[1]), m[2], reference(learnpath, pointer, block[0])) for m in re.finditer(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),', block[1])]
    def initial_moves(symbol, level):
        result = []
        for n, move, _ in learnset(symbol):
            if n <= level and move not in result:
                result.append(move)
                if len(result) > 4: result.pop(0)
        return result
    trainers, deferred_trainers = [], []
    for map_name in ('MtMoon_1F', 'MtMoon_B1F', 'MtMoon_B2F', 'Route4'):
        map_path = f'data/maps/{map_name}/map.json'
        data = json.loads(sources[map_path], object_pairs_hook=unique_pairs)
        script_paths = ['data/scripts/trainers.inc']
        if map_name != 'Route4': script_paths.append(f'data/maps/{map_name}/scripts.inc')
        names = set()
        for p in script_paths:
            for e in data['object_events'] + ([dict(script='MtMoon_B2F_EventScript_BattleMiguel')] if map_name == 'MtMoon_B2F' else []):
                label = e.get('script', '')
                if not label: continue
                block = re.search(r'^' + re.escape(label) + r'::\n(.*?)(?=^\w+::|\Z)', texts[p], re.M | re.S)
                if block is None: continue
                for match in re.finditer(r'\ttrainerbattle_(single|no_intro) (TRAINER_\w+)[^\n]*', block[1]):
                    trainer = match[2]
                    if trainer in names: continue
                    names.add(trainer)
                    record = re.search(r'^    \[' + trainer + r'\] = \{\n(.*?)^    \},', texts['src/data/trainers.h'], re.M | re.S)
                    party = re.search(r'\.party = NO_ITEM_DEFAULT_MOVES\((\w+)\)', record[1])[1]
                    party_match = re.search(r'^static const struct TrainerMonNoItemDefaultMoves ' + party + r'\[\] = \{\n(.*?)^\};', texts['src/data/trainer_parties.h'], re.M | re.S)
                    rows = re.findall(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n    \},', party_match[1])
                    if not rows or party_match[1] != ''.join(f'    {{\n        .iv = {iv},\n        .lvl = {level},\n        .species = {sp},\n    }},\n' for iv, level, sp in rows):
                        raise ValueError('Unsupported trainer party shape')
                    row = dict(trainer=trainer, map=data['id'], script=label, command='trainerbattle_' + match[1],
                        party=[dict(iv=int(iv), level=int(level), species=sp, defaultMoves=initial_moves(sp, int(level))) for iv, level, sp in rows],
                        scriptSource=reference(p, label, match[0]), trainerSource=reference('src/data/trainers.h', trainer, record[0]),
                        partySource=reference('src/data/trainer_parties.h', party, party_match[0]))
                    (deferred_trainers if map_name == 'Route4' else trainers).append(row)
    if len(trainers) != 12 or sum(len(t['party']) for t in trainers) != 28:
        raise ValueError('MtMoon trainer inventory differs')
    natural = [dict(species='SPECIES_' + name, firstNaturalLevel=minimum,
        learnedMoves=[dict(level=n, move=m, source=ref) for n, m, ref in learnset('SPECIES_' + name) if minimum <= n <= 25])
        for name, minimum in sorted(NATURAL_MIN_LEVEL.items())]
    move_path = 'src/data/battle_moves.h'
    definitions = []
    for name in REQUIRED_NEW_MOVES:
        symbol = 'MOVE_' + name
        block = re.search(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', texts[move_path], re.M | re.S)
        f = dict(re.findall(r'\.(\w+) = (.*),', block[1]))
        sid = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', texts['include/constants/moves.h'], re.M)[1])
        definitions.append(dict(id=dict(value='pret:' + symbol), sourceMoveId=sid, pp=int(f['pp']), power=int(f['power']),
            type=f['type'], accuracy=int(f['accuracy']), effect=f['effect'], secondaryEffectChance=int(f['secondaryEffectChance']),
            target=f['target'], priority=int(f['priority']), flags=[] if f['flags']=='0' else f['flags'].split(' | '),
            battleEffectImplemented=False, source=reference(move_path, symbol, block[0])))
    held = []
    for name in ('ZUBAT', 'GEODUDE', 'PARAS', 'CLEFAIRY'):
        symbol = 'SPECIES_' + name
        p = 'src/data/pokemon/species_info.h'
        b = re.search(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', texts[p], re.M | re.S)
        held.append(dict(species=symbol, common=re.search(r'\.itemCommon = (ITEM_\w+)', b[1])[1],
            rare=re.search(r'\.itemRare = (ITEM_\w+)', b[1])[1], source=reference(p, symbol, b[0])))
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, naturalLevelLimit=25, wildHeldItems=held,
        landTables=lands, trainers=trainers, route4TrainerSource=deferred_trainers, naturalParty=natural,
        newMoveDefinitions=definitions, newAbilities=['ABILITY_' + x for x in ('INNER_FOCUS', 'EFFECT_SPORE', 'MAGNET_PULL',
            'SOUNDPROOF', 'CHLOROPHYLL', 'STENCH', 'STICKY_HOLD', 'LEVITATE')],
        noEvolutionMoveBackfill=True, effectsEnabledByThisCatalog=False,
        exclusions=['Cerulean and later regions', 'Surf and fishing', 'Fossil revival',
            'Friendship/trade evolutions', 'Trainer-only natural party growth', 'Natural level above25'],
        route4TrainerNote='Crissy level31 source party remains inaccessible from the MtMoon east exit by nonaquatic traversal; retained as deferred source evidence.')


def supplement(lock, sources):
    spec = importlib.util.spec_from_file_location('mtmoon_footprints', ROOT / 'scripts/import-forest-capture-dex.py')
    footprints = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(footprints)
    codec = footprints.legacy.codec
    spec = importlib.util.spec_from_file_location('mtmoon_icons', ROOT / 'scripts/import-storage-art.py')
    storage = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(storage)
    dex_bytes = safe(ROOT, base.DEX_DEPENDENCY).read_bytes()
    dex = json.loads(dex_bytes, object_pairs_hook=unique_pairs)
    if len(dex['entries']) != 386: raise ValueError('Existing full Pokedex metadata is incomplete')
    reference = lambda path, symbol, needle=None: storage.reference(lock, sources, path, symbol, needle)
    icon_text = sources['src/pokemon_icon.c'].decode()
    palette_section = icon_text.split('const u8 gMonIconPaletteIndices[] = {', 1)[1].split('};', 1)[0]
    icon_section = icon_text.split('const u8 *const gMonIconTable[] = {', 1)[1].split('};', 1)[0]
    foot_palette = footprints.legacy.kanto_palette(sources['graphics/pokedex/kanto_dex_bgpals.pal'])
    graphics = sources['src/data/graphics/pokemon.h'].decode()
    foot_table = sources['src/data/pokemon_graphics/footprint_table.h'].decode()
    dex_source = sources['src/data/pokemon/pokedex_entries.h'].decode()
    outputs, icons, assets, cries, weights = {}, [], [], [], []
    for name, sid in ART_SPECIES:
        stem, symbol = source_stem(name), 'SPECIES_' + source_symbol(name)
        image_path = f'graphics/pokemon/{stem}/icon.png'
        m = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', palette_section, re.M)
        binding = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_section, re.M)
        if m is None or binding is None or int(m[1]) not in range(3): raise ValueError('Missing source icon binding')
        declaration = re.search(r'const u8\s+gMonIcon_' + name + r'\[\]\s*=\s*INCBIN_U8\(\s*"graphics/pokemon/' + stem + r'/icon\.4bpp"\);', graphics)
        if declaration is None: raise ValueError('Missing source icon declaration')
        index = int(m[1])
        palette_path = f'graphics/pokemon/icon_palettes/icon_palette_{index}.pal'
        width, height, pixels, _ = codec.indexed_png(sources[image_path])
        if (width, height) != (32, 64): raise ValueError('Unexpected source icon dimensions')
        pixels = pixels[:1024]
        colors = codec.palette(sources[palette_path])
        rgba = codec.rgba(pixels, colors)
        path = f'icons/{stem}'
        outputs[path + '.png'] = codec.png(32, 32, rgba)
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, texturePath=path + '.png', width=32, height=32, sourceFrameIndex=0, sourceFrameCount=2, paletteIndex=index, paletteVariant='shared-normal-shiny', transparentIndex=0, paletteRgb555=[r >> 3 | g >> 3 << 5 | b >> 3 << 10 for r, g, b in colors], sourceRefs=[reference(image_path, 'gMonIcon_' + name), reference(palette_path, 'gMonIconPalettes'), reference('src/pokemon_icon.c', 'gMonIconPaletteIndices[' + symbol + ']', m[0]), reference('src/pokemon_icon.c', 'gMonIconTable[' + symbol + ']', binding[0]), reference('src/data/graphics/pokemon.h', 'gMonIcon_' + name, declaration[0])]))
        foot_path = f'graphics/pokemon/{stem}/footprint.png'
        if f'[{symbol}] = gMonFootprint_{name},' not in foot_table or f'const u8 gMonFootprint_{name}[] = INCBIN_U8("graphics/pokemon/{stem}/footprint.1bpp");' not in graphics:
            raise ValueError('Missing source footprint binding')
        foot = codec.rgba(footprints.footprint_indices(sources[foot_path]), foot_palette[:16])
        path = stem + '-footprint'
        outputs[path + '.png'] = codec.png(16, 16, foot)
        assets.append(dict(sourceSpeciesId=sid, texturePath=path + '.png', width=16, height=16, source=reference(foot_path, 'gMonFootprint_' + name)))
        cry_table = sources['sound/cry_tables.inc'].decode()
        cry_name = 'Cry_' + name
        cry_match = re.search(r'^\tcry ' + cry_name + r'$', cry_table, re.M)
        cry_rows = re.findall(r'^\tcry (Cry_\w+)$', cry_table, re.M)
        if cry_rows[sid - 1] != cry_name: raise ValueError('Cry table index differs')
        if cry_match is None: raise ValueError('Missing source cry identity')
        cries.append(dict(sourceSpeciesId=sid, cryId=sid, tableIndex=sid - 1, crySymbol=cry_name,
            source=reference('sound/cry_tables.inc', cry_name, cry_match[0] + '\n'),
            mappingSource=reference('src/pokemon.c', 'SpeciesToCryId',
                battle.section(sources['src/pokemon.c'].decode(), 'u16 SpeciesToCryId(u16 species)')),
            playbackVerified=False, audioBytesImported=False,
            callerSource=reference('src/sound.c', 'PlayCryInternal',
                battle.section(sources['src/sound.c'].decode(), 'void PlayCryInternal(u16 species, s8 pan, s8 volume, u8 priority, u8 mode)'))))
    for name in base.LEGACY_SPECIES + tuple(source_symbol(n) for n in NEW_SPECIES):
        symbol = 'NATIONAL_DEX_' + name
        match = re.search(r'    \[' + symbol + r'\] =\s*\{(.*?)\n    \},', dex_source, re.S)
        if match is None: raise ValueError('Missing weight source')
        weight = int(re.search(r'\.weight = (\d+),', match[1])[1])
        entry = next(e for e in dex['entries'] if e['sourceSpeciesId'] == int(re.search(r'^#define SPECIES_' + name + r'\s+(\d+)', sources['include/constants/species.h'].decode(), re.M)[1]))
        if entry['weightHectograms'] != weight: raise ValueError('Reused weight differs from fixed source')
        weights.append(dict(speciesId='pret:SPECIES_' + name, sourceSpeciesId=entry['sourceSpeciesId'],
            weightHectograms=weight, source=reference('src/data/pokemon/pokedex_entries.h', symbol, match[0])))
    outputs['supplement.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        dexMetadataDependency=dict(path=base.DEX_DEPENDENCY),
        dexSpeciesIds=[sid for _, sid in ART_SPECIES], icons=icons, footprints=assets, cries=cries,
        weights=weights, normalShinyIconsIdentical=True, naturalLevelLimit=25,
        limitations=['Icons use the first original 32x32 frame and a shared normal/shiny palette.',
                    'Dex descriptions and categories are reused from the authenticated full metadata document.',
                    'Cry names and IDs are source metadata only; no pre-rendered or converted audio files are imported.']))
    return outputs


def convert(lock, sources):
    outputs = base.art(lock, sources)
    art = json.loads(outputs['battle-art.json'], object_pairs_hook=unique_pairs)
    art['limitations'][0] = 'Sixteen MtMoon dependency species, one static front/back frame each, normal and shiny offline RGBA/PNG.'
    outputs['battle-art.json'] = canonical(art)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs.update(supplement(lock, sources))
    outputs['scope.json'] = canonical(scope(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        inputCount=len(lock['files']), addedSpeciesCount=16, composedSpeciesCount=56,
        artSpeciesCount=16, textureCount=96, rgbaCount=96, upstreamScriptsExecuted=False,
        romDownloaded=False, publiclyUploaded=False, runtimeShadersUsed=False,
        outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    raw, out = safe(raw), safe(out)
    preflight_directory(raw)
    preflight_directory(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if repository is not None:
        repo = pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw, out)):
            raise ValueError('Materialization must not overlap the reference repository')
    for name in ('route1-pokemon', 'old-man-tutorial', 'forest-pokemon', 'pewter-pokemon', 'pokedex-menu', 'capture-dex', 'forest-capture-dex', 'route22-pokemon', 'route3-pokemon'):
        for kind in ('raw', 'generated'):
            protected = (ROOT / 'content' / kind / name).resolve()
            if any(p.resolve().is_relative_to(protected) or protected.is_relative_to(p.resolve()) for p in (raw, out)):
                raise ValueError('Materialization must not overlap authenticated legacy dependencies')
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
    if repository is not None: material[(raw_metadata_directory(raw, 'mtmoon-pokemon'), 'manifest.json')] = raw_manifest(lock)
    material.update({(out, p): b for p, b in outputs.items()})
    targets = [(safe(root, p), safe(root, p + '.tmp'), b) for (root, p), b in material.items()]
    for target, _, _ in targets: preflight_directory(target.parent)
    if any(p.exists() and not p.is_file() for target, stage, _ in targets for p in (target, stage)):
        raise ValueError('Input/output target is not a regular file')
    for target, stage, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage.write_bytes(data)
        stage.replace(target)
    return dict(profile=PROFILE, inputCount=len(sources), outputCount=len(outputs))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/mtmoon-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/mtmoon-pokemon.lock.json')
    args = parser.parse_args()
    if args.reference_repo is None and not args.raw.exists(): args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))

if __name__ == '__main__': main()
