#!/usr/bin/env python3
"""Authenticated Route22 Pokemon metadata and offline normal/shiny battle pixels.

Only immutable local Git objects or verified raw cache bytes are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
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
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-route22-pokemon-v1'
NEW_SPECIES = ('Spearow', 'Fearow', 'Mankey')
ART_SPECIES = (('Spearow', 21), ('Fearow', 22), ('Mankey', 56))
PATHS = frozenset('''charmap.txt
include/constants/species.h
include/constants/moves.h
include/constants/pokemon.h
include/constants/abilities.h
include/constants/items.h
include/constants/battle.h
include/pokemon.h
src/pokemon.c
src/data/pokemon/species_info.h
src/data/pokemon/level_up_learnset_pointers.h
src/data/pokemon/level_up_learnsets.h
src/data/pokemon/evolution.h
src/data/pokemon/experience_tables.h
src/data/battle_moves.h
src/data/text/species_names.h
src/data/text/move_names.h
src/data/graphics/pokemon.h
src/data/pokemon_graphics/front_pic_coordinates.h
src/data/pokemon_graphics/back_pic_coordinates.h
src/data/pokemon_graphics/front_pic_table.h
src/data/pokemon_graphics/back_pic_table.h
src/data/pokemon_graphics/palette_table.h
src/data/pokemon_graphics/shiny_palette_table.h
src/data/pokemon_graphics/enemy_mon_elevation.h
src/battle_anim_mons.c
src/battle_controller_player.c
src/battle_controller_opponent.c
tools/gbagfx/gfx.c'''.splitlines()) | frozenset(
    f'graphics/pokemon/{name.lower()}/{filename}' for name, _ in ART_SPECIES
    for filename in ('front.png', 'back.png', 'normal.pal', 'shiny.pal', 'icon.png', 'footprint.png')) | frozenset(
    f'graphics/pokemon/icon_palettes/icon_palette_{i}.pal' for i in range(3)) | frozenset(
    f'sound/direct_sound_samples/cries/{name.lower()}.wav' for name, _ in ART_SPECIES) | frozenset('''src/pokemon_icon.c
src/data/pokemon/pokedex_entries.h
src/data/pokemon/pokedex_text_fr.h
src/data/pokemon_graphics/footprint_table.h
graphics/pokedex/kanto_dex_bgpals.pal
tools/gbagfx/convert_png.c
tools/gbagfx/main.c'''.splitlines())
_spec = importlib.util.spec_from_file_location('route22_battle_pixels', ROOT / 'scripts/import-route1-battle-art.py')
battle = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(battle)


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def unique_pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is None:
        target = root
    else:
        path = pathlib.PurePosixPath(relative)
        if not relative or path.is_absolute() or any(x in ('', '.', '..') for x in relative.split('/')) or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe Route22 Pokemon path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output or ancestor rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Route22 Pokemon path escapes root')
    return target


def preflight_directory(path):
    # Check the entire existing chain before any raw/output bytes are written.
    # A missing child beneath a file can otherwise look nonexistent until mkdir.
    for directory in (path, *path.parents):
        if directory.exists() and not directory.is_dir():
            raise ValueError('Input/output ancestor is not a directory: ' + str(directory))


def load_lock(path=None):
    path = path or ROOT / 'scripts/route22-pokemon.lock.json'
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Route22 Pokemon source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Route22 Pokemon source dependency set differs')
    return lock




def git(repository, *args):
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull, GIT_NO_REPLACE_OBJECTS='1')
    return subprocess.run(['git', '--no-replace-objects', '-C', str(repository), *args],
                          env=env, check=True, capture_output=True, timeout=30).stdout


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is not None:
        safe(repository)
        if git(repository, 'rev-parse', 'HEAD').decode().strip() != COMMIT:
            raise ValueError('Route22 Pokemon reference commit/tree differs')
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

    species, names, deferred, move_symbols = [], [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for name_ in NEW_SPECIES:
        symbol = 'SPECIES_' + name_.upper()
        fields, source = block('src/data/pokemon/species_info.h', symbol)
        if fields['genderRatio'] != 'PERCENT_FEMALE(50)' or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW'):
            raise ValueError('Unsupported Route22 species profile')
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
            trade = re.fullmatch(r'\{\{EVO_TRADE, 0, (SPECIES_\w+)\}\}', m[1])
            trade_item = re.fullmatch(r'\{\{EVO_TRADE_ITEM, (ITEM_\w+), (SPECIES_\w+)\}\}', m[1])
            if level:
                evolution = dict(level=int(level[1]), target=ident(level[2]), source=ref(evolution_path, symbol, m))
            elif trade and symbol == 'SPECIES_GRAVELER' and trade[1] == 'SPECIES_GOLEM':
                evolution = dict(level=0, target=ident(trade[1]), source=ref(evolution_path, symbol, m), method='trade')
            elif trade_item and symbol == 'SPECIES_ONIX' and (trade_item[1], trade_item[2]) == ('ITEM_METAL_COAT', 'SPECIES_STEELIX'):
                evolution = dict(level=0, target=ident(trade_item[2]), source=ref(evolution_path, symbol, m),
                                 method='tradeItem', parameter=trade_item[1])
            else:
                raise ValueError('Unsupported evolution declaration: ' + m[1])
            if symbol == 'SPECIES_MANKEY':
                if (evolution['level'], evolution['target']['value']) != (28, 'pret:SPECIES_PRIMEAPE'):
                    raise ValueError('Deferred evolution differs')
                deferred.append(dict(species=ident(symbol), evolution=evolution, executable=False, reason='Target lies above current natural level 20 boundary'))
            else:
                if evolution['target']['value'] not in {'pret:SPECIES_' + n.upper() for n in NEW_SPECIES}:
                    raise ValueError('Unimported forward evolution target')
                evolutions.append(evolution)
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol),
            baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)},
            type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']),
            evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)}, growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow'}[fields['growthRate']],
            genderRatio=127, ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']),
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
    for path, symbol in [('include/constants/pokemon.h', 'EVO_LEVEL'),
                         ('include/constants/abilities.h', 'ABILITY_KEEN_EYE'),
                         ('include/constants/abilities.h', 'ABILITY_VITAL_SPIRIT')]:
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
        species=species, moves=moves, speciesNames=names, moveNames=move_names, constants=constants,
        encodedNames=encoded_names, deferredEvolutions=deferred, itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False,
        tradeEvolutionExecutorImplemented=False,
        limitations=['Adds Spearow, Fearow and Mankey over the unchanged twenty-nine species.',
                     'Full learnsets are source metadata, not a claim that every later move executes.',
                     'Spearow level 20 Fearow target is present; Mankey level 28 Primeape stays a non-executable deferred reference.',
                     'Existing six-move combat support is implemented separately; this importer does not execute battle or evolution rules.',
                     'Original English charmap name bytes include EOS; no native save storage is claimed.'])


def art(lock, sources):
    display = battle.display_contract(lock, sources)
    outputs, sprites = {}, []
    elevation_path = 'src/data/pokemon_graphics/enemy_mon_elevation.h'
    elevation_source = battle.section(sources[elevation_path].decode('utf-8'), 'const u8 gEnemyMonElevation[NUM_SPECIES] =')
    body = elevation_source.split('{', 1)[1].rsplit('}', 1)[0]
    if not re.fullmatch(r'(?:\s*\[SPECIES_[A-Z0-9_]+\] = \d+,\s*)+', body):
        raise ValueError('Unsupported enemy elevation table')
    for name, sid in ART_SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        row = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', elevation_source, re.M)
        front_elevation = int(row[1]) if row else 0
        for side in ('front', 'back'):
            image_path = f'graphics/pokemon/{stem}/{side}.png'
            coords_path = f'src/data/pokemon_graphics/{side}_pic_coordinates.h'
            coords = re.search(r'    \[' + symbol + r'\] =\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+),\s*\},', sources[coords_path].decode('utf-8'))
            if coords is None:
                raise ValueError('Missing source sprite coordinates')
            width, height, offset = map(int, coords.groups())
            indices = battle.pixels.indexed_png(sources[image_path])
            opaque = [i for i, index in enumerate(indices) if index]
            if not opaque:
                raise ValueError('Empty sprite')
            left, top = min(i % 64 for i in opaque), min(i // 64 for i in opaque)
            right, bottom = max(i % 64 for i in opaque) + 1, max(i // 64 for i in opaque) + 1
            base = display['playerBaseCenter' if side == 'back' else 'opponentBaseCenter']
            elevation = 0 if side == 'back' else front_elevation
            center = dict(x=base['x'], y=base['y'] + offset - elevation)
            image_symbol = f'gMon{side.title()}Pic_{name}'
            def reference(path, symbol_, needle=None):
                return battle.reference(lock, sources, path, symbol_, needle)
            shared = [reference(image_path, image_symbol), reference(coords_path, f'gMon{side.title()}PicCoords[{symbol}]', coords[0]),
                      reference(elevation_path, f'gEnemyMonElevation[{symbol}]', elevation_source),
                      reference('src/data/graphics/pokemon.h', image_symbol,
                          f'const u32 {image_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{side}.4bpp.lz");'),
                      reference(f'src/data/pokemon_graphics/{side}_pic_table.h', f'gMon{side.title()}PicTable',
                          f'    SPECIES_SPRITE({name.upper()}, {image_symbol}),')]
            for variant in ('normal', 'shiny'):
                palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
                colors, words = battle.pixels.palette(sources[palette_path])
                rgba = battle.pixels.rgba_pixels(indices, colors)
                filename = f'{stem}-{side}-{variant}'
                outputs[filename + '.png'] = battle.pixels.rgba_png(rgba)
                palette_symbol = 'gMon' + ('Shiny' if variant == 'shiny' else '') + 'Palette_' + name
                table = 'shiny_palette_table.h' if variant == 'shiny' else 'palette_table.h'
                macro = 'SPECIES_SHINY_PAL' if variant == 'shiny' else 'SPECIES_PAL'
                refs = shared + [reference(palette_path, palette_symbol),
                    reference('src/data/graphics/pokemon.h', palette_symbol,
                        f'const u32 {palette_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{variant}.gbapal.lz");'),
                    reference('src/data/pokemon_graphics/' + table, macro,
                        f'    {macro}({name.upper()}, {palette_symbol}),')]
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, side=side, paletteVariant=variant, texturePath=filename + '.png', sourceImagePath=image_path, sourcePalettePath=palette_path, width=64, height=64, frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0, sourceWidth=width, sourceHeight=height, sourceYOffset=offset, sourceEnemyElevation=elevation, singleBattleBaseCenter=base, singleBattleCenter=center, singleBattleTopLeft=dict(x=center['x'] - 32, y=center['y'] - 32), alphaBounds=dict(x=left, y=top, width=right - left, height=bottom - top), transparentIndex=0, paletteRgb555=words, paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False, sourceRefs=refs))
    outputs['battle-art.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
         display=display, sprites=sprites,
        runtimeShadersUsed=False, limitations=[
            'Three Route22 species, one static front/back frame each, normal and shiny offline RGBA/PNG.',
            'Default single-battle placement only; no animation, shadows, battle UI, cry audio or original-frame equivalence claim.']))
    return outputs


DEX_DEPENDENCY = 'content/generated/pokedex-menu/pokedex-menu.json'
LEGACY_SPECIES = ('BULBASAUR', 'IVYSAUR', 'VENUSAUR', 'CHARMANDER', 'CHARMELEON', 'CHARIZARD',
    'SQUIRTLE', 'WARTORTLE', 'BLASTOISE', 'PIDGEY', 'PIDGEOTTO', 'PIDGEOT', 'RATTATA', 'RATICATE',
    'WEEDLE', 'KAKUNA', 'BEEDRILL', 'CATERPIE', 'METAPOD', 'BUTTERFREE', 'PIKACHU', 'RAICHU',
    'GEODUDE', 'GRAVELER', 'GOLEM', 'SANDSHREW', 'SANDSLASH', 'ONIX', 'STEELIX')


def supplement(lock, sources):
    # These are our existing offline codecs, never code from the reference tree.
    spec = importlib.util.spec_from_file_location('route22_footprints', ROOT / 'scripts/import-forest-capture-dex.py')
    footprints = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(footprints)
    codec = footprints.legacy.codec
    spec = importlib.util.spec_from_file_location('route22_icons', ROOT / 'scripts/import-storage-art.py')
    storage = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(storage)
    dex_bytes = safe(ROOT, DEX_DEPENDENCY).read_bytes()
    dex = json.loads(dex_bytes, object_pairs_hook=unique_pairs)
    if len(dex['entries']) != 386:
        raise ValueError('Existing full Pokedex metadata is incomplete')
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
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        image_path = f'graphics/pokemon/{stem}/icon.png'
        m = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', palette_section, re.M)
        binding = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_section, re.M)
        if m is None or binding is None or int(m[1]) not in range(3):
            raise ValueError('Missing source icon binding')
        declaration = re.search(r'const u8\s+gMonIcon_' + name + r'\[\]\s*=\s*INCBIN_U8\(\s*"graphics/pokemon/' + stem + r'/icon\.4bpp"\);', graphics)
        if declaration is None:
            raise ValueError('Missing source icon declaration')
        index = int(m[1])
        palette_path = f'graphics/pokemon/icon_palettes/icon_palette_{index}.pal'
        width, height, pixels, _ = codec.indexed_png(sources[image_path])
        if (width, height) != (32, 64):
            raise ValueError('Unexpected source icon dimensions')
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
        cry_path = f'sound/direct_sound_samples/cries/{stem}.wav'
        data = sources[cry_path]
        if data[:4] != b'RIFF' or data[8:12] != b'WAVE':
            raise ValueError('Source cry WAV differs')
        outputs[f'cries/{stem}.wav'] = data
        cries.append(dict(sourceSpeciesId=sid, wavePath=f'cries/{stem}.wav',
            source=reference(cry_path, 'CRY_' + name.upper()), playbackVerified=False))
    for name in LEGACY_SPECIES + tuple(n.upper() for n in NEW_SPECIES):
        symbol = 'NATIONAL_DEX_' + name
        match = re.search(r'    \[' + symbol + r'\] =\s*\{(.*?)\n    \},', dex_source, re.S)
        if match is None:
            raise ValueError('Missing weight source')
        weight = int(re.search(r'\.weight = (\d+),', match[1])[1])
        entry = next(e for e in dex['entries'] if e['name'] == name)
        if entry['weightHectograms'] != weight:
            raise ValueError('Reused weight differs from fixed source')
        weights.append(dict(speciesId='pret:SPECIES_' + name, sourceSpeciesId=entry['sourceSpeciesId'],
            weightHectograms=weight, source=reference('src/data/pokemon/pokedex_entries.h', symbol, match[0])))
    # Reuse the complete existing text/measure metadata by authenticated document reference.
    # New content stores only assets and weights, never competing copies of the 386 entries.
    outputs['supplement.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        dexMetadataDependency=dict(path=DEX_DEPENDENCY),
        dexSpeciesIds=[sid for _, sid in ART_SPECIES], icons=icons, footprints=assets, cries=cries,
        weights=weights, normalShinyIconsIdentical=True, naturalLevelLimit=20,
        limitations=['Icons use the first original 32x32 frame and a shared normal/shiny palette.',
                    'Dex descriptions and categories are reused from the authenticated full metadata document.',
                    'Cry WAV bytes are source assets, not verified audio playback; no cry conversion is claimed.']))
    return outputs


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['catalog.json'] = canonical(metadata(lock, sources))
    outputs.update(supplement(lock, sources))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,

        inputCount=len(lock['files']), addedSpeciesCount=3, composedSpeciesCount=32,
        artSpeciesCount=3, textureCount=18, rgbaCount=18, upstreamScriptsExecuted=False,
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
    for name in ('route1-pokemon', 'old-man-tutorial', 'forest-pokemon', 'pewter-pokemon', 'pokedex-menu', 'capture-dex', 'forest-capture-dex'):
        protected = (ROOT / 'content/generated' / name).resolve()
        if any(p.resolve().is_relative_to(protected) or protected.is_relative_to(p.resolve()) for p in (raw, out)):
            raise ValueError('Materialization must not overlap authenticated legacy dependencies')
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    # Parse/render/authenticate everything, then preflight every target before the first write.
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
    if repository is not None:
        pass
    material.update({(out, p): b for p, b in outputs.items()})
    targets = [(safe(root, p), safe(root, p + '.tmp'), b) for (root, p), b in material.items()]
    for target, _, _ in targets:
        preflight_directory(target.parent)
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
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/route22-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/route22-pokemon.lock.json')
    args = parser.parse_args()
    if args.reference_repo is None and not args.raw.exists():
        args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
