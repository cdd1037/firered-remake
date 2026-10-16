"""Source-derived Vermilion switches, Cut rules, reward and beam-state art."""
import io
import re
from PIL import Image
from source_features import script_blocks, feature_worlds, feature_constants
MAPS = [('VermilionCity_Gym', 'map.vermilion-city-gym', 'vermilion-city-gym')]
def beam_metatiles(src, scripts, constants, output):
    """Render all source beam states using original indexed tiles and palettes."""
    bases = ['data/tilesets/primary/building', 'data/tilesets/secondary/vermilion_gym']
    attrs = [src.units(b + '/metatile_attributes.bin', 'I') for b in bases]
    tiles = [src.units(b + '/metatiles.bin', 'H') for b in bases]
    images = [Image.open(io.BytesIO(src.data[b + '/tiles.png'])) for b in bases]
    palettes = []
    for i in range(13):
        lines = src.text(bases[0 if i < 7 else 1] + f'/palettes/{i:02d}.pal').splitlines()
        if lines[:3] != ['JASC-PAL', '0100', '16'] or len(lines) != 19: raise ValueError('Invalid beam palette')
        palettes.append([tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in row.split()) for row in lines[3:]])
    palettes[0][0] = (0, 0, 0)
    def tile(entry):
        tid, pal = entry & 1023, entry >> 12
        index, local = (0, tid) if tid < 640 else (1, tid - 640)
        img = images[index]
        if img.mode != 'P' or pal >= 13 or local * 64 >= img.width * img.height: raise ValueError('Invalid beam tile')
        pixels = []
        for y in range(8):
            for x in range(8):
                ix, iy = (7-x if entry & 0x400 else x), (7-y if entry & 0x800 else y)
                color = img.getpixel(((local % (img.width//8))*8+ix, (local//(img.width//8))*8+iy))
                pixels.append((*palettes[pal][color], 0 if color == 0 else 255))
        result = Image.new('RGBA', (8,8)); result.putdata(pixels); return result
    rows = {b['label']: b for b in scripts['blocks']}
    blocks = src.units('data/layouts/VermilionCity_Gym/map.bin', 'H')
    definitions, states = {}, []
    output.mkdir(parents=True, exist_ok=True)
    for state, suffix in [('on','SetBeamsOn'), ('half','SetOneBeamOff'), ('off','SetBeamsOff')]:
        label = 'VermilionCity_Gym_EventScript_' + suffix
        patches = []
        for row in rows[label]['lines']:
            match = re.fullmatch(r'\s*setmetatile (\d+), (\d+), (\w+), ([01])', row['text'])
            if not match: continue
            x,y,symbol,impassable = int(match[1]),int(match[2]),match[3],bool(int(match[4]))
            mid = constants['values'][symbol]['value']; local = mid - 640
            if not 0 <= local < len(attrs[1]): raise ValueError('Beam metatile range differs')
            attr, entries = attrs[1][local], tiles[1][local*8:local*8+8]
            layer_type = (attr >> 29) & 3
            groups = {0:[[0x3014]*4,entries[:4],entries[4:]],1:[entries[:4],entries[4:],[0]*4],2:[entries[:4],[0]*4,entries[4:]]}.get(layer_type)
            if groups is None: raise ValueError('Reserved beam layer type')
            if mid not in definitions:
                textures = {}
                for layer, group in zip(('bottom','middle','top'), groups):
                    img = Image.new('RGBA',(16,16))
                    for k, entry in enumerate(group): img.paste(tile(entry), ((k%2)*8,(k//2)*8))
                    filename = f'{mid:03x}-{layer}.png'; img.save(output/filename,optimize=False,compress_level=9)
                    textures[layer+'TexturePath'] = 'beam-metatiles/' + filename
                definitions[mid] = dict(rawMetatileId=mid,symbol=symbol,rawAttributes=attr,behavior=attr&511,
                    terrain=(attr>>9)&31,encounterType=(attr>>24)&7,layerType=layer_type,tileEntries=entries,
                    sourceRef=src.ref(bases[1]+'/metatile_attributes.bin',f'metatile_{local:03x}'),**textures)
            raw = blocks[y*11+x]; collision = 3 if impassable else 0
            patches.append(dict(x=x,y=y,rawMetatileId=mid,isImpassable=impassable,collision=collision,
                sourceRawBlock=raw,sourceElevation=raw>>12,resultingRawBlock=(raw&0xf000)|mid|(collision<<10)))
        if len(patches) != 10: raise ValueError('Beam state must contain all ten source patches')
        states.append(dict(state=state,label=label,sourceRef=rows[label]['sourceRef'],patches=patches))
    return dict(schemaVersion=1,mapId=MAPS[0][1],preserveSourceElevation=True,
        definitions=[definitions[k] for k in sorted(definitions)],states=states)

def compile_gym(src, generated):
    names = ['VermilionCity_Gym']
    worlds = feature_worlds(src, names)
    for world in worlds:
        world['sourceRef'] = src.ref('data/maps/' + world['sourceName'] + '/map.json', world['events']['id'])
    scripts = script_blocks(src, names, ['EventScript_CutTree', 'EventScript_FldEffCut', 'EventScript_SetGymTrainers'])
    constants = feature_constants(src, worlds, scripts)
    directory = generated / 'maps/regions/vermilion-gym'
    directory.mkdir(parents=True, exist_ok=True)
    beams = beam_metatiles(src, scripts, constants, directory / 'beam-metatiles')
    return {'maps/regions/vermilion-gym/beam-metatiles.json': beams}
