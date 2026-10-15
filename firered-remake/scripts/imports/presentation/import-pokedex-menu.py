#!/usr/bin/env python3
"""Import authenticated FireRed numerical Pokédex metadata; read local Git/cache only.

Original descriptions are content, not permission to reveal unseen/uncaught data.
Existing page skin, seven footprints and 29 normal fronts are referenced unchanged.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT


import argparse
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-pokedex-menu-v1'
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
DEPENDENCIES = (
    'content/generated/sprites/pokemon/capture-dex/capture-dex-background.png',
    'content/generated/sprites/pokemon/capture-dex/capture-dex.json',
    'content/generated/sprites/pokemon/capture-dex/pidgey-footprint.png',
    'content/generated/sprites/pokemon/capture-dex/rattata-footprint.png',
    'content/generated/sprites/pokemon/evolution/battle-art.json',
    'content/generated/sprites/pokemon/evolution/blastoise-front-normal.png',
    'content/generated/sprites/pokemon/evolution/charizard-front-normal.png',
    'content/generated/sprites/pokemon/evolution/charmeleon-front-normal.png',
    'content/generated/sprites/pokemon/evolution/ivysaur-front-normal.png',
    'content/generated/sprites/pokemon/evolution/pidgeot-front-normal.png',
    'content/generated/sprites/pokemon/evolution/pidgeotto-front-normal.png',
    'content/generated/sprites/pokemon/evolution/raticate-front-normal.png',
    'content/generated/sprites/pokemon/evolution/venusaur-front-normal.png',
    'content/generated/sprites/pokemon/evolution/wartortle-front-normal.png',
    'content/generated/sprites/pokemon/forest-capture-dex/capture-dex.json',
    'content/generated/sprites/pokemon/forest-capture-dex/caterpie-footprint.png',
    'content/generated/sprites/pokemon/forest-capture-dex/kakuna-footprint.png',
    'content/generated/sprites/pokemon/forest-capture-dex/metapod-footprint.png',
    'content/generated/sprites/pokemon/forest-capture-dex/pikachu-footprint.png',
    'content/generated/sprites/pokemon/forest-capture-dex/weedle-footprint.png',
    'content/generated/sprites/pokemon/forest/battle-art.json',
    'content/generated/sprites/pokemon/forest/beedrill-front-normal.png',
    'content/generated/sprites/pokemon/forest/butterfree-front-normal.png',
    'content/generated/sprites/pokemon/forest/caterpie-front-normal.png',
    'content/generated/sprites/pokemon/forest/kakuna-front-normal.png',
    'content/generated/sprites/pokemon/forest/metapod-front-normal.png',
    'content/generated/sprites/pokemon/forest/pikachu-front-normal.png',
    'content/generated/sprites/pokemon/forest/raichu-front-normal.png',
    'content/generated/sprites/pokemon/forest/weedle-front-normal.png',
    'content/generated/battle/art/lab-battle-art/bulbasaur-front-normal.png',
    'content/generated/battle/art/lab-battle-art/charmander-front-normal.png',
    'content/generated/battle/art/lab-battle-art/lab-battle-art.json',
    'content/generated/battle/art/lab-battle-art/squirtle-front-normal.png',
    'content/generated/sprites/pokemon/pewter/battle-art.json',
    'content/generated/sprites/pokemon/pewter/geodude-front-normal.png',
    'content/generated/sprites/pokemon/pewter/golem-front-normal.png',
    'content/generated/sprites/pokemon/pewter/graveler-front-normal.png',
    'content/generated/sprites/pokemon/pewter/onix-front-normal.png',
    'content/generated/sprites/pokemon/pewter/sandshrew-front-normal.png',
    'content/generated/sprites/pokemon/pewter/sandslash-front-normal.png',
    'content/generated/sprites/pokemon/pewter/steelix-front-normal.png',
    'content/generated/battle/art/route1-battle-art/pidgey-front-normal.png',
    'content/generated/battle/art/route1-battle-art/rattata-front-normal.png',
    'content/generated/battle/art/route1-battle-art/route1-battle-art.json',
)


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, separators=(',', ':'), sort_keys=True) + '\n').encode('utf-8')


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


def read_sources(raw, repository=None):
    sources = {}
    for relative in sorted(PATHS):
        if repository is None:
            data = safe(raw, relative).read_bytes()
        else:
            data = subprocess.check_output(['git', '-C', str(repository), 'show', COMMIT + ':' + relative], timeout=30)
        sources[relative] = data
    return sources


def dependencies(project):
    data = {}
    for path in DEPENDENCIES:
        value = safe(project, path).read_bytes()
        data[path] = value
    return data


def string(text, symbol):
    match = re.search(r'\b' + re.escape(symbol) + r'\[\]\s*=\s*_\((.*?)\);', text, re.S)
    if not match:
        raise ValueError('Missing string: ' + symbol)
    return ''.join(json.loads(t) for t in re.findall(r'"(?:[^"\\]|\\.)*"', match[1]))


def convert(sources, old):
    if set(sources) != PATHS:
        raise ValueError('Pokédex source set differs')
    texts = {p: b.decode('utf-8') for p, b in sources.items() if p.endswith(('.c', '.h'))}
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
                    portraits[sprite['sourceSpeciesId']] = dict(texturePath=texture,  width=sprite['width'], height=sprite['height'])
    if tuple(sorted(portraits)) != CURRENT:
        raise ValueError('Current portrait species scope differs')
    for folder, page in zip(('capture-dex', 'forest-capture-dex'), old_pages):
        for sid, entry in page['entries'].items():
            asset = next(a for a in page['assets'] if a['texturePath'] == entry['footprintPath'])
            footprints[int(sid)] = dict(texturePath='content/generated/' + folder + '/' + asset['texturePath'],
                 width=16, height=16)
    # Reuse our previously reviewed decoder; no code from the reference is executed.
    spec = importlib.util.spec_from_file_location('pokedex_footprint_codec', ROOT / 'scripts/imports/pokemon/import-forest-capture-dex.py')
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
    owned_marker = dict(texturePath='content/generated/sprites/pokemon/pokedex-menu/owned-marker.png',  width=12, height=12)
    assets.append(owned_marker)
    for name in NEW_FOOTPRINTS:
        sid = species[name.upper()]
        pixels = codec.legacy.codec.rgba(codec.footprint_indices(sources['graphics/pokemon/' + name + '/footprint.png']), palette[:16])
        png = codec.legacy.codec.png(16, 16, pixels)
        filename = name + '-footprint.png'
        outputs[filename] = png
        asset = dict(texturePath='content/generated/sprites/pokemon/pokedex-menu/' + filename,  width=16, height=16)
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
            portrait=portraits.get(sid), footprint=footprints.get(sid)))
    for page in old_pages:
        for sid, legacy in page['entries'].items():
            current = next(e for e in entries if e['sourceSpeciesId'] == int(sid))
            for key in ('name', 'category', 'displayCategory', 'heightDecimeters', 'weightHectograms', 'heightDisplay', 'weightDisplay', 'description', 'habitatTitle'):
                if current[key] != legacy[key]:
                    raise ValueError('Existing capture entry changed: ' + sid + '/' + key)
    symbols = ['gText_PokedexTableOfContents', 'gText_Seen', 'gText_Owned', 'gText_Kanto', 'gText_National',
        'gText_NumericalModeKanto', 'gText_NumericalModeNational', 'gText_PokemonListNoColor', 'gText_PickOKExit',
        'gText_5Dashes', 'gText_PokedexPokemon', 'gText_HT', 'gText_WT', 'gText_Lbs', 'gText_Cry', 'gText_NextDataCancel', 'gText_PokedexNo', 'gText_ClosePokedex']
    outputs['pokedex-menu.json'] = canonical(dict(schemaVersion=1,  entries=entries,
        entryLayout=old_pages[0]['layout'], assets=assets, ownedMarker=owned_marker,
        uiTexts={n:string(texts['src/strings.c'], n) for n in symbols},
        listLayout=dict(width=240,height=160,x=16,y=16,windowWidth=184,windowHeight=128,visibleRows=9,
            numberX=12,nameX=56,cursorX=4,caughtX=40,type1X=120,type2X=152,topY=2,rowHeight=14),
        rules=dict(regionalCount=151,nationalCount=386,numericalLength='highest-seen-national-number',
            unseenLabel='-----',seenCanOpen=True,caughtRequiredForDescription=True,caughtRequiredForMeasures=True,
            caughtRequiredForFootprint=True,caughtRequiredForTypes=True,unseenCategory='???????????',
            unseenHeight='??’??”',unseenWeight='????.? lbs.',pageScroll=9,pokedexFlag=0x829,nationalFlag=0x840,
            nationalVariable=0x404e,nationalVariableValue=0x6258,nationalMagic=0xb9,startRequiresSeen=True)))
    return outputs


def run(raw, out, repository=None, project=ROOT):
    raw, out, project = safe(raw), safe(out), safe(project)
    protected = [safe(project, p) for p in DEPENDENCIES]
    if repository is not None:
        protected.append(safe(repository))
    for a,b in [(raw,out)] + [(p,t) for p in protected for t in (raw,out)]:
        if a.resolve().is_relative_to(b.resolve()) or b.resolve().is_relative_to(a.resolve()):
            raise ValueError('Pokédex input/output/dependency overlap rejected')
    sources = read_sources(raw,repository)
    outputs = convert(sources,dependencies(project))
    targets = [(safe(out,p),b) for p,b in outputs.items()]
    if repository is not None:
        targets = [(safe(raw,p),b) for p,b in sources.items()] + [] + targets
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
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/sprites/pokemon/pokedex-menu')
    args=parser.parse_args()
    outputs=run(args.raw,args.out,args.reference_repo)
    print(json.dumps(dict(status='verified',inputs=len(PATHS),entries=386,portraits=29,outputs=len(outputs))))


if __name__=='__main__':
    main()
