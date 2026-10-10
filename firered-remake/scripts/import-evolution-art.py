#!/usr/bin/env python3
"""Authenticated evolved-family offline normal/shiny battle pixels.

Only immutable local Git objects or verified raw cache bytes are read. No upstream
code, ROM, network, runtime shaders, battle effects or evolution executor is used.
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

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-evolution-art-v1'
LOCK_SHA256 = 'febf00be6af49d0a864f08e9ae514ca8765cbac1de660fdcc8af9bcd295081fa'
MANIFEST_SHA256 = '3fe8060c08125912460b7c99c950265be92705cfc5eca91c80ed7b11c6aa0f5c'
ART_SPECIES = (('Ivysaur', 2), ('Venusaur', 3), ('Charmeleon', 5), ('Charizard', 6),
               ('Wartortle', 8), ('Blastoise', 9), ('Pidgeotto', 17), ('Pidgeot', 18), ('Raticate', 20))
PATHS = frozenset('''include/constants/species.h
include/constants/battle.h
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
    for filename in ('front.png', 'back.png', 'normal.pal', 'shiny.pal'))
_spec = importlib.util.spec_from_file_location('evolution_battle_pixels', ROOT / 'scripts/import-route1-battle-art.py')
battle = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(battle)


def sha(data):
    return hashlib.sha256(data).hexdigest()


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
        if not relative or relative == '.' or path.is_absolute() or '..' in path.parts or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe evolution art path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output or ancestor rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Evolution art path escapes root')
    return target


def load_lock(path=None):
    path = path or ROOT / 'scripts/evolution-art.lock.json'
    data = safe(path).read_bytes()
    if sha(data) != LOCK_SHA256:
        raise ValueError('Reviewed evolution art source lock differs')
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('tree'),
            lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (
            1, 'pret/pokefirered', COMMIT, TREE, PROFILE, 'FIRERED', 0):
        raise ValueError('Evolution art source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Evolution art source dependency set differs')
    return lock


def verify(data, entry):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest()
    if (len(data), sha(data), blob) != (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']):
        raise ValueError('Evolution art source hash differs: ' + entry['path'])


def raw_manifest(lock):
    return canonical(dict(schemaVersion=1, profile=PROFILE, commit=COMMIT, tree=TREE,
                          lockSha256=LOCK_SHA256, fullSourceHash=battle.source_set_hash(lock['files']),
                          inputCount=len(lock['files'])))


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is None:
        if safe(raw, 'manifest.json').read_bytes() != raw_manifest(lock):
            raise ValueError('Evolution art raw manifest differs')
    else:
        # ls-tree is a read-only object identity check; source contents use git show only.
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
                              check=True, capture_output=True, timeout=30).stdout.decode('ascii').strip()
        if tree != TREE:
            raise ValueError('Evolution art source tree differs')
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            row = subprocess.run(['git', '-C', str(repository), 'ls-tree', COMMIT, '--', entry['path']],
                                 check=True, capture_output=True, timeout=30).stdout.decode('utf-8')
            if row != '100644 blob ' + entry['gitBlobSha1'] + '\t' + entry['path'] + '\n':
                raise ValueError('Evolution art input is not the pinned regular Git blob: ' + entry['path'])
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                                  check=True, capture_output=True, timeout=30).stdout
        verify(data, entry)
        sources[entry['path']] = data
    return sources


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
        species_row = re.search(r'^#define ' + symbol + r'[ \t]+' + str(sid) + r'[ \t]*$',
                                sources['include/constants/species.h'].decode('utf-8'), re.M)
        if species_row is None:
            raise ValueError('Unsupported source species ID')
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
            shared = [reference(image_path, image_symbol),
                      reference('include/constants/species.h', symbol, species_row[0]),
                      reference(coords_path, f'gMon{side.title()}PicCoords[{symbol}]', coords[0]),
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
                outputs[filename + '.rgba'] = rgba
                palette_symbol = 'gMon' + ('Shiny' if variant == 'shiny' else '') + 'Palette_' + name
                table = 'shiny_palette_table.h' if variant == 'shiny' else 'palette_table.h'
                macro = 'SPECIES_SHINY_PAL' if variant == 'shiny' else 'SPECIES_PAL'
                refs = shared + [reference(palette_path, palette_symbol),
                    reference('src/data/graphics/pokemon.h', palette_symbol,
                        f'const u32 {palette_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{variant}.gbapal.lz");'),
                    reference('src/data/pokemon_graphics/' + table, macro,
                        f'    {macro}({name.upper()}, {palette_symbol}),')]
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
            'Nine evolved starter/Route 1 species, one static front/back frame each, normal and shiny offline RGBA/PNG.',
            'Default single-battle placement only; no animation, shadows, battle UI, cry audio or original-frame equivalence claim.']))
    return outputs


def convert(lock, sources):
    outputs = art(lock, sources)
    outputs['manifest.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
        tree=TREE, lockSha256=LOCK_SHA256, fullSourceHash=battle.source_set_hash(lock['files']),
        inputCount=len(lock['files']),
        artSpeciesCount=9, textureCount=36, rgbaCount=36, upstreamScriptsExecuted=False,
        romDownloaded=False, publiclyUploaded=False, runtimeShadersUsed=False,
        outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(outputs.items())]))
    return outputs


def import_content(raw, out, repository=None, lock_path=None):
    raw, out = safe(raw), safe(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if repository is not None:
        repo = pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw, out)):
            raise ValueError('Materialization must not overlap the reference repository')
    lock = load_lock(lock_path)
    sources = read_sources(lock, raw, repository)
    outputs = convert(lock, sources)
    if sha(outputs['manifest.json']) != MANIFEST_SHA256:
        raise ValueError('Reviewed evolution art generated output hashes differ')
    # Parse/render/authenticate everything, then preflight every target before the first write.
    material = {(raw, p): b for p, b in sources.items()} if repository is not None else {}
    if repository is not None:
        material[(raw, 'manifest.json')] = raw_manifest(lock)
    material.update({(out, p): b for p, b in outputs.items()})
    targets = [(safe(root, p), safe(root, p + '.tmp'), b) for (root, p), b in material.items()]
    if any(p.exists() and not p.is_file() for target, stage, _ in targets for p in (target, stage)):
        raise ValueError('Input/output target is not a regular file')
    if any(parent.exists() and not parent.is_dir() for target, _, _ in targets for parent in target.parents):
        raise ValueError('Input/output ancestor is not a directory')
    for target, stage, data in targets:
        target.parent.mkdir(parents=True, exist_ok=True)
        stage.write_bytes(data)
        stage.replace(target)
    return dict(profile=PROFILE, inputCount=len(sources), outputCount=len(outputs),
                battleArtSha256=sha(outputs['battle-art.json']), manifestSha256=sha(outputs['manifest.json']),
                fullSourceHash=battle.source_set_hash(lock['files']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo', type=pathlib.Path, default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/evolution-art')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/evolution-art')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/evolution-art.lock.json')
    args = parser.parse_args()
    print(json.dumps(import_content(args.raw, args.out, args.reference_repo, args.lock), sort_keys=True))


if __name__ == '__main__':
    main()
