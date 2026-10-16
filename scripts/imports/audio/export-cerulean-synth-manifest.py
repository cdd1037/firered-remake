#!/usr/bin/env python3
"""Authenticate the exact Cerulean source repertoire; emit metadata without rendering audio."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

import argparse
import json
import pathlib
import re
import struct

ROOT = _PROJECT_ROOT
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
BYTES = 67625
MOVES = ("RECOVER", "SWIFT", "WATER_PULSE", "TELEPORT", "ICICLE_SPEAR", "WATER_SPORT", "HYPNOSIS", "HEADBUTT")
NEW_SE = {"SE_M_ABSORB_2", "SE_M_MEGA_KICK", "SE_M_SWIFT", "SE_M_GIGA_DRAIN",
          "SE_M_TELEPORT", "SE_M_ICY_WIND", "SE_M_SURF", "SE_M_HEADBUTT"}
NEW_CRIES = {63: "ABRA", 64: "KADABRA", 66: "MACHOP", 90: "SHELLDER", 96: "DROWZEE",
             116: "HORSEA", 118: "GOLDEEN", 120: "STARYU", 121: "STARMIE"}




def read(root, relative):
    parts = relative.split('/')
    if pathlib.PurePosixPath(relative).is_absolute() or any((p in ('', '.', '..') for p in parts)) or '\\' in relative:
        raise ValueError('unsafe Cerulean synth path')
    path = root / relative
    if any((p.is_symlink() for p in (path, *path.parents))):
        raise ValueError('Cerulean synth symlink rejected')
    return path.read_bytes()


def source_sound_effects(script):
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
            seen.add(label)
            body = blocks[label]
            effects.update(re.findall(r"\bSE_\w+", body))
            targets = re.findall(r"^\s*(?:call|goto)\s+(\w+)", body, re.M)
            if any(target not in blocks for target in targets):
                raise ValueError("unresolved source animation label")
            pending.extend(targets)
        result[move] = sorted(effects)
    return result


def verified_recipe(root=ROOT):
    data = read(root, 'scripts/recipes/audio/synth-cerulean-cues.json')
    prior = json.loads(read(root, 'scripts/recipes/audio/synth-mtmoon-cues.json'))["cues"]
    recipe = json.loads(data)
    cues = recipe["cues"]
    by_id = {c["id"]: c for c in cues}
    if recipe["sourceCommit"] != COMMIT or recipe["schemaVersion"] != 1 or len(cues) != 188 or len(by_id) != 188:
        raise ValueError("Cerulean cue inventory differs")
    if len(prior) != 170 or any(by_id.get(c["id"]) != c for c in prior):
        raise ValueError("prior170 metadata changed")
    pack = read(root, 'content/generated/audio/synth/firered.pack')
    cries = read(root, 'content/generated/audio/synth/normal-cries.bin')
    lock = json.loads(read(root, 'scripts/recipes/audio/cerulean-synth.lock.json'))
    if (lock['repository'], lock['commit']) != ('pret/pokefirered', COMMIT):
        raise ValueError("Cerulean audio source lock differs")
    source = {}
    for entry in lock["files"]:
        raw = read(root, 'content/raw/pokefirered/037335f4/' + entry['path'])
        source[entry["path"]] = raw
    effects = set().union(*map(set, source_sound_effects(source["data/battle_anim_scripts.s"].decode()).values()))
    prior_ids = {c["id"] for c in prior}
    if effects - prior_ids != NEW_SE:
        raise ValueError("new battle SE repertoire differs from exact source call closure")
    new_ids = NEW_SE | {"MUS_FUCHSIA"} | {"CRY_" + name + "_NORMAL" for name in NEW_CRIES.values()}
    if set(by_id) - prior_ids != new_ids:
        raise ValueError("unsupported or missing Cerulean cue")
    constants = {key: int(value) for key, value in re.findall(r"^#define ((?:MUS|SE)_\w+)\s+(\d+)", source["include/constants/songs.h"].decode(), re.M)}
    rows = re.findall(r"^\s*song (\w+),\s*(\d+),\s*(\d+)", source["sound/song_table.inc"].decode(), re.M)
    base, table = 0x08489968, 0x084A32CC
    for cue in (c for c in cues if c["sourcePlayer"] != 4):
        sid = constants[cue["id"]]
        address, player, secondary = struct.unpack_from("<IHH", pack, table + sid * 8 - base)
        if rows[sid] != (cue["id"].lower(), str(player), str(secondary)) or (address, player, pack[address - base + 2]) != (cue["songAddress"], cue["sourcePlayer"], cue["priority"]):
            raise ValueError("source sequence mapping differs: " + cue["id"])
    cry_names = re.findall(r"^\s*cry Cry_(\w+)\s*$", source["sound/cry_tables.inc"].decode(), re.M)
    cry_rows = {struct.unpack_from("<H", cries, offset)[0]: struct.unpack_from("<HHIIB3x", cries, offset)
                for offset in range(12, len(cries), 16)}
    for species, name in NEW_CRIES.items():
        cue = by_id["CRY_" + name + "_NORMAL"]
        if cue["species"] != species or cry_rows[species][1] != species - 1 or cry_names[species - 1].upper() != name:
            raise ValueError("source normal cry mapping differs")
    if sum(c["sourcePlayer"] == 4 for c in cues) != 65:
        raise ValueError("Cerulean normal cry inventory differs")
    return runtime_manifest_bytes(json.loads(data))


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true")
    parser.add_argument("--verify-only", action="store_true")
    args = parser.parse_args()
    data = verified_recipe()
    if not args.verify_only:
        for path, value in [(ROOT / 'content/generated/audio/synth/manifest.json', data)]:
            if args.check:
                if path.read_bytes() != value:
                    raise ValueError("generated output differs: " + str(path))
            else:
                path.write_bytes(value)
    print(json.dumps(dict(passed=True, cues=188, normalCries=65, newEffects=8, mapMusic='MUS_FUCHSIA', prior170Unchanged=True, samplePackUnchanged=True, audioRendered=False)))


if __name__ == "__main__":
    main()
