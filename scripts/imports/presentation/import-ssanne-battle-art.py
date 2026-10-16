#!/usr/bin/env python3
"""Authenticate original S.S. Anne portraits and departure sheets; execute no upstream code."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import importlib.util
import io
import json
import pathlib
import re
import subprocess

from PIL import Image

ROOT = _PROJECT_ROOT
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
PROFILE = "firered-ssanne-battle-art-v1"
LOCK_BYTES = 5576
PORTRAITS = (("sailor-front", "Sailor", "sailor", 85),
             ("fisherman-front", "Fisherman", "fisherman", 94),
             ("gentleman-front", "Gentleman", "gentleman", 123),
             ("rival-late-front", "RivalLate", "rival_late", 124))
# Reuse local reviewed bounded I/O, safe-path and canonical serialization utilities.
from trainer_portraits import canonical, safe


def read_lock(path=None):
    data = (path or safe(ROOT, 'scripts/recipes/presentation/ssanne-battle-art.lock.json')).read_bytes()
    lock = json.loads(data)
    if (lock['repository'], lock['commit'], lock['profile'], lock['gameVersion'], lock['revision'], len(lock['files'])) != ('pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0, 22):
        raise ValueError("S.S. Anne art source identity differs")
    return lock


def authenticated_sources(repository, raw, lock):
    if repository:
        tree = subprocess.check_output(["git", "-C", str(repository), "rev-parse", COMMIT + "^{tree}"]).decode().strip()
    result = {}
    for entry in lock["files"]:
        path = entry["path"]
        target = safe(raw, path)
        if path in result:
            raise ValueError("Duplicate source path")
        if repository:
            row = subprocess.check_output(["git", "-C", str(repository), "ls-tree", COMMIT, "--", path]).decode().split()
            data = subprocess.check_output(["git", "-C", str(repository), "show", COMMIT + ":" + path])
        else:
            data = (target).read_bytes()
        result[path] = data
    return result


def build(sources, lock):
    def require(path, text):
        if text not in sources[path].decode():
            raise ValueError("Source contract missing: " + text)

    require("tools/gbagfx/gfx.c", "#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)")
    require("src/battle_controller_opponent.c", "(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40")
    require("src/battle_controller_opponent.c", "CreateSprite(&gMultiuseSpriteTemplate,\n                                                     176,")
    payloads, assets = {}, []

    def convert(identity, source_image, source_palette, size, **metadata):
        with Image.open(io.BytesIO(sources[source_image])) as image:
            if image.mode != "P" or image.size != size:
                raise ValueError("Indexed source geometry differs: " + identity)
            indices = image.tobytes()
        lines = sources[source_palette].decode().splitlines()
        if lines[:3] != ["JASC-PAL", "0100", "16"] or len(lines) != 19:
            raise ValueError("Original palette shape differs")
        colors = [tuple(map(int, line.split())) for line in lines[3:]]
        if any(len(c) != 3 or any(v < 0 or v > 255 for v in c) for c in colors) or max(indices) > 15:
            raise ValueError("Palette channel/index differs")
        colors = [tuple((v // 8 << 3) | (v // 8 >> 2) for v in c) for c in colors]
        rgba = bytes(v for i in indices for v in (*colors[i], 255 if i else 0))
        png = io.BytesIO()
        Image.frombytes("RGBA", size, rgba).save(png, format="PNG")
        payloads[identity + '.png'] = png.getvalue()
        assets.append(dict(id=identity, sourceImage=source_image, sourcePalette=source_palette,
                           width=size[0], height=size[1],  **metadata))

    graphics = "src/data/graphics/trainers.h"
    table_path = "src/data/trainer_graphics/front_pic_tables.h"
    coordinates = re.findall(r"\{\.size = (\d+), \.y_offset = (\d+)\}", sources[table_path].decode().split("const struct CompressedSpriteSheet")[0])
    for identity, symbol, stem, picture in PORTRAITS:
        require(graphics, 'gTrainerFrontPic_' + symbol + '[] = INCBIN_U32("graphics/trainers/front_pics/' + stem + '_front_pic.4bpp.lz")')
        require(graphics, 'gTrainerPalette_' + symbol + '[] = INCBIN_U32("graphics/trainers/palettes/' + stem + '.gbapal.lz")')
        require(table_path, "TRAINER_SPRITE(" + stem.upper() + ", gTrainerFrontPic_" + symbol + ", 0x800)")
        require(table_path, "TRAINER_PAL(" + stem.upper() + ", gTrainerPalette_" + symbol + ")")
        value = re.search(r"^#define TRAINER_PIC_" + stem.upper() + r"\s+(\d+)$", sources["include/constants/trainers.h"].decode(), re.M)
        if not value or int(value[1]) != picture or coordinates[picture] != ("8", "1"):
            raise ValueError("Trainer source identity/coordinates differ")
        convert(identity, "graphics/trainers/front_pics/" + stem + "_front_pic.png", "graphics/trainers/palettes/" + stem + ".pal",
                (64, 64), kind="trainer", x=144, y=8, sourceTrainerPicId=picture, sourceFrame=0)

    source = sources["src/ss_anne.c"].decode()
    require("src/data/object_events/object_event_graphics.h", 'gObjectEventPal_SSAnne[] = INCBIN_U16("graphics/object_events/palettes/ss_anne.gbapal")')
    require("src/event_object_movement.c", "{gObjectEventPal_SSAnne,                  OBJ_EVENT_PAL_TAG_SS_ANNE}")
    require("src/event_object_movement.c", "sprite->oam.paletteNum = graphicsInfo->paletteSlot;")
    info = re.search(r"gObjectEventGraphicsInfo_SSAnne = \{(.*?)\n\};", sources["src/data/object_events/object_event_graphics_info.h"].decode(), re.S)[1]
    if ".paletteTag = OBJ_EVENT_PAL_TAG_SS_ANNE," not in info or ".paletteSlot = PALSLOT_NPC_SPECIAL," not in info:
        raise ValueError("Departure palette inheritance differs")
    slots = re.search(r"enum \{(.*?)\};", sources["include/event_object_movement.h"].decode(), re.S)[1]
    if re.findall(r"\b(PALSLOT_\w+),", slots).index("PALSLOT_NPC_SPECIAL") != 10:
        raise ValueError("Departure palette slot differs")
    require("src/sprite.c", "sprite->animDelayCounter = duration;")
    require("src/sprite.c", "if (duration)\n        duration--;")
    for name, size, frame_height, expected, looping in (
            ("Wake", (16, 64), 32, [(0, 12), (8, 12)], True),
            ("Smoke", (16, 72), 16, [(0, 10), (4, 20), (8, 20), (12, 30)], False)):
        body = re.search(r"s" + name + r"Anim\[\] = \{(.*?)\n\};", source, re.S)[1]
        commands = [(int(a), int(b)) for a, b in re.findall(r"ANIMCMD_FRAME\(\s*(\d+),\s*(\d+)\)", body)]
        if commands != expected or ("ANIMCMD_JUMP(0)" if looping else "ANIMCMD_END") not in body:
            raise ValueError("Departure animation source differs")
        require("src/ss_anne.c", 's' + name + 'Tiles[] = INCBIN_U16("graphics/ss_anne/' + name.lower() + '.4bpp")')
        require("src/ss_anne.c", "gSprites[spriteId].oam.paletteNum = 10;")
        oam = re.search(r"s" + name + r"OamData = \{(.*?)\n\};", source, re.S)[1]
        if (".shape = " + ("ST_OAM_V_RECTANGLE" if looping else "ST_OAM_SQUARE") not in oam or
                ".size = " + ("2" if looping else "1") not in oam):
            raise ValueError("Departure OAM geometry differs")
        frames = [dict(sourceTileOffset=tile, x=0, y=tile // 2 * 8, width=16, height=frame_height, duration=duration)
                  for tile, duration in commands]
        convert(name.lower(), "graphics/ss_anne/" + name.lower() + ".png", "graphics/object_events/palettes/ss_anne.pal", size,
                kind="departure", paletteSlot=10, loops=looping, frames=frames)

    # Values retain original task/callback integer arithmetic, distinct from animation frame durations.
    cadence = dict(initialWaitTicks=50, finishWaitTicks=40, boatMoveTickDivisor=5, smokePeriodTicks=70,
                   boatExitXExclusive=-120, wakeBoatOffsetX=80, wakeCenterY=109, wakeDriftTickDivisor=6,
                   wakeMaximumDrift=22, wakeDestroyXExclusive=-18, wakePriority=2, wakeSubpriority=255,
                   smokeBoatOffsetX=49, smokeCenterY=78, smokeMinimumSpawnX=-32,
                   smokeDriftTickDivisor=4, smokeSubpriority=8, soundId="SE_SS_ANNE_HORN")
    for text in ["gTasks[taskId].data[0] = 50;", "if (++data[3] == 40)", "x = data[2] / 5;",
                 "if (data[1] == 70)", ".x2 < -120)", ".x2 + 80;", "x, 109, 0xFF)",
                 "sprite->data[0] / 6 < 22", "sprite->x2 = sprite->data[0] / 6;", "sprite->x + sprite->x2 < -18",
                 "gSprites[spriteId].oam.priority = 2;", ".x2 + 49;", "x, 78, 8)", "if ((s16)x >= -32)",
                 "sprite->x2 = sprite->data[0] / 4;", "PlaySE(SE_SS_ANNE_HORN);", "if (sprite->animEnded)"]:
        require("src/ss_anne.c", text)
    document = dict(schemaVersion=1,
                      paletteExpansion="(rgb5 << 3) | (rgb5 >> 2)",
                    assets=assets, departure=cadence,
                    outputs=[dict(path=path) for path, data in sorted(payloads.items())])
    manifest = runtime_json_bytes(document, sort_keys=True)
    payloads["manifest.json"] = manifest
    return payloads


def run(repository, raw, output):
    raw, output = pathlib.Path(raw), pathlib.Path(output)
    if raw.resolve().is_relative_to(output.resolve()) or output.resolve().is_relative_to(raw.resolve()):
        raise ValueError("Raw/output directories overlap")
    lock = read_lock()
    sources = authenticated_sources(repository, raw, lock)
    payloads = build(sources, lock)
    for path in payloads:
        safe(output, path)
    # No source or output is written before all source and conversion contracts have passed.
    if repository:
        for path, data in sources.items():
            target = safe(raw, path)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    output.mkdir(parents=True, exist_ok=True)
    for path, data in payloads.items():
        safe(output, path).write_bytes(data)
    print(json.dumps(dict(passed=True, assets=6, sourceInputs=len(sources), outputs=len(payloads),
                         manifestBytes=len(payloads["manifest.json"]))))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-repo", type=pathlib.Path)
    parser.add_argument("--raw", type=pathlib.Path, default=ROOT / "content/raw/pokefirered/037335f4")
    parser.add_argument("--output", type=pathlib.Path, default=ROOT / "content/generated/battle/art/ssanne-battle-art")
    arguments = parser.parse_args()
    run(arguments.source_repo, arguments.raw, arguments.output)
