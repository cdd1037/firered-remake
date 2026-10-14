#!/usr/bin/env python3
"""Promote original Mr. Mime and Jynx ownership using pinned, already-shared source bytes.

Usage: REFERENCE DEPENDENCY OUTPUT [--check]. Only this compact generated package
and its lock are written; canonical species and shared raw inputs stay untouched.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
SPECIES = {"SPECIES_MR_MIME": 122, "SPECIES_JYNX": 124}
SHARED_RAW = "content/raw/fossil-lab-rules/"
DEPENDENCIES = {
    "content/generated/canonical-pokemon/catalog.json": "c5ef32d4b425c9ce720b0b29305d5855e6c7f5eff8ff05cc36d2cb51584eb9bc",
    "content/generated/fossil-lab-rules/pokemon.json": "3b1fd31e40b0f2b87ef92f392a80552e2b3c5e403308903c53a02982106a3940",
    "content/generated/fossil-lab-rules/machines.json": "c333902b0b61882e21f029ac174f476006259491c6284757726fdc97425f79f3",
    "scripts/fossil-lab-rules.lock.json": "671828fc22bfae36fe3f2318b83a0ae1324cb40ab886e6909643d27d1c9af0dc",
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
    texts, inputs, dependencies = {}, {}, []

    def read(path):
        if path not in texts:
            data = subprocess.check_output(["git", "show", COMMIT + ":" + path], cwd=reference)
            local = SHARED_RAW + path
            need((dependency / local).read_bytes() == data, "Shared source differs from pinned Git blob: " + local)
            inputs[path] = dict(path=path, localPath=local, byteLength=len(data), sha256=sha(data),
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

    def dependent(path):
        data = (dependency / path).read_bytes()
        need(sha(data) == DEPENDENCIES[path], "Frozen dependency changed: " + path)
        dependencies.append(dict(path=path, byteLength=len(data), sha256=sha(data)))
        return json.loads(data)

    canonical, previous, previous_machines, previous_lock = [dependent(path) for path in DEPENDENCIES]
    need(canonical["sourceCommit"] == previous["sourceCommit"] == previous_machines["sourceCommit"] == previous_lock["sourceCommit"] == COMMIT,
         "Exact source commit across dependencies")
    need(previous["ownedSpeciesCount"] == 159 and previous["opponentSpeciesCount"] == 190 and
         previous_machines["speciesCount"] == 159 and previous_machines["machineCount"] == 41, "Exact fossil predecessor")
    need(len(canonical["species"]) == 386, "Complete canonical species dependency")
    promoted = []
    for species, source_id in SPECIES.items():
        definition = next(s for s in canonical["species"] if s["id"] == identity(species))
        metadata = next(s for s in canonical["metadata"] if s["id"] == identity(species))
        need(definition["sourceSpeciesId"] == metadata["nationalDexNumber"] == source_id and
             not definition["evolutions"] and not metadata["sourceEvolutions"], "Canonical terminal identity: " + species)
        references = [definition["source"], definition["learnsetSource"], metadata["learnsetPointerSource"], metadata["dexSource"]]
        references += [m["source"] for m in definition["levelUpMoves"]]
        for source in references:
            text = read(source["path"])
            need(inputs[source["path"]]["sha256"] == source["sha256"] and
                 1 <= source["startLine"] <= source["endLine"] <= len(text.splitlines()), "Exact canonical source reference")
        need(one("include/constants/species.h", rf"^#define {species}[ \t]+(\d+)[ \t]*$")[1] == str(source_id), "Source species number")
        evolution = read("src/data/pokemon/evolution.h")
        need(not re.search(rf"^\s*\[{species}\]\s*=", evolution, re.M), "No source evolution: " + species)
        promoted.append(dict(definition=definition, metadata=metadata))

    machine_ids = [row["item"]["value"][5:] for row in previous_machines["additions"]]
    need(len(machine_ids) == len(set(machine_ids)) == 41, "Exactly 41 existing machines")
    tm_path = "src/data/pokemon/tmhm_learnsets.h"
    machine_rows = [dict(item=identity(item), compatibility=[]) for item in sorted(machine_ids)]
    for species, source_id in SPECIES.items():
        match = one(tm_path, rf"^    \[{species}\]\s*=\s*TMHM_LEARNSET\((.*?)\),")
        tokens = re.findall(r"TMHM\(((?:TM|HM)\d{2})_[A-Z0-9_]+\)", match[1])
        need(tokens and len(tokens) == len(set(tokens)) and
             not re.sub(r"TMHM\((?:TM|HM)\d{2}_[A-Z0-9_]+\)|[|\s]", "", match[1]), "Source machine row syntax")
        source = ref(tm_path, species, match)
        for row in machine_rows:
            row["compatibility"].append(dict(species=identity(species), sourceSpeciesId=source_id,
                                            canLearn=row["item"]["value"][10:] in tokens, source=source))
    docs = {
        "pokemon.json": dict(sourceCommit=COMMIT, sourceTree=TREE, profile="firered-npc-trade-owned-pokemon-v1",
                             canonicalCatalogSha256=DEPENDENCIES["content/generated/canonical-pokemon/catalog.json"],
                             predecessorCatalogSha256=DEPENDENCIES["content/generated/fossil-lab-rules/pokemon.json"],
                             previousOwnedSpeciesCount=159, ownedSpeciesCount=161,
                             previousOpponentSpeciesCount=190, opponentSpeciesCount=190,
                             ownedSpeciesAdded=list(SPECIES), opponentSpeciesAdded=[], evolutions=[],
                             canonicalSpeciesFingerprint=fingerprint(promoted)),
        "machines.json": dict(sourceCommit=COMMIT, profile="firered-npc-trade-teaching-v1", previousSpeciesCount=159,
                              speciesCount=161, machineCount=41, additions=machine_rows,
                              additionsSemanticSha256=fingerprint(machine_rows), itemsAdded=[]),
        "source-input-hashes.json": dict(sourceCommit=COMMIT, sourceTree=TREE,
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
    lock = dict(sourceCommit=COMMIT, sourceTree=TREE, profile="firered-npc-trade-owned-rules-v1",
                importerSha256=sha(Path(__file__).read_bytes()),
                files=[dict(path=path, byteLength=len(data), sha256=sha(data)) for path, data in sorted(files.items())])
    files["scripts/npc-trade-owned-rules.lock.json"] = (json.dumps(lock, indent=2) + "\n").encode()
    for path, data in files.items():
        destination = args.output / path
        if args.check:
            need(destination.read_bytes() == data, "Regeneration differs: " + path)
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
    print(json.dumps(dict(checked=args.check, owned=161, opponents=190, machines=41, newCells=82,
                         sharedSourceInputs=len(docs["source-input-hashes.json"]["inputs"]),
                         machineFingerprint=docs["machines.json"]["additionsSemanticSha256"],
                         hashes={path: sha(data) for path, data in files.items()}), indent=2))


if __name__ == "__main__":
    main()
