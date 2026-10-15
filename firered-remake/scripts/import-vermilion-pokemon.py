#!/usr/bin/env python3
"""Authenticated Vermilion Pokemon metadata and source RGBA art.
Requires restored local project import helpers. No network or upstream execution.
"""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

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
PROFILE = 'firered-vermilion-pokemon-v1'
TEACHING_MOVES = ()
NEW_SPECIES = ('Meowth', 'Persian')
ART_SPECIES = tuple(zip(NEW_SPECIES, (52, 53)))
TRAINERS = ('TRAINER_BUG_CATCHER_KEIGO', 'TRAINER_CAMPER_RICKY', 'TRAINER_PICNICKER_NANCY', 'TRAINER_BUG_CATCHER_ELIJAH', 'TRAINER_PICNICKER_ISABELLE', 'TRAINER_CAMPER_JEFF')
def source_symbol(name): return name.upper()
def source_stem(name): return name.lower()
spec = importlib.util.spec_from_file_location('vermilion_private_existing_codecs', ROOT / 'scripts/import-route3-pokemon.py')
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
canonical, unique_pairs = base.canonical, base.unique_pairs
safe, preflight_directory, git, battle = base.safe, base.preflight_directory, base.git, base.battle
PATHS = frozenset(p for p in base.PATHS if not p.startswith(('graphics/pokemon/', 'data/maps/')) and p != 'src/data/wild_encounters.json') | frozenset(
    f'graphics/pokemon/{source_stem(n)}/{p}' for n in NEW_SPECIES
    for p in ('front.png', 'back.png', 'normal.pal', 'shiny.pal', 'icon.png', 'footprint.png')) | frozenset(
    f'graphics/pokemon/icon_palettes/icon_palette_{i}.pal' for i in range(3)) | frozenset(
    f'data/maps/{m}/{p}' for m in ('Route5', 'Route6') for p in ('map.json', 'scripts.inc')) | frozenset(
    ('include/constants/opponents.h', 'src/battle_util.c', 'src/battle_script_commands.c', 'data/battle_scripts_1.s'))
base.PROFILE, base.NEW_SPECIES, base.ART_SPECIES = PROFILE, NEW_SPECIES, ART_SPECIES
base.LEGACY_SPECIES = base.LEGACY_SPECIES + ('EKANS', 'NIDORAN_F', 'NIDORINA', 'NIDORAN_M', 'NIDORINO', 'JIGGLYPUFF', 'MAGIKARP', 'GYARADOS',
    'ARBOK', 'CLEFAIRY', 'ZUBAT', 'GOLBAT', 'ODDISH', 'PARAS', 'PARASECT', 'BELLSPROUT', 'MAGNEMITE', 'GRIMER', 'VOLTORB', 'KOFFING', 'NIDOQUEEN', 'NIDOKING', 'CLEFABLE', 'WIGGLYTUFF', 'ABRA', 'KADABRA', 'MACHOP', 'DROWZEE', 'SHELLDER', 'HORSEA', 'GOLDEEN', 'STARYU', 'STARMIE', 'SLOWPOKE', 'GLOOM', 'WEEPINBELL')


def load_lock(path=None):
    path = path or ROOT / 'scripts/vermilion-pokemon.lock.json'
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Vermilion Pokemon source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Vermilion Pokemon source dependency set differs')
    return lock




def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is not None:
        safe(repository)
        if git(repository, 'rev-parse', 'HEAD').decode().strip() != COMMIT:
            raise ValueError('Vermilion Pokemon reference commit/tree differs')
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
        if fields['genderRatio'] not in ('PERCENT_FEMALE(25)', 'PERCENT_FEMALE(50)', 'PERCENT_FEMALE(75)', 'MON_MALE', 'MON_FEMALE', 'MON_GENDERLESS') or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW', 'GROWTH_FAST', 'GROWTH_SLOW'):
            raise ValueError('Unsupported Vermilion species profile')
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
        rows = list(re.finditer(r'^    \[' + symbol + r'\]\s*= (\{\{.*?\}\}),$', text[evolution_path], re.M | re.S))
        evolutions = []
        if len(rows) > 1: raise ValueError('Duplicate evolution row')
        m = rows[0] if rows else None
        declarations = re.findall(r'\{(EVO_\w+),\s+([A-Z_0-9]+), (SPECIES_\w+)\}', m[1]) if m else []
        if m and (not declarations or re.sub(r'\s+', '', m[1]) != '{' + ','.join('{' + ','.join(d) + '}' for d in declarations) + '}'):
            raise ValueError('Unsupported or incomplete evolution declarations')
        for method, parameter, target in declarations:
            if (symbol, method, parameter, target) != ('SPECIES_MEOWTH', 'EVO_LEVEL', '28', 'SPECIES_PERSIAN'):
                raise ValueError('Unexpected Vermilion evolution')
            evolutions.append(dict(level=28, target=ident(target), source=ref(evolution_path, symbol, m)))
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol),
            baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)},
            type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']),
            evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)},
            growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow', 'GROWTH_FAST': 'fast', 'GROWTH_SLOW': 'slow'}[fields['growthRate']],
            genderRatio={'PERCENT_FEMALE(25)': 63, 'PERCENT_FEMALE(50)': 127, 'PERCENT_FEMALE(75)': 191, 'MON_MALE': 0, 'MON_FEMALE': 254, 'MON_GENDERLESS': 255}[fields['genderRatio']],
            ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']),
            levelUpMoves=moves, evolutions=evolutions, source=source, learnsetSource=ref(learnpath, pointer, learn)))
        names.append(name('src/data/text/species_names.h', symbol))
    # Source trainer parties require definitions independently of these two complete learnsets.
    move_symbols.update(TEACHING_MOVES)
    move_symbols.update(m for t in trainer_parties(lock, sources) for mon in t['party'] for m in mon['moves'])
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
                         ('include/constants/pokemon.h', 'EVO_TRADE_ITEM'), ('include/constants/items.h', 'ITEM_LEAF_STONE'),
                         ('include/constants/items.h', 'ITEM_SUN_STONE'), ('include/constants/items.h', 'ITEM_KINGS_ROCK'),
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
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'], species=species,
        moves=moves, speciesNames=names, moveNames=move_names, constants=constants, encodedNames=encoded_names,
        deferredEvolutions=deferred, evolutionOverrides=overrides, naturalLevelLimit=28,
        itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False, tradeEvolutionExecutorImplemented=False,
        limitations=['Meowth and Persian metadata over unchanged sixty-eight-species Bill catalog; 70 species total.',
          'Meowth28 to Persian retains the source level evolution; battle ability support is separately verified.',
          'Pickup, Pay Day18 and Faint Attack25 require separately verified Vermilion execution.',
          'Complete learnsets and cries are metadata; battle execution and audible output need separate verification.'])


def trainer_parties(lock, sources):
    reference = lambda p, s, needle=None: battle.reference(lock, sources, p, s, needle)
    text = {p: b.decode() for p,b in sources.items() if p.endswith(('.h','.inc'))}
    def learnset(symbol):
        pointer = re.search(r'^    \[' + symbol + r'\] = (\w+),$', text['src/data/pokemon/level_up_learnset_pointers.h'], re.M)[1]
        block = re.search(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', text['src/data/pokemon/level_up_learnsets.h'], re.M|re.S)
        return [(int(n), m) for n,m in re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)',block[1])]
    trainers=[]
    for symbol in TRAINERS:
        record = re.search(r'^    \[' + symbol + r'\] = \{\n(.*?)^    \},', text['src/data/trainers.h'], re.M|re.S)
        shape, party_symbol = re.search(r'\.party = (NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)', record[1]).groups()
        shape_type='TrainerMonNoItemCustomMoves' if shape=='NO_ITEM_CUSTOM_MOVES' else 'TrainerMonNoItemDefaultMoves'
        party = re.search(r'^static const struct '+shape_type+' '+party_symbol+r'\[\] = \{\n(.*?)^\};',text['src/data/trainer_parties.h'],re.M|re.S)
        rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{(MOVE_\w+(?:, MOVE_\w+){3})\},\n)?    \},\n',party[1]))
        if not rows or ''.join(m[0] for m in rows)!=party[1]: raise ValueError('Unsupported Vermilion trainer party declaration')
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
    if len(trainers)!=6 or sum(len(t['party']) for t in trainers)!=12: raise ValueError('Vermilion team inventory differs')
    return trainers


def scope(lock, sources):
    doc=metadata(lock,sources)
    trainers=trainer_parties(lock,sources)
    actual={m for t in trainers for mon in t['party'] for m in mon['moves']}
    early={m['move']['value'][5:] for s in doc['species'] for m in s['levelUpMoves'] if m['level']<=28}
    return dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,naturalLevelLimit=28,trainers=trainers,
        requiredTrainerMoves=sorted(actual),teachingMoves=list(TEACHING_MOVES),nearStageLearnsetMoves=sorted(early),
        sourceDefinitions=[m for m in doc['moves'] if m['id']['value'][5:] in actual|early|set(TEACHING_MOVES)],
        newAbilities=sorted({s[k] for s in doc['species'] for k in ('ability1','ability2')} - {'ABILITY_NONE'}),
        noEvolutionMoveBackfill=True,effectsEnabledByThisCatalog=False,encountersEnabledByThisCatalog=False,
        exclusions=['Fishing and surfing', 'Beyond Route5/Route6 and Vermilion entry', 'New item and trade evolution', 'Unsupported later moves including Fake Out',
            'Unimplemented later move effects; no blanket battle level cap', 'Move metadata does not enable battle execution'])


def supplement(lock, sources):
    spec = importlib.util.spec_from_file_location('vermilion_footprints', ROOT / 'scripts/import-forest-capture-dex.py')
    footprints = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(footprints)
    codec = footprints.legacy.codec
    spec = importlib.util.spec_from_file_location('vermilion_icons', ROOT / 'scripts/import-storage-art.py')
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
        weights=weights, normalShinyIconsIdentical=True, naturalLevelLimit=28,
        limitations=['Icons use the first original 32x32 frame and a shared normal/shiny palette.',
                    'Dex descriptions and categories are reused from the authenticated full metadata document.',
                    'Cry names and IDs are source metadata only; no pre-rendered or converted audio files are imported.']))
    return outputs


def convert(lock, sources):
    outputs = base.art(lock, sources)
    art = json.loads(outputs['battle-art.json'], object_pairs_hook=unique_pairs)
    art['limitations'][0] = 'Two Vermilion dependency species, one static front/back frame each, normal and shiny offline RGBA/PNG.'
    outputs['battle-art.json'] = canonical(art)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs.update(supplement(lock, sources))
    outputs['scope.json'] = canonical(scope(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        inputCount=len(lock['files']), addedSpeciesCount=2, composedSpeciesCount=70,
        artSpeciesCount=2, textureCount=12, rgbaCount=12, upstreamScriptsExecuted=False,
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
    for name in ('route1-pokemon', 'old-man-tutorial', 'forest-pokemon', 'pewter-pokemon', 'pokedex-menu', 'capture-dex', 'forest-capture-dex', 'route22-pokemon', 'route3-pokemon', 'mtmoon-pokemon', 'cerulean-pokemon', 'cerulean-region', 'bill-region', 'bill-pokemon', 'vermilion-region'):
        for kind in ('raw', 'generated'):
            protected = (ROOT / 'content' / kind / name).resolve()
            if any(p.resolve().is_relative_to(protected) or protected.is_relative_to(p.resolve()) for p in (raw, out)):
                raise ValueError('Materialization must not overlap authenticated legacy dependencies')
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
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
    parser.add_argument('--reference-repo', type=pathlib.Path)
    parser.add_argument('--raw', type=pathlib.Path, help='Explicit raw cache rebuilds offline unless --reference-repo is also supplied')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/vermilion-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/vermilion-pokemon.lock.json')
    args = parser.parse_args()
    explicit_raw = args.raw is not None
    args.raw = args.raw or ROOT / 'content/raw/pokefirered/037335f4'
    if args.reference_repo is None and not explicit_raw:
        configured = os.environ.get('FIRERED_ORIGINAL_REFERENCE')
        if configured: args.reference_repo = pathlib.Path(configured)
        elif not args.raw.exists(): args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))

if __name__ == '__main__': main()
