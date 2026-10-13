#!/usr/bin/env python3
"""Rebuild Mewtwo pixels and cue metadata from pinned local blobs; no upstream execution."""
import argparse
import hashlib
import io
import json
import pathlib
import re
import runpy
import struct
import subprocess
import wave
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
# Reuse the established authored PNG/path/hash helpers; importing performs no rebuild.
common = runpy.run_path(str(ROOT / 'scripts/import-celio-presentation.py'))
need, sha, encode, safe, png = (common[k] for k in ('need', 'sha', 'encode', 'safe', 'png'))
COMMIT, TREE = common['COMMIT'], common['TREE']
PROFILE = 'firered-mewtwo-presentation-v1'
BASE = 'content/generated/mewtwo-presentation/'
RAW = 'content/raw/mewtwo-presentation/'
LOCK_SHA = 'f28873123459b1d1de87e871a2ebdcd0648a8a5cdc71bb338936948b3f557a38'
MANIFEST_SHA = '44f0534e2bf81d2aef828968a7914eb9361b3d14128da853c2514eb099ad05f6'
SPECIES = [('Mewtwo', 150)]

class Source:
    def __init__(self, root, reference, dependency):
        self.root, self.reference, self.dependency = root, reference, dependency
        self.files, self.raw, self.inherited = {}, {}, {}
        self.lock = None
        if reference:
            need(subprocess.check_output(['git', '-C', str(reference), 'rev-parse', COMMIT + '^{tree}']).decode().strip() == TREE, 'Pinned source tree differs')
        if LOCK_SHA:
            data = safe(root, 'scripts/mewtwo-presentation.lock.json').read_bytes()
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
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source pixels differ')
        indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
        colors = palette(pal, floor=True)
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))

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
    cues.append(dict(cues[0], id='CRY_MEWTWO_ENCOUNTER', cryMode=2))
    script_path = 'data/maps/CeruleanCave_B1F/scripts.inc'
    script = source.text(script_path)
    need('playmoncry SPECIES_MEWTWO, CRY_MODE_ENCOUNTER' in script and
         'playbgm MUS_ENCOUNTER_GYM_LEADER, 0' in script and
         'setwildbattle SPECIES_MEWTWO, 70' in script and 'special StartLegendaryBattle' in script,
         'Mewtwo encounter presentation source differs')
    sound = source.text('src/sound.c')
    need(re.search(r'#define CRY_MODE_ENCOUNTER\s+2', source.text('include/constants/sound.h')) and
         re.search(r'case CRY_MODE_ENCOUNTER:\s*release = 225;\s*pitch = 15600;\s*chorus = 20;\s*volume = 90;\s*break;', sound),
         'Encounter cry source parameters differ')
    setup_path = 'src/battle_setup.c'
    need(re.search(r'case SPECIES_MEWTWO:\s*CreateBattleStartTask\(B_TRANSITION_BLUR, MUS_VS_MEWTWO\);',
                   source.text(setup_path)), 'Mewtwo battle music binding differs')
    symbol = 'MUS_VS_MEWTWO'
    song_id = int(re.search(r'^#define ' + symbol + r'\s+(\d+)', songs, re.M)[1])
    address, player, second = struct.unpack_from('<IHH', pack, 0x084A32CC + song_id * 8 - 0x08489968)
    need(song_id == 340 and song_table[song_id] == ('mus_vs_mewtwo', str(player), str(second)) and player == 0,
         'Mewtwo music song table differs')
    path = 'sound/songs/midi/mus_vs_mewtwo.mid'
    song = source.read(path)
    need(song[:4] == b'MThd' and b'\xff\x06\x01[' in song and b'\xff\x06\x01]' in song,
         'Mewtwo music source loop differs')
    cues.append(dict(id=symbol, sourceCommit=COMMIT, sourceSymbol=symbol, sourcePath=path, sourcePlayer=player,
                     priority=pack[address - 0x08489968 + 2], sourceCounter=None, songAddress=address, species=0, looping=True, cryMode=0))
    # The field introduction cue is already admitted by the shared synth catalog.
    inherited_symbol = 'MUS_ENCOUNTER_GYM_LEADER'
    inherited_id = int(re.search(r'^#define ' + inherited_symbol + r'\s+(\d+)', songs, re.M)[1])
    inherited_address, inherited_player, inherited_second = struct.unpack_from('<IHH', pack, 0x084A32CC + inherited_id * 8 - 0x08489968)
    need(song_table[inherited_id] == (inherited_symbol.lower(), str(inherited_player), str(inherited_second)),
         'Mewtwo introduction music table differs')
    inherited_path = 'sound/songs/midi/' + inherited_symbol.lower() + '.mid'
    inherited_song = source.read(inherited_path)
    need(inherited_song[:4] == b'MThd', 'Mewtwo introduction source framing differs')
    inherited = next(c for c in json.loads(source.reuse('content/generated/synth-audio/manifest.json'))['cues'] if c['id'] == inherited_symbol)
    expected = dict(id=inherited_symbol, sourceCommit=COMMIT, sourceSymbol=inherited_symbol, sourcePath=inherited_path,
                    sourcePlayer=inherited_player, priority=pack[inherited_address - 0x08489968 + 2], sourceCounter=None,
                    songAddress=inherited_address, species=0, looping=True, cryMode=0)
    need(dict(inherited, cryMode=inherited.get('cryMode', 0)) == expected, 'Mewtwo introduction cue differs')
    inherited_cues = [expected]
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
        assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, newOwnedSpecies=[150],
        moveAnimationGeometry=dict(inheritedManifest=geometry_manifest, rows=inherited_geometry),
        encounter=dict(sourcePath=script_path, sourceLabel='CeruleanCave_B1F_EventScript_Mewtwo',
            cry='CRY_MEWTWO_ENCOUNTER', cryMode=2, cryParameters=dict(length=140, release=225, pitch=15600, chorus=20, volume=90),
            introductionMusic=inherited_symbol, battleMusic=symbol, battleMusicSourcePath=setup_path, sourceSongId=song_id,
            playbackAuthority='existing committed presentation effects and battle music selection'),
        audio=dict(additionalCueIds=[c['id'] for c in cues], inheritedCueIds=[c['id'] for c in inherited_cues],
            originalPackUnchanged=True, newAudioAssets=0, physicalAudioVerified=False),
        limits=['No runtime rules are created by this presentation package.',
            'Party icon uses original frame zero shared by normal/shiny variants.',
            'Map planes and overworld Mewtwo are supplied by the Cerulean Cave map package.',
            'Existing bounded move animation dispatch and geometry are reused; no new move animation coverage.'],
        outputs=[dict(path=p, byteLength=len(b), sha256=sha(b)) for p, b in sorted(out.items())]))
    if MANIFEST_SHA:
        need(sha(manifest) == MANIFEST_SHA, 'Reviewed presentation manifest differs')
    out['manifest.json'] = manifest
    result = {BASE + p: b for p, b in out.items()}
    result.update({RAW + p: b for p, b in source.raw.items()})
    result['scripts/mewtwo-presentation.lock.json'] = lock
    report = dict(passed=True, sourceFiles=len(source.raw), inheritedInputs=len(source.inherited), sprites=len(sprites),
        pokemonIcons=len(icons), footprints=len(footprints), assets=len(assets), moveGeometry=len(inherited_geometry),
        outputs=len(out) - 1, lockSha256=sha(lock), lockBytes=len(lock), manifestSha256=sha(manifest), manifestBytes=len(manifest),
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
