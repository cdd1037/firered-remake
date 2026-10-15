#!/usr/bin/env python3
"""Authenticate Bill's bounded source repertoire and emit metadata, never rendered audio."""
import argparse
import json
import pathlib
import re
import struct

ROOT = pathlib.Path(__file__).resolve().parents[1]
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
BYTES = 75139
MOVES = ("ACID", "ATTRACT", "SECRET_POWER", "DIG")
NEW_MOVE_SE = {"SE_M_" + name for name in (
    "ATTRACT", "ATTRACT2", "CHARM", "BITE", "MEGA_KICK2", "RAZOR_WIND",
    "TAKE_DOWN", "WATERFALL", "WHIRLPOOL", "DIG")}
NEW_FIELD = {"MUS_ROUTE24", "SE_CONTEST_CURTAIN_FALL", "SE_PIN", "SE_WARP_IN"}
NEW_CRIES = {44: "GLOOM", 70: "WEEPINBELL", 79: "SLOWPOKE", 133: "EEVEE",
             134: "VAPOREON", 135: "JOLTEON", 136: "FLAREON"}




def read(root, relative):
    parts = relative.split('/')
    if pathlib.PurePosixPath(relative).is_absolute() or any((p in ('', '.', '..') for p in parts)) or '\\' in relative:
        raise ValueError('unsafe Bill synth path')
    path = root / relative
    if any((p.is_symlink() for p in (path, *path.parents))):
        raise ValueError('Bill synth symlink rejected')
    return path.read_bytes()


def source_sound_effects(script):
    """Follow calls, jumps, conditional terrain branches and both turns of Dig."""
    labels = list(re.finditer(r"^(\w+)::?$", script, re.M))
    blocks = {m[1]: script[m.end():labels[i + 1].start() if i + 1 < len(labels) else len(script)]
              for i, m in enumerate(labels)}
    result = {}
    for move in MOVES:
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
                if op in ("call", "goto"):
                    if len(args) != 1:
                        raise ValueError("invalid source branch arity")
                    pending.extend(args)
                elif op == "jumpargeq":
                    if len(args) != 3:
                        raise ValueError("invalid source conditional branch arity")
                    pending.append(args[2])
                elif op == "choosetwoturnanim":
                    if len(args) != 2:
                        raise ValueError("invalid source two-turn branch arity")
                    pending.extend(args)
                elif op.startswith(("jump", "choose")):
                    raise ValueError("unsupported source animation branch: " + op)
        result[move] = {"effects": sorted(effects), "labels": sorted(seen)}
    return result


def verified_recipe(root=ROOT):
    data = read(root, 'scripts/synth-bill-cues.json')
    prior = json.loads(read(root, 'scripts/synth-cerulean-cues.json'))["cues"]
    recipe = json.loads(data)
    cues = recipe["cues"]
    by_id = {cue["id"]: cue for cue in cues}
    if recipe["sourceCommit"] != COMMIT or recipe["schemaVersion"] != 1 or len(cues) != 209 or len(by_id) != 209:
        raise ValueError("Bill cue inventory differs")
    if len(prior) != 188 or any(by_id.get(cue["id"]) != cue for cue in prior):
        raise ValueError("prior188 metadata changed")
    pack = read(root, 'content/generated/synth-audio/firered.pack')
    cries = read(root, 'content/generated/synth-audio/normal-cries.bin')
    lock = json.loads(read(root, 'scripts/bill-synth.lock.json'))
    if (lock['repository'], lock['commit']) != ('pret/pokefirered', COMMIT):
        raise ValueError("Bill audio source lock differs")
    source = {}
    for entry in lock["files"]:
        raw = read(root, 'content/raw/pokefirered/037335f4/' + entry['path'])
        if entry["path"] in source:
            raise ValueError("duplicate source file")
        source[entry["path"]] = raw
    prior_ids = {cue["id"] for cue in prior}
    closure = source_sound_effects(source["data/battle_anim_scripts.s"].decode())
    effects = set().union(*(set(value["effects"]) for value in closure.values()))
    if effects - prior_ids != NEW_MOVE_SE:
        raise ValueError("new battle SE repertoire differs from source branch closure")
    field = {json.loads(source["data/maps/" + name + "/map.json"])["music"]
             for name in ("Route24", "Route25", "Route25_SeaCottage")}
    for name in ("Route24", "Route25_SeaCottage"):
        field.update(re.findall(r"\b(?:MUS|SE)_\w+", source["data/maps/" + name + "/scripts.inc"].decode()))
    if field - prior_ids != NEW_FIELD:
        raise ValueError("Bill field repertoire differs")
    cottage = source["data/maps/Route25_SeaCottage/scripts.inc"].decode()
    if set(re.findall(r"\bshowmonpic\s+SPECIES_(\w+)", cottage)) != {"EEVEE", "FLAREON", "JOLTEON", "VAPOREON"}:
        raise ValueError("Bill PC picture repertoire differs")
    scrcmd = source["src/scrcmd.c"].decode()
    handler = scrcmd[scrcmd.index("bool8 ScrCmd_showmonpic("):scrcmd.index("bool8 ScrCmd_hidemonpic(")]
    if "PlayCry_Script(species, CRY_MODE_NORMAL);" not in handler:
        raise ValueError("Bill PC normal cry dispatch differs")
    new_ids = NEW_FIELD | NEW_MOVE_SE | {"CRY_" + name + "_NORMAL" for name in NEW_CRIES.values()}
    if set(by_id) - prior_ids != new_ids:
        raise ValueError("unsupported or missing Bill cue")
    constants = {key: int(value) for key, value in re.findall(r"^#define ((?:MUS|SE)_\w+)\s+(\d+)", source["include/constants/songs.h"].decode(), re.M)}
    rows = re.findall(r"^\s*song (\w+),\s*(\d+),\s*(\d+)", source["sound/song_table.inc"].decode(), re.M)
    base, table = 0x08489968, 0x084A32CC
    for cue in (c for c in cues if c["sourcePlayer"] != 4):
        sid = constants[cue["id"]]
        address, player, secondary = struct.unpack_from("<IHH", pack, table + sid * 8 - base)
        if rows[sid] != (cue["id"].lower(), str(player), str(secondary)) or (address, player, pack[address - base + 2]) != (cue["songAddress"], cue["sourcePlayer"], cue["priority"]):
            raise ValueError("source sequence mapping differs: " + cue["id"])
    cry_names = re.findall(r"^\s*cry Cry_(\w+)\s*$", source["sound/cry_tables.inc"].decode(), re.M)
    species_ids = {key: int(value) for key, value in re.findall(r"^#define SPECIES_(\w+)\s+(\d+)", source["include/constants/species.h"].decode(), re.M)}
    cry_rows = {struct.unpack_from("<H", cries, offset)[0]: struct.unpack_from("<HHIIB3x", cries, offset)
                for offset in range(12, len(cries), 16)}
    for species, name in NEW_CRIES.items():
        cue = by_id["CRY_" + name + "_NORMAL"]
        if species_ids[name] != species or cue["species"] != species or cry_rows[species][1] != species - 1 or cry_names[species - 1].upper() != name:
            raise ValueError("source normal cry mapping differs")
        if cue["sourcePath"] not in source:
            raise ValueError("missing original cry source sample")
    if sum(c["sourcePlayer"] == 4 for c in cues) != 72:
        raise ValueError("Bill normal cry inventory differs")
    return data


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    data = verified_recipe()
    if not args.verify_only:
        for path, value in [(ROOT / 'content/generated/synth-audio/manifest.json', data)]:
            if args.check:
                if path.read_bytes() != value:
                    raise ValueError("generated output differs: " + str(path))
            else:
                path.write_bytes(value)
    print(json.dumps(dict(passed=True, cues=209, normalCries=72, newCues=21, newMoveEffects=10, newFieldEffects=3, mapMusic='MUS_ROUTE24', manifestBytes=BYTES, prior188Unchanged=True, samplePackUnchanged=True, audioRendered=False)))


if __name__ == "__main__":
    main()
