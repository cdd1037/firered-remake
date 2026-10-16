#!/usr/bin/env python3
"""Verify pinned source texts and the exact compiled vitamin table; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import json
from pathlib import Path
import re
import subprocess
import sys

if len(sys.argv) != 3:
    raise SystemExit("usage: verify-vitamin-field-source.py REFERENCE_REPOSITORY_OR_PROJECT_ROOT RESULT_JSON")
reference, output = Path(sys.argv[1]), Path(sys.argv[2])
commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
paths = ['src/pokemon.c', 'src/party_menu.c', 'src/data/pokemon/item_effects.h', 'src/data/items.json', 'include/constants/item_effects.h', 'include/constants/items.h', 'include/constants/pokemon.h']
texts, files, witnesses = {}, [], []
for path in paths:
    raw = reference / "content/raw/pokefirered/037335f4"
    if raw.is_dir():
        selected = raw / path
        if not selected.is_file():
            raise SystemExit(f"missing pinned cached source: {path}")
        data = selected.read_bytes()
        source_location = str(selected)
    else:
        data = subprocess.check_output(["git", "show", f"{commit}:{path}"], cwd=reference)
        source_location = f"{reference}@{commit}:{path}"
    texts[path] = data.decode("utf-8")
    files.append({"path":path,"readFrom":source_location})

def witness(name, path, *needles):
    text = texts[path]
    locations = []
    for needle in needles:
        if needle not in text:
            raise SystemExit(f"missing source witness {name}: {needle}")
        start = text.index(needle)
        locations.append({"startLine":text.count("\n",0,start)+1,"endLine":text.count("\n",0,start+len(needle))+1})
    witnesses.append({"name":name,"path":path,"locations":locations})

witness("original per-stat 100 and total 510 caps", "include/constants/pokemon.h", "#define MAX_TOTAL_EVS 510", "#define EV_ITEM_RAISE_LIMIT 100")
witness("ten EV item delta", "include/constants/item_effects.h", "#define ITEM6_ADD_EV       10")
witness("executor clamps both caps and refreshes stats", "src/pokemon.c",
    "if (evCount >= MAX_TOTAL_EVS)\n                            return TRUE;", "if (data < EV_ITEM_RAISE_LIMIT)",
    "evDelta = EV_ITEM_RAISE_LIMIT - (data + itemEffect[idx]) + itemEffect[idx];",
    "if (evCount + evDelta > MAX_TOTAL_EVS)\n                                evDelta += MAX_TOTAL_EVS - (evCount + evDelta);",
    "SetMonData(mon, sGetMonDataEVConstants[i], &data);\n                            CalculateMonStats(mon);",
    "SetMonData(mon, sGetMonDataEVConstants[i + 2], &data);\n                            CalculateMonStats(mon);")
witness("field no-effect check includes both caps", "src/pokemon.c",
    "bool8 PokemonItemUseNoEffect(struct Pokemon *mon, u16 item, u8 partyIndex, u8 moveIndex)",
    "if (GetMonEVCount(mon) >= MAX_TOTAL_EVS)\n                            return TRUE;",
    "if (data < EV_ITEM_RAISE_LIMIT)\n                        {\n                            retVal = FALSE;")
witness("EV item bit order is SpDef before SpAtk", "src/pokemon.c",
    "MON_DATA_HP_EV,\n    MON_DATA_ATK_EV,\n    MON_DATA_DEF_EV,\n    MON_DATA_SPEED_EV,\n    MON_DATA_SPDEF_EV,\n    MON_DATA_SPATK_EV")
witness("all stats refresh and living deficit preserved without revive", "src/pokemon.c",
    "s32 level = GetLevelFromMonExp(mon);", "newMaxHP = (((n + hpEV / 4) * level) / 100) + level + 10;",
    "CALC_STAT(baseAttack, attackIV, attackEV, STAT_ATK, MON_DATA_ATK)",
    "CALC_STAT(baseDefense, defenseIV, defenseEV, STAT_DEF, MON_DATA_DEF)",
    "CALC_STAT(baseSpeed, speedIV, speedEV, STAT_SPEED, MON_DATA_SPEED)",
    "CALC_STAT(baseSpAttack, spAttackIV, spAttackEV, STAT_SPATK, MON_DATA_SPATK)",
    "CALC_STAT(baseSpDefense, spDefenseIV, spDefenseEV, STAT_SPDEF, MON_DATA_SPDEF)",
    "else if (currentHP != 0) {", "currentHP += newMaxHP - oldMaxHP;",
    "else\n            return;\n    }\n\n    SetMonData(mon, MON_DATA_HP, &currentHP);")
witness("friendship only once on success with source modifier ordering", "src/pokemon.c",
    "if (retVal == 0 && friendshipChange == 0)", "friendship += 150 * friendshipChange / 100;",
    "== ITEM_LUXURY_BALL", "== GetCurrentRegionMapSectionId()", "if (friendship > MAX_FRIENDSHIP)")
witness("party menu precheck execution and success consumption", "src/party_menu.c",
    "canHeal = PokemonItemUseNoEffect(mon, item, gPartyMenu.slotId, 0);",
    "cannotHeal = ExecuteTableBasedItemEffect_(gPartyMenu.slotId, item, 0);", "RemoveBagItem(item, 1);")
witness("HP Up party guard for Shedinja", "src/party_menu.c",
    "if (GetItemEffectType(item) == ITEM_EFFECT_HP_EV && GetMonData(mon, MON_DATA_SPECIES) == SPECIES_SHEDINJA)\n        return FALSE;")
witness("vitamin friendship 5 3 2", "src/data/pokemon/item_effects.h", "[(i) + 0] = 5", "[(i) + 1] = 3", "[(i) + 2] = 2")
items = {item["itemId"]:item for item in json.loads(texts["src/data/items.json"])["items"]}
rows=[]
constants = {name:int(value,0) for name,value in re.findall(r"^#define (ITEM\w+)\s+(0x[0-9A-Fa-f]+|[0-9]+)\s*$", texts["include/constants/item_effects.h"], re.M)}
constants["ITEM5_FRIENDSHIP_ALL"] = constants["ITEM5_FRIENDSHIP_LOW"] | constants["ITEM5_FRIENDSHIP_MID"] | constants["ITEM5_FRIENDSHIP_HIGH"]
friendship = [int(value) for value in re.findall(r"\[\(i\) \+ [012]\] = (\d+)", texts["src/data/pokemon/item_effects.h"])[:3]]
if friendship != [5,3,2]:
    raise SystemExit("vitamin friendship macro differs")
for symbol,table,number,bit,byte4,byte5 in [
    ("HP_UP","HPUp",63,"ITEM4_EV_HP",1,224), ("PROTEIN","Protein",64,"ITEM4_EV_ATK",2,224),
    ("IRON","Iron",65,"ITEM5_EV_DEF",0,225), ("CARBOS","Carbos",66,"ITEM5_EV_SPEED",0,226),
    ("CALCIUM","Calcium",67,"ITEM5_EV_SPATK",0,232), ("ZINC","Zinc",70,"ITEM5_EV_SPDEF",0,228)]:
    name="ITEM_"+symbol
    for field,value in {"pocket":"POCKET_ITEMS","type":"ITEM_TYPE_PARTY_MENU","fieldUseFunc":"FieldUseFunc_Medicine","battleUsage":0,"battleUseFunc":"NULL"}.items():
        if items[name][field] != value:
            raise SystemExit(f"item field mismatch {name} {field}")
    witness("source item ID "+name,"include/constants/items.h",f"#define {name} {number}\n")
    block=re.search(r"static const u8 sItemEffect_"+table+r"\[10\] = \{(.*?)\n\};",texts["src/data/pokemon/item_effects.h"],re.S)
    if not block or any(token not in block.group(1) for token in [bit,"ITEM5_FRIENDSHIP_ALL","[6] = ITEM6_ADD_EV","VITAMIN_FRIENDSHIP_CHANGE(7)"]):
        raise SystemExit("item effect mismatch "+name)
    source_bytes = [0] * 10
    for index,expression in re.findall(r"\[(\d+)\] = ([A-Z0-9_| ]+),", block.group(1)):
        value = 0
        for token in expression.split("|"):
            value |= constants[token.strip()]
        source_bytes[int(index)] = value
    source_bytes[7:10] = friendship
    if source_bytes != [0,0,0,0,byte4,byte5,10,5,3,2]:
        raise SystemExit("source effect evaluation differs: "+name)
    rows.append({"item":name,"sourceId":number,"effectSymbol":"sItemEffect_"+table,"effectBytes":source_bytes})
product_path = _PROJECT_ROOT / "src/Game.Domain/Items/SourceFieldVitaminRules.cs"
product = product_path.read_text()
compiled = re.findall(r'new\("(ITEM_[A-Z_]+)", (\d+), SourceVitaminStat\.([A-Za-z]+), "(sItemEffect_[A-Za-z]+)", \[([0-9,]+)\]\)', product)
if len(compiled) != 6:
    raise SystemExit("six complete compiled vitamin rows required")
expected_stats = dict(zip([r["item"] for r in rows], ["Hp","Attack","Defense","Speed","SpecialAttack","SpecialDefense"]))
for symbol,number,stat,effect_symbol,byte_values in compiled:
    row = next((r for r in rows if r["item"] == symbol), None)
    if row is None or row["sourceId"] != int(number) or row["effectSymbol"] != effect_symbol or expected_stats[symbol] != stat or row["effectBytes"] != [int(v) for v in byte_values.split(",")]:
        raise SystemExit("compiled source vitamin row differs: "+symbol)
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps({"sourceCommit":commit,"passed":True,"files":files,"witnesses":witnesses,"items":rows,
    "upstreamCodeExecuted":False,"compiledTableMatched":True,"scope":"Pinned source texts and six compiled effect rows; separate Domain execution still required."},indent=2)+"\n")
print(f"PASS {len(files)} source files; {len(witnesses)} semantic witnesses; {len(rows)} vitamin records")
