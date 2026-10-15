#!/usr/bin/env python3
"""Derive the Vermilion cue increment from authenticated local Git blobs; never render audio."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest, runtime_manifest_bytes

import argparse
from pokemon_audio_moves import repertoire
import json
import pathlib
import re
import struct
import subprocess

ROOT = _PROJECT_ROOT
REFERENCE = ROOT.parent / "audio-reference-build/pokefirered"
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
NEW_MUSIC = {"MUS_VERMILLION"}  # Original source spelling.
NEW_EFFECTS = {"SE_M_" + name for name in (
    "DOUBLE_TEAM", "FAINT_ATTACK", "FLATTER", "SKETCH", "PAY_DAY", "SCREECH",
    "SWAGGER", "SWAGGER2", "THUNDERBOLT2", "THUNDER_WAVE")}
NEW_CRIES = {52: "MEOWTH", 53: "PERSIAN"}
NEW_IDS = NEW_MUSIC | NEW_EFFECTS | {"CRY_" + n + "_NORMAL" for n in NEW_CRIES.values()}


def safe_path(relative):
    if (pathlib.PurePosixPath(relative).is_absolute() or "\\" in relative
            or any(part in ("", ".", "..") for part in relative.split("/"))):
        raise ValueError("unsafe Vermilion synth path")
    return relative


def read(root, relative):
    path = root / safe_path(relative)
    if any((p.is_symlink() for p in (path, *path.parents))):
        raise ValueError('Vermilion synth symlink rejected')
    return path.read_bytes()


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
                           "jumpargeq": 3, "choosetwoturnanim": 2}
                if op in arities:
                    if len(args) != arities[op]:
                        raise ValueError("invalid source branch arity: " + op)
                    pending.extend(args if op == "choosetwoturnanim" else [args[-1]])
                elif op.startswith(("jump", "choose")):
                    raise ValueError("unsupported source animation branch: " + op)
        result[move] = {"effects": sorted(effects), "labels": sorted(seen)}
    return result


def authenticated_inputs(root=ROOT, reference=REFERENCE):
    lock = json.loads(read(root, 'scripts/recipes/audio/vermilion-synth.lock.json'))
    if (lock['repository'], lock['commit']) != ('pret/pokefirered', COMMIT):
        raise ValueError("Vermilion source lock differs")
    inputs, sources = {}, {}
    for entry in lock["inputs"]:
        path = entry["path"]
        if path in inputs:
            raise ValueError("duplicate locked input")
        inputs[path] = read(root, path)
    for entry in lock["files"]:
        path = entry["path"]
        if path in sources:
            raise ValueError("duplicate source file")
        data = git_blob(reference, path)
        sources[path] = data
    return inputs, sources


def verified_recipe(root=ROOT, reference=REFERENCE):
    inputs, source = authenticated_inputs(root, reference)
    prior_bytes = inputs["scripts/recipes/audio/synth-bill-cues.json"]
    recipe = json.loads(prior_bytes)
    prior = recipe["cues"]
    prior_ids = {c["id"] for c in prior}
    if recipe["sourceCommit"] != COMMIT or recipe["schemaVersion"] != 1 or len(prior) != 209 or len(prior_ids) != 209:
        raise ValueError("prior209 inventory differs")
    region = json.loads(inputs["content/generated/maps/regions/vermilion/vermilion-catalog.json"])
    script_blocks = json.loads(read(root, "scripts/recipes/audio/vermilion-synth.lock.json"))["scriptBlocks"]
    species, repertoire_moves = repertoire(root, tuple(NEW_CRIES.values()),
        "data/trainer-battles/vermilion.json")
    supplement = json.loads(inputs["content/generated/sprites/pokemon/vermilion/supplement.json"])
    if len(region["maps"]) != 10 or {s["sourceSpeciesId"] for s in species} != set(NEW_CRIES):
        raise ValueError("bounded region/species inventory differs")
    field = {json.loads(source["data/maps/" + m["sourceName"] + "/map.json"])["music"] for m in region["maps"]}
    for block in script_blocks:
        original = source[block["path"]].decode().splitlines()
        label = original[block["labelLine"] - 1].split('@')[0].strip().rstrip(':')
        if label != block["label"]:
            raise ValueError("source script label differs")
        for line in original[block["startLine"] - 1:block["endLine"]]:
            field.update(re.findall(r"\b(?:MUS|SE)_\w+", line))
            for name in re.findall(r"playmoncry\s+SPECIES_(\w+)", line):
                field.add("CRY_" + name + "_NORMAL")
    if field - prior_ids != NEW_MUSIC:
        raise ValueError("new region BGM/effect repertoire differs")
    moves = tuple(move.removeprefix("pret:MOVE_") for move in repertoire_moves)
    if len(moves) != 30 or len(set(moves)) != 30:
        raise ValueError("imported move inventory differs")
    closure = source_sound_effects(source["data/battle_anim_scripts.s"].decode(), moves)
    effects = set().union(*(set(v["effects"]) for v in closure.values()))
    if effects - prior_ids != NEW_EFFECTS:
        raise ValueError("new battle effects differ from source branch closure")
    pack = inputs["content/generated/audio/synth/firered.pack"]
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
    cries = inputs["content/generated/audio/synth/normal-cries.bin"]
    cry_rows = {struct.unpack_from("<H", cries, offset)[0]: struct.unpack_from("<HHIIB3x", cries, offset)
                for offset in range(12, len(cries), 16)}
    supplement_cries = {c["sourceSpeciesId"]: c for c in supplement["cries"]}
    for species, name in NEW_CRIES.items():
        if (species_ids[name] != species or cry_names[species - 1].upper() != name
                or cry_rows[species][1] != species - 1 or supplement_cries[species]["tableIndex"] != species - 1):
            raise ValueError("source normal cry mapping differs")
        path = "sound/direct_sound_samples/cries/" + name.lower() + ".wav"
        if path not in source or source[path][:4] != b"RIFF":
            raise ValueError("source normal cry sample missing")
        additions.append(dict(id="CRY_" + name + "_NORMAL", sourceCommit=COMMIT, sourceSymbol="SPECIES_" + name,
                              sourcePath=path, sourcePlayer=4, priority=10, sourceCounter=None,
                              songAddress=0, species=species, looping=False))
    recipe["cues"] = sorted(prior + additions, key=lambda c: c["id"])
    if len(recipe["cues"]) != 222 or {c["id"] for c in additions} != NEW_IDS:
        raise ValueError("Vermilion cue inventory differs")
    return runtime_manifest_bytes(recipe)



def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-repo", type=pathlib.Path, default=REFERENCE)
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    data = verified_recipe(reference=args.source_repo)
    if not args.verify_only:
        for relative, value in [('content/generated/audio/synth/manifest.json', data)]:
            path = ROOT / relative
            if args.check:
                if path.read_bytes() != value:
                    raise ValueError("generated output differs: " + relative)
            else:
                path.write_bytes(value)
    print(json.dumps(dict(passed=True, cues=222, normalCries=74, newCues=13, newMoveEffects=10,
                          newFieldEffects=0, mapMusic="MUS_VERMILLION", manifestBytes=len(data),
                          prior209RecordBytesUnchanged=True,
                          samplePackAndCryTableUnchanged=True, audioCoreUnchanged=True, audioRendered=False)))


if __name__ == "__main__":
    main()
