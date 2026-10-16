#!/usr/bin/env python3
"""Bounded, deterministic source extraction. No network or reference-code execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import json
import pathlib
import re

ROOT = _PROJECT_ROOT
PROFILE = "firered-department-store-contract-v1"
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
FLOORS = ("1F", "2F", "3F", "4F", "5F", "Roof", "Elevator")
PREFIX = "CeladonCity_DepartmentStore_"


def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n").encode()


def need(value, message):
    if not value:
        raise ValueError(message)


def safe(root, relative):
    need(not pathlib.PurePosixPath(relative).is_absolute() and
         all(x not in ("", ".", "..") for x in relative.split("/")), "unsafe source/output path")
    path = root.joinpath(*relative.split("/"))
    need(not any(p.is_symlink() for p in (path, *path.parents)), "symlink unsupported")
    return path


def map_id(symbol):
    if symbol == "MAP_CELADON_CITY":
        return "map.celadon-city"
    if symbol == "MAP_DYNAMIC":
        return "map.dynamic"
    prefix = "MAP_CELADON_CITY_DEPARTMENT_STORE_"
    need(symbol.startswith(prefix) and symbol[len(prefix):] in [x.upper() for x in FLOORS], "unexpected map symbol")
    return "map.celadon-city-department-store-" + symbol[len(prefix):].lower()


def build_contract(lock, raw):
    files = lock["files"]
    by_path = {f["path"]: f for f in files}
    text = {p: b.decode("utf8") for p, b in raw.items()}

    def source(path, symbol):
        lines = text[path].splitlines()
        matches = [i for i, line in enumerate(lines) if line.startswith(symbol + "::")]
        need(len(matches) == 1, "source label missing/ambiguous: " + symbol)
        start = matches[0]
        end = next((i for i in range(start + 1, len(lines)) if re.match(r"^\w+::?", lines[i])), len(lines))
        return dict(path=path, symbol=symbol, lineStart=start + 1, lineEnd=end)

    def block(path, symbol):
        ref = source(path, symbol)
        return "\n".join(text[path].splitlines()[ref["lineStart"]:ref["lineEnd"]])

    maps = []
    for floor in FLOORS:
        name = PREFIX + floor
        path = f"data/maps/{name}/map.json"
        event = json.loads(raw[path])
        maps.append(dict(sourceName=name, stableId=map_id(event["id"]), sourcePath=path,
                          events=event))
    city = json.loads(raw["data/maps/CeladonCity/map.json"])
    city_entries = [dict(sourceWarpIndex=i, **w) for i, w in enumerate(city["warp_events"])
                    if w["dest_map"].startswith("MAP_CELADON_CITY_DEPARTMENT_STORE_")]
    need(len(city_entries) == 2 and all(w["dest_map"].endswith("_1F") for w in city_entries), "city entry differs")

    definitions = {i["itemId"]: i for i in json.loads(raw["src/data/items.json"])["items"]}
    item_ids = {symbol: int(value, 0) for symbol, value in re.findall(
        r"^#define\s+(ITEM_\w+)\s+(0x[\da-fA-F]+|\d+)\b", text["include/constants/items.h"], re.M)}
    stocks = []
    for floor in ("2F", "4F", "5F"):
        path = f"data/maps/{PREFIX}{floor}/scripts.inc"
        for label in re.findall(r"^([\w]+)::\n\s*\.2byte ITEM_", text[path], re.M):
            symbols = re.findall(r"\.2byte (ITEM_\w+)", block(path, label))
            need(symbols[-1:] == ["ITEM_NONE"] and len(symbols) == len(set(symbols)), "stock sentinel/uniqueness differs")
            scripts = [s for s in re.findall(r"^(\w+EventScript_\w+)::", text[path], re.M)
                       if re.search(r"\bpokemart " + re.escape(label) + r"\b", block(path, s))]
            need(len(scripts) == 1, "stock clerk differs")
            stock_items = []
            for symbol in symbols[:-1]:
                item = definitions[symbol]
                stock_items.append(dict(stableId="pret:" + symbol, sourceItemId=item_ids[symbol], metadata=item))
            ordinary = label == PREFIX + "2F_Items"
            stocks.append(dict(id=label, clerkScript=scripts[0], source=source(path, label),
                               proposedAdmission="ordinary-buy-sell" if ordinary else "deferred-explicit-boundary",
                               items=stock_items))
    need([len(s["items"]) for s in stocks] == [9, 6, 6, 7, 6], "all five stock inventories must remain intact")

    elevator_path = f"data/maps/{PREFIX}Elevator/scripts.inc"
    elevator_floors = []
    for index, floor in enumerate(("5F", "4F", "3F", "2F", "1F")):
        label = PREFIX + "Elevator_EventScript_To" + floor
        body = block(elevator_path, label)
        match = re.search(r"setdynamicwarp (MAP_\w+), (\d+), (\d+), (\d+)", body)
        need(match is not None, "elevator destination missing")
        symbol, warp, x, y = match.groups()
        current = int(re.search(r"setvar VAR_0x8006, (\d+)", body).group(1))
        need(f"goto_if_eq VAR_ELEVATOR_FLOOR, {current}," in body and
             f"setvar VAR_ELEVATOR_FLOOR, {current}" in body, "elevator same-floor branch differs")
        elevator_floors.append(dict(menuIndex=index, label=floor, sourceFloorValue=current,
                                    destinationMapSymbol=symbol, destinationMapId=map_id(symbol),
                                    destinationWarpId=int(warp), x=int(x), y=int(y), source=source(elevator_path, label)))
    menu = re.search(r"sMultichoiceList_DeptStoreElevator\[\] = \{(.*?)\};", text["src/script_menu.c"], re.S)
    labels = re.findall(r"\{ (\w+) \}", menu.group(1))
    need(labels == ["gText_5F_2", "gText_4F_2", "gText_3F_2", "gText_2F_2", "gText_1F_2", "gOtherText_Exit"], "elevator menu differs")
    animation = {}
    for symbol in ("sElevatorAnimationDuration", "sElevatorWindowAnimDuration"):
        match = re.search(symbol + r"\[\] = \{(.*?)\};", text["src/field_specials.c"], re.S)
        animation[symbol] = [int(n) for n in re.findall(r"\d+", match.group(1))]
    need(animation["sElevatorAnimationDuration"] == [8,16,24,32,38,46,53,56,57], "elevator shake table differs")
    need("if ((data[1] % 3) == 0)" in text["src/field_specials.c"], "elevator shake period differs")
    entrances, exits, static_warps = [], [], []
    for entry in maps:
        for i, warp in enumerate(entry["events"]["warp_events"]):
            row = dict(sourceMapId=entry["stableId"], sourceWarpIndex=i, **warp)
            if warp["dest_map"].endswith("_ELEVATOR"):
                entrances.append(row)
            if warp["dest_map"] == "MAP_DYNAMIC":
                exits.append(row)
            else:
                static_warps.append(row)
    need(len(entrances) == 5 and len(exits) == 2 and all(x["dest_warp_id"] == "WARP_ID_DYNAMIC" for x in exits), "dynamic elevator scope differs")

    deferred = [
        dict(map="2F", script="ClerkTMs", service="TM stock", reason="Keep all six source TMs. UI availability policy and machine-pocket transactions require explicit integration."),
        dict(map="3F", script="CounterTutor", service="Counter move tutor", reason="Original one-time tutor, compatibility, move replacement and receipt flow not admitted."),
        dict(map="4F", script="Clerk", service="Dolls, mail and evolution stones", reason="Shop and stone evolution effects not admitted by this bounded increment."),
        dict(map="5F", script="ClerkXItems", service="Battle stat items", reason="Battle item effects and their shop are not admitted."),
        dict(map="5F", script="ClerkVitamins", service="Vitamins", reason="EV, friendship and item-cap semantics and their shop are not admitted."),
        dict(map="Roof", script="VendingMachine", service="Drink vending", reason="Money-box menu, purchase loop and drink healing are not admitted."),
        dict(map="Roof", script="ThirstyGirl", service="Drink-for-TM rewards", reason="Source drink removal, bag-full branch and one-time TM receipts are not admitted.")]
    for service in deferred:
        service["mapId"] = "map.celadon-city-department-store-" + service.pop("map").lower()
        name = next(m["sourceName"] for m in maps if m["stableId"] == service["mapId"])
        service["script"] = name + "_EventScript_" + service["script"]
        service["source"] = source(f"data/maps/{name}/scripts.inc", service["script"])
        service["complete"] = False
    constants = {"VAR_ELEVATOR_FLOOR": 0x403A, "MULTICHOICE_DEPT_STORE_ELEVATOR": 31, "WARP_ID_DYNAMIC": 127, "FLAG_TEMP_2": 2}
    need(re.search(r"^#define TEMP_FLAGS_START\s+0x0$", text["include/constants/flags.h"], re.M), "temporary flag base differs")
    for name, expected in constants.items():
        if name == "FLAG_TEMP_2":
            need(re.search(r"#define FLAG_TEMP_2\s+\(TEMP_FLAGS_START \+ 0x02\)", text["include/constants/flags.h"]), "temp flag differs")
            continue
        matches = re.findall(r"^#define\s+" + name + r"\s+(0x[\da-fA-F]+|\d+)\b", "\n".join(text[p] for p in text if p.startswith("include/constants/")), re.M)
        need(len(matches) == 1 and int(matches[0], 0) == expected, "constant differs: " + name)
    return dict(schemaVersion=1,

                 completeDepartmentStore=False, maps=maps, cityEntrances=city_entries,
                stocks=stocks, staticWarps=static_warps, deferredServices=deferred, constants=constants,
                elevator=dict(mapId="map.celadon-city-department-store-elevator", floors=elevator_floors,
                    entrances=entrances, dynamicExits=exits, menuTextSymbols=labels, exitChoice=5, cancelChoice=127,
                    sourceMenuDefaultTracksCurrentFloor=True, selectionUpdatesDestinationBeforeSameFloorCheck=True,
                    cancelPreservesDestination=True, floorSelectChangesMapImmediately=False, rooftopElevatorStop=False,
                    temporaryFlag="FLAG_TEMP_2", temporaryFlagSetAfterMotion=True,
                    shakeTable=animation["sElevatorAnimationDuration"], shakePeriodFrames=3,
                    windowTable=animation["sElevatorWindowAnimDuration"]),
                requiredCommonScripts=["EventScript_GetElevatorFloor", "EventScript_ReleaseEnd"],
                requiredCommonTexts=["Text_WantWhichFloor", "Text_MayIHelpYou", "Text_PleaseComeAgain"])


def import_content(project_root, source=None, check=False):
    lock_bytes = safe(project_root, "scripts/recipes/items/department-store.lock.json").read_bytes()
    lock = json.loads(lock_bytes)
    need((lock['profile'], lock['sourceCommit']) == (PROFILE, COMMIT), "source identity differs")
    need(len(lock["files"]) == 36, "source inventory count differs")
    raw = {}
    source = source if source is not None else project_root / "content/raw/pokefirered/037335f4"
    for row in lock["files"]:
        need(row['path'] not in raw, "invalid source inventory")
        path = safe(source, row["path"])
        data = path.read_bytes()
        data.decode("utf8")
        raw[row["path"]] = data
    contract = canonical(build_contract(lock, raw))
    outputs = {"content/generated/data/services/department-store.json": contract,
               **{"content/raw/pokefirered/037335f4/"+p: b for p, b in raw.items()}}
    for relative, data in outputs.items():
        path = safe(project_root, relative)
        if check:
            need(path.is_file() and path.read_bytes() == data, "output differs: " + relative)
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
    return dict(check=check, sourceFiles=len(raw), sourceBytes=sum(map(len, raw.values())))


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=pathlib.Path)
    parser.add_argument("--project-root", type=pathlib.Path, default=ROOT)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    print(json.dumps(import_content(args.project_root, args.source, args.check), sort_keys=True))
