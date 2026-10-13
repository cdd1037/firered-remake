#!/usr/bin/env python3
"""Import all three original fossil catalogs from pinned Git blobs.

Usage: REFERENCE DEPENDENCY OUTPUT [--check]. Never executes upstream code or
rewrites the canonical catalog. Generation writes only this package and lock.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
SPECIES = ("SPECIES_OMANYTE", "SPECIES_OMASTAR", "SPECIES_KABUTO", "SPECIES_KABUTOPS", "SPECIES_AERODACTYL")
DEPENDENCIES = {
    "content/generated/canonical-pokemon/catalog.json": "c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc",
    "content/generated/fighting-dojo-rules/pokemon.json": "4679a195369fe66442a0d4b8f75000efc9714083e52232c40e717e3d4c89cdd1",
    "content/generated/fighting-dojo-rules/machines.json": "c47f335e8091e1f8b2e0cda39819af278a4a4f47140ae762942d95d7b99143d6",
    "content/generated/trainer-tower-rules/items.json": "2c2c9bcc8f4bb34fc2505794f07c7baeb3094efd10b1836e055e3bf1c3839129",
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def need(value, message):
    if not value:
        raise ValueError(message)


def fingerprint(value):
    return sha(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode())


def identity(symbol):
    return {"value": "pret:" + symbol}


def generate(reference, dependency):
    need(subprocess.check_output(["git", "rev-parse", COMMIT + "^{tree}"], cwd=reference).decode().strip() == TREE,
         "Pinned source tree")
    texts, inputs, raw, dependencies = {}, {}, {}, []

    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            raw[path] = data
            inputs[path] = dict(path=path, byteLength=len(data), sha256=sha(data),
                                gitBlobSha1=hashlib.sha1(b"blob " + str(len(data)).encode() + b"\0" + data).hexdigest())
            texts[path] = data.decode()
        return texts[path]

    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]

    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1, sha256=inputs[path]["sha256"])

    def number(path, symbol):
        value = one(path, r"^#define " + symbol + r"[ \t]+([^\n]+)")[1].split("//")[0].strip()
        return int(value, 0)

    def dependent(path):
        data = (dependency / path).read_bytes()
        need(sha(data) == DEPENDENCIES[path], "Frozen dependency changed: " + path)
        dependencies.append(dict(path=path, byteLength=len(data), sha256=sha(data)))
        return json.loads(data)

    canonical, previous, previous_machines, previous_items = [dependent(p) for p in DEPENDENCIES]
    need(previous["ownedSpeciesCount"] == 154 and previous["opponentSpeciesCount"] == 186,
         "Exact Dojo predecessor")
    need(previous_machines["speciesCount"] == 154 and previous_machines["machineCount"] == 41,
         "Exact Dojo machine predecessor")
    species = {s["id"]["value"][5:]: s for s in canonical["species"]}
    metadata = {s["id"]["value"][5:]: s for s in canonical["metadata"]}
    edges = []
    for index, name in enumerate(SPECIES):
        definition, meta = species[name], metadata[name]
        need(definition["sourceSpeciesId"] == 138 + index and not definition["evolutions"],
             "Canonical fossil identity and metadata-only evolution")
        references = [definition["source"], definition["learnsetSource"], meta["learnsetPointerSource"], meta["dexSource"]]
        references += [m["source"] for m in definition["levelUpMoves"]]
        references += [e["source"] for e in meta["sourceEvolutions"]]
        for source in references:
            read(source["path"])
            need(inputs[source["path"]]["sha256"] == source["sha256"], "Canonical source identity: " + name)
            need(1 <= source["startLine"] <= source["endLine"] <= len(texts[source["path"]].splitlines()),
                 "Canonical source span: " + name)
        need(number("include/constants/species.h", name) == 138 + index, "Source species number")
        if index in (0, 2):
            raw_edge = meta["sourceEvolutions"]
            need(len(raw_edge) == 1, "One source evolution")
            edge = raw_edge[0]
            need((edge["method"], edge["sourceMethodId"], edge["parameter"], edge["parameterValue"], edge["target"]) ==
                 ("EVO_LEVEL", 4, "40", 40, identity(SPECIES[index + 1])), "Exact source level-40 edge")
            match = one("src/data/pokemon/evolution.h", r"^    \[" + name + r"\][ \t]+= \{\{EVO_LEVEL, 40, " + SPECIES[index + 1] + r"\}\},$")
            need(ref("src/data/pokemon/evolution.h", name, match) == edge["source"], "Evolution source span identity")
            edges.append(dict(species=identity(name), **edge))
        else:
            need(not meta["sourceEvolutions"], "Terminal fossil species")

    machine_ids = [row["item"]["value"][5:] for row in previous_machines["additions"]]
    need(len(machine_ids) == len(set(machine_ids)) == 41, "Forty-one inherited machine items")
    machine_rows = []
    tm_path = "src/data/pokemon/tmhm_learnsets.h"
    for item in sorted(machine_ids):
        rows = []
        for name in SPECIES:
            match = one(tm_path, r"^    \[" + name + r"\]\s*=\s*TMHM_LEARNSET\((.*?)\),")
            tokens = re.findall(r"TMHM\(((?:TM|HM)\d{2})_[A-Z0-9_]+\)", match[1])
            need(tokens and len(tokens) == len(set(tokens)), "Source machine row syntax")
            need(not re.sub(r"TMHM\((?:TM|HM)\d{2}_[A-Z0-9_]+\)|[|\s]", "", match[1]), "Unhandled machine row")
            rows.append(dict(species=identity(name), sourceSpeciesId=species[name]["sourceSpeciesId"],
                             canLearn=item[5:] in tokens, source=ref(tm_path, name, match)))
        machine_rows.append(dict(item=identity(item), compatibility=rows))

    lab_path = "data/maps/CinnabarIsland_PokemonLab_ExperimentRoom/scripts.inc"
    gifts = []
    for kind, name, suffix, item, fossil_value in [
            ("HELIX", SPECIES[0], "Omanyte", "ITEM_HELIX_FOSSIL", 1),
            ("DOME", SPECIES[2], "Kabuto", "ITEM_DOME_FOSSIL", 2),
            ("AMBER", SPECIES[4], "Aerodactyl", "ITEM_OLD_AMBER", 3)]:
        symbol = "CinnabarIsland_PokemonLab_ExperimentRoom_EventScript_Give" + suffix
        match = one(lab_path, r"^" + symbol + r"::\n((?:(?!^\w+::).)*)")
        need(re.search(r"^\tgivemon " + name + r", 5$", match[1], re.M), "Exact level-five source gift")
        need(match[1].index("goto_if_eq VAR_RESULT, 2, EventScript_NoMoreRoomForPokemon") < match[1].index("setflag FLAG_REVIVED_" + kind),
             "No revived flag when delivery fails")
        natural = []
        for move in species[name]["levelUpMoves"]:
            if move["level"] <= 5 and move["move"] not in natural:
                natural = (natural + [move["move"]])[-4:]
        expected = {"HELIX": ("CONSTRICT", "WITHDRAW"), "DOME": ("SCRATCH", "HARDEN"), "AMBER": ("WING_ATTACK",)}[kind]
        need(natural == [identity("MOVE_" + move) for move in expected], "Source level-five initial moves")
        names_path = "src/data/text/species_names.h"
        display = one(names_path, r'^    \[' + name + r'\] = _\("([^"]+)"\),$')
        gifts.append(dict(kind=kind.lower(), fossilValue=fossil_value,
                          item=identity(item), sourceItemId=number("include/constants/items.h", item),
                          species=identity(name), sourceSpeciesId=species[name]["sourceSpeciesId"], name=display[1],
                          level=5, moves=natural, metLocation="MAPSEC_CINNABAR_ISLAND", metLevel=5,
                          metGame=4, language=2, ball="ITEM_POKE_BALL", heldItem="ITEM_NONE",
                          originalTrainerPolicy="OT_ID_PLAYER_ID", shinyLock=False,
                          revivedFlag="FLAG_REVIVED_" + kind,
                          revivedFlagId=number("include/constants/flags.h", "FLAG_REVIVED_" + kind),
                          source=ref(lab_path, symbol, match), nameSource=ref(names_path, name, display)))

    need(previous_items["itemCount"] == 124, "Exact Trainer Tower inventory predecessor")
    item_path, item_symbol = "src/data/items.json", "ITEM_OLD_AMBER"
    item_definition = next(i for i in json.loads(read(item_path))["items"] if i["itemId"] == item_symbol)
    item_match = one(item_path, r'^    \{\n      "english": "OLD AMBER",.*?^    \}')
    item_ref = ref(item_path, item_symbol, item_match)
    need(item_definition == dict(english="OLD AMBER", itemId=item_symbol, price=0,
         holdEffect="HOLD_EFFECT_NONE", holdEffectParam=0,
         description_english="A piece of amber that contains\\nthe genes of an ancient POKéMON.\\nIt is clear with a reddish tint.",
         importance=1, registrability=0, pocket="POCKET_KEY_ITEMS", type="ITEM_TYPE_BAG_MENU",
         fieldUseFunc="FieldUseFunc_OakStopsYou", battleUsage=0, battleUseFunc="NULL", secondaryId=0),
         "Exact Old Amber source definition")
    need(number("include/constants/items.h", item_symbol) == 354, "Old Amber source number")
    item_metadata = dict(id=identity(item_symbol), sourceItemSymbol=item_symbol, sourceItemId=354,
        name=item_definition["english"], price=0, importance=1, registrability=0, pocket="POCKET_KEY_ITEMS",
        type="ITEM_TYPE_BAG_MENU", holdEffect="HOLD_EFFECT_NONE", holdEffectParam=0,
        fieldUseFunction="FieldUseFunc_OakStopsYou", battleUsage=0, battleUseFunction="NULL", secondaryId=0,
        canToss=False, canSell=False, canGive=False, source=item_ref)
    amber_item = {k: item_metadata[k] for k in ("id", "sourceItemSymbol", "sourceItemId", "name", "price", "pocket",
        "importance", "type", "fieldUseFunction", "battleUsage", "battleUseFunction", "holdEffectParam", "source")}
    amber_item.update(description=item_definition["description_english"].replace("\\n", "\n"),
        healHp=0, cureStatus=0, effectBytes=[], useSupport=11, effectSource=None, sourceMetadata=item_metadata)

    exp_path = "src/data/pokemon/experience_tables.h"
    need("#define EXP_SLOW(n)((5 * CUBE(n)) / 4)" in read(exp_path) and "EXP_SLOW(5)" in read(exp_path),
         "Source slow level-five experience")
    create_path = "src/script_pokemon_util.c"
    give_match = one(create_path, r"^u8 ScriptGiveMon\([^\n]+\)\n\{\n.*?^\}")
    need("CreateMon(mon, species, level, 32, 0, 0, OT_ID_PLAYER_ID, 0);" in give_match[0], "Player OT and random IV/PID gift")
    macro = one("asm/macros/event.inc", r"^\s*\.macro givemon ([^\n]+)")
    need("item=ITEM_NONE" in macro[1], "Default gift held item")
    random_path = "include/random.h"
    need("#define Random32() (Random() | (Random() << 16))" in read(random_path) and
         "#define RAND_MULT 1103515245" in read(random_path) and
         "#define ISO_RANDOMIZE1(val)(RAND_MULT * (val) + 24691)" in read(random_path), "Pinned RNG source")
    pokemon_path = "src/pokemon.c"
    box_match = one(pokemon_path, r"^void CreateBoxMon\([^\n]+\)\n\{\n.*?^\}")
    receive_match = one(pokemon_path, r"^u8 GiveMonToPlayer\([^\n]+\)\n\{\n.*?^\}")
    need("else //Player is the OT" in box_match[0] and "value = Random();" in box_match[0], "Source player OT and packed IVs")
    need("SetMonData(mon, MON_DATA_OT_ID, gSaveBlock2Ptr->playerTrainerId);" in receive_match[0], "Source player OT receipt")
    need(number("include/constants/pokemon.h", "MON_GIVEN_TO_PARTY") == 0 and
         number("include/constants/pokemon.h", "MON_GIVEN_TO_PC") == 1 and
         number("include/constants/pokemon.h", "MON_CANT_GIVE") == 2, "Source delivery result numbers")

    def draws(seed):
        values = []
        for _ in range(4):
            seed = (seed * 1103515245 + 24691) & 0xffffffff
            values.append(seed >> 16)
        return seed, values

    shiny_seed = next(seed for seed in range(1000000) if (draws(seed)[1][0] ^ draws(seed)[1][1] ^ 42) < 8)
    female_seed = next(seed for seed in range(1000) if (draws(seed)[1][0] & 255) < 31)
    vectors = []
    for gift in gifts:
        source_species = species[gift["species"]["value"][5:]]
        for label, initial, ot in [("ordinary", 42, 42), ("shiny_player_ot", shiny_seed, 42),
                                   ("female_player_ot", female_seed, 0x12345678), ("full_uint_seed", 0xffffffff, 0x12345678)]:
            final, values = draws(initial)
            pid = values[0] | (values[1] << 16)
            ivs = [values[2] & 31, (values[2] >> 5) & 31, (values[2] >> 10) & 31,
                   values[3] & 31, (values[3] >> 5) & 31, (values[3] >> 10) & 31]
            vectors.append(dict(name=label, species=gift["species"], level=5, originalTrainerId=ot,
                                initialSeed=initial, finalSeed=final, draws=4, rawDraws=values,
                                personality=pid, nature=pid % 25, ivs=ivs, abilitySlot=pid & 1,
                                ability=source_species["ability2" if pid & 1 else "ability1"],
                                gender="female" if (pid & 255) < source_species["genderRatio"] else "male",
                                isShiny=(ot >> 16 ^ ot & 65535 ^ pid >> 16 ^ pid & 65535) < 8,
                                experience=156 if gift["kind"] == "amber" else 125, moves=gift["moves"]))
    docs = {
        "pokemon.json": dict(sourceCommit=COMMIT, sourceTree=TREE, profile="firered-fossil-lab-owned-pokemon-v1",
                             canonicalCatalogSha256=DEPENDENCIES[next(iter(DEPENDENCIES))], previousOwnedSpeciesCount=154,
                             ownedSpeciesCount=159, previousOpponentSpeciesCount=186, opponentSpeciesCount=190,
                             ownedSpeciesAdded=list(SPECIES), opponentSpeciesAdded=list(SPECIES[:4]), trainerOnlySpeciesAdded=[],
                             species=[dict(definition=species[s], metadata=metadata[s]) for s in SPECIES], evolutions=edges),
        "machines.json": dict(sourceCommit=COMMIT, profile="firered-fossil-lab-teaching-v1", previousSpeciesCount=154,
                              speciesCount=159, machineCount=41, additions=machine_rows,
                              additionsSemanticSha256=fingerprint(machine_rows), itemsAdded=[]),
        "shop.json": dict(sourceCommit=COMMIT, profile="firered-fossil-lab-inventory-v1", previousItemCount=124,
                          itemCount=125, additions=[amber_item], additionsSemanticSha256=fingerprint([amber_item]), shopsAdded=[]),
        "rules.json": dict(sourceCommit=COMMIT, profile="firered-fossil-lab-rules-v1", gifts=gifts,
                           generationCases=vectors, generationDraws=4,
                           generationOrder="Player OT; random PID low16/high16 then two packed IV draws, also on full storage. No shiny rejection. The engine's explicit Random32 operand order is tested; original target-compiler operand ordering remains unverified.",
                           creationSource=ref(create_path, "ScriptGiveMon", give_match),
                           boxCreationSource=ref(pokemon_path, "CreateBoxMon", box_match),
                           deliverySource=ref(pokemon_path, "GiveMonToPlayer", receive_match),
                           deferredSpecies=[]),
    }
    docs["source-input-hashes.json"] = dict(sourceCommit=COMMIT, sourceTree=TREE,
                                             inputs=[inputs[k] for k in sorted(inputs)], dependencyInputs=dependencies)
    return docs, raw


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("reference", "dependency", "output"):
        parser.add_argument(name, type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    docs, raw = generate(args.reference, args.dependency)
    files = {"content/generated/fossil-lab-rules/" + name: (json.dumps(doc, indent=2, ensure_ascii=False) + "\n").encode()
             for name, doc in docs.items()}
    files.update({"content/raw/fossil-lab-rules/" + name: data for name, data in raw.items()})
    lock = dict(sourceCommit=COMMIT, sourceTree=TREE, profile="firered-fossil-lab-rules-v1",
                importerSha256=sha(Path(__file__).read_bytes()),
                files=[dict(path=path, byteLength=len(data), sha256=sha(data)) for path, data in sorted(files.items())])
    files["scripts/fossil-lab-rules.lock.json"] = (json.dumps(lock, indent=2) + "\n").encode()
    for path, data in files.items():
        destination = args.output / path
        if args.check:
            need(destination.read_bytes() == data, "Regeneration differs: " + path)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=159, opponents=190, evolutionEdges=2, machines=41,
                         newCells=205, items=125, creationVectors=len(docs["rules.json"]["generationCases"]),
                         machineFingerprint=docs["machines.json"]["additionsSemanticSha256"],
                         hashes={path: sha(data) for path, data in files.items() if "/raw/" not in path}), indent=2))


if __name__ == "__main__":
    main()
