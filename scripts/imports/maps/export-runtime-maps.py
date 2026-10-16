#!/usr/bin/env python3
"""Write direct-path catalogs for the single compact map format, or inspect a source cell."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import base64
import json
from pathlib import Path
import struct
import sys
from map_runtime import VERSION, COMMIT, compact_world, normalize_runtime, convert, encode, require, no_duplicates

from runtime_map_manifest import compose, region_paths

DEFAULT_REGIONS = ('opening', 'viridian', 'viridian-center', 'forest', 'pewter', 'route22')


def export(generated, output, check=False, *, regions=DEFAULT_REGIONS, paths=(), previous=None):
    selected = region_paths(generated, regions) if regions else []
    return compose(generated, output, [*selected, *paths], previous=previous, check=check)


def resolve_cell(generated, stable_id, x, y, border=False):
    from types import SimpleNamespace
    from map_runtime import RAW_ROOT, runtime_cells
    from map_source import tileset_bindings
    for relative in sorted(region_paths(generated, DEFAULT_REGIONS)):
        document = json.loads((generated / relative).read_bytes(), object_pairs_hook=no_duplicates)
        if document["stableId"] != stable_id:
            continue
        width, height = (document["borderWidth"], document["borderHeight"]) if border else (document["width"], document["height"])
        require(0 <= x < width and 0 <= y < height, "Source lookup coordinate out of bounds")
        cell = next(cell for cell in runtime_cells(document, border=border) if (cell['x'], cell['y']) == (x, y))
        source_path = 'data/maps/' + document['sourceMapName'] + '/map.json'
        events = json.loads((RAW_ROOT / source_path).read_bytes())
        layouts = json.loads((RAW_ROOT / 'data/layouts/layouts.json').read_bytes())['layouts']
        layout = next(row for row in layouts if row.get('id') == events['layout'])
        source = SimpleNamespace(text=lambda path: (RAW_ROOT / path).read_text())
        bindings = tileset_bindings(source)
        tileset = layout['primary_tileset' if cell['metatileId'] < 640 else 'secondary_tileset']
        return dict(stableId=stable_id, x=x, y=y, border=border, mapPath=relative,
            sourceCommit=COMMIT, sourceMapPath=source_path,
            attributeSource=dict(path='data/tilesets/' + bindings[tileset] + '/metatile_attributes.bin',
                byteOffset=(cell['metatileId'] if cell['metatileId'] < 640 else cell['metatileId'] - 640) * 4),
            **{key: cell[key] for key in ('rawBlock', 'collision', 'elevation', 'rawAttributes', 'behaviorName')})
    raise ValueError("Unknown runtime map ID: " + stable_id)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--generated", type=Path, default=_PROJECT_ROOT / "content/generated")
    parser.add_argument("--regions", nargs="+", help="Explicit region directories under maps/regions")
    parser.add_argument("--map-path", action="append", default=[], help="Explicit generated-relative compact map path")
    parser.add_argument("--previous", type=Path, help="Optional existing catalog to extend")
    parser.add_argument("--output", type=Path, help="Default: content/generated/maps/catalogs/maps.json")
    parser.add_argument("--check", action="store_true", help="Verify deterministic output without writing")
    parser.add_argument("--resolve", nargs=3, metavar=("MAP_ID", "X", "Y"), help="Resolve a runtime cell to local source evidence without writing")
    parser.add_argument("--border", action="store_true", help="Resolve border-grid coordinates instead of map coordinates")
    args = parser.parse_args()
    if args.resolve:
        require(not args.check and args.output is None and not args.regions and not args.map_path and args.previous is None, "Resolve cannot be combined with output/check/selection")
        result = resolve_cell(args.generated, args.resolve[0], int(args.resolve[1]), int(args.resolve[2]), args.border)
    else:
        require(not args.border, "Border requires --resolve")
        result = export(args.generated, args.output or args.generated / "maps/catalogs/maps.json", args.check,
                        regions=args.regions if args.regions is not None else (() if args.map_path or args.previous else DEFAULT_REGIONS),
                        paths=args.map_path, previous=args.previous)
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, struct.error, StopIteration) as error:
        print("Runtime map export failed: " + str(error), file=sys.stderr)
        sys.exit(1)
