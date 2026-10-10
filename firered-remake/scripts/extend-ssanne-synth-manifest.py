#!/usr/bin/env python3
"""Derive the S.S. Anne cue increment from authenticated local Git blobs; never render audio."""
import argparse
import hashlib
import json
import pathlib
import re
import struct
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[1]
REFERENCE = ROOT.parent / "audio-reference-build/pokefirered"
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
LOCK_SHA = "c421a9ae7cb83cef706d6264b514467a38aa665955ee93ab90fcc3ad5fc23d0e"
LOCK_BYTES = 31735
SOURCE_FILE_COUNT = 120
INPUT_FILE_COUNT = 15
PRIOR_SHA = "8e7606644bb4384213c0bdbae8f9ad7bf41159f1bcda2db102e91bb0dbf2b7fe"
PRIOR_BYTES = 79802
NEW_MUSIC = {"MUS_SS_ANNE", "MUS_SURF"}
NEW_FIELD_EFFECTS = {"SE_SS_ANNE_HORN"}
NEW_MOVE_EFFECTS = {"SE_M_" + name for name in (
    "BRICK_BREAK", "CONFUSE_RAY", "CUT", "DETECT", "DIZZY_PUNCH", "DRAGON_RAGE", "PSYBEAM",
    "SKY_UPPERCUT", "SNORE", "VITAL_THROW")}
NEW_EFFECTS = NEW_FIELD_EFFECTS | NEW_MOVE_EFFECTS
NEW_CRIES = {58: "GROWLITHE", 67: "MACHOKE", 72: "TENTACOOL", 77: "PONYTA"}
NEW_IDS = NEW_MUSIC | NEW_EFFECTS | {"CRY_" + n + "_NORMAL" for n in NEW_CRIES.values()}
NEW_MOVES = {"CONSTRICT", "KINESIS", "SEISMIC_TOSS", "CUT", "BRICK_BREAK", "REST"}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(relative):
    if (pathlib.PurePosixPath(relative).is_absolute() or "\\" in relative
            or any(part in ("", ".", "..") for part in relative.split("/"))):
        raise ValueError("unsafe S.S. Anne synth path")
    return relative


def read(root, relative, digest, length):
    path = root / safe_path(relative)
    if any(p.is_symlink() for p in (path, *path.parents)):
        raise ValueError("S.S. Anne synth symlink rejected")
    if not 0 < length <= 3_000_000 or path.stat().st_size != length:
        raise ValueError("S.S. Anne synth size differs: " + relative)
    data = path.read_bytes()
    if len(data) != length or sha(data) != digest:
        raise ValueError("S.S. Anne synth hash differs: " + relative)
    return data


def git_blob(reference, relative):
    """Read the pinned object, independent of the source checkout's working files."""
    return subprocess.check_output(["git", "-C", str(reference), "show", COMMIT + ":" + safe_path(relative)])


def source_sound_effects(script, moves):
    labels = list(re.finditer(r"^(\w+)::?$", script, re.M))
    blocks = {m[1]: script[m.end():labels[i + 1].start() if i + 1 < len(labels) else len(script)]
              for i, m in enumerate(labels)}
    result = {}
    for move in moves:
        pending, seen, effects = ["Move_" + move], set(), set()
        while pending:
            label = pending.pop()
            if label in seen:
                continue
            if label not in blocks:
                raise ValueError("unresolved source animation label: " + label)
            seen.add(label)
            for line in blocks[label].splitlines():
                line = line.split("@", 1)[0].strip()
                if not line:
                    continue
                tokens = line.split(None, 1)
                op = tokens[0]
                args = [a.strip() for a in tokens[1].split(",")] if len(tokens) > 1 else []
                effects.update(re.findall(r"\bSE_\w+", line))
                arities = {"call": 1, "goto": 1, "jumprettrue": 1, "jumpretfalse": 1,
                           "jumpargeq": 3, "jumpreteq": 2, "choosetwoturnanim": 2}
                if op in arities:
                    if len(args) != arities[op]:
                        raise ValueError("invalid source branch arity: " + op)
                    pending.extend(args if op == "choosetwoturnanim" else [args[-1]])
                elif op.startswith(("jump", "choose")):
                    raise ValueError("unsupported source animation branch: " + op)
        result[move] = {"effects": sorted(effects), "labels": sorted(seen)}
    return result


def authenticated_inputs(root=ROOT, reference=REFERENCE):
    lock = json.loads(read(root, "scripts/ssanne-synth.lock.json", LOCK_SHA, LOCK_BYTES))
    if (lock["repository"], lock["commit"], lock["tree"], len(lock["files"]), len(lock["inputs"])) != (
            "pret/pokefirered", COMMIT, "7515cd4067ea55f8cdbc73624478afd2736c58b9", SOURCE_FILE_COUNT, INPUT_FILE_COUNT):
        raise ValueError("S.S. Anne source lock differs")
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


def verified_recipe(root=ROOT, reference=REFERENCE):
    inputs, source = authenticated_inputs(root, reference)
    # The locked recipe carries the historical 222 records plus the derivable 17 additions.
    # The historical portion must reconstruct the exact independently frozen Vermilion bytes.
    expected = inputs["scripts/synth-ssanne-cues.json"]
    recipe = json.loads(expected)
    if recipe["sourceCommit"] != COMMIT or recipe["schemaVersion"] != 1:
        raise ValueError("recipe schema/source identity differs")
    prior = [c for c in recipe["cues"] if c["id"] not in NEW_IDS]
    prior_ids = {c["id"] for c in prior}
    prior_recipe = {**recipe, "cues": prior}
    prior_bytes = (json.dumps(prior_recipe, indent=2) + "\n").encode()
    if len(prior) != 222 or len(prior_ids) != 222 or (len(prior_bytes), sha(prior_bytes)) != (PRIOR_BYTES, PRIOR_SHA):
        raise ValueError("prior222 exact identity differs")
    region = json.loads(inputs["content/generated/ssanne-region/ssanne-catalog.json"])
    scripts = json.loads(inputs["content/generated/ssanne-region/scripts.json"])
    pokemon = json.loads(inputs["content/generated/ssanne-pokemon/catalog.json"])
    scope = json.loads(inputs["content/generated/ssanne-pokemon/scope.json"])
    supplement = json.loads(inputs["content/generated/ssanne-pokemon/supplement.json"])
    if len(region["maps"]) != 26 or {s["sourceSpeciesId"] for s in pokemon["species"]} != {58, 72, 77}:
        raise ValueError("bounded region/species inventory differs")
    field = {json.loads(source["data/maps/" + m["sourceName"] + "/map.json"])["music"] for m in region["maps"]}
    for block in scripts["blocks"]:
        ref = block["sourceRef"]
        raw = source[ref["path"]]
        if ref["commit"] != COMMIT or ref["sha256"] != sha(raw):
            raise ValueError("imported script source identity differs")
        original = raw.decode().splitlines()
        for line in block["lines"]:
            if original[line["line"] - 1] != line["text"]:
                raise ValueError("imported script line differs")
            field.update(re.findall(r"\b(?:MUS|SE)_\w+", line["text"]))
            for name in re.findall(r"playmoncry\s+SPECIES_(\w+)", line["text"]):
                field.add("CRY_" + name + "_NORMAL")
    if field - prior_ids != NEW_MUSIC | {"CRY_MACHOKE_NORMAL"}:
        raise ValueError("new region BGM/ambient cry repertoire differs")
    if set(re.findall(r"PlaySE\((SE_\w+)\)", source["src/ss_anne.c"].decode())) != NEW_FIELD_EFFECTS:
        raise ValueError("source departure horn repertoire differs")
    # Full learnsets contain metadata for later unsupported moves. This is the bounded
    # ship trainer roster plus the three admitted machines, not blanket move admission.
    moves = {m.removeprefix("MOVE_") for m in scope["requiredTrainerMoves"] + scope["teachingMoves"]}
    catalog_moves = {m["id"]["value"].removeprefix("pret:MOVE_") for m in pokemon["moves"]}
    if len(moves) != 46 or not NEW_MOVES <= moves <= catalog_moves:
        raise ValueError("bounded source move inventory differs")
    closure = source_sound_effects(source["data/battle_anim_scripts.s"].decode(), sorted(moves))
    effects = set().union(*(set(v["effects"]) for v in closure.values()))
    if effects - prior_ids != NEW_MOVE_EFFECTS:
        raise ValueError("new battle effects differ from source branch closure")
    pack = inputs["content/generated/synth-audio/firered.pack"]
    constants = {k: int(v) for k, v in re.findall(r"^#define ((?:MUS|SE)_\w+)\s+(\d+)", source["include/constants/songs.h"].decode(), re.M)}
    rows = re.findall(r"^\s*song (\w+),\s*(\d+),\s*(\d+)", source["sound/song_table.inc"].decode(), re.M)
    additions = []
    for cue_id in sorted(NEW_MUSIC | NEW_EFFECTS):
        sid = constants[cue_id]
        address, player, secondary = struct.unpack_from("<IHH", pack, 0x084A32CC + sid * 8 - 0x08489968)
        if rows[sid] != (cue_id.lower(), str(player), str(secondary)):
            raise ValueError("source song table mapping differs: " + cue_id)
        path = "sound/songs/midi/" + cue_id.lower() + ".mid"
        if path not in source or not source[path].startswith(b"MThd"):
            raise ValueError("source MIDI missing: " + cue_id)
        additions.append(dict(id=cue_id, sourceCommit=COMMIT, sourceSymbol=cue_id, sourcePath=path,
                              sourcePlayer=player, priority=pack[address - 0x08489968 + 2], sourceCounter=None,
                              songAddress=address, species=0, looping=cue_id in NEW_MUSIC))
    cry_names = re.findall(r"^\s*cry Cry_(\w+)\s*$", source["sound/cry_tables.inc"].decode(), re.M)
    species_ids = {k: int(v) for k, v in re.findall(r"^#define SPECIES_(\w+)\s+(\d+)", source["include/constants/species.h"].decode(), re.M)}
    cries = inputs["content/generated/synth-audio/normal-cries.bin"]
    cry_rows = {struct.unpack_from("<H", cries, offset)[0]: struct.unpack_from("<HHIIB3x", cries, offset)
                for offset in range(12, len(cries), 16)}
    supplement_cries = {c["sourceSpeciesId"]: c for c in supplement["cries"]}
    for species, name in NEW_CRIES.items():
        if (species_ids[name] != species or cry_names[species - 1].upper() != name or cry_rows[species][1] != species - 1):
            raise ValueError("source normal cry mapping differs")
        if species != 67 and supplement_cries[species]["tableIndex"] != species - 1:
            raise ValueError("imported normal cry mapping differs")
        path = "sound/direct_sound_samples/cries/" + name.lower() + ".wav"
        if path not in source or source[path][:4] != b"RIFF":
            raise ValueError("source normal cry sample missing")
        additions.append(dict(id="CRY_" + name + "_NORMAL", sourceCommit=COMMIT, sourceSymbol="SPECIES_" + name,
                              sourcePath=path, sourcePlayer=4, priority=10, sourceCounter=None,
                              songAddress=0, species=species, looping=False))
    recipe["cues"] = sorted(prior + additions, key=lambda c: c["id"])
    if len(recipe["cues"]) != 239 or {c["id"] for c in additions} != NEW_IDS:
        raise ValueError("S.S. Anne cue inventory differs")
    data = (json.dumps(recipe, indent=2) + "\n").encode()
    if data != expected:
        raise ValueError("locked recipe differs from source-derived cues")
    for cue in prior:
        record = "\n".join("    " + line for line in json.dumps(cue, indent=2).splitlines()).encode()
        if record not in prior_bytes or record not in data:
            raise ValueError("prior222 record bytes changed")
    return data


def identity(data):
    return f'''// Generated by scripts/extend-ssanne-synth-manifest.py from pinned source blobs.
internal static class SynthManifestIdentity
{{
    public const string Sha256 = "{sha(data)}";
    public const int Bytes = {len(data)};
    public const int CueCount = 239;
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
    print(json.dumps(dict(passed=True, cues=239, normalCries=78, newCues=17, newMoveEffects=10,
                          newFieldEffects=1, newMusic=sorted(NEW_MUSIC), manifestBytes=len(data),
                          manifestSha256=sha(data), prior222RecordBytesUnchanged=True,
                          samplePackAndCryTableUnchanged=True, audioCoreUnchanged=True, audioRendered=False)))


if __name__ == "__main__":
    main()
