#!/usr/bin/env python3
"""Import the minimal eight-owned breeding presentation delta from pinned local source."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

from runtime_json import runtime_json_bytes
from generated_output_compare import generated_output_matches
import argparse
import io
import json
import re
import os
import subprocess
import struct
from pathlib import Path
from PIL import Image

ROOT = _PROJECT_ROOT
PACKAGE = 'breeding-presentation'
BASE = 'content/generated/sprites/pokemon/breeding/'
RAW = 'content/raw/pokefirered/037335f4/'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-breeding-presentation-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SPECIES = [('Pichu', 172), ('Cleffa', 173), ('Igglybuff', 174), ('Tyrogue', 236), ('Smoochum', 238), ('Elekid', 239), ('Wynaut', 360), ('Hitmontop', 237)]


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()


def need(condition, message):
    if not condition:
        raise ValueError(message)


def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))


# Shared raw inputs are authenticated by this package's lock and the pinned Git blob.
SHARED_RAW = {path: 'content/raw/pokefirered/037335f4/' + path for path in ['include/constants/species.h', 'src/data/graphics/pokemon.h', 'src/data/pokemon_graphics/front_pic_coordinates.h', 'src/data/pokemon_graphics/back_pic_coordinates.h', 'src/data/pokemon_graphics/enemy_mon_elevation.h', 'src/pokemon_icon.c', 'graphics/pokemon/icon_palettes/icon_palette_0.pal', 'graphics/pokedex/kanto_dex_bgpals.pal', 'src/data/pokemon_graphics/footprint_table.h', 'src/data/pokemon/pokedex_entries.h', 'src/data/pokemon/pokedex_text_fr.h', 'sound/cry_tables.inc', 'include/constants/sound.h']}
SHARED_RAW['graphics/pokemon/icon_palettes/icon_palette_2.pal'] = 'content/raw/pokefirered/037335f4/graphics/pokemon/icon_palettes/icon_palette_2.pal'
SHARED_RAW['graphics/pokemon/icon_palettes/icon_palette_1.pal'] = 'content/raw/pokefirered/037335f4/graphics/pokemon/icon_palettes/icon_palette_1.pal'
SHARED_RAW['src/daycare.c'] = 'content/raw/pokefirered/037335f4/src/daycare.c'
SHARED_RAW['src/graphics.c'] = 'content/raw/pokefirered/037335f4/src/graphics.c'
SHARED_RAW['src/data/pokemon/cry_ids.h'] = 'content/raw/pokefirered/037335f4/src/data/pokemon/cry_ids.h'
for name, package in [('Hitmontop', 'seven-island-presentation'), ('Smoochum', 'trainer-tower-presentation')]:
    for asset in ['front.png', 'back.png', 'normal.pal', 'shiny.pal']:
        path = 'graphics/pokemon/' + name.lower() + '/' + asset
        SHARED_RAW[path] = 'content/raw/pokefirered/037335f4/' + path


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
    # Read only the pinned object; never expand or run the reference checkout.
    return git(reference, 'show', COMMIT + ':' + path)


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

    def palette(path, floor):
        lines = source.text(path).splitlines()
        need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Source palette differs')
        return [tuple((int(c) >> 3) * 255 // 31 if floor else ((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2)
                      for c in line.split()) for line in lines[3:]]

    def indexed(image, pal, shape, floor, first_frame=False, crop=None):
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.crop(crop).tobytes() if crop else im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    def emit(identity, width, height, rgba, paths, **fields):
        buf = io.BytesIO()
        Image.frombytes('RGBA', (width, height), rgba).save(buf, format='PNG', optimize=False, compress_level=9)
        outputs[identity + '.png'] = buf.getvalue()
        return dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png',  **fields)

    dex = json.loads(reuse('content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json'))
    catalogs = {
        'seven': 'content/generated/sprites/pokemon/seven-island/manifest.json',
        'tower': 'content/generated/sprites/pokemon/trainer-tower/catalog.json'}
    output_manifests = dict(catalogs, tower='content/generated/sprites/pokemon/trainer-tower/manifest.json')
    catalog_data, output_data = {}, {}
    for key, path in catalogs.items():
        data = reuse(path)
        catalog_data[key] = json.loads(data)
        output_bytes = reuse(output_manifests[key])
        output_data[key] = json.loads(output_bytes)
    geometry_path = 'content/generated/battle/art/campaign-hit-art/manifest.json'
    inherited_geometry = json.loads(reuse(geometry_path))['geometry']

    def inherited_asset(catalog, collection, number, **matches):
        original = next(row for row in catalog_data[catalog][collection] if row['sourceSpeciesId'] == number and all(row.get(k) == v for k, v in matches.items()))
        row = dict(original)
        for field in ['texturePath']:
            path = row[field]
            if not path.startswith('content/'):
                path = str(Path(catalogs[catalog]).parent / path)
            data = reuse(path)
            relative = str(Path(path).relative_to(Path(catalogs[catalog]).parent))
            row[field] = path
        row.update(inherited=True)
        return row

    def exact_pixels(row, expected):

        png = reuse(row['texturePath'])
        need(Image.open(io.BytesIO(png)).convert('RGBA').tobytes() == expected, 'Inherited PNG differs')

    species_constants = source.text('include/constants/species.h')
    graphics = source.text('src/data/graphics/pokemon.h')
    sprites, icons, footprints, entries, geometry = [], [], [], [], []
    for name, number in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        catalog = 'tower' if number == 238 else 'seven'
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
                fields = dict(speciesId='pret:' + symbol, sourceSpeciesId=number, side=side, paletteVariant=variant,
                    x=144 if side == 'front' else 40, y=8 + y - elevation if side == 'front' else 48 + y,
                    sourceYOffset=y, sourceEnemyElevation=elevation if side == 'front' else 0,
                    sourceWidth=width, sourceHeight=height, paletteExpansion='floor(rgb5 * 255 / 31)', transparentIndex=0)
                if number not in (237, 238):
                    row = emit(stem + '-' + side + '-' + variant, 64, 64, rgba, [image, pal, coordinate_path], inherited=False, **fields)
                else:
                    row = inherited_asset(catalog, 'sprites', number, side=side, paletteVariant=variant)
                    need((row['width'], row['height'], row['x'], row['y']) ==
                         (64, 64, fields['x'], fields['y']), 'Inherited battler placement differs')
                    for key in ['sourceYOffset', 'sourceWidth', 'sourceHeight']:
                        if key in row:
                            need(row[key] == fields[key], 'Inherited geometry differs')
                    row.update(fields)
                    exact_pixels(row, rgba)
                sprites.append(row)
            geo = dict(species=number, side=side, yOffset=y, elevation=elevation if side == 'front' else 0, sourceWidth=width, sourceHeight=height)
            need(geo in inherited_geometry, 'Shared move geometry differs')
            geometry.append(geo)
        pal_index = int(re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', source.text('src/pokemon_icon.c'))[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need(re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', source.text('src/pokemon_icon.c')) and
             f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Source icon binding differs')
        rgba = indexed(image, pal, (32, 64), False, True)
        icon = emit(stem + '-icon', 32, 32, rgba, [image, pal], sourceSpeciesId=number,
                    speciesId='pret:' + symbol, paletteIndex=pal_index, sourceFrame=0, sourceFrameCount=2,
                    paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', transparentIndex=0, inherited=False,
                    runtimePaletteExpansion='stored')
        icons.append(icon)
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source shape differs')
        need(f'gMonFootprint_{name}[] = INCBIN_U8("{image[:-4]}.1bpp")' in graphics, 'Source footprint graphics binding differs')
        colors = palette(pal, True)
        indices = [1 if v == 0 else 0 for v in im.get_flattened_data()]
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        foot = emit(stem + '-footprint', 16, 16, rgba, [image, pal], sourceSpeciesId=number, speciesId='pret:' + symbol, inherited=False)
        footprints.append(foot)
        entry = next(e for e in dex['entries'] if e['sourceSpeciesId'] == number)
        need(entry['name'] == name.upper(), 'Inherited species name differs')
        declaration = re.search(r'\[NATIONAL_DEX_' + name.upper() + r'\]\s*=\s*\{(.*?)\n\s*\}', source.text('src/data/pokemon/pokedex_entries.h'), re.S)[1]
        need(re.search(r'\.categoryName\s*=\s*_\("' + entry['category'] + r'"\)', declaration) and
             int(re.search(r'\.height\s*=\s*(\d+)', declaration)[1]) == entry['heightDecimeters'] and
             int(re.search(r'\.weight\s*=\s*(\d+)', declaration)[1]) == entry['weightHectograms'], 'Dex category/measures differ')
        description = re.search(r'const u8 g' + name + r'PokedexText\[\]\s*=\s*_\((.*?)\);', source.text('src/data/pokemon/pokedex_text_fr.h'), re.S)[1]
        text = ''.join(json.loads('"' + part + '"') for part in re.findall(r'"([^"\n]*)"', description))
        need(text == entry['description'], 'Dex description differs')
        front = next(row for row in sprites if (row['sourceSpeciesId'], row['side'], row['paletteVariant']) == (number, 'front', 'normal'))
        texture = lambda row: {k: row[k] for k in ['texturePath',  'width', 'height']}
        entries.append(dict(entry, portrait=texture(front), footprint=texture(foot)))

    # An Egg's visible shell and icon never depend on its hidden real species.
    egg_assets = []
    egg_palette = 'graphics/pokemon/egg/normal.pal'
    egg_front = 'graphics/pokemon/egg/front.png'
    egg_icon = 'graphics/pokemon/egg/icon.png'
    daycare = source.text('src/daycare.c')
    need(re.search(r'^#define SPECIES_EGG\s+412\s*$', species_constants, re.M), 'Egg source identity differs')
    need('gMonFrontPic_Egg[] = INCBIN_U32("graphics/pokemon/egg/front.4bpp.lz")' in graphics and
         'gMonPalette_Egg[] = INCBIN_U32("graphics/pokemon/egg/normal.gbapal.lz")' in graphics, 'Egg shell declaration differs')
    coords = re.search(r'\[SPECIES_EGG\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', source.text('src/data/pokemon_graphics/front_pic_coordinates.h'))
    need(coords and tuple(map(int, coords.groups())) == (24, 24, 20), 'Egg shell source coordinates differ')
    fields = dict(sourceSpeciesId=412, speciesId='pret:SPECIES_EGG', inherited=False, transparentIndex=0)
    egg_assets.append(emit('egg-front', 64, 64, indexed(egg_front, egg_palette, (64, 64), True),
        [egg_front, egg_palette, 'src/data/pokemon_graphics/front_pic_coordinates.h'], role='shell', sourceFrame=0,
        sourceFrameCount=1, sourceWidth=24, sourceHeight=24, sourceYOffset=20,
        paletteExpansion='floor(rgb5 * 255 / 31)', **fields))
    need('[SPECIES_EGG]         = gMonIcon_Egg,' in source.text('src/pokemon_icon.c') and
         re.search(r'\[SPECIES_EGG\]\s*=\s*1,', source.text('src/pokemon_icon.c')) and
         'gMonIcon_Egg[] = INCBIN_U8("graphics/pokemon/egg/icon.4bpp")' in source.text('src/graphics.c'), 'Egg icon declaration differs')
    icon_palette = 'graphics/pokemon/icon_palettes/icon_palette_1.pal'
    for frame in range(2):
        egg_assets.append(emit('egg-icon-' + str(frame), 32, 32,
            indexed(egg_icon, icon_palette, (32, 64), False, crop=(0, frame * 32, 32, (frame + 1) * 32)),
            [egg_icon, icon_palette], role='icon', sourceFrame=frame, sourceFrameCount=2, paletteIndex=1,
            paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', **fields))
    need('sEggPalette[] = INCBIN_U16("graphics/pokemon/egg/normal.gbapal")' in daycare, 'Hatch palette binding differs')
    for role, path, prefix, shape, frame_size, stride in [
        ('hatch', 'graphics/misc/egg_hatch.png', 'EggHatch', (32, 128), 32, 16),
        ('shard', 'graphics/misc/egg_shard.png', 'EggShard', (32, 8), 8, 1)]:
        need(f's{prefix}Tiles[] = INCBIN_U8("{path[:-4]}.4bpp")' in daycare, 'Hatch graphics binding differs')
        for frame in range(4):
            anim = re.search(r'sSpriteAnim_' + prefix + str(frame) + r'\[\]\s*=\s*\{(.*?)\};', daycare, re.S)
            need(anim and re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)', anim[1]) == [(str(frame * stride), '5')], 'Hatch animation source frame differs')
            crop = (0, frame * 32, 32, (frame + 1) * 32) if role == 'hatch' else (frame * 8, 0, (frame + 1) * 8, 8)
            egg_assets.append(emit('egg-' + role + '-' + str(frame), frame_size, frame_size,
                indexed(path, egg_palette, shape, True, crop=crop), [path, egg_palette, 'src/daycare.c'],
                role=role, sourceFrame=frame, sourceFrameCount=4, sourceTileOffset=frame * stride,
                sourceDuration=5, paletteExpansion='floor(rgb5 * 255 / 31)', **fields))

    pack = reuse('content/generated/audio/synth/firered.pack')
    cries = reuse('content/generated/audio/synth/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    need('[SPECIES_WYNAUT - HOENN_MON_SPECIES_START] = CRY_WYNAUT,' in source.text('src/data/pokemon/cry_ids.h'), 'Wynaut cry mapping differs')
    wynaut_cry = int(re.search(r'CRY_WYNAUT\s*=\s*(\d+)', source.text('include/constants/hoenn_cries.h'))[1])
    need(wynaut_cry == 258, 'Wynaut source cry constant differs')
    cues, cry_evidence, new_cues = [], [], []
    for name, species in SPECIES:
        cue = dict(id='CRY_' + name.upper() + '_NORMAL', sourceSymbol='SPECIES_' + name.upper(), sourcePlayer=4, priority=10, sourceCounter=None, songAddress=0, species=species, looping=False, cryMode=0)
        if species not in (237, 238):
            cues.append(dict(inherited=False, cue=cue))
            new_cues.append(cue)
        else:
            path = 'content/generated/' + ('audio/cues/trainer-tower' if species == 238 else 'sprites/pokemon/seven-island') + '/synth-manifest.json'
            manifest = json.loads(reuse(path))
            inherited_cue = next(c for c in manifest['cues'] if c['id'] == cue['id'])
            need(inherited_cue == cue, 'Inherited normal cry binding differs')
            cues.append(dict(inherited=True, inheritedManifest=path, cue=cue))
        row = cry_rows[species]
        expected_index = wynaut_cry if species == 360 else species - 1
        need(cry_table[row[1]] == name and row[1] == expected_index, 'Species cry table differs')
        cry_evidence.append(dict(species=species, cryIndex=row[1], sampleCount=row[2], payloadBytes=row[3], guardStored=bool(row[4])))
    outputs['synth-manifest.json'] = runtime_manifest_bytes(dict(schemaVersion=1, cues=new_cues))
    outputs['dex-entries.json'] = canonical(dict(schemaVersion=1,   entries=entries))
    lock = source.lock()
    lock['inheritedInputs'] = [inherited[p] for p in sorted(inherited)]
    lb = canonical(lock)
    manifest = dict(schemaVersion=1,
                      newOwnedSpecies=[s for _, s in SPECIES],
                    sprites=sprites, pokemonIcons=icons, footprints=footprints, eggAssets=egg_assets,
                    dexEntriesPath=BASE + 'dex-entries.json', moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry),
                    audio=dict(newCueCount=6, inheritedCueCount=2, inheritedSampleCount=8, cues=cues, normalCries=cry_evidence, ),
                    limits=['Eight inherited Smoochum and Hitmontop battler variants and two cue bindings are reused unchanged.',
                            'Only twenty-four battler variants, eight frame-zero icons, eight footprints, eight Dex bindings and six cry cues are added.',
                            'All sixteen move geometry rows and eight source cry samples are inherited; no dedicated animation or audio package is added.',
                            'Smoochum adds a portrait alias to its inherited Trainer Tower front battler.',
                            'Eleven Egg-only shell, icon, hatch and shard frames are provided; the original hatch scene executor is not implemented here.',
                            'Native visual and audible output remain unverified.'],
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
            need(generated_output_matches(p, b), 'Regeneration differs: ' + str(p))
        else:
            p.parent.mkdir(parents=True, exist_ok=True)
            p.write_bytes(b)
    print(json.dumps(dict(passed=True, check=check, sourceFiles=len(source.entries), newRawFiles=len(source.entries) - len(SHARED_RAW),  inheritedInputs=len(inherited),
                         newEggAssets=11, newFootprints=8, inheritedFootprints=0, dexEntries=8, newBattleSprites=24, inheritedBattleSprites=8, newPokemonIcons=8, inheritedPokemonIcons=0, newAudioCues=6, inheritedAudioCues=2,
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
