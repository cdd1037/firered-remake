#!/usr/bin/env python3
"""Read one pinned Git object from an existing local reference; never run upstream code."""
import argparse
import json
from pathlib import Path
import subprocess

def require(condition, message):
    if not condition:
        raise SystemExit(message)

parser = argparse.ArgumentParser()
parser.add_argument("--source-repo", required=True, type=Path)
parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[1])
args = parser.parse_args()
lock = json.loads(Path(__file__).with_name("fetch-regional-flavor.lock.json").read_text())
require(lock["repository"] == "https://github.com/pret/pokefirered", "Source repository differs")
require(lock["commit"] == "037335f4c725d7c9aecdac87066f2002b4bd7e14", "Source commit differs")
require(len(lock["files"]) == 1, "Source scope differs")
item = lock["files"][0]
require(item["path"] == "data/scripts/flavor_text.inc", "Source path differs")
require(item["destination"] == "content/raw/pokefirered/037335f4/data/scripts/flavor_text.inc", "Destination differs")
data = subprocess.check_output(["git", "show", lock["commit"] + ":" + item["path"]], cwd=args.source_repo)
destination = args.project_root / item["destination"]
destination.parent.mkdir(parents=True, exist_ok=True)
if destination.exists():
    if destination.read_bytes() != data:
        raise SystemExit("Existing regional flavor source differs; refusing overwrite.")
else:
    with destination.open("xb") as stream:
        stream.write(data)
print(f"Materialized {item['path']}")
