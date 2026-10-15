#!/usr/bin/env python3
"""Rebuild bounded Four Island encounter declarations and source move/held-item witnesses.
Only reads immutable pret Git blobs; never executes upstream programs.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  json, re, subprocess
from pathlib import Path
COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
MAPS = ["MAP_FOUR_ISLAND_ICEFALL_CAVE_ENTRANCE", "MAP_FOUR_ISLAND_ICEFALL_CAVE_1F", "MAP_FOUR_ISLAND_ICEFALL_CAVE_B1F", "MAP_FOUR_ISLAND_ICEFALL_CAVE_BACK", "MAP_FOUR_ISLAND"]
def generate(reference):
    inputs = {}
    def read(path):
        data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
        inputs[path] = dict(path=path)
        return data.decode()
    raw = read("src/data/wild_encounters.json")
    encounters = [e for group in json.loads(raw)["wild_encounter_groups"] for e in group["encounters"] if e["map"] in MAPS and e["base_label"].endswith("_FireRed")]
    assert len(encounters) == 5
    encounters.sort(key=lambda e: MAPS.index(e["map"]))
    def map_id(symbol): return "map." + symbol[4:].lower().replace("_", "-")
    tables = "using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\n/// <summary>Exact pinned FireRed Four Island tables. Rod subdivisions retain their source indexes.</summary>\npublic static class FourIslandEncounterTables\n{\n"
    for field, prop, typ, weights in [("land_mons","Land","EarlyLandEncounterTable",[20,20,10,10,10,10,5,5,4,4,1,1]),("water_mons","Water","WaterEncounterTable",[60,30,5,4,1]),("fishing_mons","FishingSource","OldRodEncounterTable",[70,30,60,20,20,40,40,15,4,1])]:
        tables += f"    public static ImmutableArray<{typ}> {prop} {{ get; }} = [\n"
        for e in encounters:
            if field not in e: continue
            table = e[field]
            slots = ",".join(f'new({i},new("pret:{m["species"]}"),{m["min_level"]},{m["max_level"]},{weights[i]})' for i,m in enumerate(table["mons"]))
            tables += f'        new(new("{map_id(e["map"])}"),{table["encounter_rate"]},[{slots}]),\n'
        tables += "    ];\n"
    tables += "    public static bool IsCave(MapId map) => Land.Any(t => t.Map == map);\n}\n"
    combinations = sorted({(m["species"],lv) for e in encounters for field in ["land_mons","water_mons","fishing_mons"] for m in e.get(field,{}).get("mons",[]) for lv in range(m["min_level"],m["max_level"]+1)})
    pointers,learnsets,info,ids = [read(p) for p in ["src/data/pokemon/level_up_learnset_pointers.h","src/data/pokemon/level_up_learnsets.h","src/data/pokemon/species_info.h","include/constants/species.h"]]
    move_rows = []
    source = "using System.Collections.Immutable;\nusing Game.Domain.Ids;\nnamespace Game.Domain.Encounters;\n\npublic sealed record FourIslandInitialMoves(SpeciesId Species, byte Level, ImmutableArray<MoveId> Moves);\npublic sealed record FourIslandWildDependency(SpeciesId Species, int SourceSpeciesId, string Ability1, string Ability2, string ItemCommon, string ItemRare);\n\npublic static class FourIslandWildSource\n{\n    public static ImmutableArray<FourIslandInitialMoves> InitialMoves { get; } = [\n"
    for species,level in combinations:
        pointer = re.findall(r"^    \["+species+r"\] = (\w+),$",pointers,re.M)
        assert len(pointer)==1
        body = re.findall(r"^static const u16 "+pointer[0]+r"\[\] = \{\n(.*?)^\};",learnsets,re.M|re.S)
        assert len(body)==1
        moves=[]
        for lv,move in re.findall(r"LEVEL_UP_MOVE\(\s*(\d+), (MOVE_\w+)\)",body[0]):
            if int(lv)<=level and move not in moves: moves=(moves+[move])[-4:]
        assert 1<=len(moves)<=4
        move_rows.append(dict(species=species,level=level,moves=moves))
        slots=",".join('new("pret:'+move+'")' for move in moves)
        source += f'        new(new("pret:{species}"),{level},[{slots}]),\n'
    source += "    ];\n    public static ImmutableArray<FourIslandWildDependency> Species { get; } = [\n"
    dependencies=[]
    for species in sorted({s for s,l in combinations}):
        body=re.findall(r"^    \["+species+r"\] =\n    \{\n(.*?)^    \},",info,re.M|re.S)
        assert len(body)==1
        a,b=re.search(r"\.abilities = \{(ABILITY_\w+), (ABILITY_\w+)\}",body[0]).groups()
        common=re.search(r"\.itemCommon = (ITEM_\w+),",body[0])[1]
        rare=re.search(r"\.itemRare = (ITEM_\w+),",body[0])[1]
        number=int(re.search(r"^#define "+species+r"\s+(\d+)$",ids,re.M)[1])
        source += f'        new(new("pret:{species}"),{number},"{a}","{b}","{common}","{rare}"),\n'
        dependencies.append(dict(species=species,sourceSpeciesId=number,ability1=a,ability2=b,itemCommon=common,itemRare=rare))
    source += "    ];\n}\n"
    witness=dict(encounters=encounters,initialMoves=move_rows,species=dependencies)
    return {"src/Game.Domain/Encounters/FourIslandEncounterTables.cs":tables,"src/Game.Domain/Encounters/FourIslandWildSource.cs":source,"tests/Game.FourIslandEncounters.Tests/source-witness.json":json.dumps(witness,indent=2)+"\n"}
def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument("reference",type=Path);p.add_argument("output",type=Path);p.add_argument("--check",action="store_true");a=p.parse_args()
    files=generate(a.reference)
    for name,text in files.items():
        path=a.output/name
        if a.check: assert path.read_text()==text,name
        else: path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text)
    print(json.dumps(dict(checked=a.check)))
if __name__=="__main__": main()
