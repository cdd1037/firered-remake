#!/usr/bin/env python3
"""Import pinned FireRed Misty/Picnicker/male Swimmer frames without executing upstream code."""
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
LOCK_SHA256 = "c19f153c821aeefa2c8562371c2afdabc29aa02e4b5c6d5d05bcbd2d41d9dadb"
# Explicit FireRed constants; do not infer source stems from localized/display IDs.
GENERATED_SHA256 = "8bde284fc953b5942367877aff9007b3270aaa3d937055c2e889382be10d9c4e"
PORTRAITS = (
    ("misty-front", "LeaderMisty", "leader_misty", 117,
     "ab616302855e2d4f3f8edca08a70d9cb7adaded15405d39af46fec1397af3c13",
     "e1ef2ff488c6d844160da1e31c66cbecee2f3b06316c4c867bea79abea522315"),
    ("picnicker-front", "Picnicker", "picnicker", 87,
     "b5b8a39c60229bce28b414af2f5effed4f18f63e6625d7b582b674b706e0f565",
     "c57e3d845d910de366c56e6a3193d98dbbc1c457dbf7f678862fdacedb3ec33f"),
    ("swimmer-m-front", "SwimmerM", "swimmer_m", 95,
     "a20e873d75e5eeec45dac3fa7685aa3c3223eeae8b3cdb51fbb47cd491fac43b",
     "4d2b8dc88e9920bf1493db68859005c6b412b77605a7d4f62fe46fe31892c3d3"),
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
    if not 0 < expected_size <= 1_000_000 or path.stat().st_size != expected_size:
        raise ValueError("Source size differs: " + str(path))
    with path.open("rb") as stream:
        data = stream.read(expected_size + 1)
    if len(data) != expected_size:
        raise ValueError("Source size differs: " + str(path))
    return data


def run(repository, raw, output):
    lock_bytes = read_exact(safe(ROOT, "scripts/cerulean-trainers.lock.json"), 3714)
    if sha(lock_bytes) != LOCK_SHA256:
        raise ValueError("Lock hash differs")
    lock = json.loads(lock_bytes)
    project = json.loads((ROOT / "content/source-lock.json").read_bytes())
    if (project["source"]["repository"], project["source"]["sourceCommit"],
            project["referenceGame"]["gameEdition"], project["referenceGame"]["romRevision"]) != (
            "pret/pokefirered", COMMIT, "FIRERED", 0):
        raise ValueError("Project source profile differs")
    if (lock["repository"], lock["commit"], len(lock["files"])) != ("pret/pokefirered", COMMIT, 15):
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
    audio_lock_bytes = read_exact(safe(ROOT, "scripts/cerulean-synth.lock.json"), 3164)
    if sha(audio_lock_bytes) != "33f7335d6fdd9cd5003fb47beb9a8de18cc5f088ec0744702e4f63aacd84ed6d":
        raise ValueError("Audio source lock differs")
    audio_lock = json.loads(audio_lock_bytes)
    if (audio_lock["repository"], audio_lock["commit"], len(audio_lock["files"])) != ("pret/pokefirered", COMMIT, 13):
        raise ValueError("Audio source identity differs")
    audio_sources = {}
    for entry in audio_lock["files"]:
        path = entry["path"]
        data = (subprocess.check_output(["git", "-C", str(repository), "show", COMMIT + ":" + path])
                if repository else read_exact(safe(raw, "audio-source/" + path), entry["byteLength"]))
        blob = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
        if (len(data), sha(data), blob) != (entry["byteLength"], entry["sha256"], entry["gitBlobSha1"]):
            raise ValueError("Audio source bytes differ: " + path)
        audio_sources["audio-source/" + path] = data
    # Validate every input before persisting any recovered source.
    if repository:
        for path, data in (sources | audio_sources).items():
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
    document = dict(profile="firered-cerulean-trainers-v1", sourceCommit=COMMIT, lockSha256=sha(lock_bytes),
                    inputs=lock["files"], paletteExpansion="(rgb5 << 3) | (rgb5 >> 2)", assets=assets,
                    limitations=["Static settled trainer frames only; original slide, throw, attack timing and audio remain unverified."])
    data = canonical(document)
    if sha(data) != GENERATED_SHA256:
        raise ValueError("Pinned trainer manifest differs")
    payloads["cerulean-trainers.json"] = data
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
    parser.add_argument("--raw", type=pathlib.Path, default=ROOT / "content/raw/cerulean-trainers")
    parser.add_argument("--output", type=pathlib.Path, default=ROOT / "content/generated/cerulean-trainers")
    args = parser.parse_args()
    run(args.source_repo, args.raw, args.output)
