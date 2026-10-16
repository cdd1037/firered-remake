#!/usr/bin/env python3
"""Generate the canonical overworld catalog and co-used texture pages from local source.

Regional catalogs declare graphics IDs only. Run this after regional imports.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
from pathlib import Path
import tempfile
from PIL import Image
from overworld_sprites import graphics_records
from presentation_atlas import pack

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
POKEMON = set('articuno chansey clefairy cubone deoxys-n doduo fearow ho-oh jigglypuff kabuto kangaskhan lapras lugia machoke machop meowth mewtwo moltres nidoran-f nidoran-m nidorino omanyte pidgeot pikachu poliwrath psyduck seel slowbro slowpoke snorlax voltorb wigglytuff zapdos'.split())
OBJECTS = set('clipboard cut-tree fossil item-ball meteorite old-amber pokedex pushable-boulder rock-smash-rock ruby sapphire'.split())
TRAINERS = set('agatha beauty biker black-belt blaine blue brock bruno bug-catcher camper channeler cooltrainer-f cooltrainer-m crush-girl erika fisher giovanni hiker koga lance lass lorelei lt-surge misty picnicker poke-maniac rocker rocket-f rocket-m sabrina sailor scientist swimmer-f-land swimmer-f-water swimmer-m-land swimmer-m-water trainer-tower-dude tuber-f tuber-m-land tuber-m-water youngster'.split())
STANDALONE = {'red-normal', 'ss-anne', 'seagallop'}

class SourceFiles(dict):
    def __init__(self, raw): self.raw = raw
    def __missing__(self, key):
        value = (self.raw / key).read_bytes(); self[key] = value; return value

class Source:
    def __init__(self, raw): self.data = SourceFiles(raw)
    def text(self, path): return self.data[path].decode('utf-8')
    def ref(self, path, symbol):
        return dict(kind='pret', repository='pret/pokefirered', commit=COMMIT, path=path, symbol=symbol)


def build(raw, generated):
    ids = {row['graphicsId'] for path in [*generated.rglob('npc-source.json'), generated/'sprites/overworld/opening.json']
           for row in json.loads(path.read_text()).get('graphics', [])}
    if not ids: raise ValueError('No regional NPC graphics IDs')
    outputs = {}; catalog = {}; groups = {}
    with tempfile.TemporaryDirectory(prefix='firered-overworld-') as temp:
        folder = Path(temp) / 'npc'
        for row in graphics_records(Source(raw), ids, folder, render=True):
            name = Path(row['texturePath']).stem
            image = Image.open(folder / (name + '.png')).convert('RGBA')
            catalog[row['graphicsId']] = dict(frameWidth=row['frameWidth'],frameHeight=row['frameHeight'],
                frameCount=row['frameCount'],anchorX=row['tileCenterAnchor']['x'],anchorY=row['tileCenterAnchor']['y'],
                inanimate=row['declaration']['inanimate']=='TRUE')
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
    outputs['catalog.json'] = (json.dumps(catalog,sort_keys=True,separators=(',',':'))+'\n').encode()
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--generated', type=Path, default=ROOT/'content/generated')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args(); outputs = build(args.raw,args.generated)
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
