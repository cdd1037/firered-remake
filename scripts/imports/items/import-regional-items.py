#!/usr/bin/env python3
"""Compile selected ordinary regional item views from pinned raw files or Git blobs."""
import argparse
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from project_paths import PROJECT_ROOT
from item_catalog import item_json
from item_effects import read_effects_text
from item_icon_exports import output_materials, decode_icon_page, ATLAS_PATH
from item_source import ItemSource, unique_pairs
from presentation_source import Source, safe
from runtime_json import runtime_json_bytes

RECIPE = PROJECT_ROOT / 'scripts/recipes/items/regional-items.json'


def convert(source, group, project_root=PROJECT_ROOT):
    recipe = json.loads(RECIPE.read_text(), object_pairs_hook=unique_pairs)[group]
    items, effects, maps = (recipe[key] for key in ('items', 'effects', 'maps'))
    separate = recipe.get('separateItems', {})
    if (not items or not maps or len(set(items)) != len(items) or len(set(maps)) != len(maps)
            or not set(effects) <= set(items) or not set(separate) <= set(items)):
        raise ValueError('Invalid regional item selection')
    reader = ItemSource(source.read)
    declarations = json.loads(source.text('src/data/items.json'), object_pairs_hook=unique_pairs)['items']
    by_symbol = {row['itemId']: row for row in declarations if row['itemId'] != 'ITEM_NONE'}
    source_effects = read_effects_text(source.text('include/constants/item_effects.h'),
                                      source.text('src/data/pokemon/item_effects.h'))
    definitions, icons, outputs, separated = [], [], {}, {}
    for name in items:
        symbol = 'ITEM_' + name
        row = by_symbol[symbol]
        entry = dict(metadata=dict(item=reader.definition(symbol),
                                   description=row['description_english'].replace('\\n', '\n')),
                     effectBytes=source_effects[symbol] if name in effects else [])
        if name in separate:
            separated[separate[name]] = entry
        else:
            definitions.append(entry)
        icon, pixels = reader.icon(symbol)
        icons.append(icon)
        outputs[icon['pngPath']] = pixels
    doc = dict(schemaVersion=1, items=definitions, **separated, icons=icons, pickups=reader.pickups(maps))
    outputs[group + '-items.json'] = runtime_json_bytes(doc, default=item_json)
    outputs['manifest.json'] = (json.dumps(dict(schemaVersion=1,
        outputs=[dict(path=path) for path in sorted(outputs)]), ensure_ascii=False, indent=2) + '\n').encode()
    return outputs


def check_outputs(out, outputs, group):
    """Check every business field and exact pixels in standalone or packed form."""
    document_name = group + '-items.json'
    expected = json.loads(outputs[document_name], object_pairs_hook=unique_pairs)
    actual = json.loads(safe(out, document_name).read_bytes(), object_pairs_hook=unique_pairs)
    generated = next(p for p in (out, *out.parents) if p.name == 'generated')
    if len(actual.get('icons', [])) != len(expected['icons']):
        raise ValueError('Item icon selection differs')
    path_mapping = {}
    for original, retained in zip(expected['icons'], actual['icons']):
        path = retained.get('pngPath')
        if path == original['pngPath'] and 'region' not in retained:
            pass
        elif path == ATLAS_PATH and set(retained.get('region', {})) == {'x', 'y', 'width', 'height'}:
            pass
        else:
            raise ValueError('Unsupported item icon representation')
        image = safe(generated, path.removeprefix('content/generated/')).read_bytes()
        rebuilt = decode_icon_page(outputs[original['pngPath']], original)
        stored = decode_icon_page(image, retained)
        if rebuilt.size != stored.size or rebuilt.tobytes() != stored.tobytes():
            raise ValueError('Item icon pixels differ: ' + str(original['item']))
        path_mapping[original['pngPath']] = path
        original['pngPath'] = path
        if 'region' in retained:
            original['region'] = retained['region']
    if expected != actual:
        raise ValueError('Regional item business data differs: ' + document_name)
    expected_manifest = json.loads(outputs['manifest.json'], object_pairs_hook=unique_pairs)
    retained_outputs, seen = [], set()
    for entry in expected_manifest['outputs']:
        path = path_mapping.get(entry['path'], entry['path'])
        if path not in seen:
            retained_outputs.append(dict(entry, path=path))
            seen.add(path)
    expected_manifest['outputs'] = retained_outputs
    actual_manifest = json.loads(safe(out, 'manifest.json').read_bytes(), object_pairs_hook=unique_pairs)
    if expected_manifest != actual_manifest:
        raise ValueError('Regional item output manifest differs')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--group', choices=tuple(json.loads(RECIPE.read_text())), required=True)
    parser.add_argument('--project-root', type=Path, default=PROJECT_ROOT)
    parser.add_argument('--reference-repo', type=Path)
    parser.add_argument('--out', type=Path)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    source = Source(args.project_root, args.reference_repo)
    outputs = convert(source, args.group, args.project_root)
    out = args.out or args.project_root / 'content/generated/data/items' / args.group
    targets = [(safe(directory, path), data)
               for (directory, path), data in output_materials(out, outputs).items()]
    raw = (args.project_root / source.raw_prefix).absolute()
    if any(target.absolute().is_relative_to(raw) for target, _ in targets):
        raise ValueError('Source/output overlap')
    if args.check:
        check_outputs(out, outputs, args.group)
    else:
        for target, data in targets:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(group=args.group, outputs=len(outputs), checked=args.check)))


if __name__ == '__main__':
    main()
