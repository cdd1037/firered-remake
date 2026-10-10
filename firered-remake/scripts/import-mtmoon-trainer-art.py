#!/usr/bin/env python3
"""Import pinned FireRed Super Nerd/Hiker/male Rocket grunt frames without executing upstream code."""
import argparse
import hashlib
import io
import json
import pathlib
import re
import subprocess

from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
LOCK_SHA256 = "16e4fd0fa4f8a3ed416971c6e1c20693bac5fdc9640107db7c5b277c16b513c8"
# Explicit FireRed constants; do not infer source stems from localized/display IDs.
GENERATED_SHA256 = "dfb25c3c715441b3b965eb7997167065942e9d387249c7feb244ed7b710d7f06"
PORTRAITS = (
    ("super-nerd-front", "SuperNerd", "super_nerd", 89,
     "c249e917d815fca6c88ecb031e03f15857f33d87abd3e3082a534ae3bc5bde79",
     "63d8c8b1462d21352b41dcdaeb18bfc3b830d8409df07cb33a12c8b4e4f163b0"),
    ("hiker-front", "Hiker", "hiker", 90,
     "89c69751463d71c94db96e0ca121b84c41cd8a7ce57b32cbe2e383e4320cb077",
     "51301a38d1ee2d09ae71ac200787cbf6c5d7c1ea214c11283ae1e8d60642fd62"),
    ("rocket-m-front", "RocketGruntM", "rocket_grunt_m", 109,
     "815e95542cdc0a8cae424ced4f6fc9b981049550d47b343324c1d0a6ec1b3bad",
     "b17c029b414486e34dad2a964e8742264c874bf47fe49d2f98ee6268cac359cd"),
)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def canonical(value):
    return (json.dumps(value, sort_keys=True, indent=2) + "\n").encode()


def safe(root, relative):
    path = pathlib.PurePosixPath(relative)
    if path.is_absolute() or any(part in ("", ".", "..") for part in relative.split("/")) or "\\" in relative:
        raise ValueError("Unsafe source path: " + relative)
    target = root / path
    if any(part.is_symlink() for part in (target, *target.parents)):
        raise ValueError("Source symlink rejected: " + relative)
    return target


def read_exact(path, expected_size):
    if not 0 < expected_size <= 100_000 or path.stat().st_size != expected_size:
        raise ValueError("Source size differs: " + str(path))
    with path.open("rb") as stream:
        data = stream.read(expected_size + 1)
    if len(data) != expected_size:
        raise ValueError("Source size differs: " + str(path))
    return data


def run(repository, raw, output):
    lock_bytes = read_exact(safe(ROOT, "scripts/mtmoon-trainer-art.lock.json"), 2796)
    if sha(lock_bytes) != LOCK_SHA256:
        raise ValueError("Lock hash differs")
    lock = json.loads(lock_bytes)
    project = json.loads((ROOT / "content/source-lock.json").read_bytes())
    if (project["source"]["repository"], project["source"]["sourceCommit"],
            project["referenceGame"]["gameEdition"], project["referenceGame"]["romRevision"]) != (
            "pret/pokefirered", COMMIT, "FIRERED", 0):
        raise ValueError("Project source profile differs")
    if (lock["repository"], lock["commit"], len(lock["files"])) != ("pret/pokefirered", COMMIT, 11):
        raise ValueError("Source identity differs")
    sources = {}
    for entry in lock["files"]:
        path = entry["path"]
        target = safe(raw, path)
        data = (subprocess.check_output(["git", "-C", str(repository), "show", COMMIT + ":" + path])
                if repository else read_exact(target, entry["byteLength"]))
        git_blob = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
        if (len(data), sha(data), git_blob) != (entry["byteLength"], entry["sha256"], entry["gitBlobSha1"]):
            raise ValueError("Source hash differs: " + path)
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
    assets = []
    payloads = {}
    for identity, symbol, stem, source_id, index_hash, rgba_hash in PORTRAITS:
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
        if (sha(indices), sha(rgba)) != (index_hash, rgba_hash):
            raise ValueError("Pinned trainer output differs: " + identity)
        preview = io.BytesIO()
        Image.frombytes("RGBA", (64, 64), rgba).save(preview, format="PNG")
        payloads[identity + ".rgba"] = rgba
        payloads[identity + ".png"] = preview.getvalue()
        assets.append(dict(id=identity, rgbaPath=identity + ".rgba", rgbaSha256=sha(rgba), width=64, height=64,
                           x=176 - 32, y=(8 - int(coordinates[source_id][0])) * 4 + 40 - 32,
                           sourceImage=image_path, sourcePalette=palette_path, sourceFrame=0,
                           sourceTrainerPicId=source_id, indexSha256=sha(indices)))
    document = dict(profile="firered-mtmoon-trainer-art-v1", sourceCommit=COMMIT, lockSha256=sha(lock_bytes),
                    inputs=lock["files"], paletteExpansion="(rgb5 << 3) | (rgb5 >> 2)", assets=assets,
                    limitations=["Static settled trainer frames only; original slide, throw, attack timing and audio remain unverified."])
    data = canonical(document)
    if sha(data) != GENERATED_SHA256:
        raise ValueError("Pinned trainer manifest differs")
    payloads["mtmoon-trainer-art.json"] = data
    # All source contracts and every output are valid before publishing generated files.
    for name in payloads:
        safe(output, name)
    output.mkdir(parents=True, exist_ok=True)
    for name, payload in payloads.items():
        safe(output, name).write_bytes(payload)
    print(json.dumps(dict(sha256=sha(data), lockSha256=sha(lock_bytes), assets=assets), indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-repo", type=pathlib.Path)
    parser.add_argument("--raw", type=pathlib.Path, default=ROOT / "content/raw/mtmoon-trainer-art")
    parser.add_argument("--output", type=pathlib.Path, default=ROOT / "content/generated/mtmoon-trainer-art")
    args = parser.parse_args()
    run(args.source_repo, args.raw, args.output)
