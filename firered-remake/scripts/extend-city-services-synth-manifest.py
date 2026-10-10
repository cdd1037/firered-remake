#!/usr/bin/env python3
"""Authenticate three City Services cues from fixed local Git blobs; never render audio."""
import argparse
import hashlib
import importlib.util
import json
import pathlib
import re
import struct
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
REFERENCE = ROOT.parent / "audio-reference-build/pokefirered"
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
LOCK_SHA = "2014af0c63eec17e53dcbf47aaf6635f8dca3abbc5b3b32edfd8baf1637dca27"
LOCK_BYTES = 5426
SOURCE_FILE_COUNT = 12
INPUT_FILE_COUNT = 12
PRIOR_PATH = "content/generated/vermilion-gym-assets/synth-manifest-candidate.json"
PRIOR_SHA = "fe4aedaa4a2a86a4cd78d14c182ab2d764cea5be66acb83deec131e86f79c3e6"
PRIOR_BYTES = 87317
RECIPE_PATH = "scripts/synth-city-services-cues.json"
NEW_MUSIC = {"MUS_CYCLING"}
NEW_EFFECTS = {"SE_BIKE_BELL"}
NEW_IDS = NEW_MUSIC | NEW_EFFECTS | {"CRY_SEEL_NORMAL"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(relative):
    if (pathlib.PurePosixPath(relative).is_absolute() or "\\" in relative
            or any(part in ("", ".", "..") for part in relative.split("/"))):
        raise ValueError("unsafe City Services synth path")
    return relative


def read(root, relative, digest, length):
    path = root / safe_path(relative)
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError("City Services synth symlink rejected")
    if not 0 < length <= 3_000_000 or path.stat().st_size != length:
        raise ValueError("City Services synth size differs: " + relative)
    data = path.read_bytes()
    if len(data) != length or sha(data) != digest:
        raise ValueError("City Services synth hash differs: " + relative)
    return data


def git_blob(reference, relative):
    return subprocess.check_output(["git", "-C", str(reference), "show", COMMIT + ":" + safe_path(relative)])


def authenticated_inputs(root=ROOT, reference=REFERENCE):
    lock = json.loads(read(root, "scripts/city-services-synth.lock.json", LOCK_SHA, LOCK_BYTES))
    if (lock["schemaVersion"], lock["repository"], lock["commit"], lock["tree"],
            len(lock["files"]), len(lock["inputs"])) != (
            1, "pret/pokefirered", COMMIT, TREE, SOURCE_FILE_COUNT, INPUT_FILE_COUNT):
        raise ValueError("City Services source lock differs")
    inputs, sources = {}, {}
    for entry in lock["inputs"]:
        path = entry["path"]
        if path in inputs:
            raise ValueError("duplicate locked input")
        inputs[path] = read(root, path, entry["sha256"], entry["byteLength"])
    for entry in lock["files"]:
        path = entry["path"]
        if path in sources:
            raise ValueError("duplicate source file")
        data = git_blob(reference, path)
        if (len(data), sha(data), hashlib.sha1(f"blob {len(data)}\0".encode() + data).hexdigest()) != (
                entry["byteLength"], entry["sha256"], entry["gitBlobSha1"]):
            raise ValueError("pinned source Git blob differs: " + path)
        sources[path] = data
    return inputs, sources


def source_function(text, name):
    match = re.search(r"^(?:static )?void " + re.escape(name) + r"\([^\n]*\)\n\{(.*?)^\}", text, re.M | re.S)
    if match is None:
        raise ValueError("source function missing: " + name)
    return match[1]


def derive_additions(inputs, source):
    # The bounded field uses come from the original bicycle callbacks and Fan Club script.
    bike = source_function(source["src/bike.c"].decode(), "GetOnOffBike")
    bell = source_function(source["src/item_use.c"].decode(), "ItemUseOnFieldCB_Bicycle")
    seel = re.search(r"^VermilionCity_PokemonFanClub_EventScript_Seel::\n(.*?)(?=^\w+::|\Z)",
                     source["data/maps/VermilionCity_PokemonFanClub/scripts.inc"].decode(), re.M | re.S)
    if (set(re.findall(r"\bMUS_\w+", bike)) != NEW_MUSIC
            or set(re.findall(r"PlaySE\((SE_\w+)\)", bell)) != NEW_EFFECTS
            or seel is None or "playmoncry SPECIES_SEEL, CRY_MODE_NORMAL" not in seel[1]
            or "waitmoncry" not in seel[1]):
        raise ValueError("bounded bicycle/ambient cry source repertoire differs")
    sound = source["src/sound.c"].decode()
    counters = {name: int(counter) for name, counter in re.findall(r"= \{ (MUS_\w+),\s+(\d+) \}", sound)}
    if len(counters) != 14 or (NEW_MUSIC | NEW_EFFECTS) & counters.keys():
        raise ValueError("source fanfare counter inventory differs")
    # Reuse the fixed, locally authored MIDI parser, after authenticating its bytes.
    path = ROOT / "scripts/audit-early-audio.py"
    spec = importlib.util.spec_from_file_location("city_services_midi_audit", path)
    midi = importlib.util.module_from_spec(spec)
    exec(compile(inputs["scripts/audit-early-audio.py"], str(path), "exec"), midi.__dict__)
    pack = inputs["content/generated/synth-audio/firered.pack"]
    constants = {name: int(value) for name, value in re.findall(
        r"^#define ((?:MUS|SE)_\w+)\s+(\d+)", source["include/constants/songs.h"].decode(), re.M)}
    rows = re.findall(r"^\s*song (\w+),\s*(\d+),\s*(\d+)", source["sound/song_table.inc"].decode(), re.M)
    additions = []
    for cue_id in sorted(NEW_MUSIC | NEW_EFFECTS):
        sid = constants[cue_id]
        address, player, secondary = struct.unpack_from("<IHH", pack, 0x084A32CC + sid * 8 - 0x08489968)
        if rows[sid] != (cue_id.lower(), str(player), str(secondary)):
            raise ValueError("source song table mapping differs: " + cue_id)
        path = "sound/songs/midi/" + cue_id.lower() + ".mid"
        markers = [m["text"] for m in midi.midi(source[path])["markers"]]
        if markers != (["[", "]"] if cue_id in NEW_MUSIC else []):
            raise ValueError("source MIDI loop status differs: " + cue_id)
        additions.append(dict(id=cue_id, sourceCommit=COMMIT, sourceSymbol=cue_id, sourcePath=path,
                              sourcePlayer=player, priority=pack[address - 0x08489968 + 2],
                              sourceCounter=counters.get(cue_id), songAddress=address, species=0,
                              looping=bool(markers)))
    species = int(re.search(r"^#define SPECIES_SEEL\s+(\d+)", source["include/constants/species.h"].decode(), re.M)[1])
    priority = int(re.search(r"^#define CRY_PRIORITY_NORMAL\s+(\d+)", source["include/constants/sound.h"].decode(), re.M)[1])
    names = re.findall(r"^\s*cry Cry_(\w+)\s*$", source["sound/cry_tables.inc"].decode(), re.M)
    cries = inputs["content/generated/synth-audio/normal-cries.bin"]
    cry_rows = {struct.unpack_from("<H", cries, offset)[0]: struct.unpack_from("<HHIIB3x", cries, offset)
                for offset in range(12, len(cries), 16)}
    path = "sound/direct_sound_samples/cries/seel.wav"
    if (species != 86 or priority != 10 or names[species - 1] != "Seel" or cry_rows[species][1] != species - 1
            or not source[path].startswith(b"RIFF") or source[path][8:12] != b"WAVE"
            or "CRY_PRIORITY_NORMAL, mode" not in source_function(sound, "PlayCry_Script")):
        raise ValueError("source normal cry mapping differs")
    additions.append(dict(id="CRY_SEEL_NORMAL", sourceCommit=COMMIT, sourceSymbol="SPECIES_SEEL", sourcePath=path,
                          sourcePlayer=4, priority=priority, sourceCounter=None, songAddress=0, species=species, looping=False))
    return additions


def verified_recipe(root=ROOT, reference=REFERENCE):
    inputs, source = authenticated_inputs(root, reference)
    previous = inputs[PRIOR_PATH]
    if (len(previous), sha(previous)) != (PRIOR_BYTES, PRIOR_SHA):
        raise ValueError("prior243 exact identity differs")
    recipe = json.loads(previous)
    old = recipe["cues"]
    old_ids = {cue["id"] for cue in old}
    if (recipe["schemaVersion"] != 1 or recipe["sourceCommit"] != COMMIT or len(old) != 243
            or len(old_ids) != 243 or old_ids & NEW_IDS):
        raise ValueError("prior243 schema/source/inventory differs")
    additions = derive_additions(inputs, source)
    recipe["cues"] = sorted(old + additions, key=lambda cue: cue["id"])
    if len(recipe["cues"]) != 246 or {cue["id"] for cue in additions} != NEW_IDS:
        raise ValueError("City Services cue inventory differs")
    data = (json.dumps(recipe, indent=2) + "\n").encode()
    if data != inputs[RECIPE_PATH]:
        raise ValueError("locked recipe differs from source-derived cues")
    filtered = {**recipe, "cues": [cue for cue in recipe["cues"] if cue["id"] not in NEW_IDS]}
    if (json.dumps(filtered, indent=2) + "\n").encode() != previous:
        raise ValueError("prior243 complete bytes changed")
    for cue in old:
        record = "\n".join("    " + line for line in json.dumps(cue, indent=2).splitlines()).encode()
        if record not in previous or record not in data:
            raise ValueError("prior243 record bytes changed")
    return data


def identity(data):
    return f'''// Generated by scripts/extend-city-services-synth-manifest.py from pinned source blobs.
internal static class SynthManifestIdentity
{{
    public const string Sha256 = "{sha(data)}";
    public const int Bytes = {len(data)};
    public const int CueCount = 246;
    public const string SourceCommit = "{COMMIT}";
}}
'''.encode()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-repo", type=pathlib.Path, default=REFERENCE)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    data = verified_recipe(reference=args.source_repo)
    if not args.verify_only:
        for relative, value in [("content/generated/synth-audio/manifest.json", data),
                                ("src/Game.Godot/SynthManifestIdentity.cs", identity(data))]:
            path = ROOT / relative
            if args.check:
                if path.read_bytes() != value:
                    raise ValueError("generated output differs: " + relative)
            else:
                path.write_bytes(value)
    print(json.dumps(dict(passed=True, cues=246, normalCries=79, newCues=sorted(NEW_IDS),
                          manifestBytes=len(data), manifestSha256=sha(data), prior243RecordBytesUnchanged=True,
                          samplePackAndCryTableUnchanged=True, audioCoreUnchanged=True, audioRendered=False)))


if __name__ == "__main__":
    main()
