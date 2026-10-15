#!/usr/bin/env python3
"""Import battle dialogue from a local pokefirered source tree into runtime data."""
import argparse
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
DECLARATION = re.compile(r'(?:static )?const u8 ([A-Za-z0-9_]+)\[\] = _\("((?:\\.|[^"\\])*)"\);')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=ROOT / "content/raw/pokefirered/037335f4")
    parser.add_argument("--output", type=Path, default=ROOT / "content/generated/battle/text.json")
    args = parser.parse_args()
    texts = {}
    for name in ("src/battle_message.c", "src/strings.c"):
        for symbol, text in DECLARATION.findall((args.source / name).read_text(encoding="utf-8")):
            texts.setdefault(symbol, text)
    if not texts:
        raise ValueError("No battle text declarations found")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(texts, ensure_ascii=False, sort_keys=True, separators=(",", ":")) + "\n", encoding="utf-8")
    print(f"Imported {len(texts)} text labels to {args.output}")


if __name__ == "__main__":
    main()
