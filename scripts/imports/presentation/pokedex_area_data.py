"""Source-only Pokédex area projection imported into the existing menu document.

The approved fishing correction scans FISH_WILD_COUNT (10), not the original
LAND_WILD_COUNT (12) over-read. This importer never interprets bytes outside a
source encounter table. Static/gift encounters are not added to wild areas.
"""
import io
import json
from PIL import Image
import re

_REGION_FILES = ('kanto', 'one_island', 'two_island', 'three_island', 'four_island',
                 'five_island', 'six_island', 'seven_island')
AREA_PATHS = frozenset(('src/wild_pokemon_area.c', 'src/pokedex_area_markers.c',
    'src/field_specials.c', 'src/roamer.c', 'src/pokedex_screen.c', 'include/wild_encounter.h',
    'src/data/wild_encounters.json', 'data/maps/map_groups.json', 'graphics_file_rules.mk',
    'tools/gbagfx/main.c', 'tools/gbagfx/gfx.c', 'tools/gbagfx/convert_png.c',
    'graphics/pokedex/kanto_dex_bgpals.pal', 'graphics/pokedex/kanto_dex_bgtiles.png',
    'graphics/pokedex/blit_wide_ellipse.png')) | frozenset(
    'graphics/pokedex/map_' + name + '.png' for name in _REGION_FILES) | frozenset(
    'graphics/pokedex/area_markers/marker_' + str(i) + '.png' for i in range(7))


def _json(sources, path):
    return json.loads(sources[path].decode('utf-8'))


def _wild_rows(sources):
    groups = _json(sources, 'src/data/wild_encounters.json')['wild_encounter_groups']
    group, = [g for g in groups if g['label'] == 'gWildMonHeaders' and g['for_maps']]
    rows = [r for r in group['encounters'] if r['base_label'].endswith('_FireRed')]
    if not rows:
        raise ValueError('Missing FireRed wild encounter headers')
    return rows


def area_map_paths(sources):
    """Discover input headers from the actual source groups and FireRed encounters."""
    groups = _json(sources, 'data/maps/map_groups.json')
    names = [name for group in groups['group_order'] for name in groups[group]]
    # IDs and folder names differ only in case/underscores. Reject ambiguous matches;
    # the loaded map.json's exact id is subsequently checked against the header.
    compact = {}
    for name in names:
        key = name.replace('_', '').upper()
        if key in compact:
            raise ValueError('Ambiguous source map name: ' + name)
        compact[key] = name
    return frozenset('data/maps/' + compact[row['map'].removeprefix('MAP_').replace('_', '')] + '/map.json'
                     for row in _wild_rows(sources))


def _body(text, symbol):
    match = re.search(r'\b' + re.escape(symbol) + r'\s*(?:\[[^]]*\])*\s*=\s*\{(.*?)\n\};', text, re.S)
    if match is None:
        raise ValueError('Missing source area definition: ' + symbol)
    return re.sub(r'//[^\n]*|/\*.*?\*/', '', match[1], flags=re.S)


def build_areas(sources, species, codec):
    """Return (area definition, PNG outputs, catalog assets); no reads outside sources.

    codec is the already-used capture-dex importer, whose legacy helpers perform
    the same indexed-PNG and 5-bit palette conversion as the existing Pokédex.
    """
    text = {p: sources[p].decode('utf-8') for p in AREA_PATHS if p.endswith(('.c', '.h'))}
    screen = text['src/pokedex_screen.c']
    wild = text['src/wild_pokemon_area.c']
    marker_source = text['src/pokedex_area_markers.c']
    field = text['src/field_specials.c']
    for needle in ('if (GetRoamerIndex(species) >= 0)', 'alteringCaveNum >= NUM_ALTERING_CAVE_TABLES',
                   'alteringCaveNum != alteringCaveCount - 1', 'IsSpeciesInEncounterTable(data->fishingMonsInfo, species, FISH_WILD_COUNT)'):
        if needle not in wild:
            raise ValueError('Source area behavior changed: ' + needle)
    if 'if (!ROAMER->active)\n        return MAPSEC_NONE;' not in text['src/roamer.c']:
        raise ValueError('Source inactive roamer gate changed')
    for needle in ('CreateSprite(&spriteTemplate, 104, y + 32, 0)', 'ST_OAM_OBJ_WINDOW',
                   'BLDALPHA_BLEND(12, 8)', 'FillBgTilemapBufferRect_Palette0(1, 0x00F, 0, 0, 30, 20)'):
        if needle not in marker_source:
            raise ValueError('Source marker compositing changed: ' + needle)
    for needle in ('kantoMapVoff = 4;', 'for (i = 3; i < 7; i++)', 'kantoMapVoff = 0;',
                   'sBlitTiles_WideEllipse, 0, 0, 88, 16, 4, 28, 88, 16'):
        if needle not in screen:
            raise ValueError('Source area map layout changed: ' + needle)
    counts = {key: int(value) for key, value in re.findall(r'^#define (\w+)\s+(\d+)\s*$', text['include/wild_encounter.h'], re.M)}
    table_fields = {'land_mons': 'LAND_WILD_COUNT', 'water_mons': 'WATER_WILD_COUNT',
                    'fishing_mons': 'FISH_WILD_COUNT', 'rock_smash_mons': 'ROCK_WILD_COUNT'}
    if counts['FISH_WILD_COUNT'] != 10:
        raise ValueError('Approved fishing table size changed')
    image_codec = codec.legacy.codec
    palette = codec.legacy.kanto_palette(sources['graphics/pokedex/kanto_dex_bgpals.pal'])[:16]
    tiles = image_codec.tile_list(sources['graphics/pokedex/kanto_dex_bgtiles.png'])
    if len(tiles) != 32 or len(set(tiles[15])) != 1:
        raise ValueError('Area window overlay tile changed')
    overlay = palette[tiles[15][0]]
    staged, regions = [], []
    def asset(filename, width, height, pixels):
        result = dict(width=width, height=height)
        staged.append((filename, width, height, bytes(pixels), result))
        return result
    def window(name):
        body = _body(screen, 'sWindowTemplate_AreaMap_' + name)
        values = {key: int(value) for key, value in re.findall(r'\.(tilemapLeft|tilemapTop|width|height)\s*=\s*(\d+)', body)}
        return values['tilemapLeft'] * 8, values['tilemapTop'] * 8, values['width'] * 8, values['height'] * 8
    gates = re.findall(r'if \(FlagGet\((FLAG_WORLD_MAP_\w+)\) == TRUE\)\s*result \|= 1 << (\d+);',
                       field.split('u8 GetUnlockedSeviiAreas(void)', 1)[1].split('return result;', 1)[0])
    if [int(bit) for _, bit in gates] != list(range(7)):
        raise ValueError('Source Sevii unlock flags changed')
    names = ['Kanto'] + re.findall(r'\{&sWindowTemplate_AreaMap_(\w+),\s+sTilemap_AreaMap_\w+\}',
                                  _body(screen, 'sAreaMapStructs_SeviiIslands'))
    if len(names) != 8:
        raise ValueError('Source area maps incomplete')
    # Base BG3 tile1 fills the page interior. Source map windows are opaque.
    backdrop = bytearray(image_codec.rgba(bytes(tiles[1][(y % 8) * 8 + x % 8]
                           for y in range(160) for x in range(240)), palette, transparent=False))
    for index, name in enumerate(names):
        binding = re.search(r'\bsTilemap_AreaMap_' + name + r'\[\] = INCBIN_U32\("([^"]+)\.4bpp\.lz"\);', screen)
        if binding is None:
            raise ValueError('Source area map image binding missing: ' + name)
        path = binding[1] + '.png'
        width, height, indices, _ = image_codec.indexed_png(sources[path])
        x, y, expected_width, expected_height = window(name)
        if (width, height) != (expected_width, expected_height):
            raise ValueError('Source map window/image size differs: ' + name)
        pixels = image_codec.rgba(indices, palette, transparent=False)
        image_codec.blit(backdrop, 240, pixels, width, height, x, y)
        regions.append(dict(id=name, unlockFlag=None if index == 0 else gates[index - 1][0],
                            x=x, y=y, sprite=asset('area-map-' + str(index) + '.png', width, height, pixels)))
    recipe = sources['graphics_file_rules.mk'].decode('utf-8')
    marker_files = re.findall(r'marker_(\d+)\.4bpp', recipe.split('POKEDEXAREAMARKERFILES :=', 1)[1].split('\n\n', 1)[0])
    if marker_files != [str(i) for i in range(7)]:
        raise ValueError('Source marker concatenation changed')
    if 'WriteTileImage(outputPath, options->numTilesMode, options->numTiles, options->metatileWidth, options->metatileHeight, &image, !image.hasPalette)' not in text['tools/gbagfx/main.c']:
        raise ValueError('Source grayscale conversion changed')
    shapes = re.findall(r'\[(MARKER_\w+)\]\s*=\s*&sSubsprite_(\w+)', _body(marker_source, 'sSubsprites'))
    shape_masks = {}
    tile_offset = 0
    for index, (shape, symbol) in enumerate(shapes):
        body = _body(marker_source, 'sSubsprite_' + symbol)
        width, height = map(int, re.search(r'\.size = SPRITE_SIZE\((\d+)x(\d+)\)', body).groups())
        offset = int(re.search(r'\.tileOffset = (\d+)', body)[1])
        sw, sh, indices, _ = image_codec.indexed_png(sources['graphics/pokedex/area_markers/marker_' + str(index) + '.png'])
        if (sw, sh) != (width, height) or offset != tile_offset:
            raise ValueError('Source area marker tile layout changed')
        tile_offset += width * height // 64
        shape_masks[shape] = (width, height, indices)
    if tile_offset * 32 != 0x4A0:
        raise ValueError('Source marker graphics size differs')
    markers = []
    for area_id, shape, sx, sy in re.findall(r'\[(DEX_AREA_\w+)\]\s*=\s*\{\s*(MARKER_\w+),\s*(-?\d+),\s*(-?\d+)\s*\}',
                                             _body(marker_source, 'sAreaMarkers')):
        x, y = 104 + int(sx), 32 + int(sy)
        width, height, mask = shape_masks[shape]
        pixels = bytearray(width * height * 4)
        for dy in range(height):
            for dx in range(width):
                if not mask[dy * width + dx]:
                    continue
                if not (0 <= x + dx < 240 and 0 <= y + dy < 160):
                    raise ValueError('Source marker is outside screen')
                start = ((y + dy) * 240 + x + dx) * 4
                # GBA alpha arithmetic is 5-bit, clamped, and uses two independent
                # coefficients. A normal 75% alpha overlay would be incorrect.
                rgb = [min(31, ((overlay[c] >> 3) * 12 + (backdrop[start + c] >> 3) * 8) // 16)
                       for c in range(3)]
                target = (dy * width + dx) * 4
                pixels[target:target + 4] = bytes([((v << 3) | (v >> 2)) for v in rgb] + [255])
        markers.append(dict(id=area_id, x=x, y=y,
                            sprite=asset('area-marker-' + area_id.removeprefix('DEX_AREA_').lower() + '.png', width, height, pixels)))
    bindings = []
    for index, name in enumerate(names):
        symbol = 'sDexAreas_Kanto' if index == 0 else 'sDexAreas_Sevii' + str(index)
        for section, area in re.findall(r'\{\s*(MAPSEC_\w+),\s*(DEX_AREA_\w+)\s*\}', _body(wild, symbol)):
            if area == 'DEX_AREA_NONE':
                continue
            bindings.append(dict(mapSection=section, areaId=area, regionId=name))
    maps = {}
    for path in area_map_paths(sources):
        data = _json(sources, path)
        if data['id'] in maps:
            raise ValueError('Duplicate source wild map id')
        maps[data['id']] = data
    encounters, altering = [], 0
    for row in _wild_rows(sources):
        section = maps[row['map']]['region_map_section']
        cave_set = -1
        if section == 'MAPSEC_ALTERING_CAVE':
            cave_set = altering
            altering += 1
        found = set()
        for field_name, constant in table_fields.items():
            if field_name not in row:
                continue
            mons = row[field_name]['mons']
            if len(mons) != counts[constant]:
                raise ValueError('Wild table size differs: ' + row['base_label'] + '/' + field_name)
            found.update(species[m['species'].removeprefix('SPECIES_')] for m in mons)
        if not found:
            raise ValueError('Source wild header contains no encounters')
        encounters.append(dict(mapSection=section, alteringCaveSet=cave_set, species=sorted(found)))
    if altering != counts['NUM_ALTERING_CAVE_TABLES']:
        raise ValueError('Altering Cave table count differs')
    starters = re.findall(r'\bSPECIES_(\w+)', _body(field, 'sStarterSpecies'))
    roamers = [dict(sourceSpeciesId=species[roamer], starterVariable=starters.index(starter))
               for roamer, starter in re.findall(r'\{\s*SPECIES_(\w+),\s*SPECIES_(\w+)\s*\}', _body(wild, 'sRoamerPairs'))]
    ellipse_bytes = sources['graphics/pokedex/blit_wide_ellipse.png']
    with Image.open(io.BytesIO(ellipse_bytes)) as image:
        if image.mode != 'L' or ellipse_bytes[24] != 4:
            raise ValueError('Area Unknown ellipse must be source 4-bit grayscale')
        ew, eh = image.size
        # gbagfx inverts non-paletted grayscale nibbles when emitting 4bpp.
        ellipse = bytes(15 - value // 17 for value in image.tobytes())
    if (ew, eh) != (88, 16):
        raise ValueError('Area Unknown ellipse dimensions differ')
    unknown = asset('area-unknown.png', ew, eh, image_codec.rgba(ellipse, palette))
    # One physical texture, immutable source rectangles. The RGBA pixels are copied
    # exactly (including hidden RGB); no resampling, quantization or lossy conversion.
    atlas_width = 256
    x = y = row_height = 0
    ordered = sorted(staged, key=lambda row: (-row[2], row[0]))
    for _, width, height, _, sprite in ordered:
        if x + width > atlas_width:
            x, y, row_height = 0, y + row_height, 0
        sprite['sourceX'], sprite['sourceY'] = x, y
        x += width
        row_height = max(row_height, height)
    atlas_height = y + row_height
    atlas = dict(texturePath='content/generated/sprites/pokemon/pokedex-menu/area-atlas.png',
                 width=atlas_width, height=atlas_height)
    pixels = image_codec.canvas(atlas_width, atlas_height)
    for _, width, height, rgba, sprite in staged:
        image_codec.blit(pixels, atlas_width, rgba, width, height, sprite['sourceX'], sprite['sourceY'])
        sprite['texture'] = atlas
    outputs = {'area-atlas.png': image_codec.png(atlas_width, atlas_height, pixels)}
    return dict(regions=regions, markers=markers, bindings=bindings, encounters=encounters, roamers=roamers,
                alteringCaveSetCount=altering, unknownSprite=unknown), outputs, [atlas]
