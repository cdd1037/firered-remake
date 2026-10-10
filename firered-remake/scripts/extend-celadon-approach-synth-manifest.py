#!/usr/bin/env python3
"""Authenticate the independent three-cue CeladonApproach supplement from fixed Git blobs."""
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
REFERENCE = pathlib.Path(os.environ.get("FIRERED_ORIGINAL_REFERENCE", ROOT.parent / "audio-reference-build/pokefirered"))
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
LOCK_SHA = "622e831cc119e77ddc0b0d9b7b2ec8894372c2ca748101f7d4a917214760f7c1"
LOCK_BYTES = 9355
PRIOR_PATH = "content/generated/synth-audio/manifest.json"
PRIOR_SHA = "10906e43fa80b13eb5d54177a93003df3fae5292f5da3a22b5805ff3e21220df"
PRIOR_BYTES = 88374
RECIPE_PATH = "scripts/synth-celadon-approach-cues.json"
OUTPUT_PATH = "content/generated/celadon-approach-synth-audio/manifest.json"
IDENTITY_PATH = "src/Game.Godot/CeladonApproachSynthManifestIdentity.cs"
NEW_CRIES = {62: "POLIWRATH", 89: "MUK"}
NEW_IDS = {"MUS_CELADON"} | {"CRY_" + name + "_NORMAL" for name in NEW_CRIES.values()}
MAP_MUSIC = {'LavenderTown_PokemonCenter_1F': 'MUS_POKE_CENTER', 'LavenderTown_PokemonCenter_2F': 'MUS_POKE_CENTER', 'LavenderTown_Mart': 'MUS_POKE_CENTER', 'LavenderTown_House1': 'MUS_LAVENDER', 'LavenderTown_VolunteerPokemonHouse': 'MUS_LAVENDER', 'Route8': 'MUS_ROUTE3', 'Route8_WestEntrance': 'MUS_PEWTER', 'UndergroundPath_EastEntrance': 'MUS_PEWTER', 'UndergroundPath_EastWestTunnel': 'MUS_ROUTE1', 'UndergroundPath_WestEntrance': 'MUS_PEWTER', 'Route7': 'MUS_ROUTE3', 'Route7_EastEntrance': 'MUS_PEWTER', 'CeladonCity': 'MUS_CELADON', 'CeladonCity_PokemonCenter_1F': 'MUS_POKE_CENTER', 'CeladonCity_PokemonCenter_2F': 'MUS_POKE_CENTER'}
SOURCE_PATHS = frozenset({
    "include/constants/songs.h", "include/constants/species.h", "include/constants/sound.h",
    "sound/song_table.inc", "sound/cry_tables.inc", "src/sound.c", "sound/songs/midi/mus_celadon.mid", "data/maps/CeladonCity/scripts.inc",
} | {"data/maps/" + name + "/map.json" for name in MAP_MUSIC}
  | {"sound/direct_sound_samples/cries/" + name.lower() + ".wav" for name in NEW_CRIES.values()})
CORE_PATHS = frozenset("src/Game.Godot/OriginalAudioCore_" + name + ".cs" for name in
                       ("Cry", "CryCatalog", "Profiles", "PsgSynth", "Resources", "Sequence", "Synth"))
INPUT_PATHS = CORE_PATHS | {"content/generated/rocktunnel-synth-audio/manifest.json", "src/Game.Godot/RockTunnelSynthManifestIdentity.cs", "content/generated/route11-synth-audio/manifest.json", "src/Game.Godot/Route11SynthManifestIdentity.cs", PRIOR_PATH, RECIPE_PATH, "src/Game.Godot/SynthManifestIdentity.cs",
    "scripts/audit-early-audio.py", "content/generated/synth-audio/firered.pack",
    "content/generated/synth-audio/normal-cries.bin"}

def sha(data): return hashlib.sha256(data).hexdigest()
def canonical(value): return (json.dumps(value, indent=2) + "\n").encode()
def safe_path(relative):
    if (pathlib.PurePosixPath(relative).is_absolute() or "\\" in relative
            or any(part in ("", ".", "..") for part in relative.split("/"))):
        raise ValueError("unsafe CeladonApproach synth path")
    return relative

def safe_target(root, relative):
    path = root / safe_path(relative)
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError("CeladonApproach synth symlink rejected")
    return path

def read(root, relative, digest, length):
    path = safe_target(root, relative)
    if not 0 < length <= 3_000_000 or path.stat().st_size != length:
        raise ValueError("CeladonApproach synth size differs: " + relative)
    data = path.read_bytes()
    if len(data) != length or sha(data) != digest:
        raise ValueError("CeladonApproach synth hash differs: " + relative)
    return data

def git_blob(reference, relative):
    return subprocess.check_output(["git", "-C", str(reference), "show", COMMIT + ":" + safe_path(relative)])

def authenticated_inputs(root=ROOT, reference=REFERENCE):
    lock = json.loads(read(root, "scripts/celadon-approach-synth.lock.json", LOCK_SHA, LOCK_BYTES))
    if (lock["schemaVersion"], lock["repository"], lock["commit"], lock["tree"],
            len(lock["files"]), len(lock["inputs"])) != (
            1, "pret/pokefirered", COMMIT, TREE, len(SOURCE_PATHS), len(INPUT_PATHS)):
        raise ValueError("CeladonApproach source lock differs")
    if ({e["path"] for e in lock["files"]} != SOURCE_PATHS
            or {e["path"] for e in lock["inputs"]} != INPUT_PATHS):
        raise ValueError("CeladonApproach source dependency inventory differs")
    tree = subprocess.check_output(["git", "-C", str(reference), "show", "--format=%T", "--no-patch", COMMIT]).decode().strip()
    if tree != TREE: raise ValueError("pinned source tree differs")
    inputs, source = {}, {}
    for entry in lock["inputs"]:
        inputs[entry["path"]] = read(root, entry["path"], entry["sha256"], entry["byteLength"])
    for entry in lock["files"]:
        path = entry["path"]; data = git_blob(reference, path)
        if (len(data), sha(data), hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()) != (
                entry["byteLength"], entry["sha256"], entry["gitBlobSha1"]):
            raise ValueError("pinned source Git blob differs: " + path)
        source[path] = data
    return inputs, source

def checked_prior(inputs):
    previous = inputs[PRIOR_PATH]
    if (len(previous), sha(previous)) != (PRIOR_BYTES, PRIOR_SHA): raise ValueError("prior246 exact identity differs")
    recipe = json.loads(previous); old = recipe["cues"]; old_ids = {cue["id"] for cue in old}
    if (recipe["schemaVersion"] != 1 or recipe["sourceCommit"] != COMMIT or len(old) != 246
            or len(old_ids) != 246 or old_ids & NEW_IDS or sum(cue["sourcePlayer"] == 4 for cue in old) != 79):
        raise ValueError("prior246 schema/source/inventory differs")
    route11 = inputs["content/generated/route11-synth-audio/manifest.json"]
    if (len(route11), sha(route11)) != (2653, "0631c99d955f0eddaea29dbc65b56df14e14847ee1c9bc5d072c7ccaf39bf4b1"):
        raise ValueError("prior Route11 supplement identity differs")
    extension = json.loads(route11)["cues"]
    if len(extension) != 7 or len({c["id"] for c in extension}) != 7 or old_ids & {c["id"] for c in extension}: raise ValueError("prior253 inventory differs")
    old_ids |= {c["id"] for c in extension}
    rock = inputs["content/generated/rocktunnel-synth-audio/manifest.json"]
    if (len(rock), sha(rock)) != (5587, "478237a8683d31438da6a5504272e76a52313d1417133767e08fa83a4cc0a621"):
        raise ValueError("prior RockTunnel supplement identity differs")
    extension = json.loads(rock)["cues"]
    if len(extension) != 15 or len({c["id"] for c in extension}) != 15 or old_ids & {c["id"] for c in extension}: raise ValueError("prior268 inventory differs")
    old_ids |= {c["id"] for c in extension}
    if old_ids & NEW_IDS: raise ValueError("new cue overlaps prior268")
    return old_ids

def derive_additions(inputs, source):
    old_ids = checked_prior(inputs)
    if "playmoncry SPECIES_POLIWRATH, CRY_MODE_NORMAL" not in source["data/maps/CeladonCity/scripts.inc"].decode(): raise ValueError("Celadon NPC normal cry binding differs")
    for name, music in MAP_MUSIC.items():
        if json.loads(source["data/maps/" + name + "/map.json"])["music"] != music: raise ValueError("source map music differs: " + name)
    if set(MAP_MUSIC.values()) - old_ids != {"MUS_CELADON"}: raise ValueError("CeladonApproach map music admission differs")
    path = ROOT / "scripts/audit-early-audio.py"
    spec = importlib.util.spec_from_file_location("celadon-approach_midi_audit", path)
    audit = importlib.util.module_from_spec(spec)
    exec(compile(inputs["scripts/audit-early-audio.py"], str(path), "exec"), audit.__dict__)
    pack = inputs["content/generated/synth-audio/firered.pack"]
    constants = {name: int(value) for name, value in re.findall(r"^#define ((?:MUS|SE)_\w+)\s+(\d+)", source["include/constants/songs.h"].decode(), re.M)}
    rows = re.findall(r"^\s*song (\w+),\s*(\d+),\s*(\d+)", source["sound/song_table.inc"].decode(), re.M)
    sound = source["src/sound.c"].decode()
    counters = {name: int(counter) for name, counter in re.findall(r"= \{ (MUS_\w+),\s+(\d+) \}", sound)}
    if len(counters) != 14 or "MUS_CELADON" in counters: raise ValueError("source fanfare inventory differs")
    additions = []
    for cue_id, expected_player, looping in [("MUS_CELADON", 0, True)]:
        sid = constants[cue_id]
        address, player, secondary = struct.unpack_from("<IHH", pack, 0x084A32CC + sid * 8 - 0x08489968)
        if rows[sid] != (cue_id.lower(), str(player), str(secondary)) or player != expected_player: raise ValueError("source song table mapping differs: " + cue_id)
        path = "sound/songs/midi/" + cue_id.lower() + ".mid"; music = audit.midi(source[path])
        if looping and ([m["text"] for m in music["markers"]] != ["[", "]"] or music["markers"][1]["tick"] <= music["markers"][0]["tick"]): raise ValueError("source MIDI loop markers differ")
        additions.append(dict(id=cue_id, sourceCommit=COMMIT, sourceSymbol=cue_id, sourcePath=path,
            sourcePlayer=player, priority=pack[address - 0x08489968 + 2], sourceCounter=None,
            songAddress=address, species=0, looping=looping))
    species_ids = {name: int(value) for name, value in re.findall(r"^#define SPECIES_(\w+)\s+(\d+)", source["include/constants/species.h"].decode(), re.M)}
    priority = int(re.search(r"^#define CRY_PRIORITY_NORMAL\s+(\d+)", source["include/constants/sound.h"].decode(), re.M)[1])
    names = re.findall(r"^\s*cry Cry_(\w+)\s*$", source["sound/cry_tables.inc"].decode(), re.M)
    cries = inputs["content/generated/synth-audio/normal-cries.bin"]
    if cries[:8] != b"FRCN0001" or struct.unpack_from("<I", cries, 8)[0] != 386 or len(cries) != 6188: raise ValueError("normal cry catalog header differs")
    cry_rows = {struct.unpack_from("<H", cries, offset)[0]: struct.unpack_from("<HHIIB3x", cries, offset) for offset in range(12, len(cries), 16)}
    if priority != 10 or "CRY_PRIORITY_NORMAL, mode" not in sound: raise ValueError("source normal cry priority differs")
    for species, name in NEW_CRIES.items():
        row = cry_rows[species]; path = "sound/direct_sound_samples/cries/" + name.lower() + ".wav"; wav, pcm = audit.wav(source[path])
        if (species_ids[name] != species or names[species - 1].upper() != name or row[1] != species - 1
                or wav["effectiveEndExclusive"] != row[2] or wav["sampleCount"] != row[2] + 1
                or wav["loopEnabled"] or wav["agbPitch"] != 10764288): raise ValueError("source normal cry mapping differs: " + name)
        tone = 0x0848c914 + row[1] * 12 - 0x08489968; wave = struct.unpack_from("<I", pack, tone + 4)[0] - 0x08489968
        if struct.unpack_from("<HHIII", pack, wave) != (1, 0, 10764288, 0, row[2]): raise ValueError("source normal cry wave header differs: " + name)
        delta = (0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1); decoded = bytearray()
        for index in range(row[2] + row[4]):
            block, i = divmod(index, 64); start = wave + 16 + block * 33
            if i == 0: value = pack[start]
            else:
                packed = pack[start + (1 if i == 1 else i // 2 + 1)]
                nibble = packed & 15 if i == 1 or i % 2 else packed >> 4; value = (value + delta[nibble]) & 255
            decoded.append((value + 128) & 255)
        if decoded != pcm[:len(decoded)]: raise ValueError("source cry PCM differs from fixed pack: " + name)
        additions.append(dict(id="CRY_" + name + "_NORMAL", sourceCommit=COMMIT, sourceSymbol="SPECIES_" + name,
                              sourcePath=path, sourcePlayer=4, priority=priority, sourceCounter=None,
                              songAddress=0, species=species, looping=False))
    return sorted(additions, key=lambda cue: cue["id"])

def verified_recipe(root=ROOT, reference=REFERENCE):
    inputs, source = authenticated_inputs(root, reference); additions = derive_additions(inputs, source)
    if len(additions) != 3 or {cue["id"] for cue in additions} != NEW_IDS: raise ValueError("CeladonApproach cue inventory differs")
    data = canonical(dict(schemaVersion=1, sourceCommit=COMMIT, cues=additions))
    if data != inputs[RECIPE_PATH]: raise ValueError("locked recipe differs from source-derived cues")
    return data

def identity(data):
    return f'''// Generated by scripts/extend-celadon-approach-synth-manifest.py from pinned source blobs.
internal static class CeladonApproachSynthManifestIdentity
{{
    public const string Sha256 = "{sha(data)}";
    public const int Bytes = {len(data)};
    public const int CueCount = 3;
    public const string SourceCommit = "{COMMIT}";
}}
'''.encode()

def main():
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument("--source-repo", type=pathlib.Path, default=REFERENCE); parser.add_argument("--check", action="store_true"); parser.add_argument("--verify-only", action="store_true"); args = parser.parse_args()
    data = verified_recipe(reference=args.source_repo)
    if not args.verify_only:
        outputs = [(safe_target(ROOT, OUTPUT_PATH), data), (safe_target(ROOT, IDENTITY_PATH), identity(data))]
        for path, value in outputs:
            if args.check:
                if path.read_bytes() != value: raise ValueError("generated output differs: " + str(path))
            else: path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(value)
    print(json.dumps(dict(passed=True, supplementCues=3, supplementNormalCries=2, mergedCues=271,
        mergedNormalCries=100, newCues=sorted(NEW_IDS), manifestBytes=len(data), manifestSha256=sha(data),
        prior268BytesUnchanged=True, samplePackAndCryTableUnchanged=True, audioCoreUnchanged=True,
        sourceFiles=len(SOURCE_PATHS), localInputs=len(INPUT_PATHS), audioRendered=False)))
if __name__ == "__main__": main()
