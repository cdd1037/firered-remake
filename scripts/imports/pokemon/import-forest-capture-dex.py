#!/usr/bin/env python3
"""Authenticate and extend the settled first-capture dex with five forest species.

Reads fixed local Git objects or the verified cache. Never executes upstream code.
Existing page art, layout, catalog and family sprites are reused without rewriting.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes

import argparse
import io
import importlib.util
import json
import os
import pathlib
import re
import subprocess
import sys
import tempfile

sys.dont_write_bytecode = True
from PIL import Image

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-forest-capture-dex-v1'
NAMES = (('Caterpie', 10), ('Metapod', 11), ('Weedle', 13), ('Kakuna', 14), ('Pikachu', 25))
PATHS = frozenset('''src/pokedex_screen.c
src/data/pokemon/pokedex_entries.h
src/data/pokemon/pokedex_text.h
src/data/pokemon/pokedex_text_fr.h
src/data/pokemon/pokedex_categories.h
src/data/text/species_names.h
src/data/pokemon_graphics/footprint_table.h
src/data/graphics/pokemon.h
include/constants/species.h
include/constants/pokedex.h
include/text.h
src/strings.c
src/text.c
src/new_menu_helpers.c
tools/gbagfx/gfx.c
tools/gbagfx/main.c
tools/gbagfx/convert_png.c
graphics/pokedex/kanto_dex_bgpals.pal'''.splitlines()) | frozenset(
    f'graphics/pokemon/{name.lower()}/footprint.png' for name, _ in NAMES)
_spec = importlib.util.spec_from_file_location('forest_capture_dex_shared', ROOT / 'scripts/imports/pokemon/import-capture-dex.py')
legacy = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(legacy)


canonical = legacy.canonical
unique_pairs = legacy.unique_pairs


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is None:
        target = root
    else:
        path = pathlib.PurePosixPath(relative)
        if path.is_absolute() or any(p in ('', '.', '..') for p in relative.split('/')) or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe forest capture dex path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Forest capture dex symlink rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Forest capture dex path escapes root')
    return target


def load_lock(path):
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if tuple(lock.get(k) for k in ('schemaVersion', 'repository', 'commit', 'profile', 'gameEdition', 'romRevision')) != (
            1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Forest capture dex source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Forest capture dex source dependency set differs')
    return lock


def read_sources(lock, raw, repository=None):
    safe(raw)
    if repository is not None:
        safe(repository)
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'], check=True, capture_output=True, timeout=30).stdout.decode().strip()
    sources = {}
    for entry in lock['files']:
        path = safe(raw, entry['path'])
        if repository is None:
            data = path.read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                                  check=True, capture_output=True, timeout=30).stdout
        sources[entry['path']] = data
    return sources


def dependencies(project):
    data = safe(project, 'content/generated/sprites/pokemon/capture-dex/capture-dex.json').read_bytes()
    old = json.loads(data, object_pairs_hook=unique_pairs)
    forest = safe(project, 'content/generated/sprites/pokemon/forest')
    manifest_data = safe(forest, 'manifest.json').read_bytes()
    manifest = json.loads(manifest_data, object_pairs_hook=unique_pairs)
    artifacts = {}
    for entry in manifest['outputs']:
        data = safe(forest, entry['path']).read_bytes()
        artifacts[entry['path']] = data
    art = json.loads(artifacts['battle-art.json'], object_pairs_hook=unique_pairs)
    if len(art['sprites']) != 32:
        raise ValueError('Forest Pokemon sprite set differs')
    for name, sid in NAMES:
        sprites = [s for s in art['sprites'] if s['sourceSpeciesId'] == sid and s['side'] == 'front' and s['paletteVariant'] == 'normal']
        if len(sprites) != 1 or sprites[0]['texturePath'] != name.lower() + '-front-normal.png':
            raise ValueError('Forest dex front sprite missing')
    return old


def footprint_indices(data):
    """Read 1-bit grayscale pixels and apply the source gbagfx inversion."""
    with Image.open(io.BytesIO(data)) as image:
        if image.format != 'PNG' or image.mode != '1' or image.size != (16, 16):
            raise ValueError('Footprint PNG profile differs')
        # Pillow exposes 1-bit pixels as 0/255; source black becomes palette index 1.
        result = bytes(1 if pixel == 0 else 0 for pixel in image.convert('L').tobytes())
    return result


def convert(lock, sources, old):
    if set(sources) != PATHS:
        raise ValueError('Forest capture dex source set differs')
    screen = sources['src/pokedex_screen.c'].decode()
    for needle in ('DexScreen_DrawMonDexPage(TRUE);', 'DexScreen_DexPageZoomEffectFrame(3, 6);',
                   'while ((categoryName[index] != CHAR_SPACE) && (index < 11))',
                   'LoadMonPicInWindow(species, SHINY_ODDS, DexScreen_GetDefaultPersonality(species), TRUE, paletteOffset >> 4, windowId);',
                   'inches = 10000 * height / 254;', 'lbs = (weight * 100000) / 4536;',
                   'printerTemplate.shadowColor = 2;', '236 - GetStringWidth(FONT_SMALL, src, 0)'):
        legacy.codec.require(screen, needle)
    legacy.codec.require(sources['src/data/pokemon/pokedex_text.h'].decode(), '#if defined(FIRERED)\n#include "pokedex_text_fr.h"')
    legacy.codec.require(sources['tools/gbagfx/main.c'].decode(), 'WriteTileImage(outputPath, options->numTilesMode, options->numTiles, options->metatileWidth, options->metatileHeight, &image, !image.hasPalette);')
    palette = legacy.kanto_palette(sources['graphics/pokedex/kanto_dex_bgpals.pal'])
    dex = sources['src/data/pokemon/pokedex_entries.h'].decode()
    names = sources['src/data/text/species_names.h'].decode()
    descriptions = sources['src/data/pokemon/pokedex_text_fr.h'].decode()
    habitats = sources['src/data/pokemon/pokedex_categories.h'].decode()
    constants = sources['include/constants/pokedex.h'].decode().split('// National Pokedex order', 1)[1].split('};', 1)[0]
    national = re.findall(r'^\s*(NATIONAL_DEX_\w+),', constants, re.M)
    texts = sources['src/strings.c'].decode()
    outputs, entries, assets = {}, {}, []
    for name, sid in NAMES:
        stem, symbol, dex_symbol = name.lower(), 'SPECIES_' + name.upper(), 'NATIONAL_DEX_' + name.upper()
        if not re.search(r'^#define ' + symbol + r'\s+' + str(sid) + r'$', sources['include/constants/species.h'].decode(), re.M):
            raise ValueError('Forest dex species ID differs')
        match = re.search(r'    \[' + dex_symbol + r'\] =\s*\{(.*?)\n    \},', dex, re.S)
        if match is None:
            raise ValueError('Forest dex entry absent')
        body = match[1]
        category = json.loads(re.search(r'\.categoryName = _\(("[^"]+")\)', body)[1])
        height = int(re.search(r'\.height = (\d+)', body)[1])
        weight = int(re.search(r'\.weight = (\d+)', body)[1])
        desc_symbol = re.search(r'\.description = (\w+),', body)[1]
        english_name = json.loads(re.search(r'\[' + symbol + r'\]\s*= _\(("[^"]+")\)', names)[1])
        habitat = [m for m in re.finditer(r'static const u16 sDexCategory_(\w+)Pkmn_Page\d+\[\] = \{(.*?)\};', habitats, re.S)
                   if re.search(r'\b' + symbol + r',', m[2])]
        if len(habitat) != 1:
            raise ValueError('Forest dex habitat category differs')
        habitat_title = legacy.string(texts, 'gText_DexCategory_' + habitat[0][1] + 'Pkmn')
        inches = 10000 * height // 254
        inches += 10 if inches % 10 >= 5 else 0
        feet, remainder = inches // 120, (inches % 120) // 10
        lbs = weight * 100000 // 4536
        lbs += 10 if lbs % 10 >= 5 else 0
        footprint_path = f'graphics/pokemon/{stem}/footprint.png'
        legacy.codec.require(sources['src/data/pokemon_graphics/footprint_table.h'].decode(), f'[{symbol}] = gMonFootprint_{name},')
        legacy.codec.require(sources['src/data/graphics/pokemon.h'].decode(), f'const u8 gMonFootprint_{name}[] = INCBIN_U8("graphics/pokemon/{stem}/footprint.1bpp");')
        pixels = legacy.codec.rgba(footprint_indices(sources[footprint_path]), palette[:16])
        filename = stem + '-footprint.png'
        outputs[filename] = legacy.codec.png(16, 16, pixels)
        assets.append(dict(id=stem + '-footprint', texturePath=filename, width=16, height=16,
            x=old['layout']['footprintX'], y=old['layout']['footprintY']))
        entries[str(sid)] = dict(sourceSpeciesId=sid, nationalNumber=national.index(dex_symbol), name=english_name,
            category=category, displayCategory=category.split(' ')[0][:11], heightDecimeters=height, weightHectograms=weight,
            heightDisplay=f'{feet}’{remainder:02}”', weightDisplay=f'{lbs//100}.{(lbs//10)%10} {legacy.string(texts, "gText_Lbs")}',
            description=legacy.string(descriptions, desc_symbol), footprintPath=filename,
            frontSpritePath='forest-pokemon/' + stem + '-front-normal.png', habitatTitle=habitat_title)
    outputs['capture-dex.json'] = runtime_json_bytes(dict(schemaVersion=1, entries=entries, assets=assets, layout=old['layout']), sort_keys=True)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, inputCount=len(PATHS), outputs=[dict(path=p) for p, b in sorted(outputs.items())]))
    return outputs


def run(lock_path, raw, out, repository=None, project=ROOT):
    raw, out, project = safe(raw), safe(out), safe(project)
    protected = [project / 'content/generated/sprites/pokemon/capture-dex', project / 'content/generated/sprites/pokemon/forest']
    if repository is not None:
        protected.append(safe(repository))
    pairs = [(raw, out)] + [(p, target) for p in protected for target in (raw, out)]
    if any(a.resolve().is_relative_to(b.resolve()) or b.resolve().is_relative_to(a.resolve()) for a, b in pairs):
        raise ValueError('Forest capture dex source/output/dependency overlap rejected')
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources, dependencies(project))
    targets = [(safe(out, p), b) for p, b in outputs.items()]
    if repository is not None:
        targets = [(safe(raw, p), b) for p, b in sources.items()] + [] + targets
    # Validate every destination before any write. Stage bytes beside each final path.
    for target, _ in targets:
        if target.exists() and not target.is_file():
            raise ValueError('Forest capture dex output is not a regular file')
    for target, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        with tempfile.NamedTemporaryFile(dir=target.parent, prefix='.' + target.name, delete=False) as handle:
            staging = pathlib.Path(handle.name)
            try:
                handle.write(data)
                handle.flush()
                os.fsync(handle.fileno())
                os.replace(staging, target)
            finally:
                staging.unlink(missing_ok=True)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', '--repository', type=pathlib.Path,
                        default=pathlib.Path(os.environ['FIRERED_ORIGINAL_REFERENCE']) if os.environ.get('FIRERED_ORIGINAL_REFERENCE') else None)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/sprites/pokemon/forest-capture-dex')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/pokemon/forest-capture-dex.lock.json')
    args = parser.parse_args()
    outputs = run(args.lock, args.raw, args.out, args.reference_repo)
    print(json.dumps(dict(status='verified', sourceFiles=len(PATHS), outputs=len(outputs))))


if __name__ == '__main__':
    main()
