#!/usr/bin/env python3
"""Compile the current world directly from pinned raw into final map resources."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
import argparse
import importlib.util
import json
import tempfile
from map_source import source_directory, tileset_bindings, require_safe_target
from map_runtime import encode, require
from project_paths import PROJECT_ROOT
from world_source import WorldSource
from world_selection import load_selection
from world_map_renderer import render_map


def converter(name, relative):
    spec = importlib.util.spec_from_file_location(name, PROJECT_ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def compile_presentation(raw, generated, selected):
    fields = converter('_world_field_tilesets', 'scripts/imports/maps/import-field-tilesets.py')
    source = WorldSource(raw)
    layouts = {row.get('layout') or source.json('data/maps/' + row['sourceName'] + '/map.json')['layout'] for row in selected}
    (generated / 'maps/tilesets.json').write_bytes(encode(fields.compile_tables(PROJECT_ROOT, raw=raw, used=layouts)))
    from field_cave_art import compile_outputs as compile_cave
    for name, data in compile_cave(source).items():
        (generated / name).write_bytes(data)
    from field_map_preview_art import compile_outputs as compile_previews
    for name, data in compile_previews(source).items():
        (generated / name).write_bytes(data)
    from field_door_art import compile_outputs as compile_doors
    for name, data in compile_doors(source, layouts).items():
        (generated / name).write_bytes(data)
    sprites = converter('_world_overworld', 'scripts/imports/presentation/import-shared-overworld.py')
    for name, data in sprites.build(raw, selection=selected).items():
        path = generated / 'sprites/overworld' / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
    atlas = converter('_world_atlas', 'scripts/imports/maps/import-map-atlases.py')
    images = atlas.inventory(generated)
    atlas.compile_atlases(generated, generated / 'maps/atlases', images)
    atlas.verify(generated, generated / 'maps/atlases', expected=images)
    # Logical image paths are resolved from the final atlas; decoded staging PNGs
    # are no longer independent runtime artifacts.
    for relative in images:
        (generated / relative).unlink()


def compile_world(raw, generated, selection):
    source = WorldSource(raw)
    selected = load_selection(selection)
    require(selected, 'Empty world selection')
    require_safe_target(Path(generated), Path(generated))
    for row in selected:
        require_safe_target(Path(generated), Path(generated) / row['path'])
    require(len({row['path'] for row in selected}) == len(selected), 'Duplicate world map path')
    ordinary = [row for row in selected if 'variant' not in row]
    require(len({row['stableId'] for row in ordinary}) == len(ordinary), 'Duplicate ordinary map ID')
    symbols = {}
    for path in sorted((Path(raw) / 'data/maps').glob('*/map.json')):
        name = path.parent.name
        events = source.json(f'data/maps/{name}/map.json')
        symbols[events['id']] = 'map.' + source_directory(name)
    for row in ordinary:
        events = source.json('data/maps/' + row['sourceName'] + '/map.json')
        symbols[events['id']] = row['stableId']
    tilesets = tileset_bindings(source)
    from field_cave_art import cave_preview_flags
    previews = cave_preview_flags(source)
    entries = []
    for row in selected:
        name = row['sourceName']
        events = source.json('data/maps/' + name + '/map.json')
        require(symbols[events['id']] == row['stableId'], 'Variant map identity differs: ' + name)
        entry = dict(stableId=row['stableId'], path=row['path'], sourceMapSymbol=events['id'])
        if 'variant' in row:
            entry['variant'] = row['variant']
            entry['layout'] = row['layout']
        entry['playerTexturePath'] = row.get('playerTexturePath', 'sprites/overworld/red-normal.png')
        entry['usesLocalBorder'] = row.get('usesLocalBorder', row.get('renderBorder', True))
        entries.append(entry)
        render_map(source, name, row['stableId'], Path(generated) / row['path'], tilesets=tilesets,
                   render_border=row.get('renderBorder', True), layout_symbol=row.get('layout'),
                   technical_spawn=row.get('technicalSpawn'),
                   cave_preview_flags=previews)
    index = dict(schemaVersion=3, maps=entries, mapSymbols=symbols)
    (Path(generated) / 'maps').mkdir(parents=True, exist_ok=True)
    (Path(generated) / 'maps/world.json').write_bytes(encode(index))
    compile_presentation(Path(raw), Path(generated), selected)
    return dict(maps=len(entries), sourceFiles=len(source.data), selection=len(entries))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=Path, default=PROJECT_ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=Path, default=PROJECT_ROOT / 'content/generated')
    parser.add_argument('--selection', type=Path, default=PROJECT_ROOT / 'data/world-maps.json')
    parser.add_argument('--check', action='store_true', help='Recompile final resources in isolation and compare installed outputs')
    args = parser.parse_args()
    if args.out.resolve().is_relative_to(args.raw.resolve()) or args.raw.resolve().is_relative_to(args.out.resolve()):
        raise ValueError('Source and output trees overlap')
    if args.check:
        from generated_output_compare import generated_output_matches
        with tempfile.TemporaryDirectory(prefix='firered-world-check-') as temp:
            stage = Path(temp)
            result = compile_world(args.raw, stage, args.selection)
            for path in stage.rglob('*'):
                if path.is_file() and not generated_output_matches(args.out / path.relative_to(stage), path.read_bytes()):
                    raise ValueError('Final world output differs: ' + str(path.relative_to(stage)))
    else:
        result = compile_world(args.raw, args.out, args.selection)
    print(json.dumps(dict(result, checked=args.check), sort_keys=True))

if __name__ == '__main__': main()
