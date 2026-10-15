#!/usr/bin/env python3
"""Read pinned Git objects only; never execute upstream source or write the reference checkout."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json
from pathlib import Path
import subprocess
import sys

if len(sys.argv) != 3:
    raise SystemExit("usage: verify-pp-boost-source.py REFERENCE_REPOSITORY RESULT_JSON")
reference, output = Path(sys.argv[1]), Path(sys.argv[2])
commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
paths = ['src/pokemon.c', 'src/party_menu.c', 'src/data/pokemon/item_effects.h', 'src/data/items.json']
texts, files = {}, []
for path in paths:
    data = subprocess.check_output(["git", "show", f"{commit}:{path}"], cwd=reference)
    texts[path] = data.decode("utf-8")
    files.append({"path": path})

pokemon, party, effects = (texts[p] for p in ("src/pokemon.c", "src/party_menu.c", "src/data/pokemon/item_effects.h"))
witnesses = {
    "PP Up guard and current increment": (pokemon, "if (data < 3 && i > 4)", "data = GetMonData(mon, MON_DATA_PP1 + moveIndex, NULL) + data;"),
    "PP Max field precheck excludes Sketch": (pokemon, "if (data < 3 && tmp >= 5)", "data += gPPUpAddValues[moveIndex] * 3; // Apply 3 PP Ups (max)"),
    "max PP integer calculation": (pokemon, "return basePP + ((basePP * 20 * ((gPPUpGetMask[moveIndex] & ppBonuses) >> (2 * moveIndex))) / 100);"),
    "one friendship update and modifier order": (pokemon, "if (retVal == 0 && friendshipChange == 0)", "friendship += 150 * friendshipChange / 100;", "== ITEM_LUXURY_BALL", "== GetCurrentRegionMapSectionId()"),
    "field menu validates before effect": (party, "static void TryUsePPItemOutsideBattle(u8 taskId)", "bool8 noEffect = PokemonItemUseNoEffect", "gItemUseCB = ItemUseCB_RestorePP;", "RemoveBagItem(gSpecialVar_ItemId, 1);"),
    "cancel returns to target menu": (party, "static void Task_HandleRestoreWhichMoveInput(u8 taskId)", "if (input == MENU_B_PRESSED)", "ReturnToUseOnWhichMon(taskId);"),
    "PP tables include vitamin friendship": (effects, "static const u8 sItemEffect_PPUp[9]", "static const u8 sItemEffect_PPMax[9]", "VITAMIN_FRIENDSHIP_CHANGE(6)", "[(i) + 0] = 5", "[(i) + 1] = 3", "[(i) + 2] = 2"),
}
for name, (text, *needles) in witnesses.items():
    if any(needle not in text for needle in needles):
        raise SystemExit(f"missing source witness: {name}")
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps({"sourceCommit": commit, "passed": True, "files": files,
    "witnesses": list(witnesses), "upstreamCodeExecuted": False}, indent=2) + "\n")
print(f"PASS {len(files)} source files; {len(witnesses)} semantic witnesses")
