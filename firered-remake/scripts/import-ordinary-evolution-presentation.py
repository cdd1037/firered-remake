#!/usr/bin/env python3
"""Import four ordinary-evolution owned presentations; reuse every existing battler and cry."""
import argparse
import io
import json
import re
import os
import subprocess
import struct
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = 'ordinary-evolution-presentation'
BASE = 'content/generated/' + PACKAGE + '/'
RAW = 'content/raw/pokefirered/037335f4/'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-ordinary-evolution-presentation-v1'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
SPECIES = [('Rhydon', 112), ('Slowbro', 80), ('Crobat', 169), ('Blissey', 242)]
CATALOG = {112: 'league', 80: 'saffron', 169: 'rematch', 242: 'tower'}


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(',', ':')) + '\n').encode()


def need(condition, message):
    if not condition:
        raise ValueError(message)


def safe(path):
    need(not any(p.is_symlink() for p in [path, *path.parents]), 'Symlink rejected: ' + str(path))


# Shared raw inputs are authenticated by this package's lock and the pinned Git blob.
SHARED_RAW = {path: 'content/raw/pokefirered/037335f4/' + path for path in ['include/constants/species.h', 'src/data/graphics/pokemon.h', 'src/data/pokemon_graphics/front_pic_coordinates.h', 'src/data/pokemon_graphics/back_pic_coordinates.h', 'src/data/pokemon_graphics/enemy_mon_elevation.h', 'src/pokemon_icon.c', 'graphics/pokemon/icon_palettes/icon_palette_0.pal', 'graphics/pokedex/kanto_dex_bgpals.pal', 'src/data/pokemon_graphics/footprint_table.h', 'src/data/pokemon/pokedex_entries.h', 'src/data/pokemon/pokedex_text_fr.h', 'sound/cry_tables.inc', 'include/constants/sound.h']}
SHARED_RAW['graphics/pokemon/icon_palettes/icon_palette_1.pal'] = 'content/raw/pokefirered/037335f4/graphics/pokemon/icon_palettes/icon_palette_1.pal'
SHARED_RAW['graphics/pokemon/icon_palettes/icon_palette_2.pal'] = 'content/raw/pokefirered/037335f4/graphics/pokemon/icon_palettes/icon_palette_2.pal'
for name, number in SPECIES:
    for asset in ['front.png', 'back.png', 'normal.pal', 'shiny.pal'] + (['icon.png'] if number in (112, 80) else []) + (['footprint.png'] if number == 80 else []):
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

    def indexed(image, pal, shape, floor, first_frame=False):
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    def emit(identity, width, height, rgba, paths, **fields):
        buf = io.BytesIO()
        Image.frombytes('RGBA', (width, height), rgba).save(buf, format='PNG', optimize=False, compress_level=9)
        outputs[identity + '.png'] = buf.getvalue()
        return dict(id=identity, width=width, height=height, texturePath=BASE + identity + '.png', sourcePaths=paths, **fields)

    dex = json.loads(reuse('content/generated/pokedex-menu/pokedex-menu.json'))
    catalogs = {
        'league': 'content/generated/pokemon-league-presentation/manifest.json',
        'saffron': 'content/generated/saffron-gym-art/manifest.json',
        'rematch': 'content/generated/league-rematch-presentation/manifest.json',
        'tower': 'content/generated/trainer-tower-presentation/catalog.json'}
    catalog_data = {}
    for key, path in catalogs.items():
        data = reuse(path)
        catalog_data[key] = json.loads(data)
    output_manifests = dict(catalogs, tower='content/generated/trainer-tower-presentation/manifest.json')
    output_data = {key: json.loads(reuse(path)) for key, path in output_manifests.items()}
    geometry_path = 'content/generated/campaign-hit-art/manifest.json'
    inherited_geometry = json.loads(reuse(geometry_path))['geometry']

    def inherited_asset(catalog, collection, number, **matches):
        original = next(row for row in catalog_data[catalog][collection] if row['sourceSpeciesId'] == number and all(row.get(k) == v for k, v in matches.items()))
        row = dict(original)
        for field in ['texturePath']:
            path = row[field]
            if not path.startswith('content/'):
                path = str(Path(catalogs[catalog]).parent / path)
            data = reuse(path)
            # Saffron's legacy output inventory has a different schema; its row hashes are pinned above.
            if catalog != 'saffron':
                relative = str(Path(path).relative_to(Path(catalogs[catalog]).parent))
            row[field] = path
        row.update(inherited=True, inheritedManifest=catalogs[catalog], inheritedCollection=collection, inheritedAsset=original)
        row.setdefault('speciesId', 'pret:SPECIES_' + dict((n, s.upper()) for s, n in SPECIES)[number])
        if 'singleBattleTopLeft' in row:
            row.update(row['singleBattleTopLeft'])
        return row

    def exact_pixels(row, expected):

        png = reuse(row['texturePath'])
        need(Image.open(io.BytesIO(png)).convert('RGBA').tobytes() == expected, 'Inherited PNG differs')

    species_constants = source.text('include/constants/species.h')
    graphics = source.text('src/data/graphics/pokemon.h')
    sprites, icons, footprints, entries, geometry = [], [], [], [], []
    for name, number in SPECIES:
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        catalog = CATALOG[number]
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
                row = inherited_asset(catalog, 'sprites', number, side=side, paletteVariant=variant)
                need((row['width'], row['height'], row['x'], row['y']) ==
                     (64, 64, 144 if side == 'front' else 40, 8 + y - elevation if side == 'front' else 48 + y), 'Inherited placement differs')
                for key, expected in [('sourceYOffset', y), ('sourceWidth', width), ('sourceHeight', height)]:
                    if key in row:
                        need(row[key] == expected, 'Inherited source geometry differs')
                # Crobat's earlier catalog stores enemy elevation on both sides; keep its exact original row
                # in inheritedAsset and bind effective back elevation zero to the shared move geometry.
                row.update(id=stem + '-' + side + '-' + variant, sourceYOffset=y,
                           sourceEnemyElevation=elevation if side == 'front' else 0, sourceWidth=width, sourceHeight=height)
                exact_pixels(row, rgba)
                sprites.append(row)
            geo = dict(species=number, side=side, yOffset=y, elevation=elevation if side == 'front' else 0, sourceWidth=width, sourceHeight=height)
            need(geo in inherited_geometry, 'Shared move geometry differs')
            geometry.append(geo)
        pal_index = int(re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', source.text('src/pokemon_icon.c'))[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need(re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', source.text('src/pokemon_icon.c')) and
             f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Source icon binding differs')
        if number in (112, 80):
            icon = inherited_asset(catalog, 'icons' if catalog == 'saffron' else 'pokemonIcons', number)
            exact_pixels(icon, indexed(image, pal, (32, 64), number == 80, True))
            icon.update(id=stem + '-icon', sourceFrame=0, sourceFrameCount=2, paletteIndex=pal_index, runtimePaletteExpansion='(rgb5 << 3) | (rgb5 >> 2)' if number == 80 else 'stored')
        else:
            rgba = indexed(image, pal, (32, 64), False, True)
            icon = emit(stem + '-icon', 32, 32, rgba, [image, pal], sourceSpeciesId=number,
                        speciesId='pret:' + symbol, paletteIndex=pal_index, sourceFrame=0, sourceFrameCount=2,
                        paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', transparentIndex=0, inherited=False)
        icons.append(icon)
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source shape differs')
        need(f'gMonFootprint_{name}[] = INCBIN_U8("{image[:-4]}.1bpp")' in graphics, 'Source footprint graphics binding differs')
        colors = palette(pal, True)
        indices = [1 if v == 0 else 0 for v in im.get_flattened_data()]
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        if number == 80:
            foot = inherited_asset('saffron', 'footprints', number)
            exact_pixels(foot, rgba)
        else:
            foot = emit(stem + '-footprint', 16, 16, rgba, [image, pal], sourceSpeciesId=number, speciesId='pret:' + symbol, inherited=False)
        foot['id'] = stem + '-footprint'
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

    pack = reuse('content/generated/synth-audio/firered.pack')
    cries = reuse('content/generated/synth-audio/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', source.text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', source.text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    cues, cry_evidence = [], []
    for name, species in SPECIES:
        path = 'content/generated/' + {112: 'pokemon-league-presentation/synth-manifest.json', 80: 'saffron-gym-synth-audio/manifest.json', 169: 'league-rematch-presentation/synth-manifest.json', 242: 'trainer-tower-audio/synth-manifest.json'}[species]
        manifest = json.loads(reuse(path))
        cue = next(c for c in manifest['cues'] if c['id'] == 'CRY_' + name.upper() + '_NORMAL')
        need(cue['species'] == species and cue['sourcePlayer'] == 4 and cue['priority'] == priority and cue.get('cryMode', 0) == 0 and not cue['looping'] and cue['sourceCommit'] == COMMIT and cue['sourceSymbol'] == 'SPECIES_' + name.upper(), 'Inherited normal cry binding differs')
        row = cry_rows[species]
        need(cry_table[species - 1] == name and row[1] == species - 1, 'Species cry table differs')
        cues.append(dict(inheritedManifest=path, cue=cue))
        cry_evidence.append(dict(species=species, cryIndex=row[1], sampleCount=row[2], payloadBytes=row[3], guardStored=bool(row[4])))
    outputs['dex-entries.json'] = canonical(dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, entries=entries))
    lock = source.lock()
    lock['inheritedInputs'] = [inherited[p] for p in sorted(inherited)]
    lb = canonical(lock)
    manifest = dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT,
                    inputs=lock['files'], inheritedInputs=lock['inheritedInputs'], newOwnedSpecies=[s for _, s in SPECIES],
                    inheritedSprites=sprites, pokemonIcons=icons, footprints=footprints,
                    dexEntriesPath=BASE + 'dex-entries.json', moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry),
                    audio=dict(newCueCount=0, inheritedCueCount=4, cues=cues, normalCries=cry_evidence, originalPackUnchanged=True, physicalAudioVerified=False),
                    limits=['All sixteen battlers, two icons, one footprint and four normal cries reuse existing resources unchanged.',
                            'Only two frame-zero party icons, three footprints and four Dex bindings are added.',
                            'No new move animation coverage; native visual and audible output remain unverified.'],
                    outputs=[dict(path=p) for p, b in sorted(outputs.items())])
    mb = canonical(manifest)
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
                         newFootprints=3, inheritedFootprints=1, dexEntries=4, newBattleSprites=0, inheritedBattleSprites=16, newPokemonIcons=2, inheritedPokemonIcons=2, newAudioCues=0, inheritedAudioCues=4,
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
