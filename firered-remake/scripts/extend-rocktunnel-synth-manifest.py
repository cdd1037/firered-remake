#!/usr/bin/env python3
"""Authenticate the independent fifteen-cue RockTunnel supplement from fixed Git blobs."""
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
REFERENCE = pathlib.Path(os.environ.get("FIRERED_ROCKTUNNEL_SOURCE_REPO", str(ROOT.parent / "audio-reference-build/pokefirered")))
BASELINE = pathlib.Path(os.environ.get("FIRERED_ROCKTUNNEL_BASE_ROOT", str(ROOT)))
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
LOCK_SHA = "e521808c1bc74faa569c7ed4bbfce7710afef22e18bc2695b4cd6dc195ebea54"
LOCK_BYTES = 9695
PRIOR_PATH = "content/generated/synth-audio/manifest.json"
PRIOR_SHA = "10906e43fa80b13eb5d54177a93003df3fae5292f5da3a22b5805ff3e21220df"
PRIOR_BYTES = 88374
RECIPE_PATH = "scripts/synth-rocktunnel-cues.json"
OUTPUT_PATH = "content/generated/rocktunnel-synth-audio/manifest.json"
IDENTITY_PATH = "src/Game.Godot/RockTunnelSynthManifestIdentity.cs"
NEW_CRIES = {48:"VENONAT",49:"VENOMOTH",57:"PRIMEAPE",73:"TENTACRUEL",97:"HYPNO",101:"ELECTRODE",104:"CUBONE",105:"MAROWAK",108:"LICKITUNG",110:"WEEZING",111:"RHYHORN",117:"SEADRA",119:"SEAKING"}
NEW_IDS = {"MUS_LAVENDER", "SE_M_REFLECT"} | {"CRY_" + name + "_NORMAL" for name in NEW_CRIES.values()}
MAP_MUSIC = {"Route9":"MUS_ROUTE3","Route10":"MUS_ROUTE3","RockTunnel_1F":"MUS_MT_MOON","RockTunnel_B1F":"MUS_MT_MOON","Route10_PokemonCenter_1F":"MUS_POKE_CENTER","Route10_PokemonCenter_2F":"MUS_POKE_CENTER","LavenderTown":"MUS_LAVENDER"}
SOURCE_PATHS = frozenset({
    "include/constants/songs.h", "include/constants/species.h", "include/constants/sound.h",
    "sound/song_table.inc", "sound/cry_tables.inc", "src/sound.c", "sound/songs/midi/mus_lavender.mid", "sound/songs/midi/se_m_reflect.mid",
} | {"data/maps/" + name + "/map.json" for name in MAP_MUSIC}
  | {"sound/direct_sound_samples/cries/" + name.lower() + ".wav" for name in NEW_CRIES.values()})
CORE_PATHS = frozenset("src/Game.Godot/OriginalAudioCore_" + name + ".cs" for name in
                       ("Cry", "CryCatalog", "Profiles", "PsgSynth", "Resources", "Sequence", "Synth"))
INPUT_PATHS = CORE_PATHS | {"content/generated/route11-synth-audio/manifest.json", "src/Game.Godot/Route11SynthManifestIdentity.cs", PRIOR_PATH, RECIPE_PATH, "src/Game.Godot/SynthManifestIdentity.cs",
    "scripts/audit-early-audio.py", "content/generated/synth-audio/firered.pack",
    "content/generated/synth-audio/normal-cries.bin"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(relative):
    if (pathlib.PurePosixPath(relative).is_absolute() or "\\" in relative
            or any(part in ("", ".", "..") for part in relative.split("/"))):
        raise ValueError("unsafe RockTunnel synth path")
    return relative


def read(root, relative, digest, length):
    path = root / safe_path(relative)
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError("RockTunnel synth symlink rejected")
    if not 0 < length <= 3_000_000 or path.stat().st_size != length:
        raise ValueError("RockTunnel synth size differs: " + relative)
    data = path.read_bytes()
    if len(data) != length or sha(data) != digest:
        raise ValueError("RockTunnel synth hash differs: " + relative)
    return data


def git_blob(reference, relative):
    return subprocess.check_output(["git", "-C", str(reference), "show", COMMIT + ":" + safe_path(relative)])


def authenticated_inputs(root=ROOT, reference=REFERENCE, baseline=None):
    # An optional read-only baseline supports isolated recovery without modifying the main tree.
    baseline = root if baseline is None else baseline
    lock = json.loads(read(root, "scripts/rocktunnel-synth.lock.json", LOCK_SHA, LOCK_BYTES))
    if (lock["schemaVersion"], lock["repository"], lock["commit"], lock["tree"],
            len(lock["files"]), len(lock["inputs"])) != (
            1, "pret/pokefirered", COMMIT, TREE, len(SOURCE_PATHS), len(INPUT_PATHS)):
        raise ValueError("RockTunnel source lock differs")
    if ({e["path"] for e in lock["files"]} != SOURCE_PATHS
            or {e["path"] for e in lock["inputs"]} != INPUT_PATHS):
        raise ValueError("RockTunnel source dependency inventory differs")
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
    extension = inputs["content/generated/route11-synth-audio/manifest.json"]
    if len(extension)!=2653 or sha(extension)!="0631c99d955f0eddaea29dbc65b56df14e14847ee1c9bc5d072c7ccaf39bf4b1": raise ValueError("Prior Route11 bytes differ")
    old_ids |= {c["id"] for c in json.loads(extension)["cues"]}
    if len(old_ids)!=253 or old_ids & NEW_IDS: raise ValueError("prior253 inventory differs")
    return old_ids


def derive_additions(inputs, source):
    old_ids = checked_prior(inputs)
    for name, music in MAP_MUSIC.items():
        if json.loads(source["data/maps/" + name + "/map.json"])["music"] != music:
            raise ValueError("source map music differs: " + name)
    if set(MAP_MUSIC.values()) - old_ids != {"MUS_LAVENDER"}:
        raise ValueError("RockTunnel map music admission differs")
    # Execute only the hash-authenticated, locally authored MIDI/WAV parser.
    path = ROOT / "scripts/audit-early-audio.py"
    spec = importlib.util.spec_from_file_location("rocktunnel_midi_audit", path)
    audit = importlib.util.module_from_spec(spec)
    exec(compile(inputs["scripts/audit-early-audio.py"], str(path), "exec"), audit.__dict__)
    pack = inputs["content/generated/synth-audio/firered.pack"]
    constants = {name: int(value) for name, value in re.findall(
        r"^#define ((?:MUS|SE)_\w+)\s+(\d+)", source["include/constants/songs.h"].decode(), re.M)}
    rows = re.findall(r"^\s*song (\w+),\s*(\d+),\s*(\d+)", source["sound/song_table.inc"].decode(), re.M)
    sound = source["src/sound.c"].decode()
    counters = {name: int(counter) for name, counter in re.findall(r"= \{ (MUS_\w+),\s+(\d+) \}", sound)}
    if len(counters)!=14 or "MUS_LAVENDER" in counters: raise ValueError("fanfare inventory differs")
    additions=[]
    for cue_id, expected_player, looping in [("MUS_LAVENDER",0,True),("SE_M_REFLECT",1,False)]:
        sid=constants[cue_id]
        address,player,secondary=struct.unpack_from("<IHH",pack,0x084A32CC+sid*8-0x08489968)
        if rows[sid]!=(cue_id.lower(),str(player),str(secondary)) or player!=expected_player:raise ValueError("source song mapping differs")
        path="sound/songs/midi/"+cue_id.lower()+".mid";music=audit.midi(source[path])
        if looping and ([m["text"] for m in music["markers"]]!=["[","]"] or music["markers"][1]["tick"]<=music["markers"][0]["tick"]):raise ValueError("MIDI loop differs")
        additions.append(dict(id=cue_id,sourceCommit=COMMIT,sourceSymbol=cue_id,sourcePath=path,sourcePlayer=player,priority=pack[address-0x08489968+2],sourceCounter=None,songAddress=address,species=0,looping=looping))
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
    if len(additions) != 15 or {cue["id"] for cue in additions} != NEW_IDS:
        raise ValueError("RockTunnel cue inventory differs")
    data = canonical(dict(schemaVersion=1, sourceCommit=COMMIT, cues=additions))
    if data != inputs[RECIPE_PATH]:
        raise ValueError("locked recipe differs from source-derived cues")
    if len(data) != 5587 or sha(data) != "478237a8683d31438da6a5504272e76a52313d1417133767e08fa83a4cc0a621":
        raise ValueError("Historical RockTunnel supplement identity differs")
    return data


def identity(data):
    return f'''// Generated by scripts/extend-rocktunnel-synth-manifest.py from pinned source blobs.
internal static class RockTunnelSynthManifestIdentity
{{
    public const string Sha256 = "{sha(data)}";
    public const int Bytes = {len(data)};
    public const int CueCount = 15;
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
                raise ValueError("RockTunnel output symlink rejected")
        for path, value in outputs:
            if args.check:
                if path.read_bytes() != value:
                    raise ValueError("generated output differs: " + str(path))
            else:
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_bytes(value)
    print(json.dumps(dict(passed=True, supplementCues=15, supplementNormalCries=13, mergedCues=268,
                          mergedNormalCries=98, newCues=sorted(NEW_IDS), manifestBytes=len(data), manifestSha256=sha(data),
                          prior253BytesUnchanged=True, samplePackAndCryTableUnchanged=True, audioCoreUnchanged=True,
                          sourceFiles=len(SOURCE_PATHS), localInputs=len(INPUT_PATHS), audioRendered=False)))


if __name__ == "__main__":
    main()
