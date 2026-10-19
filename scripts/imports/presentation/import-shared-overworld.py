#!/usr/bin/env python3
"""Generate the canonical overworld catalog and co-used texture pages from local source.

Graphics selection comes directly from current-world source events and dynamic actor tables.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import re
from pathlib import Path
import tempfile
from PIL import Image
from overworld_sprites import graphics_records
from world_selection import load_selection
from world_source import WorldSource
from presentation_atlas import pack

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
POKEMON = set('articuno chansey clefairy cubone deoxys-n doduo fearow ho-oh jigglypuff kabuto kangaskhan lapras lugia machoke machop meowth mewtwo moltres nidoran-f nidoran-m nidorino omanyte pidgeot pidgey pikachu poliwrath psyduck seel slowbro slowpoke snorlax spearow voltorb wigglytuff zapdos'.split())
OBJECTS = set('clipboard cut-tree fossil item-ball meteorite old-amber pokedex pushable-boulder rock-smash-rock ruby sapphire'.split())
TRAINERS = set('agatha beauty biker black-belt blaine blue brock bruno bug-catcher camper channeler cooltrainer-f cooltrainer-m crush-girl erika fisher giovanni hiker koga lance lass lorelei lt-surge misty picnicker poke-maniac rocker rocket-f rocket-m sabrina sailor scientist swimmer-f-land swimmer-f-water swimmer-m-land swimmer-m-water trainer-tower-dude tuber-f tuber-m-land tuber-m-water youngster'.split())
STANDALONE = {'red-normal', 'ss-anne', 'seagallop'}

def build(raw, *, selection=None):
    selection = load_selection(ROOT / 'data/world-maps.json') if selection is None else selection
    ids = {'OBJ_EVENT_GFX_RED_NORMAL'}
    for name in {row['sourceName'] for row in selection}:
        events = json.loads((raw / 'data/maps' / name / 'map.json').read_bytes())
        ids.update(event['graphics_id'] for event in events['object_events']
                   if event['graphics_id'].startswith('OBJ_EVENT_GFX_')
                   and not event['graphics_id'].startswith('OBJ_EVENT_GFX_VAR_'))
        path = raw / 'data/maps' / name / 'scripts.inc'
        ids.update(re.findall(r'\bOBJ_EVENT_GFX_(?!VAR_)\w+', path.read_text()))
    # Dynamic tower actors and the playable Surf avatars are selected from their
    # source tables, rather than a second set of regional NPC inventory files.
    ids.update(re.findall(r'\bOBJ_EVENT_GFX_\w+', (raw / 'src/trainer_tower.c').read_text()))
    avatar = (raw / 'src/field_player_avatar.c').read_text()
    ride = re.search(r'\[PLAYER_AVATAR_GFX_RIDE\]\s*=\s*\{([^}]+)\}', avatar)
    if ride is None: raise ValueError('Missing source Surf avatar table')
    ids.update(re.findall(r'OBJ_EVENT_GFX_\w+', ride[1]))
    if not ids: raise ValueError('No world NPC graphics IDs')
    shadow_sizes = {name: int(value) for name, value in re.findall(
        r'^#define\s+(SHADOW_SIZE_\w+)\s+(\d+)\b',
        (raw / 'include/constants/event_objects.h').read_text(), re.M)}
    outputs = {}; catalog = {}; groups = {}
    with tempfile.TemporaryDirectory(prefix='firered-overworld-') as temp:
        folder = Path(temp) / 'npc'
        for row in graphics_records(WorldSource(raw), ids, folder, render=True):
            name = Path(row['texturePath']).stem
            image = Image.open(folder / (name + '.png')).convert('RGBA')
            catalog[row['graphicsId']] = dict(frameWidth=row['frameWidth'],frameHeight=row['frameHeight'],
                frameCount=row['frameCount'],anchorX=row['tileCenterAnchor']['x'],anchorY=row['tileCenterAnchor']['y'],
                inanimate=row['declaration']['inanimate']=='TRUE',
                shadowSize=shadow_sizes[row['declaration']['shadowSize']])
            if name in STANDALONE:
                path = name + '.png'; outputs[path] = (folder / path).read_bytes()
                catalog[row['graphicsId']].update(texturePath=path,region=dict(x=0,y=0,width=image.width,height=image.height))
            else:
                group = 'pokemon' if name in POKEMON else 'objects' if name in OBJECTS else 'trainers' if name in TRAINERS else 'residents'
                groups.setdefault(group, []).append((row['graphicsId'],image))
        for group, images in sorted(groups.items()):
            path = group + '.png'; outputs[path], regions = pack(images,path)
            for region in regions:
                catalog[region['id']].update(texturePath=path,region=region['region'])
    # The walking player uses the original nine-frame sheet, whereas the shared
    # object animation table expands repeated frames into its own sprite strip.
    player = Image.open(raw / 'graphics/object_events/pics/people/red_normal.png')
    palette = (raw / 'graphics/object_events/palettes/player.pal').read_text().splitlines()
    if player.mode != 'P' or player.size != (144, 32) or palette[:3] != ['JASC-PAL', '0100', '16']:
        raise ValueError('Walking player source format differs')
    colors = [tuple(((int(v) >> 3) << 3) | ((int(v) >> 3) >> 2) for v in row.split()) for row in palette[3:]]
    rgba = Image.new('RGBA', player.size)
    rgba.putdata([(*colors[index], 0 if index == 0 else 255) for index in player.get_flattened_data()])
    from image_io import encode_png
    outputs['opening-player.png'] = encode_png(rgba)
    # Itemfinder sprites use OBJ palette zero, the same player palette. The five
    # 16x16 frames are the four response strengths followed by the underfoot star.
    with Image.open(raw / 'graphics/itemfinder/spr_tiles.png') as finder:
        if finder.mode != 'P' or finder.size != (16, 80):
            raise ValueError('Itemfinder sprite sheet format differs')
        pixels = list(finder.get_flattened_data())
        if any(index >= len(colors) for index in pixels):
            raise ValueError('Itemfinder palette index outside player palette')
        effect = Image.new('RGBA', finder.size)
        effect.putdata([(*colors[index], 0 if index == 0 else 255) for index in pixels])
        outputs['itemfinder.png'] = encode_png(effect)
    outputs.update(shadow_outputs(raw, colors))
    outputs.update(grass_outputs(raw))
    outputs['catalog.json'] = (json.dumps(catalog,sort_keys=True,separators=(',',':'))+'\n').encode()
    return outputs


def shadow_outputs(raw, colors):
    # Shadow templates retain OBJ palette zero (the player's palette), with
    # source index zero transparent. These are the four source OAM geometries.
    from image_io import encode_png
    outputs = {}
    for size, name, dimensions in (('s', 'small', (8, 8)), ('m', 'medium', (16, 8)),
                                   ('l', 'large', (32, 8)), ('xl', 'extra_large', (64, 32))):
        with Image.open(raw / f'graphics/field_effects/pics/shadow_{name}.png') as source:
            if source.mode != 'P' or source.size != dimensions:
                raise ValueError('Overworld shadow source format differs: ' + name)
            pixels = [index & 15 for index in source.get_flattened_data()]
            image = Image.new('RGBA', dimensions)
            image.putdata([(*colors[index], 0 if index == 0 else 255) for index in pixels])
            outputs[f'field-shadow-{size}.png'] = encode_png(image)
    return outputs


def grass_outputs(raw):
    """Decode the ordinary grass overlays with their actual General1 OBJ palette."""
    from image_io import encode_png, indexed_png
    from presentation_pixels import palette, rgba
    objects = (raw / 'src/data/field_effects/field_effect_objects.h').read_text()
    graphics = (raw / 'src/data/object_events/object_event_graphics.h').read_text()
    scripts = (raw / 'data/field_effect_scripts.s').read_text()

    def body(symbol):
        found = re.search(r'\b' + symbol + r'(?:\[\])?\s*=\s*\{(.*?)\};', objects, re.S)
        if found is None:
            raise ValueError('Missing grass source declaration: ' + symbol)
        return found[1]

    def require(pattern, text, label):
        if re.search(pattern, text) is None:
            raise ValueError('Grass source binding differs: ' + label)

    require(r'\.data\s*=\s*gFieldEffectObjectPalette1\s*,\s*\.tag\s*=\s*FLDEFF_PAL_TAG_GENERAL_1',
            body('gSpritePalette_GeneralFieldEffect1'), 'General1 palette')
    require(r'gFieldEffectObjectPalette1\[\]\s*=\s*INCBIN_U16\("graphics/field_effects/palettes/general_1\.gbapal"\)',
            graphics, 'General1 palette path')
    colors = palette((raw / 'graphics/field_effects/palettes/general_1.pal').read_bytes())
    if len(colors) != 16:
        raise ValueError('Grass source palette must contain 16 colors')
    outputs = {}
    for symbol, stem in (('TallGrass', 'tall_grass'), ('JumpTallGrass', 'jump_tall_grass')):
        require(r'gFldEffScript_' + symbol + r'::\s*loadfadedpal_callnative\s+gSpritePalette_GeneralFieldEffect1,\s*FldEff_' + symbol + r'\b',
                scripts, symbol + ' palette load')
        require(r'gFieldEffectObjectPic_' + symbol + r'\[\]\s*=\s*INCBIN_U16\("graphics/field_effects/pics/' + stem + r'\.4bpp"\)',
                graphics, symbol + ' image path')
        template = body('gFieldEffectObjectTemplate_' + symbol)
        require(r'\.paletteTag\s*=\s*FLDEFF_PAL_TAG_GENERAL_1\b', template, symbol + ' palette tag')
        require(r'\.images\s*=\s*sPicTable_' + symbol + r'\b', template, symbol + ' image table')
        frames = re.findall(r'overworld_frame\(gFieldEffectObjectPic_' + symbol + r',\s*(\d+),\s*(\d+),\s*(\d+)\)',
                            body('sPicTable_' + symbol))
        frames = [tuple(map(int, frame)) for frame in frames]
        if not frames or any((x, y) != frames[0][:2] or index != n for n, (x, y, index) in enumerate(frames)):
            raise ValueError('Grass source frame table differs: ' + symbol)
        width, frame_height = (dimension * 8 for dimension in frames[0][:2])
        require(r'\.oam\s*=\s*&gObjectEventBaseOam_' + str(width) + 'x' + str(frame_height) + r'\b',
                template, symbol + ' OAM geometry')
        w, h, indices, _ = indexed_png((raw / f'graphics/field_effects/pics/{stem}.png').read_bytes())
        if (w, h) != (width, frame_height * len(frames)):
            raise ValueError('Grass source image geometry differs: ' + symbol)
        outputs['field-' + stem.replace('_', '-') + '.png'] = encode_png(Image.frombytes('RGBA', (w, h), rgba(indices, colors)))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--generated', type=Path, default=ROOT/'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args(); outputs = build(args.raw)
    destination = args.generated/'sprites/overworld'
    if args.check:
        for path, data in outputs.items():
            if (destination/path).read_bytes()!=data: raise ValueError('Shared overworld output differs: '+path)
    else:
        destination.mkdir(parents=True,exist_ok=True)
        for path,data in outputs.items(): (destination/path).write_bytes(data)
        for path in destination.glob('*.png'):
            if path.name not in outputs and path.name != 'opening-player.png': path.unlink()
    print(json.dumps(dict(graphics=len(json.loads(outputs['catalog.json'])),textures=len(outputs)-1,check=args.check)))

if __name__=='__main__': main()
