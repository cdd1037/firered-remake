#!/usr/bin/env python3
"""Read pinned git objects only; never execute upstream scripts or copy game art."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json
from pathlib import Path
import re
import subprocess
import sys

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
PATHS = ["src/hall_of_fame.c", "src/hof_pc.c", "src/strings.c", "data/scripts/pc.inc", "include/constants/help_system.h", "src/save.c"]
SYMBOLS = ["gText_HOFNumber", "gText_HOFCorrupted", "gText_UPDOWNPick_ABUTTONNext_BBUTTONBack",
           "gText_UPDOWNPick_ABUTTONBBUTTONCancel", "gText_ABUTTONExit", "gText_Number", "gText_Level", "gText_IDNumber"]


def git(repo, item):
    return subprocess.check_output(["git", "-C", str(repo), "show", item])


def build(repo):
    actual_tree = subprocess.check_output(["git", "-C", str(repo), "rev-parse", COMMIT + "^{tree}"]).decode().strip()
    raw = {path: git(repo, COMMIT + ":" + path) for path in PATHS}
    source = {path: data.decode() for path, data in raw.items()}
    hall = source[PATHS[0]]
    def positions(name):
        block = re.search(r"sHallOfFame_Mon" + name + r"TeamPositions\[\d\]\[4\]\s*=\s*\{(.*?)\};", hall, re.S).group(1)
        return [{"x": int(row[2]), "y": int(row[3])} for row in re.findall(r"\{\s*(-?\d+),\s*(-?\d+),\s*(-?\d+),\s*(-?\d+)\s*\}", block)]
    texts = {}
    for symbol in SYMBOLS:
        value = re.search(r"\b" + symbol + r'\[\]\s*=\s*_\("((?:[^"\\]|\\.)*)"\)', source["src/strings.c"]).group(1)
        texts[symbol] = bytes(value, "utf-8").decode("unicode_escape")
    return dict(
        maximumTeams=int(re.search(r"#define HALL_OF_FAME_MAX_TEAMS (\d+)", hall).group(1)),
        maximumHallNumber=int(re.search(r"GetGameStat\(GAME_STAT_ENTERED_HOF\) < (\d+)", source["src/save.c"]).group(1)),
        miscellaneousHelpContext=int(re.search(r"#define HELPCONTEXT_PC_MISC\s+(\d+)", source["include/constants/help_system.h"]).group(1)),
        fullTeamPositions=positions("Full"), halfTeamPositions=positions("Half"), text=texts)


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("usage: import-hall-pc.py PINNED_GIT_REPOSITORY OUTPUT_ROOT")
    output = Path(sys.argv[2]) / "content/generated/ui/hall-pc.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    data = (json.dumps(build(Path(sys.argv[1])), ensure_ascii=False, indent=2) + "\n").encode()
    output.write_bytes(data)
    print(json.dumps(dict(path=str(output))))
