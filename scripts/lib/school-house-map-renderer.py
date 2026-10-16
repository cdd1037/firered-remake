"""Source-authenticated cleared-VRAM policy for callback-free indoor tilesets."""
import re
from PIL import Image
from map_source import render_map


def import_map(src, name, stable_id, out, codec):
    reset = src.text('src/overworld.c')
    loader = src.text('src/fieldmap.c')
    decompress = src.text('src/new_menu_helpers.c')
    if not all(part in reset for part in (
        'static void InitOverworldBgs(void)\n{\n    MoveSaveBlocks_ResetHeap_();\n    ResetScreenForMapLoad();',
        'DmaFillLarge16(3, 0, (void *)(VRAM + 0x0), 0x18000, 0x1000);',
        'case 0:\n        InitOverworldBgs();',
        'case 8:\n        CopySecondaryTilesetToVram(gMapHeader.mapLayout);')) or \
       'DecompressAndCopyTileDataToVram2(2, tileset->tiles, numTiles * 32, offset, 0);' not in loader or \
       'if (sizeOut > size)\n            sizeOut = size;' not in decompress or \
       'CopyDecompressedTileDataToVram(bgId, ptr, sizeOut, offset, mode);' not in decompress:
        raise ValueError('Cleared VRAM load rule differs from pinned source')

    events = src.json(f'data/maps/{name}/map.json')
    layout = next(x for x in src.json('data/layouts/layouts.json')['layouts'] if x.get('id') == events['layout'])
    headers = src.text('src/data/tilesets/headers.h')
    bases = ['data/tilesets/' + codec.TILESETS[layout[k]] for k in ('primary_tileset', 'secondary_tileset')]
    for kind in ('primary_tileset', 'secondary_tileset'):
        body = re.search(r'const struct Tileset ' + layout[kind] + r' =\s*\{(.*?)\n\};', headers, re.S)[1]
        if not re.search(r'\.isCompressed = TRUE', body) or not re.search(r'\.callback = NULL', body):
            raise ValueError('Cleared VRAM renderer only admits compressed callback-free tilesets')
    cleared = set()

    def unloaded_tile(map_name, tile_id, palette, emitted):
        index, local = (0, tile_id) if tile_id < 640 else (1, tile_id - 640)
        cleared.add((index, local, tile_id, palette))
        return Image.new('RGBA', (8, 8))

    world = render_map(src, name, stable_id, out, tilesets=codec.TILESETS, unloaded_tile=unloaded_tile)
    world.update(profile=codec.PROFILE, runtimeIntegrated=False,
        clearedVramTiles=[dict(tileset=bases[i], localTile=local, tile=tile_id, palette=palette)
                         for i, local, tile_id, palette in sorted(cleared)])
    return world
