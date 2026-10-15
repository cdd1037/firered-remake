"""Single compact runtime map format; original text is read from the shared raw tree."""
import base64
import json
from pathlib import Path
import struct

VERSION = "firered-runtime-map-v2"
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
RAW_ROOT = Path(__file__).resolve().parents[1] / "content/raw/pokefirered/037335f4"

def require(value, message):
    if not value:
        raise ValueError(message)


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


def compact_world(world, sources=None):
    require(world["schemaVersion"] == 1 and world["tileSize"] == 16, "Unsupported source version/tile size")
    reference = world["sourceRef"]
    require(reference["commit"] == COMMIT and reference["repository"] == "pret/pokefirered" and reference["kind"] == "pret", "Source identity differs")
    embedded = dict(sources or {})
    for item in world.get("embeddedSource", []):
        path = item["path"]
        data = base64.b64decode(item["base64"], validate=True)
        require(path not in embedded or embedded[path] == data, "Conflicting embedded source path: " + path)
        embedded[path] = data
    if not embedded:
        embedded = {p.relative_to(RAW_ROOT).as_posix(): p.read_bytes() for p in RAW_ROOT.rglob("*") if p.is_file()}
    layouts = json.loads(embedded["data/layouts/layouts.json"])["layouts"]
    layout = world.get("layout") or next(v for v in layouts if v.get("id") == world["events"]["layout"])
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
            require(src['kind'] == 'pret' and src['commit'] == COMMIT and (src['repository'] == 'pret/pokefirered') and (src['symbol'] == f'metatile_{local_id:03x}'), "Cell source differs")
            attribute, = struct.unpack_from("<I", embedded[attribute_path], local_id * 4)
            decoded = (attribute & 511, (attribute >> 9) & 31, (attribute >> 14) & 15, (attribute >> 18) & 63,
                       (attribute >> 24) & 7, (attribute >> 27) & 3, (attribute >> 29) & 3, attribute >> 31)
            require(cell["rawAttributes"] == attribute and decoded == tuple(cell[k] for k in
                    ("behaviorId", "terrain", "attribute2", "attribute3", "encounterType", "attribute5", "layerType", "attribute7")), "Cell attributes differ")
            require(cell["layerType"] <= 2 and cell["movement"] == movement(cell["behaviorId"]), "Cell movement/layer differs")
            entry = (attribute, attribute_path)
            require(tables.get(cell["metatileId"], entry) == entry, "Conflicting metatile identity")
            tables[cell["metatileId"]] = entry
            source = {"path": attribute_path}
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
            scripts.append(path)
    require(any(s.endswith("/scripts.inc") for s in scripts), "Missing live script dependency")
    return {"schemaVersion": 2, "converterVersion": VERSION, "sourceCommit": COMMIT,
            **{k: world[k] for k in ("stableId", "width", "height", "tileSize", "borderWidth", "borderHeight",
                                    "texturePath", "events")},
            "sourceRef": {k: v for k, v in reference.items() if k != "sha256"},
            "artifacts": [{k: v for k, v in item.items() if k != "sha256"} for item in world["artifacts"]],
            "technicalSpawn": world.get("technicalSpawn"), "blocks": blocks[0], "borderBlocks": blocks[1],
            "attributeSources": [sources[p] for p in source_paths],
            "metatiles": [[key, value[0], source_paths.index(value[1])] for key, value in sorted(tables.items())],
            "behaviors": [{"id": key, "name": value} for key, value in sorted(behaviors.items())], "scriptPaths": scripts}



def normalize_runtime(document):
    require(document.get("schemaVersion") == 2 and document.get("converterVersion") == VERSION,
            "Regenerate map with the current source importer")
    require(document.get("scriptPaths") and not any(key in document for key in ("scripts", "cells", "embeddedSource")),
            "Runtime map must contain source paths, not embedded data")
    return dict(document)


def convert(raw):
    document = json.loads(raw, object_pairs_hook=no_duplicates) if isinstance(raw, (bytes, str)) else raw
    return compact_world(document) if "cells" in document else normalize_runtime(document)


def runtime_cells(document, *, border=False):
    """Inspect compact cells in memory; never recreate persisted per-cell provenance."""
    document = normalize_runtime(document)
    width, height = ((document['borderWidth'], document['borderHeight']) if border
                     else (document['width'], document['height']))
    require(type(width) is int and type(height) is int and 0 < width <= 1024 and 0 < height <= 1024
            and width * height <= 262144, 'Map dimensions out of bounds')
    blocks = base64.b64decode(document['borderBlocks' if border else 'blocks'], validate=True)
    require(len(blocks) == width * height * 2, 'Incomplete compact map grid')
    attributes = {row[0]: row[1] for row in document['metatiles']}
    require(len(attributes) == len(document['metatiles']), 'Duplicate compact metatile')
    behaviors = {row['id']: row['name'] for row in document['behaviors']}
    for index, (block,) in enumerate(struct.iter_unpack('<H', blocks)):
        metatile = block & 1023
        require(metatile in attributes, 'Missing compact metatile')
        attribute = attributes[metatile]; behavior = attribute & 511
        require(behavior in behaviors, 'Missing compact behavior')
        yield dict(x=index % width, y=index // width, rawBlock=block, metatileId=metatile,
                   collision=(block >> 10) & 3, elevation=block >> 12, rawAttributes=attribute,
                   behaviorId=behavior, behaviorName=behaviors[behavior], terrain=(attribute >> 9) & 31,
                   attribute2=(attribute >> 14) & 15, attribute3=(attribute >> 18) & 63,
                   encounterType=(attribute >> 24) & 7, attribute5=(attribute >> 27) & 3,
                   layerType=(attribute >> 29) & 3, attribute7=attribute >> 31, movement=movement(behavior))
