#!/usr/bin/env python3
"""Import pinned FireRed Youngster/Lass frames without executing upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import io
import json
import pathlib
import re
import subprocess

from PIL import Image

ROOT = _PROJECT_ROOT
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
PORTRAITS = (("youngster-front", "Youngster", 82), ("lass-front", "Lass", 84))


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + "\n").encode()


def safe(root, relative):
    path = pathlib.PurePosixPath(relative)
    if path.is_absolute() or any(part in (".", "..") for part in relative.split("/")) or "\\" in relative:
        raise ValueError("Unsafe source path: " + relative)
    target = root / path
    if any(part.is_symlink() for part in (target, *target.parents)):
        raise ValueError("Source symlink rejected: " + relative)
    return target


def run(repository, raw, output):
    lock_bytes = (ROOT / "scripts/recipes/trainers/route3-trainer-art.lock.json").read_bytes()
    lock = json.loads(lock_bytes)
    project = json.loads((ROOT / "scripts/recipes/config/source.json").read_bytes())
    if (project["source"]["repository"], project["source"]["sourceCommit"],
            project["referenceGame"]["gameEdition"], project["referenceGame"]["romRevision"]) != (
            "pret/pokefirered", COMMIT, "FIRERED", 0):
        raise ValueError("Project source profile differs")
    if (lock["repository"], lock["commit"], len(lock["files"])) != ("pret/pokefirered", COMMIT, 9):
        raise ValueError("Source identity differs")
    sources = {}
    for entry in lock["files"]:
        path = entry["path"]
        target = safe(raw, path)
        data = (subprocess.check_output(["git", "-C", str(repository), "show", COMMIT + ":" + path])
                if repository else target.read_bytes())
        sources[path] = data
    # Validate every input before persisting any recovered source.
    if repository:
        for path, data in sources.items():
            target = safe(raw, path)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)

    def require(path, needle):
        if needle not in sources[path].decode():
            raise ValueError("Source contract missing: " + needle)

    graphics = "src/data/graphics/trainers.h"
    table_path = "src/data/trainer_graphics/front_pic_tables.h"
    table = sources[table_path].decode().split("const struct CompressedSpriteSheet")[0]
    coordinates = re.findall(r"\{\.size = (\d+), \.y_offset = (\d+)\}", table)
    require("src/battle_controller_opponent.c", "(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40")
    require("src/battle_controller_opponent.c", "CreateSprite(&gMultiuseSpriteTemplate,\n                                                     176,")
    require("tools/gbagfx/gfx.c", "#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)")
    output.mkdir(parents=True, exist_ok=True)
    assets = []
    for identity, symbol, source_id in PORTRAITS:
        stem = symbol.lower()
        image_path = "graphics/trainers/front_pics/" + stem + "_front_pic.png"
        palette_path = "graphics/trainers/palettes/" + stem + ".pal"
        require(graphics, 'gTrainerFrontPic_' + symbol + '[] = INCBIN_U32("graphics/trainers/front_pics/' + stem + '_front_pic.4bpp.lz")')
        require(graphics, 'gTrainerPalette_' + symbol + '[] = INCBIN_U32("graphics/trainers/palettes/' + stem + '.gbapal.lz")')
        require(table_path, "TRAINER_SPRITE(" + stem.upper() + ", gTrainerFrontPic_" + symbol + ", 0x800)")
        require(table_path, "TRAINER_PAL(" + stem.upper() + ", gTrainerPalette_" + symbol + ")")
        match = re.search(r"^#define TRAINER_PIC_" + stem.upper() + r"\s+(\d+)$",
                          sources["include/constants/trainers.h"].decode(), re.M)
        if not match or int(match[1]) != source_id or coordinates[source_id] != ("8", "1"):
            raise ValueError("Trainer source identity or coordinates differ: " + identity)
        with Image.open(io.BytesIO(sources[image_path])) as image:
            if image.mode != "P" or image.size != (64, 64):
                raise ValueError("Trainer source geometry differs: " + identity)
            indices = image.tobytes()
        lines = sources[palette_path].decode().splitlines()
        if lines[:3] != ["JASC-PAL", "0100", "16"] or len(lines) != 19:
            raise ValueError("Palette shape differs")
        original_colors = [tuple(map(int, line.split())) for line in lines[3:]]
        if any(len(color) != 3 or any(value < 0 or value > 255 for value in color) for color in original_colors):
            raise ValueError("Palette channel differs")
        colors = [tuple((value // 8 << 3) | (value // 8 >> 2) for value in color) for color in original_colors]
        if max(indices) > 15 or not any(indices) or 0 not in indices:
            raise ValueError("Trainer palette indices differ")
        rgba = bytes(value for index in indices for value in (*colors[index], 255 if index else 0))
        Image.frombytes("RGBA", (64, 64), rgba).save(safe(output, identity + ".png"))
        assets.append(dict(id=identity, texturePath=identity + '.png', width=64, height=64, x=176 - 32, y=(8 - int(coordinates[source_id][0])) * 4 + 40 - 32, sourceImage=image_path, sourcePalette=palette_path, sourceFrame=0, sourceTrainerPicId=source_id))
    document = dict(
                     paletteExpansion="(rgb5 << 3) | (rgb5 >> 2)", assets=assets)
    data = runtime_json_bytes(document, sort_keys=True)
    safe(output, "route3-trainer-art.json").write_bytes(data)
    print(json.dumps(dict( assets=assets), indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-repo", type=pathlib.Path)
    parser.add_argument("--raw", type=pathlib.Path, default=ROOT / "content/raw/pokefirered/037335f4")
    parser.add_argument("--output", type=pathlib.Path, default=ROOT / "content/generated/sprites/trainers/route3")
    args = parser.parse_args()
    run(args.source_repo, args.raw, args.output)
