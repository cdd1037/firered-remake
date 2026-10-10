#!/usr/bin/env python3
"""Import authenticated FireRed numerical Pokédex metadata; read local Git/cache only.

Original descriptions are content, not permission to reveal unseen/uncaught data.
Existing page skin, seven footprints and 29 normal fronts are referenced unchanged.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-pokedex-menu-v1'
LOCK_SHA256 = '65a9bc5af5d36a674acedd46de16418ccdc4a00fabfc16726041096dd5c6d61a'
MANIFEST_SHA256 = 'd6a1df21be8a945eab0223cabd0704f0b38c3e853c262638ffbbc1778bd3f5f8'
CURRENT = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 25, 26, 27, 28, 74, 75, 76, 95, 208)
NEW_FOOTPRINTS = ('bulbasaur', 'ivysaur', 'venusaur', 'charmander', 'charmeleon', 'charizard', 'squirtle', 'wartortle', 'blastoise',
                  'butterfree', 'beedrill', 'pidgeotto', 'pidgeot', 'raticate', 'raichu', 'sandshrew', 'sandslash', 'geodude', 'graveler', 'golem', 'onix', 'steelix')
PATHS = frozenset('''src/start_menu.c
src/pokedex_screen.c
src/pokedex.c
src/event_data.c
src/list_menu.c
src/pokemon.c
src/strings.c
src/graphics.c
src/data/text/species_names.h
src/data/pokemon/pokedex_entries.h
src/data/pokemon/pokedex_text.h
src/data/pokemon/pokedex_text_fr.h
src/data/pokemon/pokedex_categories.h
src/data/pokemon/species_info.h
src/data/pokemon_graphics/footprint_table.h
src/data/graphics/pokemon.h
include/constants/pokedex.h
include/constants/species.h
include/constants/flags.h
include/constants/vars.h
include/menu.h
graphics/interface/menu_info.png
graphics/interface/dex_caught_pokeball.pal
graphics/pokedex/kanto_dex_bgpals.pal
tools/gbagfx/gfx.c
tools/gbagfx/main.c
tools/gbagfx/convert_png.c'''.splitlines()) | frozenset('graphics/pokemon/' + n + '/footprint.png' for n in NEW_FOOTPRINTS)
# Immutable hashes of existing catalogs, page assets and normal front textures.
DEPENDENCIES = {'content/generated/capture-dex/capture-dex-background.png': '580534eb2e35e17b05f7e6fb242be081a369ce5ea05ae4ac7ad1c659895a3906',
 'content/generated/capture-dex/capture-dex.json': '689d076758311629ab50a5e599345030aca63040e13c4956146a92cb6129bcd4',
 'content/generated/capture-dex/pidgey-footprint.png': '1955a3f8570a4ffe00e20c7a385b69edb56e33ebd09ac258032577982e86f9e1',
 'content/generated/capture-dex/rattata-footprint.png': '0ef8c3424c7005a30158e38e1c9e21b7d1ee12d7b2f6599f42e973347bed2cb3',
 'content/generated/evolution-art/battle-art.json': '9404e2c2fa127c807165edb47d0b22bafa533ad0cb5a4e599f3b05683f0d9f1c',
 'content/generated/evolution-art/blastoise-front-normal.png': 'e8c397f929f2d324c49635fdb4061786c5cf66b6a7711a465a923d9ead388452',
 'content/generated/evolution-art/charizard-front-normal.png': 'fee3d2e9878399d173c95d5901f5027cc456982e0a53d98d16cf25e22ea35098',
 'content/generated/evolution-art/charmeleon-front-normal.png': '11bb9cd2a89aa050f2aa3bc358ddb1ba5919fc6f0f39fc5aa762c54352c21041',
 'content/generated/evolution-art/ivysaur-front-normal.png': '66c9b16e8f9a7b317376fc50cfdf6e34b22cf6a0c4fff2aa36430c08f71f72b6',
 'content/generated/evolution-art/pidgeot-front-normal.png': '9626672dd1c9368e4a97d7e437df7168b48438cef7474692de6a313d0040f86c',
 'content/generated/evolution-art/pidgeotto-front-normal.png': '7892ce37e55110bf4118de8b57c96688cd7d0e77afa7da1746fd32fa9a137579',
 'content/generated/evolution-art/raticate-front-normal.png': '7670e8903899d3dda54d8f38cdedcb151c6278bb44caef88f5bc3ebd1d08d02c',
 'content/generated/evolution-art/venusaur-front-normal.png': '842fe037395803654ad2f743dfd7fcf2e1cbd3db960d40a3fa79cefc83ca1590',
 'content/generated/evolution-art/wartortle-front-normal.png': 'b73acac9691b733fd19527840b305079926f1d55e7da398d0e739c3c6c0bc677',
 'content/generated/forest-capture-dex/capture-dex.json': '1bbd7697c985340f787836d626b4b6c1ff8b71885ba03c3e86c7c28001bddcb1',
 'content/generated/forest-capture-dex/caterpie-footprint.png': '7c417b2881debaaa11ebb1afbb79342e2f874edae466296fce672c3a40f1b8d9',
 'content/generated/forest-capture-dex/kakuna-footprint.png': '782069d8125396d0d5c5115ea5a1b81e029bf5615d52ea40453b6f3f1a3420af',
 'content/generated/forest-capture-dex/metapod-footprint.png': '782069d8125396d0d5c5115ea5a1b81e029bf5615d52ea40453b6f3f1a3420af',
 'content/generated/forest-capture-dex/pikachu-footprint.png': '6a19f837549dabee7ebbec59a1600f12309320a02e363cafe2e318d43585dba0',
 'content/generated/forest-capture-dex/weedle-footprint.png': '7c417b2881debaaa11ebb1afbb79342e2f874edae466296fce672c3a40f1b8d9',
 'content/generated/forest-pokemon/battle-art.json': '326869d4a8df16a11d0a5d0e1093cf1e11a76a906e267c1cd01e2e61f8fed0a5',
 'content/generated/forest-pokemon/beedrill-front-normal.png': '1759d804dda2c5e0b4f8c4741a5b9475502f752775b2027bdb9dc2cde5cdc4c9',
 'content/generated/forest-pokemon/butterfree-front-normal.png': 'a2df32ceafd4a8816c26914771957203354d65f65e0445869f36ba880814d779',
 'content/generated/forest-pokemon/caterpie-front-normal.png': '34631b844a5b38f473201fcf086cb30113793b357bcfd08da9cfe4fd7997a565',
 'content/generated/forest-pokemon/kakuna-front-normal.png': '4b9557fb1b5d10f38cdf9743fc729e2d0a8e89c719f3c88843f27642a1c36009',
 'content/generated/forest-pokemon/metapod-front-normal.png': '65cd1e720b3f23b191cae34e8734ebce44ad604c224f5fb5ff0cb9ebf848fe7e',
 'content/generated/forest-pokemon/pikachu-front-normal.png': '3323e4a19ac1ebaf219125e2108b438e415e97114f41c2b0b177093f30ad4e63',
 'content/generated/forest-pokemon/raichu-front-normal.png': '2b1aff2dcf64374bdca724f82660502151e57c30174c5143981d73d07abf38ed',
 'content/generated/forest-pokemon/weedle-front-normal.png': 'ba22e07eede464ce4e9a24f2d3db62e517cf5fc7f2ac62ea6b143a326a41af07',
 'content/generated/lab-battle-art/bulbasaur-front-normal.png': '125ef159efd3a1b470a9658735146f17c1172948a896aae48bc78fc455ccc68a',
 'content/generated/lab-battle-art/charmander-front-normal.png': 'c16180054c0fae38d424403b0cb3330e59cfceeccb145f26c9037cd60eb5bf86',
 'content/generated/lab-battle-art/lab-battle-art.json': 'd1516447a3e856a257c63e7293e8ebc8c7c555d65dbd49adca1eaa7dca7a547d',
 'content/generated/lab-battle-art/squirtle-front-normal.png': '950c3f04b75b03b851aeeb8a6356b857ba8b93de92ed6cfcf635e6ebe0c24ede',
 'content/generated/pewter-pokemon/battle-art.json': '3543357d9cc9d2f25e0fe389da3ac8f88f18df1ab9afd782ea91b01f0f625fdb',
 'content/generated/pewter-pokemon/geodude-front-normal.png': '980aeba2f67d8f25802f5d0ea75ba5d7719bc857bc74fdfd6db3e1dcc3191a15',
 'content/generated/pewter-pokemon/golem-front-normal.png': '81bfde6e3fcb575000b1961c0a91b878138d2ee733cf8b44bc6d76d33565393f',
 'content/generated/pewter-pokemon/graveler-front-normal.png': '7b41134b0720d6b3c390ae8d66e27444697753b36edfda95c68c1d26f13b4f97',
 'content/generated/pewter-pokemon/onix-front-normal.png': 'c84cbb522fdddf01af0070361f0772a67bbeecc4e4a9e3aec830c1bc0a4c37ab',
 'content/generated/pewter-pokemon/sandshrew-front-normal.png': '3ccacdecbbcebb2f2222f9c683f4fc6953dc4426a9ae2f082ac2b677f555bdd6',
 'content/generated/pewter-pokemon/sandslash-front-normal.png': '0bdc15fdf9b8765751da98546d246494914bfddc2fc1d4156a727d355ec3f3a0',
 'content/generated/pewter-pokemon/steelix-front-normal.png': '154c875dea588a4a74f49872c4b92535fa748e8b96c399d7674589a250f9e7ea',
 'content/generated/route1-battle-art/pidgey-front-normal.png': '975b439562bbec89602ce43268ee1f740614324ff9452dd4dfb43130dc1a749b',
 'content/generated/route1-battle-art/rattata-front-normal.png': '058fa9a31b07213ac500c05e523bcc321b92710075a62fb575bdc0423384f3d2',
 'content/generated/route1-battle-art/route1-battle-art.json': '5fedaf6c584e4e76e1e8b248fba83a30c5d9ff5aa661947a38e0c640ee63691d'}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, sort_keys=True) + '\n').encode('utf-8')


def pairs(rows):
    result = {}
    for key, value in rows:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def decode(data):
    return json.loads(data.decode('utf-8'), object_pairs_hook=pairs)


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is not None:
        if not relative or pathlib.PurePosixPath(relative).is_absolute() or '\\' in relative or any(p in ('', '.', '..') for p in relative.split('/')):
            raise ValueError('Unsafe Pokédex path: ' + relative)
        target = root / relative
    else:
        target = root
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Pokédex symlink rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Pokédex path escapes root')
    return target


def verify(data, entry):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest()
    if len(data) != entry['byteLength'] or sha(data) != entry['sha256'] or blob != entry['gitBlobSha1']:
        raise ValueError('Pinned Pokédex source differs: ' + entry['path'])


def source_hash(lock):
    return sha(''.join(e['path'] + '\0' + e['sha256'] + '\n' for e in lock['files']).encode('utf-8'))


def load_lock(path):
    data = safe(path).read_bytes()
    if sha(data) != LOCK_SHA256:
        raise ValueError('Reviewed Pokédex lock hash differs')
    lock = decode(data)
    if tuple(lock.get(k) for k in ('schemaVersion', 'repository', 'commit', 'tree', 'profile', 'gameEdition', 'romRevision')) != (1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0):
        raise ValueError('Pokédex source identity differs')
    if len(lock['files']) != len(PATHS) or [e['path'] for e in lock['files']] != sorted(PATHS):
        raise ValueError('Pokédex source set differs')
    return lock


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
        lockSha256=LOCK_SHA256, fullSourceHash=source_hash(lock), inputCount=len(PATHS)))


def read_sources(lock, raw, repository=None):
    if repository is None:
        if safe(raw, 'manifest.json').read_bytes() != raw_manifest(lock):
            raise ValueError('Pokédex raw manifest differs')
    else:
        safe(repository)
        tree = subprocess.check_output(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'], timeout=30).decode('ascii').strip()
        if tree != TREE:
            raise ValueError('Reference source tree differs')
    sources = {}
    for entry in lock['files']:
        path = safe(raw, entry['path'])
        if repository is None:
            data = path.read_bytes()
        else:
            row = subprocess.check_output(['git', '-C', str(repository), 'ls-tree', COMMIT, '--', entry['path']], timeout=30).decode('utf-8')
            if row != '100644 blob ' + entry['gitBlobSha1'] + '\t' + entry['path'] + '\n':
                raise ValueError('Source is not the locked regular Git blob')
            data = subprocess.check_output(['git', '-C', str(repository), 'cat-file', 'blob', entry['gitBlobSha1']], timeout=30)
        verify(data, entry)
        sources[entry['path']] = data
    return sources


def dependencies(project):
    data = {}
    for path, expected in DEPENDENCIES.items():
        value = safe(project, path).read_bytes()
        if sha(value) != expected:
            raise ValueError('Existing Pokédex dependency differs: ' + path)
        data[path] = value
    return data


def string(text, symbol):
    match = re.search(r'\b' + re.escape(symbol) + r'\[\]\s*=\s*_\((.*?)\);', text, re.S)
    if not match:
        raise ValueError('Missing string: ' + symbol)
    return ''.join(json.loads(t) for t in re.findall(r'"(?:[^"\\]|\\.)*"', match[1]))


def convert(lock, sources, old):
    for entry in lock['files']:
        verify(sources[entry['path']], entry)
    if set(sources) != PATHS:
        raise ValueError('Pokédex source set differs')
    texts = {p: b.decode('utf-8') for p, b in sources.items() if p.endswith(('.c', '.h'))}
    def ref(path, symbol, fragment=None):
        value = texts[path]
        if fragment is None:
            pattern = r'^[\w *]+\b' + re.escape(symbol) + r'\([^;]*?\)\n\{.*?^\}'
            match = re.search(pattern, value, re.M | re.S)
            fragment = match[0] if match else next(line for line in value.splitlines() if symbol in line)
        start = value[:value.index(fragment)].count('\n') + 1
        return dict(kind='source', repository='pret/pokefirered', commit=COMMIT, path=path, symbol=symbol,
            sha256=sha(sources[path]), url=f'https://github.com/pret/pokefirered/blob/{COMMIT}/{path}#L{start}', startLine=start, endLine=start + fragment.count('\n'))
    screen = texts['src/pokedex_screen.c']
    for needle in ('ret = ndex_num;', '.maxShowed = 9,', 'gText_5Dashes', '(caught << 17) + (seen << 16)',
                   'while ((categoryName[index] != CHAR_SPACE) && (index < 11))', 'inches = 10000 * height / 254;',
                   'lbs = (weight * 100000) / 4536;', 'LoadMonPicInWindow(species, SHINY_ODDS,'):
        if needle not in screen:
            raise ValueError('Pokédex behavior source differs: ' + needle)
    if '#if defined(FIRERED)\n#include "pokedex_text_fr.h"' not in texts['src/data/pokemon/pokedex_text.h']:
        raise ValueError('FireRed description selection differs')
    constants = texts['include/constants/pokedex.h'].split('// National Pokedex order', 1)[1].split('};', 1)[0]
    national = re.findall(r'^\s*NATIONAL_DEX_(\w+),', constants, re.M)[:387]
    species = {n: int(v) for n, v in re.findall(r'^#define SPECIES_(\w+)\s+(\d+)\s*$', texts['include/constants/species.h'], re.M)}
    mapping = texts['src/pokemon.c'].split('static const u16 sSpeciesToNationalPokedexNum', 1)[1].split('};', 1)[0]
    if len(national) != 387 or national[151] != 'MEW' or national[386] != 'DEOXYS':
        raise ValueError('National order capacity differs')
    old_pages = [decode(old['content/generated/' + p + '/capture-dex.json']) for p in ('capture-dex', 'forest-capture-dex')]
    portraits, footprints, assets, outputs = {}, {}, [], {}
    for path, data in old.items():
        if path.endswith('art.json'):
            for sprite in decode(data).get('sprites', []):
                if sprite.get('side') == 'front' and sprite.get('paletteVariant') == 'normal':
                    texture = str(pathlib.PurePosixPath(path).parent / sprite['texturePath'])
                    portraits[sprite['sourceSpeciesId']] = dict(texturePath=texture, sha256=sprite['sha256'],
                        rgbaSha256=sprite['rgbaSha256'], width=sprite['width'], height=sprite['height'])
    if tuple(sorted(portraits)) != CURRENT:
        raise ValueError('Current portrait species scope differs')
    for folder, page in zip(('capture-dex', 'forest-capture-dex'), old_pages):
        for sid, entry in page['entries'].items():
            asset = next(a for a in page['assets'] if a['texturePath'] == entry['footprintPath'])
            footprints[int(sid)] = dict(texturePath='content/generated/' + folder + '/' + asset['texturePath'],
                sha256=asset['sha256'], rgbaSha256=asset['rgbaSha256'], width=16, height=16)
    # Reuse our previously reviewed decoder; no code from the reference is executed.
    spec = importlib.util.spec_from_file_location('pokedex_footprint_codec', ROOT / 'scripts/import-forest-capture-dex.py')
    codec = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(codec)
    palette = codec.legacy.kanto_palette(sources['graphics/pokedex/kanto_dex_bgpals.pal'])
    # Original 12x12 caught marker, source tile offset 0 at 128-pixel bitmap stride.
    # The indexed PNG contains several palette banks; upstream 4bpp conversion takes index modulo 16.
    for needle in ('[MENU_INFO_ICON_CAUGHT] = { 12, 12, 0x00 },',
                   'palette = gMenuInfoElements1_Pal;',
                   '0, 0, 128, 128, x, y, sMenuInfoIcons[iconId].width, sMenuInfoIcons[iconId].height'):
        if needle not in texts['src/list_menu.c']:
            raise ValueError('Caught menu marker binding differs')
    if not re.search(r'^#define MENU_INFO_ICON_CAUGHT\s+0$', texts['include/menu.h'], re.M):
        raise ValueError('Caught menu icon ID differs')
    for needle in ('const u16 gMenuInfoElements1_Pal[] = INCBIN_U16("graphics/interface/dex_caught_pokeball.gbapal");',
                   'const u8 gMenuInfoElements_Gfx[] = INCBIN_U8("graphics/interface/menu_info.4bpp");'):
        if needle not in texts['src/graphics.c']:
            raise ValueError('Caught marker graphics/palette binding differs')
    width, height, menu_indices, _ = codec.legacy.codec.indexed_png(sources['graphics/interface/menu_info.png'])
    if (width, height) != (128, 128):
        raise ValueError('Menu icon sheet size differs')
    marker_indices = bytes(menu_indices[y * width + x] for y in range(12) for x in range(12))
    marker_pixels = codec.legacy.codec.rgba(marker_indices, codec.legacy.codec.palette(sources['graphics/interface/dex_caught_pokeball.pal']))
    marker_png = codec.legacy.codec.png(12, 12, marker_pixels)
    outputs['owned-marker.png'] = marker_png
    outputs['owned-marker.rgba'] = marker_pixels
    owned_marker = dict(texturePath='content/generated/pokedex-menu/owned-marker.png', sha256=sha(marker_png),
                        rgbaSha256=sha(marker_pixels), width=12, height=12)
    assets.append(owned_marker)
    for name in NEW_FOOTPRINTS:
        sid = species[name.upper()]
        pixels = codec.legacy.codec.rgba(codec.footprint_indices(sources['graphics/pokemon/' + name + '/footprint.png']), palette[:16])
        png = codec.legacy.codec.png(16, 16, pixels)
        filename = name + '-footprint.png'
        outputs[filename] = png
        outputs[name + '-footprint.rgba'] = pixels
        asset = dict(texturePath='content/generated/pokedex-menu/' + filename, sha256=sha(png), rgbaSha256=sha(pixels), width=16, height=16)
        footprints[sid] = asset
        assets.append(asset)
    entries = []
    for number, name in enumerate(national[1:], 1):
        if f'SPECIES_TO_NATIONAL({name})' not in mapping:
            raise ValueError('Species-to-national binding missing: ' + name)
        sid = species[name]
        match = re.search(r'    \[NATIONAL_DEX_' + name + r'\] =\s*\{(.*?)\n    \},', texts['src/data/pokemon/pokedex_entries.h'], re.S)
        if not match:
            raise ValueError('Missing dex entry: ' + name)
        body = match[1]
        category = json.loads(re.search(r'\.categoryName = _\(("[^"]+")\)', body)[1])
        height, weight = (int(re.search(r'\.' + field + r' = (\d+)', body)[1]) for field in ('height', 'weight'))
        desc = re.search(r'\.description = (\w+),', body)[1]
        english = json.loads(re.search(r'\[SPECIES_' + name + r'\]\s*= _\(("[^"]+")\)', texts['src/data/text/species_names.h'])[1])
        types = re.search(r'\[SPECIES_' + name + r'\] =\s*\{.*?\.types = \{(TYPE_\w+), (TYPE_\w+)\}', texts['src/data/pokemon/species_info.h'], re.S)
        habitat = [m for m in re.finditer(r'static const u16 sDexCategory_(\w+)Pkmn_Page\d+\[\] = \{(.*?)\};', texts['src/data/pokemon/pokedex_categories.h'], re.S) if re.search(r'\bSPECIES_' + name + r',', m[2])]
        if len(habitat) != 1 or types is None:
            raise ValueError('Habitat or type identity differs: ' + name)
        inches = 10000 * height // 254
        inches += 10 if inches % 10 >= 5 else 0
        lbs = weight * 100000 // 4536
        lbs += 10 if lbs % 10 >= 5 else 0
        entries.append(dict(sourceSpeciesId=sid, nationalNumber=number, name=english, category=category,
            displayCategory=category.split(' ')[0][:11], heightDecimeters=height, weightHectograms=weight,
            heightDisplay=f'{inches//120}’{(inches%120)//10:02}”', weightDisplay=f'{lbs//100}.{(lbs//10)%10} lbs.',
            description=string(texts['src/data/pokemon/pokedex_text_fr.h'], desc), type1=types[1], type2=types[2],
            habitatTitle=string(texts['src/strings.c'], 'gText_DexCategory_' + habitat[0][1] + 'Pkmn'),
            portrait=portraits.get(sid), footprint=footprints.get(sid),
            source=ref('src/data/pokemon/pokedex_entries.h', 'NATIONAL_DEX_' + name, match[0]),
            descriptionSource=ref('src/data/pokemon/pokedex_text_fr.h', desc,
                re.search(r'const u8 ' + desc + r'\[\] = _\(.*?\);', texts['src/data/pokemon/pokedex_text_fr.h'], re.S)[0])))
    for page in old_pages:
        for sid, legacy in page['entries'].items():
            current = next(e for e in entries if e['sourceSpeciesId'] == int(sid))
            for key in ('name', 'category', 'displayCategory', 'heightDecimeters', 'weightHectograms', 'heightDisplay', 'weightDisplay', 'description', 'habitatTitle'):
                if current[key] != legacy[key]:
                    raise ValueError('Existing capture entry changed: ' + sid + '/' + key)
    symbols = ['gText_PokedexTableOfContents', 'gText_Seen', 'gText_Owned', 'gText_Kanto', 'gText_National',
        'gText_NumericalModeKanto', 'gText_NumericalModeNational', 'gText_PokemonListNoColor', 'gText_PickOKExit',
        'gText_5Dashes', 'gText_PokedexPokemon', 'gText_HT', 'gText_WT', 'gText_Lbs', 'gText_Cry', 'gText_NextDataCancel', 'gText_PokedexNo', 'gText_ClosePokedex']
    refs = [ref('src/pokedex_screen.c', n) for n in ('DexScreen_CountMonsInOrderedList', 'Task_DexScreen_NumericalOrder',
        'DexScreen_TryScrollMonsVertical', 'DexScreen_PrintMonCategory', 'DexScreen_PrintMonHeight', 'DexScreen_PrintMonWeight',
        'DexScreen_PrintMonFlavorText', 'DexScreen_DrawMonFootprint', 'DexScreen_DrawMonDexPage', 'ItemPrintFunc_OrderedListMenu',
        'DexScreen_InitListMenuForOrderedList', 'DexScreen_DestroyDexOrderListMenu', 'DexScreen_LoadMonPicInWindow', 'sListMenuTemplate_OrderedListMenu')]
    refs += [ref('src/start_menu.c', n) for n in ('StartMenuPokedexCallback', 'StartMenuPokedexSanityCheck')]
    refs += [ref('src/event_data.c', 'IsNationalPokedexEnabled'), ref('src/list_menu.c', 'ListMenu_ProcessInput')]
    refs += [ref('src/list_menu.c', 'BlitMenuInfoIcon'), ref('src/list_menu.c', 'ListMenuLoadStdPalAt'),
             ref('src/list_menu.c', 'MENU_INFO_ICON_CAUGHT'), ref('src/graphics.c', 'gMenuInfoElements1_Pal'),
             ref('src/graphics.c', 'gMenuInfoElements_Gfx')]
    outputs['pokedex-menu.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        fullSourceHash=source_hash(lock), inputs=lock['files'], entries=entries,
        dependencies=[dict(path=p, sha256=h, byteLength=len(old[p])) for p,h in sorted(DEPENDENCIES.items())],
        entryLayout=old_pages[0]['layout'], assets=assets, ownedMarker=owned_marker,
        uiTexts={n:string(texts['src/strings.c'], n) for n in symbols},
        listLayout=dict(width=240,height=160,x=16,y=16,windowWidth=184,windowHeight=128,visibleRows=9,
            numberX=12,nameX=56,cursorX=4,caughtX=40,type1X=120,type2X=152,topY=2,rowHeight=14),
        rules=dict(regionalCount=151,nationalCount=386,numericalLength='highest-seen-national-number',
            unseenLabel='-----',seenCanOpen=True,caughtRequiredForDescription=True,caughtRequiredForMeasures=True,
            caughtRequiredForFootprint=True,caughtRequiredForTypes=True,unseenCategory='???????????',
            unseenHeight='??’??”',unseenWeight='????.? lbs.',pageScroll=9,pokedexFlag=0x829,nationalFlag=0x840,
            nationalVariable=0x404e,nationalVariableValue=0x6258,nationalMagic=0xb9,startRequiresSeen=True),
        rulesSources=refs, limitations=[
            '386 metadata records do not add battle species, acquisition, routes, encounters, moves, evolutions or national unlock.',
            'Current 29 species normal portraits reuse existing authenticated art; remaining 357 portraits and footprints are absent.',
            'Numerical Kanto/national lists and caught-gated data projection only; habitat/search/area/size/cry/fades are not implemented here.',
            'Original list coordinates and control tokens are supplied; full original menu composition and native visual acceptance remain separate.',
            'Twenty-two missing current-species footprints and the original 12x12 caught marker are preconverted; seven existing footprints and settled page skin are reused unchanged.']))
    outputs['manifest.json'] = canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,tree=TREE,
        lockSha256=LOCK_SHA256,fullSourceHash=source_hash(lock),inputCount=len(PATHS),entryCount=386,regionalCount=151,
        portraitCount=29,newFootprintCount=22,ownedMarkerCount=1,
        outputs=[dict(path=p,sha256=sha(b),byteLength=len(b)) for p,b in sorted(outputs.items())],
        upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False,runtimeShadersUsed=False))
    return outputs


def run(lock_path, raw, out, repository=None, project=ROOT):
    raw, out, project = safe(raw), safe(out), safe(project)
    protected = [safe(project, p) for p in DEPENDENCIES] + [safe(lock_path)]
    if repository is not None:
        protected.append(safe(repository))
    for a,b in [(raw,out)] + [(p,t) for p in protected for t in (raw,out)]:
        if a.resolve().is_relative_to(b.resolve()) or b.resolve().is_relative_to(a.resolve()):
            raise ValueError('Pokédex input/output/dependency overlap rejected')
    lock = load_lock(lock_path)
    sources = read_sources(lock,raw,repository)
    outputs = convert(lock,sources,dependencies(project))
    if sha(outputs['manifest.json']) != MANIFEST_SHA256:
        raise ValueError('Reviewed Pokédex conversion manifest differs')
    targets = [(safe(out,p),b) for p,b in outputs.items()]
    if repository is not None:
        targets = [(safe(raw,p),b) for p,b in sources.items()] + [(safe(raw,'manifest.json'),raw_manifest(lock))] + targets
    for target,_ in targets:
        if target.exists() and not target.is_file():
            raise ValueError('Pokédex destination is not a regular file')
    for target,data in targets:
        target.parent.mkdir(parents=True,exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=target.parent,prefix='.'+target.name,delete=False) as handle:
            staging=pathlib.Path(handle.name)
            try:
                handle.write(data);handle.flush();os.fsync(handle.fileno());os.replace(staging,target)
            finally:
                staging.unlink(missing_ok=True)
    return outputs


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path)
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokedex-menu')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/pokedex-menu')
    parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/fetch-pokedex-menu.lock.json')
    args=parser.parse_args()
    outputs=run(args.lock,args.raw,args.out,args.reference_repo)
    print(json.dumps(dict(status='verified',inputs=len(PATHS),entries=386,portraits=29,outputs=len(outputs),
        documentSha256=sha(outputs['pokedex-menu.json']),manifestSha256=sha(outputs['manifest.json']))))


if __name__=='__main__':
    main()
