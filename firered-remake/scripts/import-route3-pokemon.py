#!/usr/bin/env python3
"""Authenticated Route3 Pokemon metadata and offline normal/shiny battle pixels.

Only immutable local Git objects or verified raw cache bytes are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
"""
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
PROFILE = 'firered-route3-pokemon-v1'
LOCK_SHA256 = '35a3a6947c8554a2450c057722137bf350fcf75c6e3cf8e84d8868898e0e9a74'
MANIFEST_SHA256 = '17ce7ede18cf33bcd69c9d8477000a030bb973f94a963aa3ac0221e1be52e811'
NEW_SPECIES = ('Ekans', 'NidoranF', 'Nidorina', 'NidoranM', 'Nidorino', 'Jigglypuff', 'Magikarp', 'Gyarados')
SYMBOL_NAMES = {'NidoranF': 'NIDORAN_F', 'NidoranM': 'NIDORAN_M'}
def source_symbol(name):
    return SYMBOL_NAMES.get(name, name.upper())
def source_stem(name):
    return source_symbol(name).lower()
ART_SPECIES = (('Ekans', 23), ('NidoranF', 29), ('Nidorina', 30), ('NidoranM', 32),
               ('Nidorino', 33), ('Jigglypuff', 39), ('Magikarp', 129), ('Gyarados', 130))
PATHS = frozenset('''charmap.txt
include/constants/species.h
include/constants/moves.h
include/constants/pokemon.h
include/constants/abilities.h
include/constants/items.h
include/constants/battle.h
include/pokemon.h
src/pokemon.c
src/sound.c
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
    f'graphics/pokemon/{source_stem(name)}/{filename}' for name, _ in ART_SPECIES
    for filename in ('front.png', 'back.png', 'normal.pal', 'shiny.pal', 'icon.png', 'footprint.png')) | frozenset(
    f'graphics/pokemon/icon_palettes/icon_palette_{i}.pal' for i in range(3)) | frozenset('''src/pokemon_icon.c
src/data/pokemon/pokedex_entries.h
src/data/pokemon/pokedex_text_fr.h
src/data/pokemon_graphics/footprint_table.h
graphics/pokedex/kanto_dex_bgpals.pal
tools/gbagfx/convert_png.c
tools/gbagfx/main.c
src/data/wild_encounters.json
src/data/trainers.h
src/data/trainer_parties.h
data/maps/Route3/map.json
data/maps/Route4/map.json
data/maps/Route4_PokemonCenter_1F/scripts.inc
data/scripts/trainers.inc
sound/cry_tables.inc'''.splitlines())
_spec = importlib.util.spec_from_file_location('route3_battle_pixels', ROOT / 'scripts/import-route1-battle-art.py')
battle = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(battle)


_spec = importlib.util.spec_from_file_location('route3_existing_import_helpers', ROOT / 'scripts/import-route22-pokemon.py')
legacy = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(legacy)
sha, canonical, unique_pairs = legacy.sha, legacy.canonical, legacy.unique_pairs
safe, preflight_directory = legacy.safe, legacy.preflight_directory
verify, git = legacy.verify, legacy.git


def load_lock(path=None):
    path = path or ROOT / 'scripts/route3-pokemon.lock.json'
    data = safe(path).read_bytes()
    if sha(data) != LOCK_SHA256:
        raise ValueError('Reviewed Route3 Pokemon source lock differs')
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('tree'),
            lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (
            1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0):
        raise ValueError('Route3 Pokemon source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Route3 Pokemon source dependency set differs')
    return lock


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
                          lockSha256=LOCK_SHA256, fullSourceHash=battle.source_set_hash(lock['files']),
                          inputCount=len(lock['files'])))


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is None:
        if safe(raw, 'manifest.json').read_bytes() != raw_manifest(lock):
            raise ValueError('Route3 Pokemon raw manifest differs')
    else:
        safe(repository)
        if git(repository, 'rev-parse', 'HEAD').decode().strip() != COMMIT or git(repository, 'rev-parse', COMMIT + '^{tree}').decode().strip() != TREE:
            raise ValueError('Route3 Pokemon reference commit/tree differs')
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            row = git(repository, 'ls-tree', COMMIT, '--', entry['path']).decode('utf-8')
            if row != '100644 blob ' + entry['gitBlobSha1'] + '\t' + entry['path'] + '\n':
                raise ValueError('Route3 Pokemon input is not the pinned regular Git blob: ' + entry['path'])
            data = git(repository, 'cat-file', 'blob', entry['gitBlobSha1'])
        verify(data, entry)
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
                    endLine=text[path].count('\n', 0, match.end()) + 1, sha256=entries[path]['sha256'])

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
    display = battle.display_contract(lock, sources)
    outputs, sprites = {}, []
    elevation_path = 'src/data/pokemon_graphics/enemy_mon_elevation.h'
    elevation_source = battle.section(sources[elevation_path].decode('utf-8'), 'const u8 gEnemyMonElevation[NUM_SPECIES] =')
    body = elevation_source.split('{', 1)[1].rsplit('}', 1)[0]
    if not re.fullmatch(r'(?:\s*\[SPECIES_[A-Z0-9_]+\] = \d+,\s*)+', body):
        raise ValueError('Unsupported enemy elevation table')
    for name, sid in ART_SPECIES:
        stem, symbol = source_stem(name), 'SPECIES_' + source_symbol(name)
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
                          f'    SPECIES_SPRITE({source_symbol(name)}, {image_symbol}),')]
            for variant in ('normal', 'shiny'):
                palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
                colors, words = battle.pixels.palette(sources[palette_path])
                rgba = battle.pixels.rgba_pixels(indices, colors)
                filename = f'{stem}-{side}-{variant}'
                outputs[filename + '.png'] = battle.pixels.rgba_png(rgba)
                outputs[filename + '.rgba'] = rgba
                palette_symbol = 'gMon' + ('Shiny' if variant == 'shiny' else '') + 'Palette_' + name
                table = 'shiny_palette_table.h' if variant == 'shiny' else 'palette_table.h'
                macro = 'SPECIES_SHINY_PAL' if variant == 'shiny' else 'SPECIES_PAL'
                refs = shared + [reference(palette_path, palette_symbol),
                    reference('src/data/graphics/pokemon.h', palette_symbol,
                        f'const u32 {palette_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{variant}.gbapal.lz");'),
                    reference('src/data/pokemon_graphics/' + table, macro,
                        f'    {macro}({source_symbol(name)}, {palette_symbol}),')]
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, side=side,
                    paletteVariant=variant, texturePath=filename + '.png', sha256=sha(outputs[filename + '.png']),
                    rgbaPath=filename + '.rgba', rgbaSha256=sha(rgba), sourceImagePath=image_path,
                    sourcePalettePath=palette_path, indexSha256=sha(indices), width=64, height=64,
                    frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0,
                    sourceWidth=width, sourceHeight=height, sourceYOffset=offset, sourceEnemyElevation=elevation,
                    singleBattleBaseCenter=base, singleBattleCenter=center,
                    singleBattleTopLeft=dict(x=center['x'] - 32, y=center['y'] - 32),
                    alphaBounds=dict(x=left, y=top, width=right-left, height=bottom-top), transparentIndex=0,
                    paletteRgb555=words, paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False, sourceRefs=refs))
    outputs['battle-art.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        fullSourceHash=battle.source_set_hash(lock['files']), display=display, sprites=sprites,
        runtimeShadersUsed=False, limitations=[
            'Eight Route3 species, one static front/back frame each, normal and shiny offline RGBA/PNG.',
            'Default single-battle placement only; no animation, shadows, battle UI, cry audio or original-frame equivalence claim.']))
    return outputs


DEX_DEPENDENCY = 'content/generated/pokedex-menu/pokedex-menu.json'
DEX_SHA256 = '7b4b6c54230ebd8e0a588a5a8d7d0fce43c68fa1da7a16a91c08d286bdf29806'
LEGACY_SPECIES = ('BULBASAUR', 'IVYSAUR', 'VENUSAUR', 'CHARMANDER', 'CHARMELEON', 'CHARIZARD',
    'SQUIRTLE', 'WARTORTLE', 'BLASTOISE', 'PIDGEY', 'PIDGEOTTO', 'PIDGEOT', 'RATTATA', 'RATICATE',
    'WEEDLE', 'KAKUNA', 'BEEDRILL', 'CATERPIE', 'METAPOD', 'BUTTERFREE', 'PIKACHU', 'RAICHU',
    'GEODUDE', 'GRAVELER', 'GOLEM', 'SANDSHREW', 'SANDSLASH', 'ONIX', 'STEELIX', 'SPEAROW', 'FEAROW', 'MANKEY')


def supplement(lock, sources):
    # These are our existing offline codecs, never code from the reference tree.
    spec = importlib.util.spec_from_file_location('route3_footprints', ROOT / 'scripts/import-forest-capture-dex.py')
    footprints = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(footprints)
    codec = footprints.legacy.codec
    spec = importlib.util.spec_from_file_location('route3_icons', ROOT / 'scripts/import-storage-art.py')
    storage = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(storage)
    dex_bytes = safe(ROOT, DEX_DEPENDENCY).read_bytes()
    if sha(dex_bytes) != DEX_SHA256:
        raise ValueError('Existing full Pokedex metadata differs')
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
        stem, symbol = source_stem(name), 'SPECIES_' + source_symbol(name)
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
        outputs[path + '.png'], outputs[path + '.rgba'] = codec.png(32, 32, rgba), rgba
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid,
            texturePath=path + '.png', rgbaPath=path + '.rgba', sha256=sha(outputs[path + '.png']),
            rgbaSha256=sha(rgba), width=32, height=32, sourceFrameIndex=0, sourceFrameCount=2,
            paletteIndex=index, paletteVariant='shared-normal-shiny', transparentIndex=0, indexSha256=sha(pixels),
            paletteRgb555=[(r >> 3) | ((g >> 3) << 5) | ((b >> 3) << 10) for r, g, b in colors],
            sourceRefs=[reference(image_path, 'gMonIcon_' + name), reference(palette_path, 'gMonIconPalettes'),
                        reference('src/pokemon_icon.c', 'gMonIconPaletteIndices[' + symbol + ']', m[0]),
                        reference('src/pokemon_icon.c', 'gMonIconTable[' + symbol + ']', binding[0]),
                        reference('src/data/graphics/pokemon.h', 'gMonIcon_' + name, declaration[0])]))
        foot_path = f'graphics/pokemon/{stem}/footprint.png'
        if f'[{symbol}] = gMonFootprint_{name},' not in foot_table or f'const u8 gMonFootprint_{name}[] = INCBIN_U8("graphics/pokemon/{stem}/footprint.1bpp");' not in graphics:
            raise ValueError('Missing source footprint binding')
        foot = codec.rgba(footprints.footprint_indices(sources[foot_path]), foot_palette[:16])
        path = stem + '-footprint'
        outputs[path + '.png'], outputs[path + '.rgba'] = codec.png(16, 16, foot), foot
        assets.append(dict(sourceSpeciesId=sid, texturePath=path + '.png', rgbaPath=path + '.rgba',
            sha256=sha(outputs[path + '.png']), rgbaSha256=sha(foot), width=16, height=16,
            source=reference(foot_path, 'gMonFootprint_' + name)))
        # Kanto cry API IDs equal species IDs. Preserve only source symbol/table metadata,
        # not WAV/OGG/QOA data or a claim that the runtime synth supports a new sample.
        cry_table = sources['sound/cry_tables.inc'].decode()
        cry_name = 'Cry_' + name
        cry_match = re.search(r'^\tcry ' + cry_name + r'$', cry_table, re.M)
        cry_rows = re.findall(r'^\tcry (Cry_\w+)$', cry_table, re.M)
        if cry_rows[sid - 1] != cry_name:
            raise ValueError('Cry table index differs')
        if cry_match is None:
            raise ValueError('Missing source cry identity')
        cries.append(dict(sourceSpeciesId=sid, cryId=sid, tableIndex=sid - 1, crySymbol=cry_name,
            source=reference('sound/cry_tables.inc', cry_name, cry_match[0]),
            mappingSource=reference('src/pokemon.c', 'SpeciesToCryId',
                battle.section(sources['src/pokemon.c'].decode(), 'u16 SpeciesToCryId(u16 species)')),
            playbackVerified=False, audioBytesImported=False,
            callerSource=reference('src/sound.c', 'PlayCryInternal',
                battle.section(sources['src/sound.c'].decode(), 'void PlayCryInternal(u16 species, s8 pan, s8 volume, u8 priority, u8 mode)'))))
    for name in LEGACY_SPECIES + tuple(source_symbol(n) for n in NEW_SPECIES):
        symbol = 'NATIONAL_DEX_' + name
        match = re.search(r'    \[' + symbol + r'\] =\s*\{(.*?)\n    \},', dex_source, re.S)
        if match is None:
            raise ValueError('Missing weight source')
        weight = int(re.search(r'\.weight = (\d+),', match[1])[1])
        entry = next(e for e in dex['entries'] if e['sourceSpeciesId'] == int(re.search(r'^#define SPECIES_' + name + r'\s+(\d+)', sources['include/constants/species.h'].decode(), re.M)[1]))
        if entry['weightHectograms'] != weight:
            raise ValueError('Reused weight differs from fixed source')
        weights.append(dict(speciesId='pret:SPECIES_' + name, sourceSpeciesId=entry['sourceSpeciesId'],
            weightHectograms=weight, source=reference('src/data/pokemon/pokedex_entries.h', symbol, match[0])))
    # Reuse the complete existing text/measure metadata by authenticated document reference.
    # New content stores only assets and weights, never competing copies of the 386 entries.
    outputs['supplement.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        dexMetadataDependency=dict(path=DEX_DEPENDENCY, sha256=DEX_SHA256, byteLength=len(dex_bytes)),
        dexSpeciesIds=[sid for _, sid in ART_SPECIES], icons=icons, footprints=assets, cries=cries,
        weights=weights, normalShinyIconsIdentical=True, naturalLevelLimit=20,
        limitations=['Icons use the first original 32x32 frame and a shared normal/shiny palette.',
                    'Dex descriptions and categories are reused from the authenticated full metadata document.',
                    'Cry names and IDs are source metadata only; no pre-rendered or converted audio files are imported.']))
    return outputs


def scope(lock, sources):
    """Trace the bounded additions to actual FireRed slots, initial trainers and gift.

    These are requirements/evidence only. Encounter reachability and execution are
    validated by the separately authenticated region/application profile.
    """
    reference = lambda path, symbol, needle=None: battle.reference(lock, sources, path, symbol, needle)
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
        tree=TREE, lockSha256=LOCK_SHA256, fullSourceHash=battle.source_set_hash(lock['files']),
        inputCount=len(lock['files']), addedSpeciesCount=8, composedSpeciesCount=40,
        artSpeciesCount=8, textureCount=48, rgbaCount=48, upstreamScriptsExecuted=False,
        romDownloaded=False, publiclyUploaded=False, runtimeShadersUsed=False,
        outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(outputs.items())]))
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
    for name in ('route1-pokemon', 'old-man-tutorial', 'forest-pokemon', 'pewter-pokemon', 'pokedex-menu', 'capture-dex', 'forest-capture-dex', 'route22-pokemon'):
        for kind in ('raw', 'generated'):
            protected = (ROOT / 'content' / kind / name).resolve()
            if any(p.resolve().is_relative_to(protected) or protected.is_relative_to(p.resolve()) for p in (raw, out)):
                raise ValueError('Materialization must not overlap authenticated legacy dependencies')
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    if sha(outputs['manifest.json']) != MANIFEST_SHA256:
        raise ValueError('Reviewed Route3 Pokemon generated output hashes differ')
    # Parse/render/authenticate everything, then preflight every target before the first write.
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
    if repository is not None:
        material[(raw, 'manifest.json')] = raw_manifest(lock)
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
    return dict(profile=PROFILE, inputCount=len(sources), outputCount=len(outputs),
                catalogSha256=sha(outputs['catalog.json']), manifestSha256=sha(outputs['manifest.json']),
                fullSourceHash=battle.source_set_hash(lock['files']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/route3-pokemon')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/route3-pokemon')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/route3-pokemon.lock.json')
    args = parser.parse_args()
    if args.reference_repo is None and not args.raw.exists():
        args.reference_repo = pathlib.Path((ROOT / '.tools/reference-root').read_text().strip())
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
