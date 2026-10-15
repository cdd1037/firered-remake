#!/usr/bin/env python3
"""Promote original Mr. Mime and Jynx ownership using pinned, already-shared source bytes.

Usage: REFERENCE DEPENDENCY OUTPUT [--check]. Only this compact generated package
and its lock are written; canonical species and shared raw inputs stay untouched.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from machine_catalog import compatibility_additions, machine_definitions, admission_species

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
SPECIES = {"SPECIES_MR_MIME": 122, "SPECIES_JYNX": 124}
SHARED_RAW = "content/raw/pokefirered/037335f4/"
DEPENDENCIES = (
    'content/generated/canonical-pokemon/catalog.json',
    'content/generated/fossil-lab-rules/pokemon.json',
)


def need(value, message):
    if not value:
        raise ValueError(message)


def identity(symbol):
    return {"value": "pret:" + symbol}


def generate(reference, dependency):
    texts, inputs, dependencies = {}, {}, []

    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            local = SHARED_RAW + path
            inputs[path] = dict(path=path, localPath=local)
            texts[path] = data.decode()
        return texts[path]

    def one(path, pattern):
        matches = list(re.finditer(pattern, read(path), re.M | re.S))
        need(len(matches) == 1, "Expected one source declaration: " + pattern)
        return matches[0]

    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=read(path).count("\n", 0, match.start()) + 1,
                    endLine=read(path).count("\n", 0, match.end()) + 1)

    def dependent(path):
        data = (dependency / path).read_bytes()
        dependencies.append(dict(path=path))
        return json.loads(data)

    canonical, previous = [dependent(path) for path in DEPENDENCIES]
    need(previous['ownedSpeciesCount'] == 159 and previous['opponentSpeciesCount'] == 190, 'Exact fossil ownership')
    need(len(canonical["species"]) == 386, "Complete canonical species dependency")
    promoted = []
    for species, source_id in SPECIES.items():
        definition = next(s for s in canonical["species"] if s["id"] == identity(species))
        metadata = next(s for s in canonical["metadata"] if s["id"] == identity(species))
        need(definition["sourceSpeciesId"] == metadata["nationalDexNumber"] == source_id and
             not definition["evolutions"] and not metadata["sourceEvolutions"], "Canonical terminal identity: " + species)
        need(one("include/constants/species.h", rf"^#define {species}[ \t]+(\d+)[ \t]*$")[1] == str(source_id), "Source species number")
        evolution = read("src/data/pokemon/evolution.h")
        need(not re.search(rf"^\s*\[{species}\]\s*=", evolution, re.M), "No source evolution: " + species)
        promoted.append(dict(definition=definition, metadata=metadata))

    machine_rows = compatibility_additions(dependency, 'content/generated/npc-trade-owned-rules/machines.json')
    docs = {
        "pokemon.json": dict(sourceCommit=COMMIT,  profile="firered-npc-trade-owned-pokemon-v1",


                             previousOwnedSpeciesCount=159, ownedSpeciesCount=161,
                             previousOpponentSpeciesCount=190, opponentSpeciesCount=190,
                             ownedSpeciesAdded=list(SPECIES), opponentSpeciesAdded=[], evolutions=[]),
        "machines.json": dict(sourceCommit=COMMIT, profile="firered-npc-trade-teaching-v1", previousSpeciesCount=159,
                              speciesCount=161, machineCount=41, additions=machine_rows,
                               itemsAdded=[]),
        "source-input-hashes.json": dict(sourceCommit=COMMIT,
                                         inputs=[inputs[k] for k in sorted(inputs)], dependencyInputs=dependencies),
    }
    return docs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("reference", "dependency", "output"):
        parser.add_argument(name, type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    docs = generate(args.reference, args.dependency)
    files = {"content/generated/npc-trade-owned-rules/" + name: (json.dumps(doc, indent=2, ensure_ascii=False) + "\n").encode()
             for name, doc in docs.items()}
    lock = dict(sourceCommit=COMMIT,  profile="firered-npc-trade-owned-rules-v1",

                files=[dict(path=path) for path, data in sorted(files.items())])
    files["scripts/npc-trade-owned-rules.lock.json"] = (json.dumps(lock, indent=2) + "\n").encode()
    for path, data in files.items():
        destination = args.output / path
        if args.check:
            need(destination.read_bytes() == data, "Regeneration differs: " + path)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=161, opponents=190, machines=41, newCells=82), indent=2))


if __name__ == "__main__":
    main()
