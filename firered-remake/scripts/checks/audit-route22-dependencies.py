#!/usr/bin/env python3
"""Read pinned local Git objects; emit a Route 22 implementation manifest to stdout.

No upstream code, network, checkout writes, product imports, or asset copies.
The generated manifest is planning evidence, not gameplay acceptance.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import collections
import hashlib
import json
import os
from pathlib import Path
import re
import struct
import subprocess
import sys

ROOT = _PROJECT_ROOT
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
PROFILE = "firered-route22-dependency-audit-v1"
NEW_MAPS = ("Route22", "Route22_NorthEntrance", "Route23")
SPECIES = ("MANKEY", "SPEAROW", "FEAROW")
CONNECTED_SPECIES = ("BULBASAUR", "IVYSAUR", "CHARMANDER", "CHARMELEON", "SQUIRTLE", "WARTORTLE",
                     "CATERPIE", "METAPOD", "BUTTERFREE", "WEEDLE", "KAKUNA", "BEEDRILL",
                     "PIDGEY", "PIDGEOTTO", "RATTATA", "RATICATE", "PIKACHU")
TILESETS = {"gTileset_General": "primary/general", "gTileset_ViridianCity": "secondary/viridian_city",
            "gTileset_IndigoPlateau": "secondary/indigo_plateau", "gTileset_Building": "primary/building",
            "gTileset_GenericBuilding2": "secondary/generic_building_2"}
CORE_PATHS = (
    "data/layouts/layouts.json", "data/maps/ViridianCity/map.json", "data/scripts/route23.inc",
    "data/scripts/movement.inc", "data/scripts/trainer_battle.inc", "data/text/route23.inc",
    "data/event_scripts.s", "data/maps/PalletTown_ProfessorOaksLab/scripts.inc",
    "data/maps/PalletTown_ProfessorOaksLab/text.inc", "data/maps/CeruleanCity/scripts.inc",
    "data/maps/ViridianCity_Gym/scripts.inc", "asm/macros/event.inc", "charmap.txt",
    "include/battle.h", "include/constants/battle.h", "include/constants/battle_setup.h", "include/constants/battle_ai.h",
    "include/constants/flags.h", "include/constants/vars.h", "include/constants/opponents.h",
    "include/constants/trainers.h", "include/constants/moves.h", "include/constants/species.h",
    "include/constants/abilities.h", "include/constants/metatile_behaviors.h", "include/constants/pokemon.h",
    "include/constants/items.h", "include/constants/songs.h", "include/pokemon.h",
    "include/constants/menu.h", "src/script_menu.c", "src/strings.c",
    "src/data/trainers.h", "src/data/trainer_parties.h",
    "src/data/wild_encounters.json", "src/data/pokemon/level_up_learnsets.h",
    "src/data/pokemon/species_info.h", "src/data/pokemon/evolution.h", "src/data/pokemon/level_up_learnset_pointers.h",
    "src/data/pokemon/pokedex_entries.h", "src/data/battle_moves.h",
    "src/data/text/species_names.h", "src/data/text/move_names.h", "src/data/pokemon/experience_tables.h",
    "src/battle_setup.c", "src/battle_main.c", "src/battle_script_commands.c", "src/battle_util.c",
    "src/battle_ai_script_commands.c", "src/battle_ai_switch_items.c", "data/battle_ai_scripts.s",
    "data/battle_scripts_1.s", "src/wild_encounter.c", "src/pokemon.c", "src/scrcmd.c",
    "src/event_object_movement.c", "src/data/tilesets/headers.h", "src/data/tilesets/graphics.h",
    "src/data/tilesets/metatiles.h", "src/graphics.c", "tileset_rules.mk",
    "src/data/object_events/object_event_graphics_info.h",
    "src/data/object_events/object_event_graphics.h", "src/data/object_events/object_event_pic_tables.h",
    "src/data/pokemon_graphics/front_pic_table.h", "src/data/pokemon_graphics/back_pic_table.h",
    "src/data/pokemon_graphics/palette_table.h", "src/data/pokemon_graphics/shiny_palette_table.h",
    "src/data/pokemon_graphics/front_pic_coordinates.h", "src/data/pokemon_graphics/back_pic_coordinates.h",
    "src/data/pokemon_graphics/enemy_mon_elevation.h", "src/pokemon_icon.c", "src/pokedex.c",
    "src/data/graphics/pokemon.h", "src/battle_anim_mons.c", "src/battle_controller_player.c",
    "src/battle_controller_opponent.c", "src/data/pokemon_graphics/footprint_table.h",
    "sound/songs/midi/mus_route3.mid", "sound/songs/midi/mus_victory_road.mid",
)


class AuditError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise AuditError(message)


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def canonical(data):
    return json.dumps(data, ensure_ascii=False, sort_keys=True, indent=2) + "\n"


class PinnedSource:
    def __init__(self, root):
        self.root = Path(root)
        require(self.root.is_dir(), "Reference repository is missing: " + str(self.root))
        self.cache = {}
        self.roles = collections.defaultdict(set)
        require(self.git("rev-parse", "HEAD").decode().strip() == COMMIT, "Reference HEAD differs from pinned commit")
        require(self.git("rev-parse", COMMIT + "^{tree}").decode().strip() == TREE, "Pinned tree differs")
        self.paths = set(self.git("ls-tree", "-r", "--name-only", COMMIT).decode().splitlines())

    def git(self, *args):
        env = {key: value for key, value in os.environ.items() if not key.startswith("GIT_")}
        env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull,
                   GIT_NO_REPLACE_OBJECTS="1", GIT_OPTIONAL_LOCKS="0")
        result = subprocess.run(["git", "--no-replace-objects", "--no-optional-locks", "-C", str(self.root), *args],
                                capture_output=True, env=env, check=False)
        require(result.returncode == 0, "Pinned Git read failed: " + " ".join(args) + ": " + result.stderr.decode().strip())
        return result.stdout

    @staticmethod
    def verify_blob(data, blob_id):
        actual = hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest()
        require(actual == blob_id, "Git blob content identity differs")

    def read(self, path, role="source-semantics"):
        require(path in self.paths, "Missing pinned source file: " + path)
        self.roles[path].add(role)
        if path not in self.cache:
            blob_id = self.git("rev-parse", COMMIT + ":" + path).decode().strip()
            data = self.git("cat-file", "blob", blob_id)
            self.verify_blob(data, blob_id)
            self.cache[path] = (data, blob_id)
        return self.cache[path][0]

    def text(self, path, role="source-semantics"):
        return self.read(path, role).decode("utf-8")

    def json(self, path, role="source-semantics"):
        return json.loads(self.read(path, role))

    def ref(self, path, needle):
        text = self.text(path)
        index = text.find(needle)
        require(index >= 0, "Missing source anchor: " + path + " / " + needle)
        line = text.count("\n", 0, index) + 1
        return {"path": path, "line": line, "url": f"https://github.com/pret/pokefirered/blob/{COMMIT}/{path}#L{line}"}

    def inventory(self):
        return [{"path": path, "gitBlobSha1": blob, "sha256": sha256(data), "byteLength": len(data),
                 "roles": sorted(self.roles[path])} for path, (data, blob) in sorted(self.cache.items())]


def definition(src, path, symbol):
    text = src.text(path)
    match = re.search(r"\[" + re.escape(symbol) + r"\]\s*=\s*\{(.*?)\n    \},", text, re.S)
    require(match is not None, "Missing C definition: " + symbol)
    values = dict(re.findall(r"\.(\w+)\s*=\s*([^\n]+?),?\s*$", match[1], re.M))
    return {"fields": values, "source": src.ref(path, "[" + symbol + "]")}


def number(src, path, symbol):
    match = re.search(r"^#define\s+" + re.escape(symbol) + r"\s+(0x[0-9a-fA-F]+|[0-9]+)\b", src.text(path), re.M)
    require(match is not None, "Missing numeric constant: " + symbol)
    return int(match[1], 0)


def learnset(src, name):
    path = "src/data/pokemon/level_up_learnsets.h"
    symbol = "s" + name.title() + "LevelUpLearnset"
    match = re.search(r"\b" + symbol + r"\[\] = \{(.*?)\n\};", src.text(path), re.S)
    require(match is not None, "Missing learnset: " + symbol)
    return [{"level": int(level), "move": move} for level, move in re.findall(r"LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)", match[1])
            if int(level) <= 20]


def script_blocks(src, path):
    text = src.text(path)
    labels = list(re.finditer(r"^(\w+)::?[^\n]*\n", text, re.M))
    blocks = []
    for i, label in enumerate(labels):
        end = labels[i + 1].start() if i + 1 < len(labels) else len(text)
        body = text[label.end():end]
        commands = [line.strip() for line in body.splitlines() if line.strip() and not line.lstrip().startswith("@")]
        blocks.append({"label": label[1], "source": src.ref(path, label[0].strip()), "commands": commands,
                       "bodySha256": sha256(body.encode())})
    require(blocks, "Empty script inventory: " + path)
    return blocks


def map_cells(src, layout):
    data = src.read(layout["blockdata_filepath"], "map-layout")
    require(len(data) == layout["width"] * layout["height"] * 2, "Map block dimensions differ")
    attributes = []
    for field in ("primary_tileset", "secondary_tileset"):
        path = "data/tilesets/" + TILESETS[layout[field]] + "/metatile_attributes.bin"
        attributes.append([item[0] for item in struct.iter_unpack("<I", src.read(path, "map-attributes"))])
    cells = {}
    for i, (raw,) in enumerate(struct.iter_unpack("<H", data)):
        tile = raw & 1023
        attr = attributes[int(tile >= 640)][tile - 640 if tile >= 640 else tile]
        cells[(i % layout["width"], i // layout["width"])] = {
            "collision": (raw >> 10) & 3, "elevation": raw >> 12,
            "behavior": attr & 511, "encounterType": (attr >> 24) & 7}
    return cells


def route23_south(src, route, layout):
    """Static source proof only: flat collision-zero south corridor, guard bounce, NPC occupancy."""
    cells = map_cells(src, layout)
    south_arrow = number(src, "include/constants/metatile_behaviors.h", "MB_SOUTH_ARROW_WARP")
    triggers = {(e["x"], e["y"]) for e in route["coord_events"] if e["var_value"] == "1"}
    objects = {(e["x"], e["y"]) for e in route["object_events"]}
    entry = (route["warp_events"][2]["x"], route["warp_events"][2]["y"])
    visited, queue = {entry}, collections.deque([entry])
    attempted = set()
    while queue:
        x, y = queue.popleft()
        for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            cell = cells.get(point)
            if cell is None or cell["collision"] != 0 or point in objects:
                continue
            require(cell["elevation"] == 3 and cell["behavior"] in (0, south_arrow), "South-corridor proof needs a new movement behavior")
            if point in triggers:
                attempted.add(point)
            elif point not in visited:
                visited.add(point)
                queue.append(point)
    require(min(y for x, y in visited) == 149, "First-badge boundary differs")
    require(all(cells[p]["encounterType"] == 0 for p in visited | attempted), "Reachable Route23 encounters need implementation")
    rows = [{"y": y, "x": sorted(x for x, row in visited if row == y)} for y in sorted({y for x, y in visited})]
    return {"method": "Static pinned collision/flat-elevation/NPC/coordinate-trigger proof; not runtime pathfinding acceptance",
            "entryWarpIndex": 2, "entry": list(entry), "stableReachableCellCount": len(visited), "stableRows": rows,
            "attemptedCascadeTriggerCells": sorted(map(list, attempted)),
            "allCascadeTriggerCells": sorted(map(list, triggers)), "reachableEncounterCells": 0,
            "returnWarpIndices": [2, 3], "blockingNpc": [15, 149], "requiredScene": 1,
            "requiredMissingBadge": "FLAG_BADGE02_GET", "blockingScript": "Route23_EventScript_MissingBadgeTrigger"}


def north_connection_boundary(src, maps, layouts):
    route22 = layouts[maps["Route22"]["layout"]]
    route23 = layouts[maps["Route23"]["layout"]]
    left = map_cells(src, route22)
    right = map_cells(src, route23)
    width = min(route22["width"], route23["width"])
    source_open = [x for x in range(width) if left[(x, 0)]["collision"] == 0]
    target_open = [x for x in range(width) if right[(x, route23["height"] - 1)]["collision"] == 0]
    require(not source_open and not target_open, "Direct Route22/23 walking boundary changed")
    return {"offset": 0, "overlappingX": [0, width - 1], "route22NorthPassableOverlap": source_open,
            "route23SouthPassableOverlap": target_open, "directWalkingBypass": False}


def trainer_records(src):
    path = "src/data/trainer_parties.h"
    text = src.text(path)
    records = []
    for starter_var, player, opponent in ((2, "BULBASAUR", "SQUIRTLE"), (1, "CHARMANDER", "BULBASAUR"), (0, "SQUIRTLE", "CHARMANDER")):
        symbol = "TRAINER_RIVAL_ROUTE22_EARLY_" + opponent
        record = definition(src, "src/data/trainers.h", symbol)
        party_name = "sParty_RivalRoute22Early" + opponent.title()
        match = re.search(r"\b" + party_name + r"\[\] = \{(.*?)\n\};", text, re.S)
        require(match is not None, "Missing rival party")
        party = [{"ivByte": int(iv), "fixedIv": int(iv) * 31 // 255, "level": int(level),
                  "species": species, "moves": [m.strip() for m in moves.split(",")]}
                 for iv, level, species, moves in re.findall(
                     r"\.iv = (\d+),\s*\.lvl = (\d+),\s*\.species = (SPECIES_\w+),\s*\.moves = \{([^}]+)\}", match[1])]
        require(len(party) == 2 and all(p["level"] == 9 for p in party), "Unexpected early rival roster")
        trainer_id = number(src, "include/constants/opponents.h", symbol)
        records.append({"trainer": symbol, "trainerId": trainer_id, "playerStarter": "SPECIES_" + player,
                        "starterVar": starter_var, "rivalBattleFlags": 0, "battleMode": 9,
                        "partyFlags": 1, "partyEncoding": "NO_ITEM_CUSTOM_MOVES", "aiFlags": 7,
                        "wonTrainerFlag": 0x500 + trainer_id, "basePrizeMoney": 144,
                        "party": party, "trainerDefinition": record, "partySource": src.ref(path, party_name + "[]")})
    return records


def audit(reference_root):
    src = PinnedSource(reference_root)
    for path in CORE_PATHS:
        src.read(path)
    maps = {}
    layouts = {x.get("id"): x for x in src.json("data/layouts/layouts.json")["layouts"]}
    scripts = []
    for name in NEW_MAPS:
        maps[name] = src.json(f"data/maps/{name}/map.json", "map-events")
        scripts += script_blocks(src, f"data/maps/{name}/scripts.inc")
        path = f"data/maps/{name}/text.inc"
        if path in src.paths:
            src.read(path, "map-text")
        layout = layouts[maps[name]["layout"]]
        for key in ("border_filepath", "blockdata_filepath"):
            src.read(layout[key], "map-layout")
        for key in ("primary_tileset", "secondary_tileset"):
            prefix = "data/tilesets/" + TILESETS[layout[key]] + "/"
            assets = [p for p in src.paths if p.startswith(prefix)]
            require(any(p.endswith("tiles.png") for p in assets), "Missing tileset image")
            for path in sorted(assets):
                src.read(path, "asset-manifest-only")
    scripts += script_blocks(src, "data/scripts/route23.inc")
    for name in SPECIES:
        for path in sorted(p for p in src.paths if p.startswith("graphics/pokemon/" + name.lower() + "/")):
            src.read(path, "asset-manifest-only")
        src.read("sound/direct_sound_samples/cries/" + name.lower() + ".wav", "asset-manifest-only")
    for path in ("graphics/object_events/pics/people/blue.png", "graphics/object_events/pics/people/policeman.png",
                 "graphics/object_events/palettes/npc_blue.pal", "graphics/object_events/palettes/npc_green.pal"):
        src.read(path, "asset-manifest-only")
    for path in sorted(p for p in src.paths if p.startswith("graphics/pokemon/icon_palettes/")):
        src.read(path, "asset-manifest-only")

    wild_groups = src.json("src/data/wild_encounters.json")["wild_encounter_groups"]
    wild = [e for group in wild_groups for e in group["encounters"]
            if e["map"] in ("MAP_ROUTE22", "MAP_ROUTE23") and e["base_label"].endswith("_FireRed")]
    require(len(wild) == 2, "Expected exactly two FireRed encounter tables")
    route22_land = next(e["land_mons"] for e in wild if e["map"] == "MAP_ROUTE22")
    field_group = next(group for group in wild_groups if any(e["base_label"] == "sRoute22_FireRed" for e in group["encounters"]))
    weights = next(field["encounter_rates"] for field in field_group["fields"] if field["type"] == "land_mons")
    require(weights == [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1], "Land slot weights differ")
    require(len(route22_land["mons"]) == len(weights), "Unexpected land slot count")
    slots = [dict(slot=i, weightPercent=weights[i], **mon) for i, mon in enumerate(route22_land["mons"])]
    connected_moves = {m["move"] for species in CONNECTED_SPECIES for m in learnset(src, species)}
    require(len(connected_moves) == 35, "Connected natural through-20 baseline differs")
    extra_species = []
    extra_moves = set()
    for name in SPECIES:
        moves = learnset(src, name)
        retained = [{"move": "MOVE_PURSUIT", "from": "SPECIES_SPEAROW", "learnedAt": 19}] if name == "FEAROW" else []
        extra_moves.update(m["move"] for m in moves + retained)
        extra_species.append({"species": "SPECIES_" + name, "sourceId": number(src, "include/constants/species.h", "SPECIES_" + name),
                              "learnsetThrough20": moves, "retainedPreEvolutionMoves": retained,
                              "definition": definition(src, "src/data/pokemon/species_info.h", "SPECIES_" + name),
                              "weight": definition(src, "src/data/pokemon/pokedex_entries.h", "NATIONAL_DEX_" + name)["fields"]["weight"]})
    missing_moves = sorted(extra_moves - connected_moves)
    require(missing_moves == ["MOVE_FURY_SWIPES", "MOVE_KARATE_CHOP", "MOVE_LEER", "MOVE_LOW_KICK", "MOVE_PECK", "MOVE_PURSUIT"],
            "Route22 natural move delta differs")
    move_records = [{"move": move, "sourceId": number(src, "include/constants/moves.h", move),
                     **definition(src, "src/data/battle_moves.h", move)} for move in missing_moves]
    evolution = src.text("src/data/pokemon/evolution.h")
    require("[SPECIES_SPEAROW]    = {{EVO_LEVEL, 20, SPECIES_FEAROW}}" in evolution, "Spearow evolution differs")
    require("[SPECIES_MANKEY]     = {{EVO_LEVEL, 28, SPECIES_PRIMEAPE}}" in evolution, "Mankey evolution differs")
    city = src.json("data/maps/ViridianCity/map.json")
    edges = []
    for name, record in [("ViridianCity", city)] + list(maps.items()):
        for edge in record.get("connections") or []:
            edges.append(dict(kind="connection", source=name, **edge))
        for index, warp in enumerate(record["warp_events"]):
            if name != "ViridianCity":
                edges.append(dict(kind="warp", source=name, warpIndex=index, **warp))
    scenes = []
    for path in ("data/maps/PalletTown_ProfessorOaksLab/scripts.inc", "data/maps/Route22/scripts.inc",
                 "data/maps/CeruleanCity/scripts.inc", "data/maps/ViridianCity_Gym/scripts.inc"):
        for match in re.finditer(r"setvar VAR_MAP_SCENE_ROUTE22, (\d+)", src.text(path)):
            scenes.append({"value": int(match[1]), "source": src.ref(path, match[0])})
    points = {
        "earlyRivalLossCallback": ("src/battle_setup.c", "static void CB2_EndTrainerBattle(void)\n{"),
        "customTrainerGeneration": ("src/battle_main.c", "static u8 CreateNPCTrainerParty(struct Pokemon *party, u16 trainerNum)\n{"),
        "lowKickWeightThresholds": ("src/battle_script_commands.c", "static const u16 sWeightToDamageTable[]"),
        "lowKickPower": ("src/battle_script_commands.c", "static void Cmd_weightdamagecalculation(void)\n{"),
        "pursuitSwitchInterception": ("src/battle_script_commands.c", "static void Cmd_jumpifnopursuitswitchdmg(void)\n{"),
        "vitalSpiritSleepEffect": ("src/battle_script_commands.c", "if (gBattleMons[gEffectBattler].ability == ABILITY_VITAL_SPIRIT)"),
        "fireRedFieldAbilityScope": ("src/wild_encounter.c", "static u8 GetAbilityEncounterRateModType(void)\n{"),
        "initialHiddenRival": ("data/event_scripts.s", "setflag FLAG_HIDE_ROUTE_22_RIVAL"),
    }
    label_paths = [p for p in src.cache if p.endswith(".inc") and p.startswith("data/")]
    label_refs = {}
    for path in label_paths:
        for label in re.findall(r"^(\w+)::?", src.text(path), re.M):
            label_refs[label] = src.ref(path, label + ":")
    referenced_labels = set()
    for entry in scripts:
        referenced_labels.update(re.findall(r"\b(?:Route22(?:_NorthEntrance)?|Route23)_(?:EventScript|Movement|Text)_\w+|\bCommon_Movement_\w+|\bText_\w+",
                                            "\n".join(entry["commands"])))
    require(referenced_labels <= label_refs.keys(), "Unresolved script/text label: " + str(sorted(referenced_labels - label_refs.keys())))
    target_weights = []
    for name in sorted(set(CONNECTED_SPECIES + SPECIES + ("SANDSHREW", "GEODUDE", "ONIX"))):
        record = definition(src, "src/data/pokemon/pokedex_entries.h", "NATIONAL_DEX_" + name)
        weight = int(record["fields"]["weight"])
        power = next((power for ceiling, power in ((100, 20), (250, 40), (500, 60), (1000, 80), (2000, 100)) if weight < ceiling), 120)
        target_weights.append({"species": "SPECIES_" + name, "weightHectograms": weight, "lowKickPower": power, "source": record["source"]})
    badge_strings = []
    for badge in ("BOULDER", "CASCADE"):
        symbol = "STDSTRING_" + badge + "_BADGE"
        binding = re.search(r"\[" + symbol + r"\]\s*=\s*(\w+)", src.text("src/script_menu.c"))
        require(binding is not None, "Badge string binding missing")
        string = re.search(re.escape(binding[1]) + r'\[\] = _\("([^"\n]+)"\);', src.text("src/strings.c"))
        require(string is not None, "Badge text missing")
        badge_strings.append({"symbol": symbol, "sourceId": number(src, "include/constants/menu.h", symbol),
                              "stringSymbol": binding[1], "text": string[1], "bindingSource": src.ref("src/script_menu.c", "[" + symbol + "]"),
                              "textSource": src.ref("src/strings.c", binding[1] + "[]")})
    result = {"schemaVersion": 1, "profile": PROFILE, "repository": "pret/pokefirered", "commit": COMMIT, "tree": TREE,
              "gameVersion": "FIRERED", "revision": 0, "completeGame": False, "productModified": False,
              "assetBytesCopied": 0, "verificationKind": "source-only-planning",
              "maps": [{"name": name, "layout": layouts[record["layout"]], "events": record} for name, record in maps.items()],
              "mapEdges": edges, "route23FirstBadgeBoundary": route23_south(src, maps["Route23"], layouts["LAYOUT_ROUTE23"]),
              "route22Route23ConnectionBoundary": north_connection_boundary(src, maps, layouts),
              "route22SceneWrites": scenes, "scriptEntries": scripts,
              "scriptReferences": [{"label": label, "definition": label_refs[label]} for label in sorted(referenced_labels)],
              "requiredBadgeStrings": badge_strings,
              "earlyRivalTrainers": trainer_records(src),
              "encounters": {"route22LandRate": route22_land["encounter_rate"], "route22LandSlots": slots,
                             "deferredSourceTables": wild, "waterFishingAvailableInFirstBadgeScope": False},
              "pokemonDelta": {"connectedSpeciesThrough20": len(CONNECTED_SPECIES), "connectedNaturalMoveCount": 35,
                               "combinedSpeciesThrough20": 20, "combinedNaturalMoveCount": len(connected_moves | extra_moves),
                               "species": extra_species, "additionalNaturalMoves": move_records,
                               "additionalAbility": {"symbol": "ABILITY_VITAL_SPIRIT", "sourceId": number(src, "include/constants/abilities.h", "ABILITY_VITAL_SPIRIT")},
                               "requiredDefenderWeights": target_weights,
                               "evolutions": [{"from": "SPECIES_SPEAROW", "level": 20, "to": "SPECIES_FEAROW", "included": True},
                                              {"from": "SPECIES_MANKEY", "level": 28, "to": "SPECIES_PRIMEAPE", "included": False}]},
              "semanticAnchors": {name: src.ref(*value) for name, value in points.items()}}
    result["sources"] = src.inventory()
    result["sourceSetSha256"] = sha256("".join(e["path"] + "\0" + e["sha256"] + "\n" for e in result["sources"]).encode())
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-root", type=Path, help="Local repository; HEAD must equal the pinned commit")
    args = parser.parse_args(argv)
    try:
        root = args.reference_root
        if root is None:
            config = ROOT / ".tools/reference-root"
            require(config.is_file(), "Missing .tools/reference-root; pass --reference-root")
            root = Path(config.read_text().strip())
        sys.stdout.write(canonical(audit(root)))
        return 0
    except (AuditError, OSError, UnicodeError, ValueError, KeyError, IndexError, struct.error) as error:
        print("Route22 audit failed: " + str(error), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
