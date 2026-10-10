#!/usr/bin/env python3
"""Import only the frozen original-driver MONO batch. Never builds, downloads or packages a ROM."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import struct
import tempfile

ROOT = Path(__file__).resolve().parents[1]
SOURCE_COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
SOURCE_LOCK_SHA256 = "6bd7cb1ea5e5f01d74781ab15ba290cde8324f4a29c5dbeaad5bd6e520b521be"
MANIFEST_SHA256 = "cd4d857bb6e220925834302119ae66efb1cfdc4a014f49053dbee1956a62aaec"
FROZEN_SHA256 = "55b52063287673387d84037049f95c3a3a8de8700ec9c680eea88ea7b9829818"
VERIFICATION_SHA256 = "2cf0a6a44cacd6f87b60d24766016dd4bf7f21b62651e24c6ebd73a80f556667"
ROM_SHA1 = "41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc"
ROM_SHA256 = "3d0c79f1627022e18765766f6cb5ea067f6b5bf7dca115552189ad65a5c3a8ac"
RENDERER = "mGBA-0.10.5-original-FireRed-rev0-m4a-offline-v1"
RENDERER_SOURCE_SHA256 = "a8267d4ff5244c688f7a22a82a684134f9b731f066cd8ee122718be06a98db7f"
RENDER_BINARY_SHA256 = "3d158c1301984157a724f37418ea12a6caadbcba2bd9a2b83955d4f15daff0fb"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_file(root, relative):
    require(isinstance(relative, str) and relative and "\\" not in relative and ":" not in relative,
            "Invalid relative audio path")
    path = PurePosixPath(relative)
    require(not path.is_absolute() and all(x not in ("", ".", "..") for x in relative.split("/")),
            "Escaping/noncanonical audio path")
    current = Path(root).absolute()
    require(not current.is_symlink(), "Symlink audio root")
    for part in path.parts:
        current /= part
        require(not current.is_symlink(), "Symlink audio path: " + relative)
    require(current.is_file(), "Missing original audio file: " + relative)
    return current


def read_pinned(root, relative, digest):
    data = safe_file(root, relative).read_bytes()
    require(sha(data) == digest, "Original audio digest mismatch: " + relative)
    return data


def strict_json(data):
    def pairs(rows):
        result = {}
        for key, value in rows:
            require(key not in result, "Duplicate JSON key: " + key)
            result[key] = value
        return result
    return json.loads(data, object_pairs_hook=pairs)


def validate_clip(c):
    require(isinstance(c["id"], str) and re.fullmatch(r"(?:MUS_|SE_|CRY_)[A-Z0-9_]+", c["id"]), "Invalid audio ID")
    require(c["sourceCommit"] == SOURCE_COMMIT and c["renderer"] == RENDERER
            and c["isOriginalTestFixture"] is False and c["audibleOutputVerified"] is False,
            "Unknown source, renderer or unsupported acceptance claim")
    require(c["sourceSoundMode"] == "mono" and c["sourceSoundCntH"] == 0x3302,
            "Only original MONO routing is frozen; stereo files are not substitutes")
    require(type(c["sourcePlayer"]) is int and 0 <= c["sourcePlayer"] <= 4
            and type(c["priority"]) is int and 0 <= c["priority"] <= 255, "Invalid source player/priority")
    require(c["sourceCounter"] is None or (type(c["sourceCounter"]) is int and c["sourceCounter"] > 0),
            "Invalid source fanfare counter")
    require(c["sampleRate"] == 48000 and c["channels"] == 2 and type(c["frameCount"]) is int
            and c["frameCount"] > 0, "Invalid PCM transport")
    require(c["wavPath"] == c["id"].lower() + ".wav" and c["tracePath"] == c["id"].lower() + ".csv",
            "Unexpected WAV/trace path")
    require(all(re.fullmatch(r"[a-f0-9]{64}", c[k]) for k in ("wavSha256", "pcmSha256", "traceSha256")),
            "Missing audio hash")
    begin, end = c["loopStartFrame"], c["loopEndFrame"]
    require((begin is None) == (end is None), "Partial loop bounds")
    require(begin is None or (type(begin) is int and type(end) is int and 0 <= begin < end <= c["frameCount"]),
            "Invalid exclusive audio loop range")
    require((c["sourceLoop"] is None) == (begin is None), "Missing loop evidence")
    require(c["sourcePlayer"] != 4 or (begin is None and c["sourceCounter"] is None), "Invalid normal cry")
    require(c["sourceCounter"] is None or (c["sourcePlayer"] == 2 and begin is None), "Invalid fanfare")
    m = c["measurements"]
    require(0 < m["peakPcm16"] <= 32768 and 0 < m["rmsPcm16"] <= m["peakPcm16"]
            and 0 < m["nonzeroSampleValues"] <= c["frameCount"] * 2
            and 0 <= m["firstNonzeroFrame"] <= m["lastNonzeroFrame"] < c["frameCount"], "Silent/invalid PCM measurements")


def validate_wav(c, data):
    validate_clip(c)
    require(len(data) == 44 + c["frameCount"] * 4 and len(data) >= 48, "Empty/truncated original WAV")
    header = struct.unpack("<4sI4s4sIHHIIHH4sI", data[:44])
    require(header == (b"RIFF", len(data) - 8, b"WAVE", b"fmt ", 16, 1, 2, 48000, 192000,
                       4, 16, b"data", len(data) - 44), "Unsupported PCM16 WAV structure")
    pcm = data[44:]
    require(sha(data) == c["wavSha256"] and sha(pcm) == c["pcmSha256"], "Original WAV/PCM hash mismatch")
    require(any(pcm), "Silent original WAV")


def import_audio(render_dir, rom, output_dir, project_root=ROOT):
    render_dir, output_dir, rom = Path(render_dir).absolute(), Path(output_dir).absolute(), Path(rom)
    require(render_dir != output_dir and not output_dir.is_symlink(), "Invalid import destination")
    rom_bytes = rom.read_bytes()
    require(len(rom_bytes) == 16777216 and hashlib.sha1(rom_bytes).hexdigest() == ROM_SHA1
            and sha(rom_bytes) == ROM_SHA256, "Original FireRed revision-0 ROM identity differs")
    del rom_bytes  # Only its verified identity is recorded; ROM bytes never enter the product.
    read_pinned(project_root, "content/early-audio-source-lock.json", SOURCE_LOCK_SHA256)
    manifest_bytes = read_pinned(render_dir, "manifest.json", MANIFEST_SHA256)
    lock_bytes = read_pinned(render_dir, "source-lock.json", SOURCE_LOCK_SHA256)
    frozen_bytes = read_pinned(render_dir, "FROZEN.json", FROZEN_SHA256)
    evidence = read_pinned(render_dir, "verification.json", VERIFICATION_SHA256)
    manifest, source, frozen = map(strict_json, (manifest_bytes, lock_bytes, frozen_bytes))
    require(manifest["schemaVersion"] == 1 and manifest["sourceLockSha256"] == SOURCE_LOCK_SHA256
            and manifest["romSha1"] == ROM_SHA1 and manifest["rendererSourceSha256"] == RENDERER_SOURCE_SHA256
            and manifest["renderBinarySha256"] == RENDER_BINARY_SHA256
            and (manifest["frameRateNumerator"], manifest["frameRateDenominator"]) == (16777216, 280896),
            "Original renderer identity differs")
    require(source["source"]["repository"] == "pret/pokefirered" and source["source"]["commit"] == SOURCE_COMMIT,
            "Original source identity differs")
    expected = {}
    for s in source["songs"]:
        priority = re.search(r"(?:^|\s)-P(\d+)(?:\s|$)", s["mid2agbOptions"])
        expected[s["symbol"]] = (s["symbol"], s["path"], s["player"], int(priority[1]) if priority else 0, s["fanfareCounterFrames"])
    for c in source["cries"]:
        expected["CRY_" + c["species"][8:] + "_NORMAL"] = (c["species"], c["path"], 4, 10, None)
    clips = manifest["clips"]
    require(len(expected) == len(clips) == len({c["id"] for c in clips}) == 134
            and {c["id"] for c in clips} == set(expected), "Incomplete original audio inventory")
    output_dir.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix=".early-audio-import-", dir=output_dir.parent))
    try:
        for name, data in (("manifest.json", manifest_bytes), ("source-lock.json", lock_bytes),
                           ("FROZEN.json", frozen_bytes), ("verification.json", evidence)):
            (staging / name).write_bytes(data)
        for row in frozen["rendererSources"]:
            data = read_pinned(render_dir, row["path"], row["sha256"])
            require(len(data) == row["bytes"], "Renderer source length differs")
            target = staging / row["path"]
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        for c in clips:
            validate_clip(c)
            require(tuple(c[k] for k in ("sourceSymbol", "sourcePath", "sourcePlayer", "priority", "sourceCounter")) == expected[c["id"]],
                    "Clip differs from source inventory: " + c["id"])
            data = read_pinned(render_dir, c["wavPath"], c["wavSha256"])
            validate_wav(c, data)
            (staging / c["wavPath"]).write_bytes(data)
            trace = read_pinned(render_dir, c["tracePath"], c["traceSha256"])
            require(trace, "Empty source-clock trace")
            (staging / c["tracePath"]).write_bytes(trace)
        # Catch replacement of metadata while copying, even if called on a live render directory.
        read_pinned(render_dir, "manifest.json", MANIFEST_SHA256)
        read_pinned(render_dir, "FROZEN.json", FROZEN_SHA256)
        if output_dir.exists():
            actual = {p.relative_to(output_dir).as_posix() for p in output_dir.rglob("*") if p.is_file() or p.is_symlink()}
            planned = {p.relative_to(staging).as_posix() for p in staging.rglob("*") if p.is_file()}
            require(actual == planned, "Existing output inventory differs; choose a fresh output directory")
            for relative in sorted(planned):
                require(safe_file(output_dir, relative).read_bytes() == (staging / relative).read_bytes(),
                        "Existing generated bytes differ: " + relative)
        else:
            staging.rename(output_dir)
        return {"status": "passed", "clips": len(clips), "wavBytes": sum(44 + c["frameCount"] * 4 for c in clips),
                "manifestSha256": MANIFEST_SHA256, "sourceSoundMode": "mono",
                "physicalAudioOutputVerified": False, "loopSeamFidelityAccepted": False}
    finally:
        if staging.exists():
            shutil.rmtree(staging)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--render-dir", type=Path, required=True)
    parser.add_argument("--rom", type=Path, required=True, help="Existing local hash-matched ROM; read only, never copied")
    parser.add_argument("--output-dir", type=Path, default=ROOT / "content/generated/early-audio")
    args = parser.parse_args()
    print(json.dumps(import_audio(args.render_dir, args.rom, args.output_dir), indent=2))


if __name__ == "__main__":
    main()
