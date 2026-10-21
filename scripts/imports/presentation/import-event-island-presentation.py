#!/usr/bin/env python3
"""Rebuild Event Island pixels and motion metadata from pinned local blobs; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from presentation_pixels import PixelReader
from presentation_source import Source, need
from presentation_output import asset_emitter, publish
import argparse
import io
import json
import pathlib
import re
import runpy
import struct
import subprocess
from PIL import Image

ROOT = _PROJECT_ROOT
BASE = 'content/generated/sprites/pokemon/event-island/'
RAW = 'content/raw/pokefirered/037335f4/'
SPECIES = [('Lugia',249),('HoOh',250),('Deoxys',410)]


def build(root, reference, dependency):
    source = Source(root, reference, dependency)
    out, assets = {}, []

    emit = asset_emitter(source, out, assets, BASE)

    pixels = PixelReader(source)
    palette, indexed = pixels.palette, pixels.indexed

    sprites, icons, footprints = [], [], []
    graphics = source.text('src/data/graphics/pokemon.h')
    constants = source.text('include/constants/species.h')
    for name, species in SPECIES:
        stem = 'ho_oh' if name == 'HoOh' else name.lower()
        symbol = 'SPECIES_' + stem.upper()
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
                indices, rgba = indexed(image, pal, (64,128) if species==410 else (64,64), floor=True, second_frame=species==410)
                x, y = (144, 8 + y_offset - elevation) if side == 'front' else (40, 48 + y_offset)
                sprites.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, renderSpeciesId=species, sourceFrameIndex=1 if species==410 else 0, form='attack' if species==410 else None, side=side, paletteVariant=variant,
                    x=x, y=y, sourceWidth=width, sourceHeight=height, sourceYOffset=y_offset, sourceEnemyElevation=elevation,
                     sourceCoordinatePath=coordinates, paletteExpansion='floor(rgb5 * 255 / 31)',
                    transparentIndex=0, **emit(f'{stem}-{side}-{variant}', 64, 64, rgba, [image, pal, coordinates])))
        icon_table = source.text('src/pokemon_icon.c')
        match = re.search(r'\[' + symbol + r'\]\s*=\s*(\d+),', icon_table)
        need(match and re.search(r'\[' + symbol + r'\]\s*=\s*gMonIcon_' + name + r',', icon_table), 'Species icon binding differs')
        pal_index = int(match[1])
        image, pal = f'graphics/pokemon/{stem}/' + ('icon_attack.png' if species==410 else 'icon.png'), f'graphics/pokemon/icon_palettes/icon_palette_{pal_index}.pal'
        need((f'gMonIcon_{name}[] = INCBIN_U8("graphics/pokemon/deoxys/icon.4bpp", "{image[:-4]}.4bpp")' if species==410 else f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")') in graphics, 'Species icon image binding differs')
        indices, rgba = indexed(image, pal, (32, 64), floor=True, first_frame=True)
        icons.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, form='attack' if species==410 else None, paletteIndex=pal_index, sourceFrameIndex=0,
                          sourceFrameCount=2,  **emit('icons/' + stem, 32, 32, rgba, [image, pal])))
        image, pal = f'graphics/pokemon/{stem}/footprint.png', 'graphics/pokedex/kanto_dex_bgpals.pal'
        need('[' + symbol + '] = gMonFootprint_' + name + ',' in source.text('src/data/pokemon_graphics/footprint_table.h'), 'Species footprint binding differs')
        im = Image.open(io.BytesIO(source.read(image)))
        need(im.mode == '1' and im.size == (16, 16), 'Footprint source pixels differ')
        indices = bytes(1 if v == 0 else 0 for v in im.get_flattened_data())
        colors = palette(pal, floor=True)
        rgba = bytes(v for index in indices for v in (*colors[index], 255 if index else 0))
        footprints.append(dict(speciesId='pret:' + symbol, sourceSpeciesId=species, **emit(stem + '-footprint', 16, 16, rgba, [image, pal])))

    names = []
    for name, species in SPECIES:
        symbol='SPECIES_'+('HO_OH' if name=='HoOh' else name.upper())
        display=re.search(r'\['+symbol+r'\]\s*=\s*_\("([^"]+)"\)',source.text('src/data/text/species_names.h'))[1]
        names.append(dict(speciesId='pret:'+symbol,sourceSpeciesId=species,name=display))
    decompress=source.text('src/decompress.c')
    need('CpuCopy32(pointer + 0x800, pointer, 0x800)' in decompress,'Attack form frame-copy binding differs')
    need('iconSprite += 0x400;' in source.text('src/pokemon_icon.c'),'Attack icon offset differs')
    map_music=[]
    maps=['BirthIsland_Exterior','BirthIsland_Harbor','NavelRock_Summit','NavelRock_Base','NavelRock_Exterior','NavelRock_Harbor']
    for name in maps:
        path='data/maps/'+name+'/map.json'
        map_music.append(dict(map=name, music=json.loads(source.read(path))['music']))
    for name in ['BirthIsland_Exterior','NavelRock_Summit','NavelRock_Base']:
        source.read('data/maps/'+name+'/scripts.inc')
    geometry_path='content/generated/battle/art/campaign-hit-art/manifest.json'
    geometry=json.loads(source.reuse(geometry_path))['geometry'];geometry_rows=[]
    for row in sprites:
        if row['paletteVariant']!='normal':continue
        expected=dict(species=row['sourceSpeciesId'],side=row['side'],sourceWidth=row['sourceWidth'],sourceHeight=row['sourceHeight'],yOffset=row['sourceYOffset'],elevation=row['sourceEnemyElevation'] if row['side']=='front' else 0)
        need(expected in geometry,'Inherited battle geometry differs');geometry_rows.append(expected)
    # Source object palette slot 10 is independent of the fixed normal overworld Deoxys.
    field_sources=['src/field_effect.c','src/field_specials.c','src/field_weather.c','src/field_weather_effects.c','src/data/object_events/object_event_graphics.h','src/data/object_events/object_event_graphics_info.h','src/data/object_events/object_event_anims.h','include/event_object_movement.h','src/palette.c']
    for path in field_sources:source.read(path)
    anim=source.text('src/data/object_events/object_event_anims.h')
    need(re.search(r'sAnim_HoOhFlapWings\[\] = \{\s*ANIMCMD_FRAME\(3, 8\),\s*ANIMCMD_FRAME\(4, 8\),\s*ANIMCMD_FRAME\(3, 8\),\s*ANIMCMD_FRAME\(4, 8\),\s*ANIMCMD_JUMP\(0\)',anim),'Ho-Oh source flap animation differs')
    graphics_source=source.text('src/data/object_events/object_event_graphics.h')
    meteorite_path=re.search(r'gObjectEventPic_BirthIslandStone\[\]\s*= INCBIN_U16\("([^"]+)',graphics_source)[1].replace('.4bpp','.png')
    field=source.text('src/field_specials.c')
    triangle=[]
    for state in range(11):
        pal=f'graphics/field_specials/deoxys_rock_{state}.pal'
        need(pal.replace('.pal','.gbapal') in field,'Triangle palette source binding differs')
        indices,rgba=indexed(meteorite_path,pal,(32,32))
        triangle.append(dict(interactionNumber=state,objectPaletteSlot=10,paletteRgb5=[[int(c)>>3 for c in line.split()] for line in source.text(pal).splitlines()[3:]],**emit(f'triangle/state-{state}',32,32,rgba,[meteorite_path,pal])))
    fragments=[]
    for ordinal,direction in enumerate(['top_left','top_right','bottom_left','bottom_right']):
        image=f'graphics/field_effects/pics/deoxys_rock_fragment_{direction}.png';pal='graphics/field_specials/deoxys_rock_10.pal'
        need(image.replace('.png','.4bpp') in source.text('src/field_effect.c'),'Fragment source binding differs')
        indices,rgba=indexed(image,pal,(8,8))
        fragments.append(dict(ordinal=ordinal,deltaX=-16 if ordinal%2==0 else 16,deltaY=-12 if ordinal<2 else 12,**emit('fragments/'+direction,8,8,rgba,[image,pal])))
    # Summit shade uses the same source gamma index 3 as Tanoby. Authenticate used BG palettes.
    layout_id=json.loads(source.read('data/maps/NavelRock_Summit/map.json'))['layout']
    layout=next(l for l in json.loads(source.read('data/layouts/layouts.json'))['layouts'] if l.get('id')==layout_id)
    tile_headers=source.text('src/data/tilesets/headers.h');tile_bindings=source.text('src/data/tilesets/metatiles.h');tables=[]
    for tag in ['primary_tileset','secondary_tileset']:
        record=re.search(r'const struct Tileset '+layout[tag]+r'\s*=\s*\{(.*?)\n\};',tile_headers,re.S)[1]
        symbol=re.search(r'\.metatiles = (\w+)',record)[1]
        path=re.search(r'const u16 '+symbol+r'\[\] = INCBIN_U16\("([^"]+)"\)',tile_bindings)[1]
        data=source.read(path);tables.append(struct.unpack('<'+'H'*(len(data)//2),data))
    used=set()
    for tag in ['border_filepath','blockdata_filepath']:
        data=source.read(layout[tag])
        for block in struct.unpack('<'+'H'*(len(data)//2),data):
            meta=block&1023;table=tables[meta>=640];local=meta-640 if meta>=640 else meta
            need(local*8+8<=len(table),'Summit shade metatile range differs')
            used.update(tile>>12 for tile in table[local*8:local*8+8])
    need(used and max(used)<=12,'Summit BG requires mixed shade treatment')
    manifest = runtime_json_bytes(dict(schemaVersion=1,     assets=assets, sprites=sprites, pokemonIcons=icons, footprints=footprints, speciesNames=names, newOwnedSpecies=[s for _, s in SPECIES], mapMusic=map_music, moveAnimationGeometry=dict(inheritedManifest=geometry_path, rows=geometry_rows),  triangle=triangle, fragments=fragments, fieldPresentation=dict( paletteTint='ApplyGlobalFieldPaletteTint(10)', destroy=dict(fragmentFrame=122, totalFrames=206, amplitude=4, decayInterval=21, flashBlend=16, flashBackgroundOnly=True), hoOhFlap=dict(frames=[3, 4, 3, 4], frameDuration=8), summitShade=dict(gammaIndex=3, paletteIndices=sorted(used), normalChannels=[c * 13 // 16 for c in range(32)])), outputs=[dict(path=p) for p, b in sorted(out.items())]), sort_keys=True)
    out['manifest.json']=manifest
    result={BASE+p:b for p,b in out.items()};result.update({RAW+p:b for p,b in source.raw.items()})
    report=dict(passed=True,sourceFiles=len(source.raw),inheritedInputs=len(source.inherited),sprites=len(sprites),icons=len(icons),footprints=len(footprints),trianglePalettes=len(triangle),fragments=len(fragments),assets=len(assets),geometry=len(geometry_rows),outputs=len(out)-1,manifestBytes=len(manifest),nativeWindowVerified=False)
    return result,report
if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project-root', type=pathlib.Path, default=ROOT)
    parser.add_argument('--reference-root', type=pathlib.Path)
    parser.add_argument('--dependency-root', type=pathlib.Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    output, report = build(args.project_root, args.reference_root, args.dependency_root or args.project_root)
    publish(args.project_root, output, check=args.check)
    print(json.dumps(report, sort_keys=True))
