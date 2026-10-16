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

# Map paths for the initial regional composition.
INPUTS = {
    'maps/regions/forest/forest-north-gate.json',
    'maps/regions/forest/forest-south-gate.json',
    'maps/regions/forest/route-2.json',
    'maps/regions/forest/viridian-forest.json',
    'maps/regions/opening/pallet.json',
    'maps/regions/pewter/pewter-city.json',
    'maps/regions/pewter/pewter-gym.json',
    'maps/regions/pewter/pewter-house-1.json',
    'maps/regions/pewter/pewter-house-2.json',
    'maps/regions/pewter/pewter-mart.json',
    'maps/regions/pewter/pewter-museum-1f.json',
    'maps/regions/pewter/pewter-museum-2f.json',
    'maps/regions/pewter/pewter-pokemon-center-1f.json',
    'maps/regions/opening/players-house-1f.json',
    'maps/regions/opening/players-house-2f.json',
    'maps/regions/opening/professor-oaks-lab.json',
    'maps/regions/opening/rivals-house.json',
    'maps/regions/route22/route-22.json',
    'maps/regions/route22/route-22-north-entrance.json',
    'maps/regions/route22/route-23.json',
    'maps/regions/viridian-center/viridian-pokemon-center-1f.json',
    'maps/regions/viridian/route-1.json',
    'maps/regions/viridian/viridian-city.json',
    'maps/regions/viridian/viridian-mart.json',
}


def export(generated, output, check=False):
    from runtime_map_manifest import compose
    return compose(generated, output, INPUTS, check=check)


def resolve_cell(generated, stable_id, x, y, border=False):
    for relative in sorted(INPUTS):
        path = relative
        document = json.loads((generated / path).read_bytes(), object_pairs_hook=no_duplicates)
        if document["stableId"] != stable_id:
            continue
        width, height = (document["borderWidth"], document["borderHeight"]) if border else (document["width"], document["height"])
        require(0 <= x < width and 0 <= y < height, "Source lookup coordinate out of bounds")
        blocks = base64.b64decode(document["borderBlocks" if border else "blocks"], validate=True)
        block, = struct.unpack_from("<H", blocks, (y * width + x) * 2)
        tile = next(row for row in document["metatiles"] if row[0] == block & 1023)
        attribute_path = document["attributeSources"][tile[2]]["path"]
        behavior_name = next(row["name"] for row in document["behaviors"] if row["id"] == tile[1] & 511)
        return {"stableId": stable_id, "x": x, "y": y, "border": border, "mapPath": path,
                "sourceCommit": COMMIT, "sourceMapPath": document["sourceRef"]["path"],
                "attributeSource": {"path": attribute_path, "byteOffset": (tile[0] if tile[0] < 640 else tile[0] - 640) * 4},
                "rawBlock": block, "collision": (block >> 10) & 3, "elevation": block >> 12,
                "rawAttributes": tile[1], "behaviorName": behavior_name}
    raise ValueError("Unknown runtime map ID: " + stable_id)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--generated", type=Path, default=_PROJECT_ROOT / "content/generated")
    parser.add_argument("--output", type=Path, help="Default: content/generated/maps/catalogs/maps.json")
    parser.add_argument("--check", action="store_true", help="Verify deterministic output without writing")
    parser.add_argument("--resolve", nargs=3, metavar=("MAP_ID", "X", "Y"), help="Resolve a runtime cell to local source evidence without writing")
    parser.add_argument("--border", action="store_true", help="Resolve border-grid coordinates instead of map coordinates")
    args = parser.parse_args()
    if args.resolve:
        require(not args.check and args.output is None, "Resolve cannot be combined with output/check")
        result = resolve_cell(args.generated, args.resolve[0], int(args.resolve[1]), int(args.resolve[2]), args.border)
    else:
        require(not args.border, "Border requires --resolve")
        result = export(args.generated, args.output or args.generated / "maps/catalogs/maps.json", args.check)
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, struct.error, StopIteration) as error:
        print("Runtime map export failed: " + str(error), file=sys.stderr)
        sys.exit(1)
