#!/usr/bin/env python3
"""Reproduce small lifecycle fixtures/table from explicitly hash-pinned research inputs.
The algorithm fixture is authored recovery v2 (2026-10-13), not the lost original bytes.
Use --reference-root to rebuild numeric contracts directly from authenticated pinned Git blobs.
This mode requires the exact authored algorithm fixture already included in source-only packages.
The legacy --source-package mode retains both original input gates.
Does not execute upstream code, load Content, fetch a ROM, or modify existing game catalogs.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

SOURCE_COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
SOURCE_TREE = "7515cd4067ea55f8cdbc73624478afd2736c58b9"
SOURCE_CONTRACT_SHA256 = "50dd7b3ad77cae1af98bed7cd572510ab94b35783ba3dda15ccd2246e0b7c51f"
LOCK = {
    "species-and-locations.json": "34babe0df6fb7d06dc10c1b5a5811e4601d4ce71d07397df70c01d2ccddfb72e",
    "algorithm-vectors.json": "d2f1597451e37bbcd48c0592f46e53393d60288d7608c19ab692746bafa6462e",
}



def pinned_json(path, digest):
    raw = path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != digest:
        raise ValueError("Pinned input hash mismatch: " + path.name)
    value = json.loads(raw)
    if value["sourceCommit"] != SOURCE_COMMIT:
        raise ValueError("Pinned source commit mismatch: " + path.name)
    return raw, value


def source_block(text, pattern):
    match = re.search(pattern, text)
    if match is None:
        raise ValueError("Missing pinned source declaration: " + pattern)
    start = text.index("{", match.start())
    depth, end = 1, start + 1
    while depth and end < len(text):
        depth += (text[end] == "{") - (text[end] == "}")
        end += 1
    if depth:
        raise ValueError("Unterminated pinned source declaration: " + pattern)
    return text[start + 1:end - 1]


def reference_contract(reference_root):
    """Parse numeric facts only; execute no upstream source, scripts or build tools."""
    def git(*args):
        return subprocess.check_output(["git", "-C", str(reference_root), *args], timeout=30)

    if git("rev-parse", "--verify", SOURCE_COMMIT + "^{commit}").decode().strip() != SOURCE_COMMIT:
        raise ValueError("Pinned source commit unavailable")
    if git("rev-parse", SOURCE_COMMIT + "^{tree}").decode().strip() != SOURCE_TREE:
        raise ValueError("Pinned source tree mismatch")
    inputs = {}

    def read(path):
        if path.startswith("/") or ".." in Path(path).parts:
            raise ValueError("Invalid pinned source path")
        raw = git("show", SOURCE_COMMIT + ":" + path)
        blob_id = git("rev-parse", SOURCE_COMMIT + ":" + path).decode().strip()
        actual = hashlib.sha1(b"blob " + str(len(raw)).encode() + b"\0" + raw).hexdigest()
        if actual != blob_id:
            raise ValueError("Pinned source blob mismatch: " + path)
        inputs[path] = {"gitBlobSha1": blob_id, "sha256": hashlib.sha256(raw).hexdigest()}
        return raw.decode()

    roamer = read("src/roamer.c")
    raw_rows = source_block(roamer, r"sRoamerLocations\[\]\[7\]\s*=")
    symbols = []
    for raw_row in re.findall(r"\{([^{}]+)\}", raw_rows):
        row = []
        for token in raw_row.split(","):
            token = token.strip()
            match = re.fullmatch(r"MAP_NUM\((MAP_[A-Z0-9_]+)\)", token)
            if token != "___" and match is None:
                raise ValueError("Unknown source location token: " + token)
            row.append("MAP_UNDEFINED" if token == "___" else match.group(1))
        symbols.append(row)
    if len(symbols) != 26 or any(len(row) != 7 for row in symbols):
        raise ValueError("Incomplete pinned source location table")
    groups = json.loads(read("data/maps/map_groups.json"))
    if groups["group_order"][3] != "gMapGroup_TownsAndRoutes":
        raise ValueError("Unexpected source roamer map group")
    numbers = {"MAP_UNDEFINED": 255}
    required = {symbol for row in symbols for symbol in row} - {"MAP_UNDEFINED"}
    for number, name in enumerate(groups[groups["group_order"][3]]):
        if not name.startswith("Route"):
            continue
        if re.fullmatch(r"[A-Za-z0-9_]+", name) is None:
            raise ValueError("Invalid pinned source map name")
        map_data = json.loads(read("data/maps/" + name + "/map.json"))
        if map_data["id"] in required:
            if map_data["id"] in numbers:
                raise ValueError("Duplicate pinned map symbol")
            numbers[map_data["id"]] = number
    rows = [[{"number": numbers[symbol], "symbol": symbol} for symbol in row] for row in symbols]
    source_ids = read("include/constants/species.h")
    stats_source = read("src/data/pokemon/species_info.h")
    learns_source = read("src/data/pokemon/level_up_learnsets.h")
    species = []
    for name in ("Raikou", "Entei", "Suicune"):
        symbol = "SPECIES_" + name.upper()
        stats_block = source_block(stats_source, r"\[" + symbol + r"\]\s*=")
        fields = {"attack": "Attack", "defense": "Defense", "hp": "HP",
                  "specialAttack": "SpAttack", "specialDefense": "SpDefense", "speed": "Speed"}
        stats = {}
        for key, suffix in fields.items():
            match = re.search(r"\.base" + suffix + r"\s*=\s*(\d+)", stats_block)
            if match is None:
                raise ValueError("Missing pinned species stat: " + symbol + "/" + key)
            stats[key] = int(match.group(1))
        source_id = re.search(r"#define\s+" + symbol + r"\s+(\d+)", source_ids)
        if source_id is None:
            raise ValueError("Missing pinned species ID: " + symbol)
        learnset = source_block(learns_source, r"s" + name + r"LevelUpLearnset\[\]\s*=")
        moves = []
        for level, move in re.findall(r"LEVEL_UP_MOVE\((\d+),\s*(MOVE_\w+)\)", learnset):
            if int(level) <= 50 and "pret:" + move not in moves:
                moves = (moves + ["pret:" + move])[-4:]
        if len(moves) != 4:
            raise ValueError("Incomplete pinned level50 moveset: " + symbol)
        species.append({"symbol": symbol, "definition": {"id": {"value": "pret:" + symbol},
                        "sourceSpeciesId": int(source_id.group(1)), "baseStats": stats}, "level50Moves": moves})
    return {"sourceCommit": SOURCE_COMMIT, "locationRows": rows, "species": species}, inputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    source_input = parser.add_mutually_exclusive_group(required=True)
    source_input.add_argument("--source-package", type=Path, help="Legacy exactly hash-pinned research package")
    source_input.add_argument("--reference-root", type=Path, help="Local pret Git repository containing the pinned commit")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    fixture = root / "tests/Game.RoamerLifecycle.Tests/fixtures"
    raw = {}
    source_inputs = {}
    if args.reference_root:
        # This authored golden is a mandatory package input. Never infer it from product output.
        raw["algorithm-vectors.json"], _ = pinned_json(fixture / "algorithm-vectors.json", LOCK["algorithm-vectors.json"])
        source, source_inputs = reference_contract(args.reference_root)
    else:
        data = {}
        for name, digest in LOCK.items():
            raw[name], data[name] = pinned_json(args.source_package / name, digest)
        source = data["species-and-locations.json"]
    rows = source["locationRows"]
    if len(rows) != 26 or any(len(row) != 7 for row in rows) or any(x["number"] != 255 for x in rows[-1]):
        raise ValueError("Incomplete source location table")
    table = "using System.Collections.Immutable;\n\nnamespace Game.Domain.Roaming;\n\n"
    table += "// Source src/roamer.c:36-62 at " + SOURCE_COMMIT + ".\n"
    table += "// Blank columns deliberately remain255: rejection draws are part of the algorithm.\n"
    table += "internal static class SourceRoamerLocations\n{\n    internal static ImmutableArray<ImmutableArray<int>> Rows { get; } =\n    [\n"
    for row in rows[:-1]:
        table += "        [" + ", ".join(str(x["number"]) for x in row) + "], // " + row[0]["symbol"] + "\n"
    table += "    ];\n}\n"
    contract = {"sourceCommit": SOURCE_COMMIT, "locationRows": rows, "species": [
        {"symbol": species["symbol"], "definition": {
            key: species["definition"][key] for key in ("id", "sourceSpeciesId", "baseStats")
        }, "level50Moves": species["level50Moves"]} for species in source["species"]
    ]}
    contract_bytes = (json.dumps(contract, indent=2) + "\n").encode()
    if hashlib.sha256(contract_bytes).hexdigest() != SOURCE_CONTRACT_SHA256:
        raise ValueError("Rebuilt source-contract hash mismatch")
    outputs = {
        fixture / "algorithm-vectors.json": raw["algorithm-vectors.json"],
        fixture / "source-contract.json": contract_bytes,
        root / "src/Game.Domain/Roaming/SourceRoamerLocations.cs": table.encode(),
    }
    for path, content in outputs.items():
        if args.check:
            if path.read_bytes() != content:
                raise ValueError("Non-deterministic or changed output: " + str(path))
        else:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(content)
    print(json.dumps({"checked": args.check, "outputs": len(outputs), "sourceCommit": SOURCE_COMMIT,
                      "mode": "reference-root" if args.reference_root else "source-package",
                      "sourceContractSha256": SOURCE_CONTRACT_SHA256,
                      "algorithmFixtureSha256": LOCK["algorithm-vectors.json"],
                      "authenticatedSourceInputs": source_inputs}, indent=2))


if __name__ == "__main__":
    main()
