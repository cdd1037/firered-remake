#!/usr/bin/env python3
"""Rebuild the bounded Celio pixels from pinned Git blobs, without source execution."""
import argparse
import hashlib
import io
import json
import pathlib
import re
import struct
import subprocess
import zlib
import wave
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-celio-presentation-v1'
BASE = 'content/generated/celio-presentation/'
RAW = 'content/raw/celio-presentation/'
LOCK_SHA = 'a8830aadada3dd30be755be3ac982ec53911bcd4396737fdb268204ef7dade2d'
MANIFEST_SHA = 'd7ae9d6c4fe63c768da9207d5cb13606722b3a22f68ff3293f83b086ebe8d52d'
OWNED = [('Raikou', 243), ('Entei', 244), ('Suicune', 245)]
SPECIES = OWNED
REUSED_ASSETS = {}

def need(ok, message):
    if not ok:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + '\n').encode()


def safe(root, path):
    need(path and not pathlib.PurePosixPath(path).is_absolute() and '\\' not in path and
         ':' not in path and all(p not in ('', '.', '..') for p in path.split('/')), 'Unsafe presentation path')
    result = pathlib.Path(root).absolute() / path
    need(not any(p.is_symlink() for p in (result, *result.parents)), 'Presentation symlink rejected')
    return result


def png(width, height, rgba):
    # Stored DEFLATE blocks keep output independent of zlib compression versions.
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data) & 0xffffffff)
    scan = b''.join(b'\0' + rgba[y * width * 4:(y + 1) * width * 4] for y in range(height))
    blocks = [scan[i:i + 65535] for i in range(0, len(scan), 65535)]
    compressed = b'\x78\x01' + b''.join(bytes([int(i == len(blocks) - 1)]) +
        struct.pack('<HH', len(block), len(block) ^ 65535) + block for i, block in enumerate(blocks))
    compressed += struct.pack('>I', zlib.adler32(scan) & 0xffffffff)
    return b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)) + chunk(b'IDAT', compressed) + chunk(b'IEND', b'')


class Source:
    def __init__(self, root, reference, dependency):
        self.root, self.reference, self.dependency = root, reference, dependency
        self.files, self.raw, self.inherited = {}, {}, {}
        self.lock = None
        if reference:
            need(subprocess.check_output(['git', '-C', str(reference), 'rev-parse', COMMIT + '^{tree}']).decode().strip() == TREE, 'Pinned source tree differs')
        if LOCK_SHA:
            data = safe(root, 'scripts/celio-presentation.lock.json').read_bytes()
            need(sha(data) == LOCK_SHA, 'Pinned presentation lock differs')
            self.lock = json.loads(data)
            need((self.lock['schemaVersion'], self.lock['profile'], self.lock['repository'], self.lock['commit'], self.lock['tree']) ==
                 (1, PROFILE, 'pret/pokefirered', COMMIT, TREE), 'Source lock identity differs')

    def read(self, path):
        if path not in self.raw:
            data = subprocess.check_output(['git', '-C', str(self.reference), 'show', COMMIT + ':' + path]) if self.reference else safe(self.root, RAW + path).read_bytes()
            row = dict(path=path, byteLength=len(data), sha256=sha(data),
                       gitBlobSha1=hashlib.sha1(f'blob {len(data)}\0'.encode() + data).hexdigest())
            if self.lock:
                need(row in self.lock['files'], 'Source input differs: ' + path)
            self.raw[path], self.files[path] = data, row
        return self.raw[path]

    def text(self, path):
        return self.read(path).decode()

    def reuse(self, path):
        data = safe(self.dependency, path).read_bytes()
        row = dict(path=path, byteLength=len(data), sha256=sha(data))
        if self.lock:
            need(row in self.lock['inheritedInputs'], 'Inherited pixels differ: ' + path)
        self.inherited[path] = row
        return data


def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets = {}, []

    def emit(identity, width, height, rgba, inputs):
        need(len(rgba) == width * height * 4, 'RGBA dimensions differ')
        if identity in REUSED_ASSETS:
            path = REUSED_ASSETS[identity]
            need(source.reuse(path) == rgba, 'Inherited asset source pixels differ: ' + identity)
            row = dict(id=identity, width=width, height=height, rgbaPath=path,
                       rgbaSha256=sha(rgba), sourcePaths=inputs, inherited=True)
            assets.append(row)
            return row
        out[identity + '.rgba'], out[identity + '.png'] = rgba, png(width, height, rgba)
        row = dict(id=identity, width=width, height=height, rgbaPath=BASE + identity + '.rgba',
                   texturePath=BASE + identity + '.png', rgbaSha256=sha(rgba), sha256=sha(out[identity + '.png']), sourcePaths=inputs)
        assets.append(row)
        return row

    def palette(path, floor=False):
        lines = source.text(path).splitlines()
        need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Source palette framing differs')
        return [tuple((int(c) >> 3) * 255 // 31 if floor else ((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2) for c in line.split()) for line in lines[3:]]

    def indexed(image, pal, shape, floor=False, first_frame=False):
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return indices, bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    sprites, icons, footprints = [], [], []
    graphics = source.text('src/data/graphics/pokemon.h')
    constants = source.text('include/constants/species.h')
    for name, species in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        need(re.search(r'^#define ' + symbol + r'\s+' + str(species) + r'\s*$', constants, re.M), 'Species ID differs')
        elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source.text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
        elevation = int(elevation_match[1]) if elevation_match else 0
        for side in ('front', 'back'):
            coordinates = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text(coordinates))
            need(match, 'Species coordinates missing')
            width, height, y_offset = map(int, match.groups())
            image = f'graphics/pokemon/{stem}/{side}.png'
            need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Species image binding differs')
            for variant in ('normal', 'shiny'):
                pal = f'graphics/pokemon/{stem}/{variant}.pal'
                palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Species palette binding differs')
                indices, rgba = indexed(image, pal, (64, 64), floor=True)
                x, y = (144, 8 + y_offset - elevation) if side == 'front' else (40, 48 + y_offset)
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, side=side, paletteVariant=variant,
                    x=x, y=y, sourceWidth=width, sourceHeight=height, sourceYOffset=y_offset, sourceEnemyElevation=elevation,
                    indexSha256=sha(indices), sourceCoordinatePath=coordinates, paletteExpansion='floor(rgb5 * 255 / 31)',
                    transparentIndex=0, **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
        icon_table = source.text('src/pokemon_icon.c')
        match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
        need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
        pal_index = int(match[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need(f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Species icon image binding differs')
        indices, rgba = indexed(image, pal, (32, 64), floor=True, first_frame=True)
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, paletteIndex=pal_index, sourceFrameIndex=0,
                          sourceFrameCount=2, indexSha256=sha(indices), **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
        if (name, species) not in OWNED:
            continue
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source pixels differ')
        indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
        colors = palette(pal, floor=True)
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))

    tiles, layers = [], []
    labels = {k: int(v, 16) for k, v in re.findall(r'^#define\s+(METATILE_\w+)\s+(0x[\da-fA-F]+)', source.text('include/constants/metatile_labels.h'), re.M)}
    layouts = json.loads(source.read('data/layouts/layouts.json'))['layouts']
    tile_graphics = source.text('src/data/tilesets/graphics.h') + '\n' + source.text('src/graphics.c')
    tile_headers = source.text('src/data/tilesets/headers.h')
    tile_bindings = source.text('src/data/tilesets/metatiles.h')
    tile_rules = source.text('tileset_rules.mk')
    patch_specs = [
        ('METATILE_PokemonCenter_NetworkMachine_Sapphire', 'OneIsland_PokemonCenter_1F', 'map.one-island-pokemon-center-1f', [(16, 4)], 1),
        ('METATILE_PokemonCenter_NetworkMachine_ScreenLeft_On', 'OneIsland_PokemonCenter_1F', 'map.one-island-pokemon-center-1f', [(14, 4)], 1),
        ('METATILE_PokemonCenter_NetworkMachine_ScreenRight_On', 'OneIsland_PokemonCenter_1F', 'map.one-island-pokemon-center-1f', [(15, 4)], 1)]
    for constant, map_name, map_id, positions, collision in patch_specs:
        source_map = json.loads(source.read('data/maps/' + map_name + '/map.json'))
        layout = next(row for row in layouts if row.get('id') == source_map['layout'])
        script = source.text('data/maps/' + map_name + '/scripts.inc')
        for x, y in positions:
            need(f'setmetatile {x}, {y}, {constant}, {collision}' in script, 'Map patch source binding differs')
        bases, metatile_paths, attribute_paths = [], [], []
        for secondary, key in enumerate(('primary_tileset', 'secondary_tileset')):
            symbol = layout[key]
            header = re.search(r'const struct Tileset ' + symbol + r'\s*=\s*\{(.*?)\};', tile_headers, re.S)[1]
            need('.isSecondary = ' + ('TRUE' if secondary else 'FALSE') in header, 'Map tileset side differs')
            image_symbol = re.search(r'\.tiles = (\w+)', header)[1]
            image_match = re.search(image_symbol + r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)', tile_graphics)
            need(image_match, 'Map tileset graphics binding missing')
            base = image_match[1].rsplit('/', 1)[0]
            for field, paths in [('metatiles', metatile_paths), ('metatileAttributes', attribute_paths)]:
                binding = re.search(r'\.' + field + r' = (\w+)', header)[1]
                bound = re.search(binding + r'\[\] = INCBIN_U(?:16|32)\("([^\"]+)"\)', tile_bindings)
                need(bound, 'Map metatile binding differs')
                paths.append(bound[1])
            bases.append(base)
        images = [Image.open(io.BytesIO(source.read(base + '/tiles.png'))) for base in bases]
        palettes = [palette(bases[0 if n < 7 else 1] + f'/palettes/{n:02d}.pal') for n in range(13)]
        palettes[0][0] = (0, 0, 0)
        emitted = []
        for base, im in zip(bases, images):
            need(im.mode == 'P' and im.width % 8 == 0 and im.height % 8 == 0 and max(im.tobytes()) < 16, 'Map tile pixels differ')
            match = re.search(re.escape('$(TILESETGFXDIR)/' + base.removeprefix('data/tilesets/') + '/tiles.4bpp:') + r'.*?\n\t[^\n]*?-num_tiles (\d+)', tile_rules)
            emitted.append(int(match[1]) if match else im.width * im.height // 64)
        mid = labels[constant]
        bi, local = (0, mid) if mid < 640 else (1, mid - 640)
        attribute = struct.unpack_from('<I', source.read(attribute_paths[bi]), local * 4)[0]
        values = struct.unpack_from('<8H', source.read(metatile_paths[bi]), local * 16)
        layer_type = (attribute >> 29) & 3
        groups = {0: [[0x3014] * 4, values[:4], values[4:]], 1: [values[:4], values[4:], [0] * 4], 2: [values[:4], [0] * 4, values[4:]]}.get(layer_type)
        need(groups is not None, 'Reserved map layer type')
        tiles.append(dict(map=map_id, sourceMap=map_name, constant=constant, metatileId=mid,
                          layerType=layer_type, rawAttributes=attribute, tileEntries=values, positions=[dict(x=x,y=y) for x,y in positions], collision=collision))
        for layer, group in enumerate(groups):
            rgba = bytearray(16 * 16 * 4)
            for n, value in enumerate(group):
                tid, pal = value & 1023, value >> 12
                tile_bi, tile_local = (0, tid) if tid < 640 else (1, tid - 640)
                im = images[tile_bi]
                need(pal < 13 and tile_local < emitted[tile_bi], 'Uninitialized map tile/palette')
                for y in range(8):
                    for x in range(8):
                        ix, iy = (7 - x if value & 0x400 else x), (7 - y if value & 0x800 else y)
                        color = im.getpixel((tile_local % (im.width // 8) * 8 + ix, tile_local // (im.width // 8) * 8 + iy))
                        pos = ((n // 2 * 8 + y) * 16 + n % 2 * 8 + x) * 4
                        rgba[pos:pos + 4] = bytes((*palettes[pal][color], 255 if color else 0))
            layers.append(dict(map=map_id, metatileId=mid, layer=layer, **emit('tiles/' + str(mid) + '-' + str(layer), 16, 16, bytes(rgba), bases + metatile_paths + attribute_paths)))

    # Declare original sequences and cry samples already present in the source pack.
    pack = source.reuse('content/generated/synth-audio/firered.pack')
    cries = source.reuse('content/generated/synth-audio/normal-cries.bin')
    need(sha(pack) == '00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d' and len(pack) == 0x086fbea4 - 0x08489968 and
         sha(cries) == '623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e' and len(cries) == 6188, 'Original audio pack identity differs')
    songs = source.text('include/constants/songs.h')
    song_table = re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)', source.text('sound/song_table.inc'), re.M)
    cues = []
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    for name, species in [(name.upper(), number) for name, number in SPECIES]:
        row = cry_rows[species]
        need(cry_table[species - 1].upper() == name and row[1] == species - 1, 'Species cry table differs')
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave_offset = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
        need(struct.unpack_from('<HHIII', pack, wave_offset) == (1, 0, 10764288, 0, row[2]), 'Species packed cry header differs')
        path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
        wave_source = source.read(path)
        need(wave_source[:4] == b'RIFF' and wave_source[8:12] == b'WAVE', 'Species source cry framing differs')
        with wave.open(io.BytesIO(wave_source)) as original:
            need((original.getnchannels(), original.getsampwidth(), original.getframerate(), original.getnframes(), original.getcomptype()) ==
                 (1, 1, 10512, row[2] + 1, 'NONE'), 'Source cry sample/header dimensions differ')
        cues.append(dict(id='CRY_' + name + '_NORMAL', sourceCommit=COMMIT, sourceSymbol='SPECIES_' + name,
                         sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))
    inherited_cues = []
    inherited_specs = ('MUS_OBTAIN_TMHM', 'SE_CLICK', 'SE_PC_ON', 'MUS_NET_CENTER')
    source_script = 'data/maps/OneIsland_PokemonCenter_1F/scripts.inc'
    handoff = source.text(source_script).split('OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire::', 1)[1].split('\n\n', 1)[0]
    handoff_cues = re.findall(r'^\s*(playfanfare|playse) (\w+)$', handoff, re.M)
    need(handoff_cues == [('playfanfare', 'MUS_OBTAIN_TMHM'), ('playse', 'SE_CLICK'), ('playse', 'SE_PC_ON')], 'Celio handoff cue order differs')
    counters = source.text('src/sound.c')
    for symbol in inherited_specs:
        song_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
        address, player, second = struct.unpack_from('<IHH', pack, 0x084A32CC + song_id * 8 - 0x08489968)
        name = symbol.lower()
        need(song_table[song_id] == (name, str(player), str(second)), 'Inherited audio table binding differs')
        path = 'sound/songs/midi/' + name + '.mid'
        song = source.read(path)
        need(song[:4] == b'MThd', 'Inherited audio source framing differs')
        looping = symbol == 'MUS_NET_CENTER'
        need(not looping or (player == 0 and b'\xff\x06\x01[' in song and b'\xff\x06\x01]' in song), 'Inherited music loop binding differs')
        counter = int(re.search(r'\{\s*' + symbol + r',\s*(\d+)\s*\}', counters)[1]) if symbol == 'MUS_OBTAIN_TMHM' else None
        need(symbol != 'MUS_OBTAIN_TMHM' or counter == 220, 'Celio handoff fanfare counter differs')
        cue = dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath=path, sourcePlayer=player,
                   priority=pack[address - 0x08489968 + 2], sourceCounter=counter, songAddress=address, species=0, looping=looping, cryMode=0)
        inherited_path = 'content/generated/first-sevii-presentation/synth-manifest.json' if symbol == 'MUS_NET_CENTER' else 'content/generated/synth-audio/manifest.json'
        inherited = next(c for c in json.loads(source.reuse(inherited_path))['cues'] if c['id'] == symbol)
        need(dict(inherited, cryMode=inherited.get('cryMode', 0)) == cue, 'Inherited cue record differs: ' + symbol)
        inherited_cues.append(cue)
    data = json.loads(source.read('data/maps/OneIsland_PokemonCenter_1F/map.json'))
    need(data['music'] == 'MUS_NET_CENTER', 'Source Network Center music differs')
    # Existing source move animations already cover these source geometry rows.
    geometry_manifest = 'content/generated/campaign-hit-art/manifest.json'
    geometry = json.loads(source.reuse(geometry_manifest))['geometry']
    inherited_geometry = []
    for row in sprites:
        if row['paletteVariant'] != 'normal':
            continue
        expected = dict(species=row['sourceSpeciesId'], side=row['side'], sourceWidth=row['sourceWidth'],
                        sourceHeight=row['sourceHeight'], yOffset=row['sourceYOffset'],
                        elevation=row['sourceEnemyElevation'] if row['side'] == 'front' else 0)
        need(expected in geometry, 'Inherited move-animation geometry differs')
        inherited_geometry.append(expected)
    out['synth-manifest.json'] = encode(dict(schemaVersion=1, sourceCommit=COMMIT, cues=cues, inheritedCues=inherited_cues))

    lock = encode(dict(schemaVersion=1, profile=PROFILE, repository='pret/pokefirered', commit=COMMIT, tree=TREE,
                       files=[source.files[p] for p in sorted(source.files)], inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)]))
    if LOCK_SHA:
        need(sha(lock) == LOCK_SHA, 'Presentation input inventory differs')
    manifest = encode(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, sourceTree=TREE, lockSha256=sha(lock),
        inputs=[source.files[p] for p in sorted(source.files)], inheritedInputs=[source.inherited[p] for p in sorted(source.inherited)],
        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, tiles=tiles, layers=layers,
        newOwnedSpecies=[number for _, number in OWNED], newOpponentOnlySpecies=[],
        moveAnimationGeometry=dict(inheritedManifest=geometry_manifest, rows=inherited_geometry),
        handoff=dict(sourcePath=source_script, sourceLabel='OneIsland_PokemonCenter_1F_EventScript_GiveCelioSapphire',
                     orderedCues=[dict(opcode=op, cue=cue) for op, cue in handoff_cues], fanfareCounter=220,
                     mapMusic='MUS_NET_CENTER', playbackAuthority='existing committed presentation effects; no custom replay'),
        audio=dict(additionalCueIds=[c['id'] for c in cues], inheritedCueIds=[c['id'] for c in inherited_cues], originalPackUnchanged=True, newAudioAssets=0, physicalAudioVerified=False),
        limits=['No runtime rules are created by this presentation package.', 'Party icons use original frame zero shared by normal/shiny variants.',
                'Move art reuses the existing bounded animation dispatch; this package adds no move animation coverage.'],
        outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(out.items())]))
    if MANIFEST_SHA:
        need(sha(manifest) == MANIFEST_SHA, 'Reviewed presentation manifest differs')
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})
    result['scripts/celio-presentation.lock.json'] = lock
    report = dict(passed=True, sourceFiles=len(source.raw), sprites=len(sprites), pokemonIcons=len(icons), footprints=len(footprints),
        dynamicMetatiles=len(tiles), dynamicLayers=len(layers), moveGeometry=len(inherited_geometry), outputs=len(out) - 1,
        lockSha256=sha(lock), lockBytes=len(lock), manifestSha256=sha(manifest), manifestBytes=len(manifest),
        synthSha256=sha(out['synth-manifest.json']), synthBytes=len(out['synth-manifest.json']), sourceAndStaticChecksOnly=True)
    return result, report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--dependency-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root, args.dependency_root or args.project_root)
    for relative, data in output.items():
        target = safe(args.project_root, relative)
        if args.check:
            need(target.read_bytes() == data, 'Generated bytes differ: ' + relative)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(report, sort_keys=True))
