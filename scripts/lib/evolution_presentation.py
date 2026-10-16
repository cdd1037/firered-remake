#!/usr/bin/env python3
"""Shared source-to-presentation compiler for evolution admission selections."""
from runtime_json import runtime_json_bytes
from generated_output_compare import generated_output_matches
import io
import json
import re
import struct
from pathlib import Path
from PIL import Image

from project_paths import PROJECT_ROOT
from pokemon_import_io import canonical, read_source, safe, write_files, unique_pairs
from image_io import png

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
RAW = 'content/raw/pokefirered/037335f4/'
RECIPE = PROJECT_ROOT / 'scripts/recipes/pokemon/evolution-presentation.json'


def load_recipe(path=RECIPE):
    recipe = json.loads(safe(path).read_bytes(), object_pairs_hook=unique_pairs)
    need(recipe.get('schemaVersion') == 1 and recipe.get('selections') and recipe.get('catalogs'), 'Invalid evolution presentation recipe')
    for selection in recipe['selections'].values():
        need(selection['outputSchema'] in ('normalized', 'inherited'), 'Unknown output schema')
        names = [row['name'] for row in selection['species']]
        need(names and len(names) == len(set(names)), 'Empty or duplicate species')
        for row in selection['species']:
            need(re.fullmatch(r'[A-Za-z0-9]+', row['name']) and row['catalog'] in recipe['catalogs'], 'Unknown source catalog or species')
            if 'icon' in row:
                need(row['icon']['palette'] in ('floor', 'replicate'), 'Unknown palette expansion')
    return recipe


def need(condition, message):
    if not condition:
        raise ValueError(message)


def build(selection, target, dependency, reference=None, check=False, recipe_path=RECIPE):
    recipe = load_recipe(recipe_path)
    need(selection in recipe['selections'], 'Unknown evolution presentation selection')
    options = recipe['selections'][selection]
    normalized = options['outputSchema'] == 'normalized'
    base = 'content/generated/sprites/pokemon/' + selection + '/'
    source_data = {}

    def read(path):
        if path not in source_data:
            source_data[path] = read_source(dependency / RAW, path, commit=COMMIT, repository=reference)
        return source_data[path]

    def text(path):
        return read(path).decode('utf-8')

    species = []
    for row in options['species']:
        matches = re.findall(r'^#define SPECIES_' + row['name'].upper() + r'[ \t]+(\d+)[ \t]*$', text('include/constants/species.h'), re.M)
        need(len(matches) == 1, 'Missing or ambiguous source species')
        species.append((row['name'], int(matches[0])))
    inherited, outputs = {}, {}

    def reuse(path):
        data = safe(dependency, path).read_bytes()
        inherited[path] = dict(path=path)
        return data

    def palette(path, floor):
        lines = text(path).splitlines()
        need(lines[:2] == ['JASC-PAL', '0100'] and len(lines) == int(lines[2]) + 3, 'Source palette differs')
        return [tuple((int(c) >> 3) * 255 // 31 if floor else ((int(c) >> 3) << 3) | ((int(c) >> 3) >> 2)
                      for c in line.split()) for line in lines[3:]]

    def indexed(image, pal, shape, floor, first_frame=False):
        im = Image.open(io.BytesIO(read(image)))
        need(im.mode == 'P' and im.size == shape and max(im.tobytes()) < 16, 'Source indexed image differs: ' + image)
        indices = im.tobytes()[:1024] if first_frame else im.tobytes()
        colors = palette(pal, floor)
        return bytes(v for index in indices for v in (*colors[index], 255 if index else 0))

    def emit(identity, width, height, rgba, **fields):
        outputs[identity + '.png'] = png(width, height, rgba)
        return dict(id=identity, width=width, height=height, texturePath=base + identity + '.png',  **fields)

    dex = json.loads(reuse('content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json'))
    catalogs = {row['catalog']: recipe['catalogs'][row['catalog']] for row in options['species']}
    catalog_data = {key: json.loads(reuse(path)) for key, path in catalogs.items()}
    geometry_path = 'content/generated/battle/art/campaign-hit-art/manifest.json'
    inherited_geometry = json.loads(reuse(geometry_path))['geometry']

    def inherited_asset(catalog, collection, number, **matches):
        original = next(row for row in catalog_data[catalog][collection] if row['sourceSpeciesId'] == number and all(row.get(k) == v for k, v in matches.items()))
        row = dict(original)
        path = row['texturePath']
        if not path.startswith('content/'):
            path = (Path(catalogs[catalog]).parent / path).as_posix()
        reuse(path)
        row['texturePath'] = path
        row.update(inherited=True)
        row.setdefault('speciesId', 'pret:SPECIES_' + dict((n, s.upper()) for s, n in species)[number])
        if 'singleBattleTopLeft' in row:
            row.update(row['singleBattleTopLeft'])
        return row

    def exact_pixels(row, expected):

        png = reuse(row['texturePath'])
        need(Image.open(io.BytesIO(png)).convert('RGBA').tobytes() == expected, 'Inherited PNG differs')

    species_constants = text('include/constants/species.h')
    graphics = text('src/data/graphics/pokemon.h')
    sprites, icons, footprints, entries, geometry = [], [], [], [], []
    for (name, number), policy in zip(species, options['species']):
        stem, symbol = name.lower(), 'SPECIES_' + name.upper()
        catalog = policy['catalog']
        need(re.search(r'^#define ' + symbol + r'\s+' + str(number) + r'\s*$', species_constants, re.M), 'Species identity differs')
        for side in ['front', 'back']:
            coordinate_path = 'src/data/pokemon_graphics/' + side + '_pic_coordinates.h'
            match = re.search(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', text(coordinate_path))
            need(match, 'Source battler coordinates missing')
            width, height, y = map(int, match.groups())
            elevation = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
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
                    if not normalized or key in row:
                        need(row[key] == expected, 'Inherited source geometry differs')
                if normalized:
                    # Normalize older catalogs to effective back elevation zero.
                    row.update(id=stem + '-' + side + '-' + variant, sourceYOffset=y,
                               sourceEnemyElevation=elevation if side == 'front' else 0, sourceWidth=width, sourceHeight=height)
                else:
                    need(row['sourceEnemyElevation'] == elevation, 'Inherited elevation differs')
                exact_pixels(row, rgba)
                sprites.append(row)
            geo = dict(species=number, side=side, yOffset=y, elevation=elevation if side == 'front' else 0, sourceWidth=width, sourceHeight=height)
            need(geo in inherited_geometry, 'Shared move geometry differs')
            geometry.append(geo)
        pal_index = int(re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', text('src/pokemon_icon.c'))[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need(re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', text('src/pokemon_icon.c')) and
             f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Source icon binding differs')
        if 'icon' in policy:
            icon_policy = policy['icon']
            icon = inherited_asset(catalog, icon_policy['collection'], number)
            exact_pixels(icon, indexed(image, pal, (32, 64), icon_policy['palette'] == 'floor', True))
            icon.update(sourceFrame=0, paletteIndex=pal_index,
                        runtimePaletteExpansion='(rgb5 << 3) | (rgb5 >> 2)' if icon_policy['palette'] == 'floor' else 'stored')
            if normalized:
                icon.update(id=stem + '-icon', sourceFrameCount=2)
        else:
            rgba = indexed(image, pal, (32, 64), False, True)
            icon = emit(stem + '-icon', 32, 32, rgba, sourceSpeciesId=number,
                        speciesId='pret:' + symbol, paletteIndex=pal_index, sourceFrame=0, sourceFrameCount=2,
                        paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', transparentIndex=0, inherited=False)
        icons.append(icon)
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in text('src/data/pokemon_graphics/footprint_table.h'), 'Footprint binding differs')
        im = Image.open(io.BytesIO(read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source shape differs')
        need(f'gMonFootprint_{name}[] = INCBIN_U8("{image[:-4]}.1bpp")' in graphics, 'Source footprint graphics binding differs')
        colors = palette(pal, True)
        indices = [1 if v == 0 else 0 for v in im.get_flattened_data()]
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        if 'footprint' in policy:
            foot = inherited_asset(catalog, policy['footprint'], number)
            exact_pixels(foot, rgba)
        else:
            foot = emit(stem + '-footprint', 16, 16, rgba, sourceSpeciesId=number, speciesId='pret:' + symbol, inherited=False)
        if normalized:
            foot['id'] = stem + '-footprint'
        footprints.append(foot)
        entry = next(e for e in dex['entries'] if e['sourceSpeciesId'] == number)
        need(entry['name'] == name.upper(), 'Inherited species name differs')
        declaration = re.search(r'\[NATIONAL_DEX_' + name.upper() + r'\]\s*=\s*\{(.*?)\n\s*\}', text('src/data/pokemon/pokedex_entries.h'), re.S)[1]
        need(re.search(r'\.categoryName\s*=\s*_\("' + entry['category'] + r'"\)', declaration) and
             int(re.search(r'\.height\s*=\s*(\d+)', declaration)[1]) == entry['heightDecimeters'] and
             int(re.search(r'\.weight\s*=\s*(\d+)', declaration)[1]) == entry['weightHectograms'], 'Dex category/measures differ')
        description = re.search(r'const u8 g' + name + r'PokedexText\[\]\s*=\s*_\((.*?)\);', text('src/data/pokemon/pokedex_text_fr.h'), re.S)[1]
        description_text = ''.join(json.loads('"' + part + '"') for part in re.findall(r'"([^"\n]*)"', description))
        need(description_text == entry['description'], 'Dex description differs')
        front = next(row for row in sprites if (row['sourceSpeciesId'], row['side'], row['paletteVariant']) == (number, 'front', 'normal'))
        texture = lambda row: {k: row[k] for k in ['texturePath',  'width', 'height']}
        entries.append(dict(entry, portrait=texture(front), footprint=texture(foot)))

    pack = reuse('content/generated/audio/synth/firered.pack')
    cries = reuse('content/generated/audio/synth/normal-cries.bin')
    need(len(pack) == 141541028 - 138975592 and len(cries) == 6188, 'Original audio pack identity differs')
    cry_table = re.findall(r'^\s*cry Cry_(\w+)\s*$', text('sound/cry_tables.inc').split('gCryTable::', 1)[1].split('gCryTable_Reverse::', 1)[0], re.M)
    cry_rows = {struct.unpack_from('<H', cries, offset)[0]: struct.unpack_from('<HHIIB3x', cries, offset) for offset in range(12, len(cries), 16)}
    priority = int(re.search(r'^#define CRY_PRIORITY_NORMAL\s+(\d+)', text('include/constants/sound.h'), re.M)[1])
    need(priority == 10, 'Normal cry priority differs')
    cues, cry_evidence = [], []
    for (name, number), policy in zip(species, options['species']):
        path = policy['cryManifest']
        manifest = json.loads(reuse(path))
        cue = next(c for c in manifest['cues'] if c['id'] == 'CRY_' + name.upper() + '_NORMAL')
        need(cue['species'] == number and cue['sourcePlayer'] == 4 and cue['priority'] == priority and cue.get('cryMode', 0) == 0 and not cue['looping'] and cue['sourceSymbol'] == 'SPECIES_' + name.upper(), 'Inherited normal cry binding differs')
        row = cry_rows[number]
        need(cry_table[number - 1] == name and row[1] == number - 1, 'Species cry table differs')
        cues.append(dict(inheritedManifest=path, cue=cue))
        cry_evidence.append(dict(species=number, cryIndex=row[1], sampleCount=row[2], payloadBytes=row[3], guardStored=bool(row[4])))
    outputs['dex-entries.json'] = canonical(dict(schemaVersion=1, entries=entries))
    manifest = dict(schemaVersion=1, newOwnedSpecies=[s for _, s in species], inheritedSprites=sprites, pokemonIcons=icons, footprints=footprints, dexEntriesPath=base + 'dex-entries.json', moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry), audio=dict(newCueCount=0, inheritedCueCount=len(species), cues=cues, normalCries=cry_evidence), limits=options['limits'], outputs=[dict(path=p) for p, b in sorted(outputs.items())])
    mb = runtime_json_bytes(manifest, sort_keys=True)
    files = {base + 'manifest.json': mb, **{base + path: data for path, data in outputs.items()}}
    if check:
        for path, data in files.items():
            need(generated_output_matches(safe(target, path), data), 'Regeneration differs: ' + path)
    else:
        write_files(target, files)
    return dict(checked=check, sourceFiles=len(source_data), inheritedInputs=len(inherited),
                species=len(species), outputFiles=len(files))
