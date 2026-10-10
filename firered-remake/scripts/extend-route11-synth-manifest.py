#!/usr/bin/env python3
"""Authenticate the independent seven-cue Route11 supplement from fixed Git blobs."""
import argparse
import hashlib
import importlib.util
import json
import os
import pathlib
import re
import struct
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
REFERENCE = pathlib.Path(os.environ.get("FIRERED_ROUTE11_SOURCE_REPO", str(ROOT.parent / "audio-reference-build/pokefirered")))
BASELINE = pathlib.Path(os.environ.get("FIRERED_ROUTE11_BASE_ROOT", str(ROOT)))
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
LOCK_SHA = "5e6db74d5af6c3a8b77cf3bee50ec3ec4f14cfaf00e83f134dbf92465ccdc69a"
LOCK_BYTES = 8045
PRIOR_PATH = "content/generated/synth-audio/manifest.json"
PRIOR_SHA = "10906e43fa80b13eb5d54177a93003df3fae5292f5da3a22b5805ff3e21220df"
PRIOR_BYTES = 88374
RECIPE_PATH = "scripts/synth-route11-cues.json"
OUTPUT_PATH = "content/generated/route11-synth-audio/manifest.json"
IDENTITY_PATH = "src/Game.Godot/Route11SynthManifestIdentity.cs"
NEW_CRIES = {50: "DIGLETT", 51: "DUGTRIO", 82: "MAGNETON", 60: "POLIWAG", 61: "POLIWHIRL", 37: "VULPIX"}
NEW_IDS = {"MUS_ROUTE11"} | {"CRY_" + name + "_NORMAL" for name in NEW_CRIES.values()}
MAP_MUSIC = {
    "Route11": "MUS_ROUTE11",
    "DiglettsCave_SouthEntrance": "MUS_VIRIDIAN_FOREST",
    "DiglettsCave_B1F": "MUS_VIRIDIAN_FOREST",
    "DiglettsCave_NorthEntrance": "MUS_VIRIDIAN_FOREST",
    "Route2_EastBuilding": "MUS_PEWTER",
    "Route2_House": "MUS_PEWTER",
    "Route11_EastEntrance_1F": "MUS_VERMILLION",
    "Route11_EastEntrance_2F": "MUS_VERMILLION",
    "Route2": "MUS_ROUTE1",
}
SOURCE_PATHS = frozenset({
    "include/constants/songs.h", "include/constants/species.h", "include/constants/sound.h",
    "sound/song_table.inc", "sound/cry_tables.inc", "src/sound.c", "sound/songs/midi/mus_route11.mid",
} | {"data/maps/" + name + "/map.json" for name in MAP_MUSIC}
  | {"sound/direct_sound_samples/cries/" + name.lower() + ".wav" for name in NEW_CRIES.values()})
CORE_PATHS = frozenset("src/Game.Godot/OriginalAudioCore_" + name + ".cs" for name in
                       ("Cry", "CryCatalog", "Profiles", "PsgSynth", "Resources", "Sequence", "Synth"))
INPUT_PATHS = CORE_PATHS | {PRIOR_PATH, RECIPE_PATH, "src/Game.Godot/SynthManifestIdentity.cs",
    "scripts/audit-early-audio.py", "content/generated/synth-audio/firered.pack",
    "content/generated/synth-audio/normal-cries.bin"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(relative):
    if (pathlib.PurePosixPath(relative).is_absolute() or "\\" in relative
            or any(part in ("", ".", "..") for part in relative.split("/"))):
        raise ValueError("unsafe Route11 synth path")
    return relative


def read(root, relative, digest, length):
    path = root / safe_path(relative)
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError("Route11 synth symlink rejected")
    if not 0 < length <= 3_000_000 or path.stat().st_size != length:
        raise ValueError("Route11 synth size differs: " + relative)
    data = path.read_bytes()
    if len(data) != length or sha(data) != digest:
        raise ValueError("Route11 synth hash differs: " + relative)
    return data


def git_blob(reference, relative):
    return subprocess.check_output(["git", "-C", str(reference), "show", COMMIT + ":" + safe_path(relative)])


def authenticated_inputs(root=ROOT, reference=REFERENCE, baseline=None):
    # An optional read-only baseline supports isolated recovery without modifying the main tree.
    baseline = root if baseline is None else baseline
    lock = json.loads(read(root, "scripts/route11-synth.lock.json", LOCK_SHA, LOCK_BYTES))
    if (lock["schemaVersion"], lock["repository"], lock["commit"], lock["tree"],
            len(lock["files"]), len(lock["inputs"])) != (
            1, "pret/pokefirered", COMMIT, TREE, len(SOURCE_PATHS), len(INPUT_PATHS)):
        raise ValueError("Route11 source lock differs")
    if ({e["path"] for e in lock["files"]} != SOURCE_PATHS
            or {e["path"] for e in lock["inputs"]} != INPUT_PATHS):
        raise ValueError("Route11 source dependency inventory differs")
    tree = subprocess.check_output(["git", "-C", str(reference), "show", "--format=%T", "--no-patch", COMMIT]).decode().strip()
    if tree != TREE:
        raise ValueError("pinned source tree differs")
    inputs, source = {}, {}
    for entry in lock["inputs"]:
        owner = root if entry["path"] == RECIPE_PATH else baseline
        inputs[entry["path"]] = read(owner, entry["path"], entry["sha256"], entry["byteLength"])
    for entry in lock["files"]:
        path = entry["path"]
        data = git_blob(reference, path)
        if (len(data), sha(data), hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()) != (
                entry["byteLength"], entry["sha256"], entry["gitBlobSha1"]):
            raise ValueError("pinned source Git blob differs: " + path)
        source[path] = data
    return inputs, source


def canonical(value):
    return (json.dumps(value, indent=2) + "\n").encode()


def checked_prior(inputs):
    previous = inputs[PRIOR_PATH]
    if (len(previous), sha(previous)) != (PRIOR_BYTES, PRIOR_SHA):
        raise ValueError("prior246 exact identity differs")
    recipe = json.loads(previous)
    old = recipe["cues"]
    old_ids = {cue["id"] for cue in old}
    if (recipe["schemaVersion"] != 1 or recipe["sourceCommit"] != COMMIT or len(old) != 246
            or len(old_ids) != 246 or old_ids & NEW_IDS
            or sum(cue["sourcePlayer"] == 4 for cue in old) != 79):
        raise ValueError("prior246 schema/source/inventory differs")
    return old_ids


def derive_additions(inputs, source):
    old_ids = checked_prior(inputs)
    for name, music in MAP_MUSIC.items():
        if json.loads(source["data/maps/" + name + "/map.json"])["music"] != music:
            raise ValueError("source map music differs: " + name)
    if set(MAP_MUSIC.values()) - old_ids != {"MUS_ROUTE11"}:
        raise ValueError("Route11 map music admission differs")
    # Execute only the hash-authenticated, locally authored MIDI/WAV parser.
    path = ROOT / "scripts/audit-early-audio.py"
    spec = importlib.util.spec_from_file_location("route11_midi_audit", path)
    audit = importlib.util.module_from_spec(spec)
    exec(compile(inputs["scripts/audit-early-audio.py"], str(path), "exec"), audit.__dict__)
    pack = inputs["content/generated/synth-audio/firered.pack"]
    constants = {name: int(value) for name, value in re.findall(
        r"^#define ((?:MUS|SE)_\w+)\s+(\d+)", source["include/constants/songs.h"].decode(), re.M)}
    rows = re.findall(r"^\s*song (\w+),\s*(\d+),\s*(\d+)", source["sound/song_table.inc"].decode(), re.M)
    sound = source["src/sound.c"].decode()
    counters = {name: int(counter) for name, counter in re.findall(r"= \{ (MUS_\w+),\s+(\d+) \}", sound)}
    if len(counters) != 14 or "MUS_ROUTE11" in counters:
        raise ValueError("source fanfare inventory differs")
    sid = constants["MUS_ROUTE11"]
    address, player, secondary = struct.unpack_from("<IHH", pack, 0x084A32CC + sid * 8 - 0x08489968)
    if sid != 294 or rows[sid] != ("mus_route11", str(player), str(secondary)) or player != 0:
        raise ValueError("source Route11 song table mapping differs")
    path = "sound/songs/midi/mus_route11.mid"
    music = audit.midi(source[path])
    if ([m["text"] for m in music["markers"]] != ["[", "]"]
            or music["markers"][1]["tick"] <= music["markers"][0]["tick"]):
        raise ValueError("source MIDI loop markers differ")
    additions = [dict(id="MUS_ROUTE11", sourceCommit=COMMIT, sourceSymbol="MUS_ROUTE11", sourcePath=path,
                      sourcePlayer=player, priority=pack[address - 0x08489968 + 2], sourceCounter=None,
                      songAddress=address, species=0, looping=True)]
    species_ids = {name: int(value) for name, value in re.findall(
        r"^#define SPECIES_(\w+)\s+(\d+)", source["include/constants/species.h"].decode(), re.M)}
    priority = int(re.search(r"^#define CRY_PRIORITY_NORMAL\s+(\d+)", source["include/constants/sound.h"].decode(), re.M)[1])
    names = re.findall(r"^\s*cry Cry_(\w+)\s*$", source["sound/cry_tables.inc"].decode(), re.M)
    cries = inputs["content/generated/synth-audio/normal-cries.bin"]
    if cries[:8] != b"FRCN0001" or struct.unpack_from("<I", cries, 8)[0] != 386 or len(cries) != 6188:
        raise ValueError("normal cry catalog header differs")
    cry_rows = {struct.unpack_from("<H", cries, offset)[0]: struct.unpack_from("<HHIIB3x", cries, offset)
                for offset in range(12, len(cries), 16)}
    if priority != 10 or "CRY_PRIORITY_NORMAL, mode" not in sound:
        raise ValueError("source normal cry priority differs")
    for species, name in NEW_CRIES.items():
        row = cry_rows[species]
        path = "sound/direct_sound_samples/cries/" + name.lower() + ".wav"
        wav, pcm = audit.wav(source[path])
        if (species_ids[name] != species or names[species - 1].upper() != name or row[1] != species - 1
                or wav["effectiveEndExclusive"] != row[2] or wav["sampleCount"] != row[2] + 1
                or wav["loopEnabled"] or wav["agbPitch"] != 10764288):
            raise ValueError("source normal cry mapping differs: " + name)
        tone = 0x0848c914 + row[1] * 12 - 0x08489968
        wave = struct.unpack_from("<I", pack, tone + 4)[0] - 0x08489968
        if struct.unpack_from("<HHIII", pack, wave) != (1, 0, 10764288, 0, row[2]):
            raise ValueError("source normal cry wave header differs: " + name)
        delta = (0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1)
        decoded = bytearray()
        for index in range(row[2] + row[4]):
            block, i = divmod(index, 64)
            start = wave + 16 + block * 33
            if i == 0:
                value = pack[start]
            else:
                packed = pack[start + (1 if i == 1 else i // 2 + 1)]
                nibble = packed & 15 if i == 1 or i % 2 else packed >> 4
                value = (value + delta[nibble]) & 255
            decoded.append((value + 128) & 255)
        if decoded != pcm[:len(decoded)]:
            raise ValueError("source cry PCM differs from fixed pack: " + name)
        additions.append(dict(id="CRY_" + name + "_NORMAL", sourceCommit=COMMIT, sourceSymbol="SPECIES_" + name,
                              sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None,
                              songAddress=0, species=species, looping=False))
    return sorted(additions, key=lambda cue: cue["id"])


def verified_recipe(root=ROOT, reference=REFERENCE, baseline=None):
    inputs, source = authenticated_inputs(root, reference, baseline)
    additions = derive_additions(inputs, source)
    if len(additions) != 7 or {cue["id"] for cue in additions} != NEW_IDS:
        raise ValueError("Route11 cue inventory differs")
    data = canonical(dict(schemaVersion=1, sourceCommit=COMMIT, cues=additions))
    if data != inputs[RECIPE_PATH]:
        raise ValueError("locked recipe differs from source-derived cues")
    if len(data) != 2653 or sha(data) != "0631c99d955f0eddaea29dbc65b56df14e14847ee1c9bc5d072c7ccaf39bf4b1":
        raise ValueError("Historical Route11 supplement identity differs")
    return data


def identity(data):
    return f'''// Generated by scripts/extend-route11-synth-manifest.py from pinned source blobs.
internal static class Route11SynthManifestIdentity
{{
    public const string Sha256 = "{sha(data)}";
    public const int Bytes = {len(data)};
    public const int CueCount = 7;
    public const string SourceCommit = "{COMMIT}";
}}
'''.encode()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-repo", type=pathlib.Path, default=REFERENCE)
    parser.add_argument("--baseline-root", type=pathlib.Path, default=BASELINE)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    data = verified_recipe(reference=args.source_repo, baseline=args.baseline_root)
    if not args.verify_only:
        outputs = [(ROOT / OUTPUT_PATH, data), (ROOT / IDENTITY_PATH, identity(data))]
        for path, _ in outputs:
            if any(p.is_symlink() for p in (path, *path.parents)):
                raise ValueError("Route11 output symlink rejected")
        for path, value in outputs:
            if args.check:
                if path.read_bytes() != value:
                    raise ValueError("generated output differs: " + str(path))
            else:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(value)
    print(json.dumps(dict(passed=True, supplementCues=7, supplementNormalCries=6, mergedCues=253,
                          mergedNormalCries=85, newCues=sorted(NEW_IDS), manifestBytes=len(data), manifestSha256=sha(data),
                          prior246BytesUnchanged=True, samplePackAndCryTableUnchanged=True, audioCoreUnchanged=True,
                          sourceFiles=len(SOURCE_PATHS), localInputs=len(INPUT_PATHS), audioRendered=False)))


if __name__ == "__main__":
    main()
