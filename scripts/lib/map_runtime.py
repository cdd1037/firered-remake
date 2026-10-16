"""Single compact runtime map format; original text is read from the shared raw tree."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import base64
import json
from pathlib import Path
import struct

VERSION = "firered-runtime-map-v3"
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
RAW_ROOT = _PROJECT_ROOT / "content/raw/pokefirered/037335f4"

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





def normalize_runtime(document):
    require(document.get("schemaVersion") == 3,
            "Regenerate map with the current source importer")
    require(document.get("scriptPaths") and not any(key in document for key in ("scripts", "cells", "embeddedSource")),
            "Runtime map must contain source paths, not embedded data")
    return dict(document)




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




def generated_root(package_directory):
    """Locate the explicit generated tree for a package at any functional depth."""
    path = Path(package_directory).resolve()
    for parent in (path, *path.parents):
        if parent.name == 'generated' and parent.parent.name == 'content':
            return parent
    raise ValueError('Map output must be inside an explicit content/generated tree: ' + str(path))
