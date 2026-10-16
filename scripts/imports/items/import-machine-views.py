#!/usr/bin/env python3
"""Build early TM/HM views from the shared admission catalog and original item icons."""
from pathlib import Path
import argparse
import json
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from project_paths import PROJECT_ROOT
from machine_catalog import machine_definitions
from image_io import indexed_png, png
from item_icon_exports import item_icon_path, output_materials
from runtime_json import runtime_json_bytes

# Only output-shape/icon selection remains local. Species, moves, PP and machine
# memberships are owned by the existing canonical admission/source catalogs.
ICONS = {
    'cerulean': ('TM03',),
    'bill': ('TM45', 'TM43', 'TM28'),
    'ssanne': ('TM31', 'TM44', 'HM01'),
    'route11': ('HM05',),
}


def convert(region, project=PROJECT_ROOT, raw=None):
    from presentation_pixels import palette, rgba
    if region not in ICONS:
        raise ValueError('Unknown machine view: ' + region)
    project = Path(project)
    raw = Path(raw) if raw is not None else project / 'content/raw/pokefirered/037335f4'
    item_table = (raw / 'src/data/item_icon_table.h').read_text()
    graphics = (raw / 'src/data/graphics/items.h').read_text()

    def one(text, pattern):
        rows = re.findall(pattern, text, re.M)
        if len(rows) != 1:
            raise ValueError('Missing or ambiguous source icon binding: ' + pattern)
        return rows[0]

    outputs, icons = {}, []
    for name in ICONS[region]:
        symbols = one(item_table, r'^    \[ITEM_' + re.escape(name) + r'\]\s*= \{(\w+), (\w+)\},')
        paths = [one(graphics, r'^const u32 ' + re.escape(symbol) + r'\[\]\s*= INCBIN_U32\("([^"]+)"\);')
                 .replace('.4bpp.lz', '.png').replace('.gbapal.lz', '.pal') for symbol in symbols]
        image, colors = paths
        width, height, indices, _ = indexed_png((raw / image).read_bytes())
        if (width, height) != (24, 24):
            raise ValueError('Machine icon must be 24x24')
        output = item_icon_path(image, colors)
        outputs[output] = png(width, height, rgba(indices, palette((raw / colors).read_bytes())))
        icons.append(dict(item=dict(value='pret:ITEM_' + name), width=width, height=height, pngPath=output))
    relative = f'content/generated/data/machine-learning/{region}.json'
    doc = dict(schemaVersion=1, machines=machine_definitions(project, relative, 'machines'))
    doc.update(dict(icon=icons[0]) if region == 'cerulean' else dict(icons=icons))
    outputs[region + '.json'] = runtime_json_bytes(doc)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--region', choices=ICONS, required=True)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--raw', type=Path)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    target = args.out or args.project_root / f'content/generated/data/machine-learning/{args.region}.json'
    if target.suffix != '.json':
        parser.error('--out must name a JSON file')
    raw = (args.raw or args.project_root / 'content/raw/pokefirered/037335f4').resolve()
    if target.resolve().is_relative_to(raw):
        parser.error('--out must not overwrite raw inputs')
    outputs = convert(args.region, args.project_root, raw)
    for (directory, relative), data in output_materials(target, outputs).items():
        path = directory / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_name(path.name + '.tmp')
        temporary.write_bytes(data)
        temporary.replace(path)
    print(json.dumps(dict(region=args.region, outputCount=len(outputs))))


if __name__ == '__main__':
    main()
