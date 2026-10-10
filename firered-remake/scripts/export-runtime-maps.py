#!/usr/bin/env python3
"""Export the reviewed 24 local maps; never changes rich evidence or content IDs.

Only Python's standard library is used. The fixed input hashes authenticate the
development converter outputs; no untrusted self-reported digest is a trust root.
"""
import argparse
import base64
import hashlib
import json
from pathlib import Path
import struct
import sys

VERSION = "firered-runtime-map-v1"
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
# Reviewed local world outputs at c8d92c2. Changes require explicit source review.
INPUTS = {
    "forest-region/forest-north-gate": ("25e1b36f6f355d10b768fd9b81dacf00bae69011f8949e9fb7c8869c02a65cb3", "6233feb1f3f5ee0807bde325c687b09766b7aa6826a00bfa21ef72bf33c9b1c1"),
    "forest-region/forest-south-gate": ("0a562299ff6aa5bf81eedd616f02e1fd73ff4aec0e0865f1be5feb85b791adf8", "6233feb1f3f5ee0807bde325c687b09766b7aa6826a00bfa21ef72bf33c9b1c1"),
    "forest-region/route-2": ("741ddca58b2137e2dad1d2bb3e9817272b5956fbfa92d0aab9541a139087ce7b", "6233feb1f3f5ee0807bde325c687b09766b7aa6826a00bfa21ef72bf33c9b1c1"),
    "forest-region/viridian-forest": ("10e36d4698bf27388cabd00b43f17a535d6985f52440da266dde9f6663365e83", "6233feb1f3f5ee0807bde325c687b09766b7aa6826a00bfa21ef72bf33c9b1c1"),
    "pallet": ("89d9d3575a12c8ed518efd22e045e4f48266a7b430d819394129b15af8345629", "8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4"),
    "pewter-region/pewter-city": ("25d1b9fda9f84a22852accd39438b5fc431e25ddc29e24253113342016fd4382", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "pewter-region/pewter-gym": ("5a699ec37e708286972f2ce9734dfe3cd43b6a09e1a82096fd88eeea300e7eb9", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "pewter-region/pewter-house-1": ("c966a31eb833ba044b1a6128eda52802ce6f57a7e4f9b4aacba2445d30d7413a", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "pewter-region/pewter-house-2": ("eb0eee3fa6babfc10f28b427b12415b7185a8265022f8fd788ad3521f4f7b88f", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "pewter-region/pewter-mart": ("ed9d8588ab564d12f402ed54981aaa8ed8a1f1c0b390144d5648fe443ef9753e", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "pewter-region/pewter-museum-1f": ("f931d6690dd6504c8fbf0cd43aee29bfea8b6c863d6e27985d317ef526ac4f7a", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "pewter-region/pewter-museum-2f": ("bb3e94bcf23fd268ed2a46cd3b2033811706538a9ad39b8f4728877525eced33", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "pewter-region/pewter-pokemon-center-1f": ("5bd8f4ed976aafeed967d8fb3552b8791564ecff6dda4e3743e451d22e12aa64", "a408f7258942f836944ffeddb23f8c38c05818cc0ffe1d50f468afd0d1dea888"),
    "players-house-1f": ("44fb2d0bdc587ecd4af2b4e22cfbeeaeb7ab4701377c9b98a02f4e61f17823ff", "8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4"),
    "players-house-2f": ("a4e2f2cf12930bf02534faec2fd1ea330934d5cb6be3e14571bbe1570c22e959", "8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4"),
    "professor-oaks-lab": ("04eb2f0b77ac2464d4ae8c675af234d8460fc38c0ca61c56c038f0c1fafb2757", "8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4"),
    "rivals-house": ("2323b13e668343488258b0425187b3d0b717ba4b252ab6243fd68673b4198d80", "8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4"),
    "route22-region/route-22": ("458abc3124f82ff0d5844111225525d840f05fe66c13af8e84e998d1df992af8", "6b159958100637881d7d89edead7a6302be24bdfd37991f76e61b113495a62f3"),
    "route22-region/route-22-north-entrance": ("3989b6ef286758f959bd341ce308de9d16802abc4af4f229b2f6f0e0fac504d0", "6b159958100637881d7d89edead7a6302be24bdfd37991f76e61b113495a62f3"),
    "route22-region/route-23": ("da542bd57be0b23cec22c823c26358635babb1a4a0f8721eda2b2a0e2db57f10", "6b159958100637881d7d89edead7a6302be24bdfd37991f76e61b113495a62f3"),
    "viridian-center/viridian-pokemon-center-1f": ("ca396a70ffd1f21966a5f10dc10324f176f48280a694d9fd5e0a322ab077eecd", "6a9b0808f6b28adda753582c1480f9417dc3c745b09f8d15766b7e5083f4bbd3"),
    "viridian-region/route-1": ("354a29169087f2a6400a4320597cf227f851152e5c694e3dc462fa124cb34c6d", "c4c7f01ac8170d3de7435c9f7b3bec8694f743ca7307a2526cc408d3854f3256"),
    "viridian-region/viridian-city": ("5d739dca7cde920dbe05fcf5dc925d65d159f59938c4259ff0e9f831acaa7257", "c4c7f01ac8170d3de7435c9f7b3bec8694f743ca7307a2526cc408d3854f3256"),
    "viridian-region/viridian-mart": ("27a11a663890647db1840aa2d8b8ca68c930d06dab4791b94006545d457ae34b", "c4c7f01ac8170d3de7435c9f7b3bec8694f743ca7307a2526cc408d3854f3256"),
}


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def encode(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True) + "\n").encode("utf-8")


def no_duplicates(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, "Duplicate JSON property: " + key)
        result[key] = value
    return result


def movement(behavior):
    if behavior == 0: return "normal"
    if behavior == 2: return "grassUnsupported"
    if 0x38 <= behavior <= 0x3b: return "ledgeUnsupported"
    if behavior == 0x69: return "doorUnsupported"
    if 0x62 <= behavior <= 0x65: return "arrowWarpUnsupported"
    if 0x6c <= behavior <= 0x6f: return "stairWarpUnsupported"
    if behavior in (0x60, 0x61, 0x66, 0x67, 0x68, 0x71): return "warpUnsupported"
    if behavior in (0x10, 0x11, 0x12, 0x13, 0x15): return "waterUnsupported"
    if behavior == 0x84: return "signBlocked"
    return "unsupported"


def convert(raw, expected_sha, source_hash):
    require(sha(raw) == expected_sha, "Development world differs from reviewed input")
    world = json.loads(raw, object_pairs_hook=no_duplicates)
    require(world["schemaVersion"] == 1 and world["tileSize"] == 16, "Unsupported source version/tile size")
    reference = world["sourceRef"]
    require(reference["commit"] == COMMIT and reference["repository"] == "pret/pokefirered" and reference["kind"] == "pret", "Source identity differs")
    embedded = {}
    for item in world["embeddedSource"]:
        path = item["path"]
        data = base64.b64decode(item["base64"], validate=True)
        require(path not in embedded and sha(data) == item["sha256"], "Embedded source hash/duplicate differs: " + path)
        embedded[path] = data
    require(sha(embedded[reference["path"]]) == reference["sha256"], "Map source hash differs")
    require(json.loads(embedded[reference["path"]]) == world["events"], "Events differ from original")
    layouts = json.loads(embedded["data/layouts/layouts.json"])["layouts"]
    layout = next(v for v in layouts if v.get("id") == world["events"]["layout"])
    require((world["width"], world["height"], world["borderWidth"], world["borderHeight"]) ==
            tuple(layout[k] for k in ("width", "height", "border_width", "border_height")), "Source dimensions differ")
    tables, sources, behaviors = {}, {}, {}
    blocks = []
    for name, width, height, path in (("cells", world["width"], world["height"], layout["blockdata_filepath"]),
                                     ("borderCells", world["borderWidth"], world["borderHeight"], layout["border_filepath"])):
        cells = world[name]
        require(0 < width <= 1024 and 0 < height <= 1024 and width * height <= 262144, "Map dimensions out of bounds")
        require(len(cells) == width * height and len(embedded[path]) == len(cells) * 2, "Incomplete source grid")
        packed = bytearray()
        for i, cell in enumerate(cells):
            block, = struct.unpack_from("<H", embedded[path], i * 2)
            require((cell["x"], cell["y"], cell["rawBlock"], cell["metatileId"], cell["collision"], cell["elevation"]) ==
                    (i % width, i // width, block, block & 1023, (block >> 10) & 3, block >> 12), "Cell block decoding differs")
            src = cell["sourceRef"]
            attribute_path = src["path"]
            local_id = cell["metatileId"] if cell["metatileId"] < 640 else cell["metatileId"] - 640
            require(src["kind"] == "pret" and src["commit"] == COMMIT and src["repository"] == "pret/pokefirered" and
                    src["symbol"] == f"metatile_{local_id:03x}" and sha(embedded[attribute_path]) == src["sha256"], "Cell source differs")
            attribute, = struct.unpack_from("<I", embedded[attribute_path], local_id * 4)
            decoded = (attribute & 511, (attribute >> 9) & 31, (attribute >> 14) & 15, (attribute >> 18) & 63,
                       (attribute >> 24) & 7, (attribute >> 27) & 3, (attribute >> 29) & 3, attribute >> 31)
            require(cell["rawAttributes"] == attribute and decoded == tuple(cell[k] for k in
                    ("behaviorId", "terrain", "attribute2", "attribute3", "encounterType", "attribute5", "layerType", "attribute7")), "Cell attributes differ")
            require(cell["layerType"] <= 2 and cell["movement"] == movement(cell["behaviorId"]), "Cell movement/layer differs")
            entry = (attribute, attribute_path)
            require(tables.get(cell["metatileId"], entry) == entry, "Conflicting metatile identity")
            tables[cell["metatileId"]] = entry
            source = {"path": attribute_path, "sha256": src["sha256"]}
            require(sources.get(attribute_path, source) == source, "Conflicting attribute source")
            sources[attribute_path] = source
            require(behaviors.get(cell["behaviorId"], cell["behaviorName"]) == cell["behaviorName"], "Conflicting behavior symbol")
            behaviors[cell["behaviorId"]] = cell["behaviorName"]
            packed.extend(struct.pack("<H", block))
        blocks.append(base64.b64encode(packed).decode("ascii"))
    source_paths = sorted(sources)
    scripts = []
    for name in ("scripts.inc", "text.inc"):
        path = reference["path"].removesuffix("map.json") + name
        if path in embedded:
            scripts.append({"path": path, "sha256": sha(embedded[path]), "text": embedded[path].decode("utf-8")})
    require(any(s["path"].endswith("/scripts.inc") for s in scripts), "Missing live script dependency")
    return {"schemaVersion": 1, "converterVersion": VERSION, "sourceCommit": COMMIT,
            "sourceWorldSha256": expected_sha, "fullSourceHash": source_hash,
            **{k: world[k] for k in ("stableId", "sourceRef", "width", "height", "tileSize", "borderWidth", "borderHeight",
                                    "texturePath", "artifacts", "events")},
            "technicalSpawn": world.get("technicalSpawn"), "blocks": blocks[0], "borderBlocks": blocks[1],
            "attributeSources": [sources[p] for p in source_paths],
            "metatiles": [[key, value[0], source_paths.index(value[1])] for key, value in sorted(tables.items())],
            "behaviors": [{"id": key, "name": value} for key, value in sorted(behaviors.items())], "scripts": scripts}


def export(generated, output, check=False):
    results, payloads, base_metadata = [], {}, None
    for relative, (expected_sha, source_hash) in sorted(INPUTS.items()):
        source = generated / relative / "world.json"
        raw = source.read_bytes()
        compact = convert(raw, expected_sha, source_hash)
        metadata = json.loads(raw).get("metadata")
        if metadata is not None:
            require(base_metadata is None or metadata == base_metadata, "Conflicting base source metadata")
            base_metadata = metadata
        data = encode(compact)
        path = relative + "/runtime-map.json"
        payloads[path] = data
        results.append({"stableId": compact["stableId"], "path": path, "byteLength": len(data), "sha256": sha(data),
                        "sourcePath": relative + "/world.json", "sourceByteLength": len(raw),
                        "sourceWorldSha256": expected_sha, "fullSourceHash": source_hash})
    require(len(results) == 24 and len({r["stableId"] for r in results}) == 24, "Incomplete/duplicate map collection")
    require(base_metadata is not None and len(base_metadata["files"]) == 128, "Incomplete live base source registry")
    registry_bytes = encode({"schemaVersion": 1, "converterVersion": VERSION, "sourceCommit": COMMIT,
                             "metadata": base_metadata})
    payloads["source-registry.json"] = registry_bytes
    manifest = {"schemaVersion": 1, "converterVersion": VERSION, "sourceCommit": COMMIT,
                "converterSha256": sha(Path(__file__).read_bytes()), "maps": results,
                "sourceRegistry": {"path": "source-registry.json", "byteLength": len(registry_bytes), "sha256": sha(registry_bytes)}}
    manifest_bytes = encode(manifest)
    payloads["manifest.json"] = manifest_bytes
    # Validate everything before any write; atomic per-file replacement prevents truncated files.
    for relative, data in payloads.items():
        target = output / relative
        if check:
            require(target.is_file() and target.read_bytes() == data, "Missing/stale runtime file: " + relative)
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            temporary = target.with_suffix(target.suffix + ".tmp")
            temporary.write_bytes(data)
            temporary.replace(target)
    total = sum(len(v) for v in payloads.values())
    return {"maps": len(results), "sourceWorldBytes": sum(r["sourceByteLength"] for r in results),
            "runtimeMapBytes": total - len(manifest_bytes) - len(registry_bytes), "manifestBytes": len(manifest_bytes),
            "sourceRegistryBytes": len(registry_bytes),
            "runtimeTotalBytes": total, "manifestSha256": sha(manifest_bytes), "converterSha256": manifest["converterSha256"]}


def resolve_cell(generated, stable_id, x, y, border=False):
    for relative, (expected_sha, source_hash) in sorted(INPUTS.items()):
        raw = (generated / relative / "world.json").read_bytes()
        require(sha(raw) == expected_sha, "Development world differs from reviewed input")
        world = json.loads(raw, object_pairs_hook=no_duplicates)
        if world["stableId"] != stable_id:
            continue
        convert(raw, expected_sha, source_hash)
        width, height = (world["borderWidth"], world["borderHeight"]) if border else (world["width"], world["height"])
        require(0 <= x < width and 0 <= y < height, "Source lookup coordinate out of bounds")
        cell = world["borderCells" if border else "cells"][y * width + x]
        embedded = {e["path"]: e for e in world["embeddedSource"]}
        layouts = json.loads(base64.b64decode(embedded["data/layouts/layouts.json"]["base64"]))["layouts"]
        layout = next(v for v in layouts if v.get("id") == world["events"]["layout"])
        block_path = layout["border_filepath" if border else "blockdata_filepath"]
        local_id = cell["metatileId"] if cell["metatileId"] < 640 else cell["metatileId"] - 640
        return {"stableId": stable_id, "x": x, "y": y, "border": border, "sourceCommit": COMMIT,
                "sourceWorldPath": relative + "/world.json", "sourceWorldSha256": expected_sha,
                "gridSource": {"path": block_path, "sha256": embedded[block_path]["sha256"], "byteOffset": (y * width + x) * 2},
                "attributeSource": {**cell["sourceRef"], "byteOffset": local_id * 4},
                "rawBlock": cell["rawBlock"], "rawAttributes": cell["rawAttributes"], "behaviorName": cell["behaviorName"]}
    raise ValueError("Unknown runtime map ID: " + stable_id)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--generated", type=Path, default=Path(__file__).resolve().parents[1] / "content/generated")
    parser.add_argument("--output", type=Path, help="Default: content/generated/runtime-maps")
    parser.add_argument("--check", action="store_true", help="Verify deterministic output without writing")
    parser.add_argument("--resolve", nargs=3, metavar=("MAP_ID", "X", "Y"), help="Resolve a runtime cell to local source evidence without writing")
    parser.add_argument("--border", action="store_true", help="Resolve border-grid coordinates instead of map coordinates")
    args = parser.parse_args()
    if args.resolve:
        require(not args.check and args.output is None, "Resolve cannot be combined with output/check")
        result = resolve_cell(args.generated, args.resolve[0], int(args.resolve[1]), int(args.resolve[2]), args.border)
    else:
        require(not args.border, "Border requires --resolve")
        result = export(args.generated, args.output or args.generated / "runtime-maps", args.check)
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, struct.error, StopIteration) as error:
        print("Runtime map export failed: " + str(error), file=sys.stderr)
        sys.exit(1)
