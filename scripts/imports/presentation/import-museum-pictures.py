#!/usr/bin/env python3
"""Convert original museum fossil pictures from pinned pret 037335f4 inputs.

The original .4bpp and .gbapal both come from the authored indexed PNG.
Only generated picture identities, dimensions and pixels enter runtime data.
"""
import argparse
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from generated_output_compare import generated_output_matches
from image_io import indexed_png, png
from pokemon_import import battle_indices
from presentation_pixels import rgba
from presentation_source import safe
from project_paths import PROJECT_ROOT
from world_source import SourceFiles


def match(pattern, text):
    found = re.search(pattern, text, re.S | re.M)
    if found is None:
        raise ValueError('Missing original museum picture binding: ' + pattern)
    return found


def build(source):
    script = source['src/script_menu.c'].decode()
    constants = source['include/constants/species.h'].decode()
    opening = match(r'bool8 OpenMuseumFossilPic\(void\)\s*\{(.*?)\n\}', script)[1]
    branches = re.findall(
        r'if \(gSpecialVar_0x8004 == (SPECIES_\w+)\)\s*\{\s*'
        r'LoadSpriteSheets\((\w+)\);\s*LoadPalette\((\w+),', opening)
    if not branches or len({row[0] for row in branches}) != len(branches):
        raise ValueError('Museum picture species bindings must be nonempty and unique')
    for expected in (
        'gSpecialVar_0x8005 * 8 + 40, gSpecialVar_0x8006 * 8 + 40',
        'CreateWindowFromRect(gSpecialVar_0x8005, gSpecialVar_0x8006, 8, 8)',
        'SetStdWindowBorderStyle(gTasks[taskId].tWindowId, TRUE)',
    ):
        if expected not in opening:
            raise ValueError('Museum picture layout changed: ' + expected)
    match(r'sMuseumFossilOamData\s*=\s*\{\s*\.shape = SPRITE_SHAPE\(64x64\),\s*'
          r'\.size = SPRITE_SIZE\(64x64\)\s*\}', script)
    match(r'sMuseumFossilAnim0\[\]\s*=\s*\{\s*ANIMCMD_FRAME\(0, 10\),\s*ANIMCMD_END\s*\}', script)
    output, assets = {}, []
    for species, sheet, palette in branches:
        tiles = match(r'static const struct SpriteSheet ' + re.escape(sheet) +
                      r'\[\]\s*=\s*\{\s*\{(\w+), sizeof\(\1\), GFXTAG_FOSSIL\}', script)[1]
        image_path = match(r'\b' + re.escape(tiles) +
                           r'\[\]\s*=\s*INCBIN_U16\("([^"]+)\.4bpp"\)', script)[1]
        palette_path = match(r'\b' + re.escape(palette) +
                             r'\[\]\s*=\s*INCBIN_U16\("([^"]+)\.gbapal"\)', script)[1]
        if image_path != palette_path:
            raise ValueError('Museum image no longer uses its embedded source palette')
        data = source[image_path + '.png']
        indices = battle_indices(data)  # Source 64x64, sixteen colors, index-zero transparency.
        width, height, _, colors = indexed_png(data)
        colors = [tuple(((c >> 3) << 3) | ((c >> 3) >> 2) for c in color) for color in colors]
        name = Path(image_path).name.replace('_', '-')
        if name in {asset['id'] for asset in assets}:
            raise ValueError('Duplicate named museum picture')
        number = int(match(r'^#define\s+' + re.escape(species) + r'\s+(\d+)\s*$', constants)[1])
        texture = name + '.png'
        output[texture] = png(width, height, rgba(indices, colors))
        assets.append(dict(id=name, sourceSpeciesId=number, width=width, height=height, texturePath=texture))
    output['pictures.json'] = (json.dumps(dict(schemaVersion=1, assets=sorted(assets, key=lambda a: a['id'])),
                                          sort_keys=True, separators=(',', ':')) + '\n').encode()
    return output


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated/ui/museum-pictures')
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    raw, output = args.raw.resolve(), args.out.resolve()
    if raw.is_relative_to(output) or output.is_relative_to(raw):
        raise ValueError('Source and generated output directories overlap')
    results = build(SourceFiles(args.raw))
    targets = [(safe(args.out, path), data) for path, data in results.items()]
    for path, data in targets:
        if args.check:
            if not generated_output_matches(path, data):
                raise ValueError('Museum picture output differs: ' + str(path))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    print(json.dumps(dict(passed=True, files=len(results), check=args.check)))


if __name__ == '__main__':
    main()
