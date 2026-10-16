#!/usr/bin/env python3
"""Build early TM/HM inventory icons directly from retained original images."""
from pathlib import Path
import argparse
import json
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from project_paths import PROJECT_ROOT
from item_icon_exports import output_materials
from item_source import ItemSource
from presentation_source import safe
from generated_output_compare import check_item_outputs
from runtime_json import runtime_json_bytes

# Actual icon selection only; teaching definitions and compatibility are owned
# by the canonical machine catalog, not these presentation documents.
ICONS = {
    'cerulean': ('TM03',),
    'bill': ('TM45', 'TM43', 'TM28'),
    'route11': ('HM05',),
}


def convert(region, project=PROJECT_ROOT, raw=None):
    if region not in ICONS:
        raise ValueError('Unknown machine icon selection: ' + region)
    raw = Path(raw) if raw is not None else Path(project) / 'content/raw/pokefirered/037335f4'
    source = ItemSource(lambda path: safe(raw, path).read_bytes())
    outputs, icons = {}, []
    for name in ICONS[region]:
        icon, image = source.icon('ITEM_' + name)
        outputs[icon['pngPath']] = image
        icons.append(icon)
    outputs[region + '.json'] = runtime_json_bytes(dict(schemaVersion=1, icons=icons))
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--region', choices=ICONS, required=True)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--raw', type=Path)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    target = args.out or args.project_root / f'content/generated/data/machine-learning/{args.region}.json'
    target = target.absolute()
    if target.suffix != '.json':
        parser.error('--out must name a JSON file')
    raw = args.raw or args.project_root / 'content/raw/pokefirered/037335f4'
    outputs = convert(args.region, args.project_root, raw)
    materials = [(safe(directory, relative), data)
                 for (directory, relative), data in output_materials(target, outputs).items()]
    if any(path.resolve().is_relative_to(raw.resolve()) for path, _ in materials):
        parser.error('--out must not overwrite raw inputs')
    if args.check:
        generated = next(path for path in (target, *target.parents) if path.name == 'generated').absolute()
        check_item_outputs(generated, {path.relative_to(generated).as_posix(): data for path, data in materials})
    else:
        for path, data in materials:
            path.parent.mkdir(parents=True, exist_ok=True)
            temporary = safe(path.parent, path.name + '.tmp')
            temporary.write_bytes(data)
            temporary.replace(path)
    print(json.dumps(dict(region=args.region, outputCount=len(outputs), checked=args.check)))


if __name__ == '__main__':
    main()
