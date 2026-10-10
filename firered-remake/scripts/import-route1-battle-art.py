#!/usr/bin/env python3
"""Import four pinned Route 1 opponent sprites without executing upstream code.

Only Pidgey/Rattata static front frames and normal/shiny palettes are supported.
Input bytes and fixed lock identity are checked before any output is touched.
"""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import re
import sys
import subprocess

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
REPOSITORY = 'pret/pokefirered'
PROFILE = 'firered-route1-battle-static-v1'
LOCK_SHA256 = 'aa8d344d2ac20a46d8c40a91b4f60e6c35c354055dff641d3213b785c2658c74'
EXPECTED_OUTPUT_HASHES = {'manifest.json': 'd1c61b136d6233a9b0d8facacc53ddb0a3b75111a5c76181300c19e27c4b6505',
 'pidgey-front-normal.png': '975b439562bbec89602ce43268ee1f740614324ff9452dd4dfb43130dc1a749b',
 'pidgey-front-shiny.png': '2b6db56500afd289b4c0f3f8318f4521a363df97cac217f8b34400a51c1df763',
 'rattata-front-normal.png': '058fa9a31b07213ac500c05e523bcc321b92710075a62fb575bdc0423384f3d2',
 'rattata-front-shiny.png': '302317e75c0c0aad893ae7d3f9a22c5ad21524f12b1bddda8b8099fe4d08cbf7',
 'route1-battle-art.json': '5fedaf6c584e4e76e1e8b248fba83a30c5d9ff5aa661947a38e0c640ee63691d'}
NAMES = (('Pidgey', 16), ('Rattata', 19))
_spec = importlib.util.spec_from_file_location('local_starter_pixels', ROOT / 'scripts/import-starter-art.py')
pixels = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pixels)
REQUIRED_PATHS = {
    f'graphics/pokemon/{name.lower()}/{filename}'
    for name, _ in NAMES for filename in ('front.png', 'normal.pal', 'shiny.pal')
} | {
    'include/constants/species.h', 'include/constants/battle.h',
    'src/battle_anim_mons.c', 'src/battle_controller_player.c', 'src/battle_controller_opponent.c',
    'src/data/graphics/pokemon.h', 'tools/gbagfx/gfx.c',
    'src/data/pokemon_graphics/front_pic_coordinates.h',
    'src/data/pokemon_graphics/front_pic_table.h',
    'src/data/pokemon_graphics/palette_table.h',
    'src/data/pokemon_graphics/shiny_palette_table.h',
    'src/data/pokemon_graphics/enemy_mon_elevation.h',
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(document):
    return (json.dumps(document, ensure_ascii=False, sort_keys=True, indent=2) + '\n').encode('utf-8')


def safe_path(root, relative):
    root = pathlib.Path(root).resolve()
    path = root / relative
    if not path.resolve().is_relative_to(root):
        raise ValueError('Input/output symlink escapes root: ' + relative)
    return path


def source_set_hash(entries):
    return sha(''.join(e['path'] + '\0' + e['sha256'] + '\n'
                       for e in sorted(entries, key=lambda e: e['path'])).encode())


def load_inputs(lock_path, project_lock_path, raw, source_repo=None):
    lock_bytes = pathlib.Path(lock_path).read_bytes()
    if sha(lock_bytes) != LOCK_SHA256:
        raise ValueError('Route 1 battle-art lock hash mismatch')
    lock = json.loads(lock_bytes)
    project = json.loads(pathlib.Path(project_lock_path).read_bytes())
    if (lock.get('repository'), lock.get('commit')) != (REPOSITORY, COMMIT):
        raise ValueError('Unsupported route1-battle-art lock identity')
    if (project['source']['repository'], project['source']['sourceCommit'],
            project['referenceGame']['gameEdition'], project['referenceGame']['romRevision']) != (
            REPOSITORY, COMMIT, 'FIRERED', 0):
        raise ValueError('Project source/edition/revision differs')
    entries = lock.get('files', [])
    paths = [entry['path'] for entry in entries]
    if len(paths) != len(set(paths)) or set(paths) != REQUIRED_PATHS:
        raise ValueError('Missing or duplicate route1-battle-art input')
    sources = {}
    for entry in entries:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts or str(path) != entry['path'] or '\\' in str(path):
            raise ValueError('Unsafe source path')
        if type(entry['byteLength']) is not int or not 0 < entry['byteLength'] <= 1_000_000:
            raise ValueError('Invalid source byte length')
        if not re.fullmatch('[0-9a-f]{64}', entry['sha256']) or not re.fullmatch('[0-9a-f]{40}', entry['gitBlobSha1']):
            raise ValueError('Invalid source hash')
        if source_repo is None:
            data = safe_path(raw, entry['path']).read_bytes()
        else:
            # Read the fixed commit object, never the possibly modified worktree.
            data = subprocess.run(['git', '-C', str(source_repo), 'show',
                                   COMMIT + ':' + entry['path']], check=True,
                                  capture_output=True, timeout=30).stdout
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        if (len(data), sha(data), blob) != (entry['byteLength'], entry['sha256'], entry['gitBlobSha1']):
            raise ValueError('Source hash mismatch: ' + entry['path'])
        sources[entry['path']] = data
    return lock, sources


def reference(lock, sources, path, symbol, needle=None):
    entry = next(entry for entry in lock['files'] if entry['path'] == path)
    result = dict(kind='pret', repository=REPOSITORY, commit=COMMIT, path=path,
                  symbol=symbol, sha256=entry['sha256'],
                  url=f'https://github.com/{REPOSITORY}/blob/{COMMIT}/{path}')
    if needle is not None:
        text = sources[path].decode('utf-8')
        if text.count(needle) != 1:
            raise ValueError('Missing/ambiguous source binding: ' + symbol)
        result['startLine'] = text.count('\n', 0, text.index(needle)) + 1
        result['endLine'] = result['startLine'] + needle.count('\n')
        result['url'] += f'#L{result["startLine"]}-L{result["endLine"]}'
    return result


def section(text, signature):
    if text.count(signature) != 1:
        raise ValueError('Missing/ambiguous source function: ' + signature)
    start = text.index(signature)
    return text[start:text.index('\n}', start) + 2]


def display_contract(lock, sources):
    path = 'src/battle_anim_mons.c'
    source = sources[path].decode()
    coords = section(source, 'static const struct UCoords8 sBattlerCoords[][MAX_BATTLERS_COUNT] =')
    single = re.search(r'\{ // Single battle\s*((?:\{ \d+, \d+ \},\s*){4})\}', coords)
    if single is None:
        raise ValueError('Unsupported single-battle coordinates')
    points = [tuple(map(int, pair)) for pair in re.findall(r'\{ (\d+), (\d+) \}', single.group(1))]
    constants = sources['include/constants/battle.h'].decode()
    for symbol, index in [('B_POSITION_PLAYER_LEFT', 0), ('B_POSITION_OPPONENT_LEFT', 1)]:
        if re.search(r'^#define ' + symbol + r'\s+' + str(index) + r'$', constants, re.M) is None:
            raise ValueError('Unsupported battler position mapping')
    default = section(source, 'u8 GetBattlerSpriteDefault_Y(u8 battlerId)\n')
    getter = section(source, 'u8 GetBattlerSpriteCoord(u8 battlerId, u8 coordType)\n')
    delta = section(source, 'static u8 GetBattlerYDelta(u8 battlerId, u16 species)\n')
    final = section(source, 'static u8 GetBattlerSpriteFinal_Y(u8 battlerId, u16 species, bool8 a3)\n')
    elevation = section(source, 'static u8 GetBattlerElevation(u8 battlerId, u16 species)\n')
    expected = [(default, 'return GetBattlerSpriteCoord(battlerId, BATTLER_COORD_Y_PIC_OFFSET_DEFAULT);'),
                (getter, 'retVal = GetBattlerSpriteFinal_Y(battlerId, species, FALSE);'),
                (delta, 'ret = gMonBackPicCoords[species].y_offset;'),
                (delta, 'ret = gMonFrontPicCoords[species].y_offset;'),
                (final, 'offset -= GetBattlerElevation(battlerId, species);'),
                (final, 'y = offset + sBattlerCoords[IS_DOUBLE_BATTLE()][GetBattlerPosition(battlerId)].y;'),
                (elevation, 'ret = gEnemyMonElevation[species];')]
    if any(needle not in text for text, needle in expected):
        raise ValueError('Unsupported battle-coordinate semantics')
    refs = [reference(lock, sources, path, symbol, text) for symbol, text in [
        ('sBattlerCoords', coords), ('GetBattlerSpriteDefault_Y', default),
        ('GetBattlerSpriteCoord', getter), ('GetBattlerYDelta', delta),
        ('GetBattlerSpriteFinal_Y', final), ('GetBattlerElevation', elevation)]]
    for controller in ('player', 'opponent'):
        cpath = f'src/battle_controller_{controller}.c'
        text = section(sources[cpath].decode(), 'static void StartSendOutAnim(u8 battlerId, bool8 dontClearSubstituteBit)\n')
        if 'GetBattlerSpriteCoord(battlerId, BATTLER_COORD_X_2)' not in text or 'GetBattlerSpriteDefault_Y(battlerId)' not in text:
            raise ValueError('Unsupported send-out coordinate binding')
        refs.append(reference(lock, sources, cpath, 'StartSendOutAnim', text))
    refs += [reference(lock, sources, 'tools/gbagfx/gfx.c', symbol, needle) for symbol, needle in [
        ('DOWNCONVERT_BIT_DEPTH', '#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)'),
        ('UPCONVERT_BIT_DEPTH', '#define UPCONVERT_BIT_DEPTH(x) (((x) * 255) / 31)')]]
    refs += [reference(lock, sources, 'include/constants/battle.h', symbol,
                       re.search(r'^#define ' + symbol + r'\s+\d+$', constants, re.M).group(0))
             for symbol in ('B_POSITION_PLAYER_LEFT', 'B_POSITION_OPPONENT_LEFT')]
    return dict(logicalWidth=240, logicalHeight=160, frameWidth=64, frameHeight=64,
                profile='ordinary-untransformed-single-battle-default-position',
                playerBaseCenter={'x': points[0][0], 'y': points[0][1]},
                opponentBaseCenter={'x': points[1][0], 'y': points[1][1]},
                playerSide='back', opponentSide='front', textureFilter='nearest', flipH=False,
                frameIndex=0, applySourceYOffset=True, applyAnimationTargetExtra8=False,
                centerFormula='baseCenterY + sourceYOffset - sourceEnemyElevation',
                sourceRefs=refs)


def convert(lock, sources):
    display = display_contract(lock, sources)
    sprites, outputs = [], {}
    graphics_path = 'src/data/graphics/pokemon.h'
    elevation_path = 'src/data/pokemon_graphics/enemy_mon_elevation.h'
    for name, sid in NAMES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        species_row = re.search(r'^#define ' + symbol + r'\s+' + str(sid) + r'\s*$',
                                sources['include/constants/species.h'].decode(), re.M)
        if species_row is None:
            raise ValueError('Unsupported source species ID')
        elevation_source = section(sources[elevation_path].decode(), 'const u8 gEnemyMonElevation[NUM_SPECIES] =')
        elevation_body = elevation_source.split('{', 1)[1].rsplit('}', 1)[0]
        if not re.fullmatch(r'(?:\s*\[SPECIES_[A-Z0-9_]+\] = \d+,\s*)+', elevation_body):
            raise ValueError('Unsupported enemy elevation initializer')
        elevation_row = re.search(r'^\s*\[' + symbol + r'\]\s*=\s*(\d+),', elevation_source, re.M)
        # C static-array members omitted from designated initializers are zero.
        enemy_elevation = int(elevation_row.group(1)) if elevation_row else 0
        for side in ('front',):
            image_path = f'graphics/pokemon/{stem}/{side}.png'
            coords_path = f'src/data/pokemon_graphics/{side}_pic_coordinates.h'
            coordinates = re.search(r'    \[' + symbol + r'\] =\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+),\s*\},',
                                    sources[coords_path].decode())
            if coordinates is None:
                raise ValueError('Missing source image coordinates')
            width, height, offset = map(int, coordinates.groups())
            indices = pixels.indexed_png(sources[image_path])
            opaque = [i for i, index in enumerate(indices) if index]
            if not opaque:
                raise ValueError('Empty battle sprite')
            left, top = min(i % 64 for i in opaque), min(i // 64 for i in opaque)
            right, bottom = max(i % 64 for i in opaque) + 1, max(i // 64 for i in opaque) + 1
            base = display['playerBaseCenter' if side == 'back' else 'opponentBaseCenter']
            elevation = 0 if side == 'back' else enemy_elevation
            center = {'x': base['x'], 'y': base['y'] + offset - elevation}
            image_symbol = f'gMon{side.title()}Pic_{name}'
            shared_refs = [reference(lock, sources, image_path, image_symbol),
                           reference(lock, sources, 'include/constants/species.h', symbol, species_row.group(0).rstrip()),
                           reference(lock, sources, coords_path, f'gMon{side.title()}PicCoords[{symbol}]', coordinates.group(0)),
                           reference(lock, sources, elevation_path, f'gEnemyMonElevation[{symbol}]', elevation_source),
                           reference(lock, sources, graphics_path, image_symbol,
                                     f'const u32 {image_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{side}.4bpp.lz");')]
            shared_refs.append(reference(lock, sources, 'src/data/pokemon_graphics/front_pic_table.h',
                                         'gMonFrontPicTable', f'    SPECIES_SPRITE({name.upper()}, {image_symbol}),'))
            for variant in ('normal', 'shiny'):
                palette_path = f'graphics/pokemon/{stem}/{variant}.pal'
                colors, words = pixels.palette(sources[palette_path])
                rgba = pixels.rgba_pixels(indices, colors)
                filename = f'{stem}-{side}-{variant}.png'
                outputs[filename] = pixels.rgba_png(rgba)
                pal_symbol = 'gMon' + ('Shiny' if variant == 'shiny' else '') + 'Palette_' + name
                refs = [*shared_refs, reference(lock, sources, palette_path, pal_symbol),
                        reference(lock, sources, graphics_path, pal_symbol,
                                  f'const u32 {pal_symbol}[] = INCBIN_U32("graphics/pokemon/{stem}/{variant}.gbapal.lz");')]
                if variant == 'normal':
                    refs.append(reference(lock, sources, 'src/data/pokemon_graphics/palette_table.h',
                                          'gMonPaletteTable', f'    SPECIES_PAL({name.upper()}, {pal_symbol}),'))
                if variant == 'shiny':
                    refs.append(reference(lock, sources, 'src/data/pokemon_graphics/shiny_palette_table.h',
                                          'gMonShinyPaletteTable', f'    SPECIES_SHINY_PAL({name.upper()}, {pal_symbol}),'))
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=sid, side=side,
                                    paletteVariant=variant, texturePath=filename, sha256=sha(outputs[filename]),
                                    sourceImagePath=image_path, sourcePalettePath=palette_path,
                                    rgbaSha256=sha(rgba), indexSha256=sha(indices), width=64, height=64,
                                    frameWidth=64, frameHeight=64, frameCount=1, sourceFrameIndex=0,
                                    sourceWidth=width, sourceHeight=height, sourceYOffset=offset,
                                    sourceEnemyElevation=elevation, singleBattleBaseCenter=base,
                                    singleBattleCenter=center,
                                    singleBattleTopLeft={'x': center['x'] - 32, 'y': center['y'] - 32},
                                    alphaBounds={'x': left, 'y': top, 'width': right-left, 'height': bottom-top},
                                    transparentIndex=0, paletteRgb555=words,
                                    paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False, sourceRefs=refs))
    full_hash = source_set_hash(lock['files'])
    used = [entry for entry in lock['files'] if entry['path'] in REQUIRED_PATHS]
    catalog = dict(schemaVersion=1, profile=PROFILE, repository=REPOSITORY, sourceCommit=COMMIT,
                   fullSourceHash=full_hash, artSourceHash=source_set_hash(used),
                   display=display, sprites=sprites, limitations=[
                       'Pidgey and Rattata only; one static opponent front frame in normal/shiny colors.',
                       'Default untransformed single-battle placement only; no send-out, move, faint or shiny animation.',
                       'No healthbox, battle terrain, menus, trainer art, shadow, sound or battle timing is supplied.',
                       'Local source research only; original game assets are not cleared for redistribution.',
                   ])
    outputs['route1-battle-art.json'] = canonical(catalog)
    manifest = dict(schemaVersion=1, profile=PROFILE, repository=REPOSITORY, commit=COMMIT,
                    fullSourceHash=full_hash, artSourceHash=source_set_hash(used),
                    inputs=lock['files'], usedArtInputPaths=sorted(REQUIRED_PATHS), sprites=sprites,
                    outputs=[dict(path=path, byteLength=len(data), sha256=sha(data))
                             for path, data in sorted(outputs.items())],
                    counts={'species': 2, 'sourceFrames': 2, 'palettes': 4, 'textures': 4},
                    pngEncoding='RGBA8 filter-0 one stored DEFLATE block',
                    upstreamScriptsExecuted=False, romDownloaded=False, publiclyUploaded=False)
    outputs['manifest.json'] = canonical(manifest)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source-repo', type=pathlib.Path,
                        help='Read pinned Git blobs from this local repository instead of --raw')
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/route1-battle-art')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/generated/route1-battle-art')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/route1-battle-art.lock.json')
    parser.add_argument('--project-lock', type=pathlib.Path, default=ROOT / 'content/source-lock.json')
    args = parser.parse_args()
    if args.raw.resolve().is_relative_to(args.output.resolve()) or args.output.resolve().is_relative_to(args.raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    lock, sources = load_inputs(args.lock, args.project_lock, args.raw, args.source_repo)
    outputs = convert(lock, sources)
    if {name: sha(data) for name, data in outputs.items()} != EXPECTED_OUTPUT_HASHES:
        raise ValueError('Route 1 battle-art output hash mismatch')
    targets = [(safe_path(args.output, name), safe_path(args.output, name + '.tmp'), data)
               for name, data in outputs.items()]
    args.output.mkdir(parents=True, exist_ok=True)
    for target, staged, data in targets:
        staged.write_bytes(data)
        staged.replace(target)
    print(json.dumps(dict(status='imported', textures=4, catalog=str(args.output / 'route1-battle-art.json'),
                          catalogSha256=sha(outputs['route1-battle-art.json']),
                          manifestSha256=sha(outputs['manifest.json']), fullSourceHash=source_set_hash(lock['files']))))


if __name__ == '__main__':
    main()
