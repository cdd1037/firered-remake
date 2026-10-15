#!/usr/bin/env python3
"""Compile music fanfare wait durations from local source; no runtime source parsing."""
import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / "content/raw/pokefirered/037335f4")
    parser.add_argument("--output", type=Path, default=ROOT / "content/generated/presentation-cues/fanfares.json")
    args = parser.parse_args()
    source = (args.source / "src/sound.c").read_text(encoding="utf-8")
    table = re.search(r"static const struct Fanfare sFanfares\[\] = \{(.*?)\n\};", source, re.S)
    if table is None:
        raise ValueError("Fanfare table is missing")
    counters = {cue: int(frames) for cue, frames in re.findall(r"\[FANFARE_[A-Z_]+\]\s*=\s*\{\s*(MUS_[A-Z_]+),\s*([0-9]+)\s*\}", table.group(1))}
    if "MUS_LEVEL_UP" not in counters or any(frames <= 0 for frames in counters.values()):
        raise ValueError("Invalid fanfare durations")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(counters, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"Imported {len(counters)} fanfare durations")


if __name__ == "__main__":
    main()
