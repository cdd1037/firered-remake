#!/usr/bin/env python3
"""Import Articuno presentation and Roger's Bird Keeper portrait from pinned local source blobs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

from runtime_json import runtime_json_bytes
from presentation_pixels import PixelReader
import argparse
import io
import json
import re
import os
import subprocess
import struct
import wave
from pathlib import Path
from PIL import Image

ROOT = _PROJECT_ROOT
PACKAGE = 'seafoam-presentation'
BASE = 'content/generated/sprites/pokemon/seafoam/'
RAW = 'content/raw/pokefirered/037335f4/'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-seafoam-presentation-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SPECIES = [('Articuno', 144)]


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=False) + '\n').encode()


def need(condition, message):
    if not condition:
        raise ValueError(message)


def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))


# Shared raw inputs are authenticated by this package's lock and the pinned Git blob.
SHARED_RAW = {path: 'content/raw/pokefirered/037335f4/' + path for path in ['include/constants/species.h', 'src/data/graphics/pokemon.h', 'src/data/pokemon_graphics/front_pic_coordinates.h', 'src/data/pokemon_graphics/back_pic_coordinates.h', 'src/data/pokemon_graphics/enemy_mon_elevation.h', 'src/pokemon_icon.c', 'graphics/pokemon/icon_palettes/icon_palette_0.pal', 'graphics/pokedex/kanto_dex_bgpals.pal', 'src/data/pokemon_graphics/footprint_table.h', 'src/data/pokemon/pokedex_entries.h', 'src/data/pokemon/pokedex_text_fr.h', 'sound/cry_tables.inc', 'include/constants/sound.h']}
SHARED_RAW.update({path: 'content/raw/pokefirered/037335f4/' + path for path in [
    'include/constants/trainers.h', 'src/data/graphics/trainers.h',
    'src/data/trainer_graphics/front_pic_tables.h', 'src/data/text/trainer_class_names.h',
    'src/data/trainers.h', 'src/battle_controller_opponent.c']})


def safe_path(root, relative):
    need(isinstance(relative, str) and relative and not Path(relative).is_absolute() and
         not any(p in ('', '.', '..') for p in relative.split('/')) and
         '\\' not in relative and ':' not in relative, 'Unsafe relative resource path')
    path = root / relative
    safe(path)
    return path


def git(reference, *args):
    env = {k: v for k, v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=os.devnull,
               GIT_NO_REPLACE_OBJECTS='1', GIT_OPTIONAL_LOCKS='0')
    return subprocess.check_output(['git', '--no-replace-objects', '--no-optional-locks',
                                   '-C', str(reference), *args], env=env)


def read_git_blob(reference, path):
    listing = git(reference, 'ls-tree', '-z', COMMIT, '--', path).split(b'\0')
    need(len(listing) == 2 and listing[1] == b'', 'Expected one pinned source blob: ' + path)
    header, name = listing[0].split(b'\t')
    mode, kind, object_id = header.decode().split()
    need((mode, kind, name.decode()) == ('100644', 'blob', path), 'Source object is not a regular file')
    data = git(reference, 'cat-file', 'blob', object_id)
    return data


class Source:
    def __init__(self, reference, dependency, target):
        self.reference, self.dependency, self.target = reference, dependency, target
        self.data, self.entries = {}, {}

    def read(self, path):
        if path not in self.data:
            raw_path = SHARED_RAW.get(path, RAW + path)
            if self.reference:
                data = read_git_blob(self.reference, path)
                if path in SHARED_RAW:
                    need(safe_path(self.dependency, raw_path).read_bytes() == data,
                         'Shared raw reference differs from pinned source: ' + path)
            else:
                data = safe_path(self.dependency if path in SHARED_RAW else self.target, raw_path).read_bytes()
            self.data[path] = data
            self.entries[path] = dict(path=path, rawPath=raw_path)
        return self.data[path]

    def text(self, path):
        return self.read(path).decode()

    def lock(self):
        need(set(SHARED_RAW) <= self.entries.keys(), 'Unused shared raw input')
        return dict(schemaVersion=1, repository='pret/pokefirered', commit=COMMIT,
                    profile=PROFILE, files=[self.entries[p] for p in sorted(self.entries)])


def build(target, dependency, reference, check):
    source = Source(reference, dependency, target)
    inherited, outputs = {}, {}

    def reuse(path):
        data = safe_path(dependency, path).read_bytes()
        inherited[path] = dict(path=path)
        return data

    pixels = PixelReader(source)
    palette, indexed = pixels.palette, pixels.pixels

    def emit(identity, width, height, rgba, paths, **fields):
        buf = io.BytesIO()
        Image.frombytes('RGBA', (width, height), rgba).save(buf, format='PNG', optimize=False, compress_level=9)
        outputs[identity + '.png'] = buf.getvalue()
        return dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png',  **fields)

    dex = json.loads(reuse('content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json'))
    species_constants = source.text('include/constants/species.h')
    graphics = source.text('src/data/graphics/pokemon.h')
    sprites, icons, footprints, entries, geometry = [], [], [], [], []
    for name, number in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        need(re.search(r'^#define ' + symbol + r'\s+' + str(number) + r'\s*$', species_constants, re.M), 'Species identity differs')
        for side in ['front', 'back']:
            coordinate_path = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text(coordinate_path))
            need(match, 'Source battler coordinates missing')
            width, height, y = map(int, match.groups())
            elevation = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', source.text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
            elevation = int(elevation[1]) if elevation else 0
            image = f'graphics/pokemon/{stem}/{side}.png'
            need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics, 'Source sprite declaration differs')
            for variant in ['normal', 'shiny']:
                pal = f'graphics/pokemon/{stem}/{variant}.pal'
                palette_symbol = 'gMonPalette_' if variant == 'normal' else 'gMonShinyPalette_'
                need(f'{palette_symbol}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics, 'Source palette declaration differs')
                rgba = indexed(image, pal, (64, 64), True)
                sprites.append(emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal], sourceSpeciesId=number,
                    speciesId='pret:' + symbol, side=side, paletteVariant=variant,
                    x=144 if side == 'front' else 40, y=8 + y - elevation if side == 'front' else 48 + y,
                    sourceYOffset=y, sourceElevation=elevation if side == 'front' else 0,
                    sourceWidth=width, sourceHeight=height, paletteExpansion='floor(rgb5 * 255 / 31)', transparentIndex=0))
            geometry.append(dict(species=number, side=side, yOffset=y, elevation=elevation if side == 'front' else 0,
                                 sourceWidth=width, sourceHeight=height))
        pal_index = int(re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', source.text('src/pokemon_icon.c'))[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need(re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', source.text('src/pokemon_icon.c')) and
             f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Source icon binding differs')
        rgba = indexed(image, pal, (32, 64), False, True)
        icons.append(emit(stem + '-icon', 32, 32, rgba, [image, pal], sourceSpeciesId=number,
                          speciesId='pret:' + symbol, paletteIndex=pal_index, sourceFrame=0, sourceFrameCount=2,
                          paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', transparentIndex=0))
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source shape differs')
        need(f'gMonFootprint_{name}[] = INCBIN_U8("{image[:-4]}.1bpp")' in graphics, 'Source footprint graphics binding differs')
        colors = palette(pal, True)
        indices = [1 if v == 0 else 0 for v in im.get_flattened_data()]
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        buf = io.BytesIO()
        Image.frombytes('RGBA', (16, 16), rgba).save(buf, format='PNG', optimize=False, compress_level=9)
        identity = stem + '-footprint'
        outputs[identity + '.png'] = buf.getvalue()
        foot = dict(id=identity, sourceSpeciesId=number, speciesId='pret:' + symbol, width=16, height=16, texturePath=BASE + identity + '.png',  inherited=False)
        footprints.append(foot)
        entry = next(e for e in dex['entries'] if e['sourceSpeciesId'] == number)
        declaration = re.search(r'\[NATIONAL_DEX_' + name.upper() + r'\]\s*=\s*\{(.*?)\n\s*\}', source.text('src/data/pokemon/pokedex_entries.h'), re.S)[1]
        need(re.search(r'\.categoryName\s*=\s*_\("' + entry['category'] + r'"\)', declaration) and
             int(re.search(r'\.height\s*=\s*(\d+)', declaration)[1]) == entry['heightDecimeters'] and
             int(re.search(r'\.weight\s*=\s*(\d+)', declaration)[1]) == entry['weightHectograms'], 'Dex category/measures differ')
        description = re.search(r'const u8 g' + name + r'PokedexText\[\]\s*=\s*_\((.*?)\);', source.text('src/data/pokemon/pokedex_text_fr.h'), re.S)[1]
        text = ''.join(json.loads('"' + s + '"') for s in re.findall(r'"([^"\n]*)"', description))
        need(text == entry['description'], 'Dex description differs')
        front = next(s for s in sprites if (s['sourceSpeciesId'], s['side'], s['paletteVariant']) == (number, 'front', 'normal'))
        texture = lambda r: {k: r[k] for k in ['texturePath',  'width', 'height']}
        entries.append(dict(entry, portrait=texture(front), footprint=texture(foot)))

    trainer_image = 'graphics/trainers/front_pics/bird_keeper_front_pic.png'
    trainer_palette = 'graphics/trainers/palettes/bird_keeper.pal'
    trainer_graphics = source.text('src/data/graphics/trainers.h')
    need('gTrainerFrontPic_BirdKeeper[] = INCBIN_U32("graphics/trainers/front_pics/bird_keeper_front_pic.4bpp.lz")' in trainer_graphics and
         'gTrainerPalette_BirdKeeper[] = INCBIN_U32("graphics/trainers/palettes/bird_keeper.gbapal.lz")' in trainer_graphics,
         'Bird Keeper source graphics binding differs')
    trainer_constants = source.text('include/constants/trainers.h')
    need(re.search(r'^#define TRAINER_PIC_BIRD_KEEPER\s+104\s*$', trainer_constants, re.M) and
         re.search(r'^#define TRAINER_CLASS_BIRD_KEEPER\s+79\s*$', trainer_constants, re.M),
         'Bird Keeper source constants differ')
    trainer_tables = source.text('src/data/trainer_graphics/front_pic_tables.h')
    need('TRAINER_SPRITE(BIRD_KEEPER, gTrainerFrontPic_BirdKeeper, 0x800)' in trainer_tables and
         'TRAINER_PAL(BIRD_KEEPER, gTrainerPalette_BirdKeeper)' in trainer_tables, 'Bird Keeper sprite/palette table differs')
    trainer_coordinates = re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}',
                                    trainer_tables.split('const struct CompressedSpriteSheet')[0])
    need(trainer_coordinates[104] == ('8', '1') and
         '(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40' in source.text('src/battle_controller_opponent.c'),
         'Bird Keeper original portrait geometry differs')
    need(re.search(r'\[TRAINER_CLASS_BIRD_KEEPER\]\s*= _\("BIRD KEEPER"\)',
                   source.text('src/data/text/trainer_class_names.h')), 'Bird Keeper class display differs')
    roger = re.search(r'\[TRAINER_BIRD_KEEPER_ROGER\]\s*=\s*\{(.*?)\n\s*\},', source.text('src/data/trainers.h'), re.S)
    need(roger and '.trainerClass = TRAINER_CLASS_BIRD_KEEPER,' in roger[1] and
         '.trainerPic = TRAINER_PIC_BIRD_KEEPER,' in roger[1] and '.trainerName = _("ROGER"),' in roger[1],
         'Roger original portrait binding differs')
    trainer_rgba = indexed(trainer_image, trainer_palette, (64, 64), False)
    trainer_indices = Image.open(io.BytesIO(source.read(trainer_image))).tobytes()
    portraits = [emit('bird-keeper-front', 64, 64, trainer_rgba, [trainer_image, trainer_palette],
        sourceTrainerPicId=104, trainerPic='TRAINER_PIC_BIRD_KEEPER', trainerClass='TRAINER_CLASS_BIRD_KEEPER',
        className='BIRD KEEPER', x=144, y=8, sourceFrame=0, sourceSize=8, sourceYOffset=1,
         paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', transparentIndex=0)]
    trainer_bindings = [dict(trainer='pret:TRAINER_BIRD_KEEPER_ROGER', name='ROGER',
        trainerClass='TRAINER_CLASS_BIRD_KEEPER', className='BIRD KEEPER', trainerPic='TRAINER_PIC_BIRD_KEEPER',
        portrait='bird-keeper-front')]

    geometry_path = 'content/generated/battle/art/campaign-hit-art/manifest.json'
    inherited_geometry = json.loads(reuse(geometry_path))['geometry']
    need(all(row in inherited_geometry for row in geometry), 'Existing source move geometry differs')
    pack = reuse('content/generated/audio/synth/firered.pack')
    cries = reuse('content/generated/audio/synth/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    cues, cry_evidence = [], []
    for name, species in SPECIES:
        row = cry_rows[species]
        need(cry_table[species - 1] == name and row[1] == species - 1, 'Species cry table differs')
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave_offset = struct.unpack_from('<I', pack, tone + 4)[0] - 0x08489968
        need(struct.unpack_from('<HHIII', pack, wave_offset) == (1, 0, 10764288, 0, row[2]), 'Species packed cry header differs')
        path = 'sound/direct_sound_samples/cries/' + name.lower() + '.wav'
        with wave.open(io.BytesIO(source.read(path))) as original:
            need((original.getnchannels(), original.getsampwidth(), original.getframerate(), original.getnframes(), original.getcomptype()) ==
                 (1, 1, 10512, row[2] + 1, 'NONE'), 'Source cry sample/header dimensions differ')
            pcm = original.readframes(original.getnframes())
        delta = (0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1)
        decoded = bytearray()
        for index in range(row[2] + row[4]):
            block, i = divmod(index, 64)
            start = wave_offset + 16 + block * 33
            if i == 0:
                value = pack[start]
            else:
                packed = pack[start + (1 if i == 1 else i // 2 + 1)]
                nibble = packed & 15 if i == 1 or i % 2 else packed >> 4
                value = (value + delta[nibble]) & 255
            decoded.append((value + 128) & 255)
        need(decoded == pcm[:len(decoded)], 'Cry pack/source PCM differs: ' + name)
        cry_evidence.append(dict(species=species, cryIndex=row[1], sampleCount=row[2], payloadBytes=row[3], guardStored=bool(row[4]),
            decodedBytes=len(decoded),  sourcePath=path, sourcePlayer=4, priority=priority))
        cues.append(dict(id='CRY_' + name.upper() + '_NORMAL', sourceSymbol='SPECIES_' + name.upper(), sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0))

    outputs['synth-manifest.json'] = runtime_manifest_bytes(dict(schemaVersion=1, cues=cues))
    outputs['dex-entries.json'] = canonical(dict(schemaVersion=1,   entries=entries))
    lock = source.lock()
    lock['inheritedInputs'] = [inherited[p] for p in sorted(inherited)]
    lb = canonical(lock)
    manifest = dict(schemaVersion=1,
                      newOwnedSpecies=[s for _, s in SPECIES],
                    sprites=sprites, pokemonIcons=icons, footprints=footprints,
                    trainerPortraits=portraits, trainerBindings=trainer_bindings,
                    dexEntriesPath=BASE + 'dex-entries.json', moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry),
                    audio=dict(newCueCount=1, normalCries=cry_evidence, ),
                    limits=['Existing species textures remain in their original packages; this package adds Articuno and the Bird Keeper trainer portrait.',
                            'The party icon uses original frame zero for normal and shiny variants.',
                            'No new move animation coverage; native visual and audible output remain unverified.'],
                    outputs=[dict(path=p) for p, b in sorted(outputs.items())])
    mb = runtime_json_bytes(manifest, sort_keys=True)
    writes = [(target / BASE / 'manifest.json', mb)]
    writes += [(target / RAW / p, b) for p, b in sorted(source.data.items()) if p not in SHARED_RAW]
    writes += [(target / BASE / p, b) for p, b in sorted(outputs.items())]
    for p, b in writes:
        safe(p)
        if p.suffix == '.json':
            json.loads(b)
        if check:
            need(p.is_file() and p.read_bytes() == b, 'Regeneration differs: ' + str(p))
        else:
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b)
    print(json.dumps(dict(passed=True, check=check, sourceFiles=len(source.entries), newRawFiles=len(source.entries) - len(SHARED_RAW),  inheritedInputs=len(inherited),
                         newFootprints=1, dexEntries=1, newBattleSprites=4, newPokemonIcons=1, newAudioCues=1, newTrainerPortraits=1,
                          lockBytes=len(lb),  manifestBytes=len(mb)), indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', type=Path)
    parser.add_argument('--project-root', type=Path, default=ROOT)
    parser.add_argument('--dependency-root', type=Path, default=ROOT)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    for p in [p for p in [args.reference_root, args.project_root, args.dependency_root] if p is not None]:
        safe(p.absolute())
    target, dependency = args.project_root.resolve(), args.dependency_root.resolve()
    reference = args.reference_root.resolve() if args.reference_root else None
    need(reference is None or not (target == reference or target.is_relative_to(reference) or reference.is_relative_to(target)), 'Source/output overlap')
    build(target, dependency, reference, args.check)
