"""Source-driven legacy species-art manifests; region recipes only select species.

Source IDs, image symbols, icon palettes, coordinates and dex text come from raw
C tables. No metadata importer or historical stage inventory is executed.
"""
import io
import json
import re
from pathlib import Path
from PIL import Image
from image_io import png
from pokemon_import import battle_palette, footprint_indices
from project_paths import PROJECT_ROOT
from presentation_source import safe, need
from presentation_pixels import PixelReader



def match(pattern, text):
    result = re.search(pattern, text, re.S | re.M)
    if result is None:
        raise ValueError('Missing source binding: ' + pattern)
    return result


def build(root, profile, raw_root=None):
    recipes = json.loads((PROJECT_ROOT / 'scripts/recipes/presentation/species-art.json').read_bytes())
    recipe = recipes[profile]
    raw = raw_root or Path(root) / 'content/raw/pokefirered/037335f4'
    cache = {}
    def read(path):
        if path not in cache:
            cache[path] = safe(raw, path).read_bytes()
        return cache[path]
    def text(path):
        return read(path).decode()
    def indices(path, shape):
        with Image.open(io.BytesIO(read(path))) as image:
            if image.mode != 'P' or image.size != shape:
                raise ValueError('Indexed image dimensions differ: ' + path)
            values = image.tobytes()
        if recipe.get('reduceIndices'):
            return bytes(i & 15 for i in values)
        if max(values) > 15:
            raise ValueError('Source palette index exceeds four bits: ' + path)
        return values
    def rgba(values, colors):
        return bytes(c for i in values for c in (*colors[i], 0 if i == 0 else 255))
    output, sprites, icons, footprints, dex = {}, [], [], [], []
    def add(stem, width, height, pixels):
        output[stem + '.png'] = png(width, height, pixels)
        return dict(texturePath=stem + '.png', width=width, height=height)
    graphics = text('src/data/graphics/pokemon.h')
    def graphic(symbol, suffix):
        path = match(r'\b' + re.escape(symbol) + r'\[\]\s*=\s*INCBIN_U(?:8|32)\("([^"]+)"\)', graphics)[1]
        if not path.endswith(suffix):
            raise ValueError('Unsupported source graphic encoding: ' + path)
        return path[:-len(suffix)] + ('.pal' if 'gbapal' in suffix else '.png')
    for selected in recipe['species']:
        symbol = 'SPECIES_' + selected
        number = int(match(r'^#define ' + symbol + r'\s+(\d+)\s*$', text('include/constants/species.h'))[1])
        stem = selected.lower()
        elevation_match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+)', text('src/data/pokemon_graphics/enemy_mon_elevation.h'))
        elevation = int(elevation_match[1]) if elevation_match else 0
        for side in ('front', 'back'):
            coordinates = match(r'\[' + symbol + r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)', text(f'src/data/pokemon_graphics/{side}_pic_coordinates.h'))
            width, height, yoff = map(int, coordinates.groups())
            imagepath = f'graphics/pokemon/{stem}/{side}.png'
            if imagepath[:-4] + '.4bpp.lz' not in graphics:
                raise ValueError('Sprite binding missing: ' + imagepath)
            values = indices(imagepath, (64, 64))
            opaque = [(i % 64, i // 64) for i, v in enumerate(values) if v]
            if not opaque:
                raise ValueError('Empty battle sprite: ' + imagepath)
            xs, ys = zip(*opaque)
            x, y = (144, 8 + yoff - elevation) if side == 'front' else (40, 48 + yoff)
            for variant in ('normal', 'shiny'):
                palettepath = f'graphics/pokemon/{stem}/{variant}.pal'
                colors, words = battle_palette(read(palettepath))
                asset = add(f'{stem}-{side}-{variant}', 64, 64, rgba(values, colors))
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=number, side=side,
                    paletteVariant=variant, sourceImagePath=imagepath, sourcePalettePath=palettepath,
                    frameCount=1, sourceFrameIndex=0, frameWidth=64, frameHeight=64,
                    sourceWidth=width, sourceHeight=height, sourceYOffset=yoff, sourceEnemyElevation=elevation,
                    singleBattleBaseCenter=dict(x=176 if side=='front' else 72, y=40 if side=='front' else 80),
                    singleBattleCenter=dict(x=x+32, y=y+32), singleBattleTopLeft=dict(x=x, y=y),
                    alphaBounds=dict(x=min(xs), y=min(ys), width=max(xs)-min(xs)+1, height=max(ys)-min(ys)+1),
                    transparentIndex=0, paletteRgb555=words, paletteExpansion='floor(rgb5 * 255 / 31)', flipH=False, **asset))
        icontext = text('src/pokemon_icon.c')
        palette_index = int(match(r'\[' + symbol + r'\]\s*=\s*(\d+),', icontext)[1])
        icon_symbol = match(r'\[' + symbol + r'\]\s*=\s*(gMonIcon_\w+),', icontext)[1]
        iconpath = graphic(icon_symbol, '.4bpp')
        colors, words = battle_palette(read(f'graphics/pokemon/icon_palettes/icon_palette_{palette_index}.pal'))
        asset = add('icons/' + stem, 32, 32, rgba(indices(iconpath, (32,64))[:1024], colors))
        icons.append(dict(speciesId='pret:'+symbol, sourceSpeciesId=number, sourceFrameIndex=0,
            sourceFrameCount=2, paletteIndex=palette_index, paletteVariant='shared-normal-shiny', transparentIndex=0, paletteRgb555=words, **asset))
        footprint_symbol = match(r'\[' + symbol + r'\]\s*=\s*(gMonFootprint_\w+),', text('src/data/pokemon_graphics/footprint_table.h'))[1]
        footprintpath = graphic(footprint_symbol, '.1bpp')
        lines = text('graphics/pokedex/kanto_dex_bgpals.pal').splitlines()
        if lines[:3] != ['JASC-PAL', '0100', '256'] or len(lines) != 259:
            raise ValueError('Kanto dex palette differs')
        colors = [tuple((int(c)>>3)*255//31 for c in line.split()) for line in lines[3:19]]
        asset = add(stem+'-footprint', 16, 16, rgba(footprint_indices(read(footprintpath)), colors))
        footprints.append(dict(sourceSpeciesId=number, **asset))
        declaration = match(r'\[NATIONAL_DEX_' + selected + r'\]\s*=\s*\{(.*?)\n    \}', text('src/data/pokemon/pokedex_entries.h'))[1]
        fields = dict(re.findall(r'\.(\w+) = (.*?),\n', declaration))
        description = match(r'const u8 '+re.escape(fields['description'])+r'\[\] = _\(\n(.*?)\);', text('src/data/pokemon/pokedex_text_fr.h'))[1]
        name = match(r'\['+symbol+r'\] = _\("([^"]+)"\)', text('src/data/text/species_names.h'))[1]
        dex.append(dict(sourceSpeciesId=number, name=name, category=match(r'_\("([^"]+)"\)', fields['categoryName'])[1],
            heightDecimeters=int(fields['height']), weightHectograms=int(fields['weight']),
            description=''.join(json.loads(x) for x in re.findall(r'"(?:[^"\\]|\\.)*"', description))))
    manifest = dict(schemaVersion=1, sprites=sprites, icons=icons, footprints=footprints,
        dex=dex[0] if recipe.get('singleDex') else dex, outputs=[dict(path=p) for p in sorted(output)])
    output['manifest.json'] = (json.dumps(manifest, sort_keys=True, separators=(',', ':'), ensure_ascii=False)+'\n').encode()
    return output


def selected_species_art(source, selection, footprint_species, emit):
    """Build ordinary 64px battle art, frame-zero icons and selected footprints.

    Species and owned-footprint selections belong to the caller. Palette expansion
    stays floor RGB5 here; trainer/UI bit replication is a separate conversion.
    """
    pixels = PixelReader(source)
    palette, indexed = pixels.palette, pixels.indexed
    sprites, icons, footprints = [], [], []
    graphics = source.text('src/data/graphics/pokemon.h')
    constants = source.text('include/constants/species.h')
    for name, species in selection:
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
                     sourceCoordinatePath=coordinates, paletteExpansion='floor(rgb5 * 255 / 31)',
                    transparentIndex=0, **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
        icon_table = source.text('src/pokemon_icon.c')
        match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
        need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
        pal_index = int(match[1])
        image, pal = f'graphics/pokemon/{stem}/icon.png', f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need(f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics, 'Species icon image binding differs')
        indices, rgba = indexed(image, pal, (32, 64), floor=True, first_frame=True)
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, paletteIndex=pal_index, sourceFrameIndex=0,
                          sourceFrameCount=2,  **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
        if (name, species) not in footprint_species:
            continue
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
        indices = footprint_indices(source.read(image))
        colors = palette(pal, floor=True)
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))

    return sprites, icons, footprints
