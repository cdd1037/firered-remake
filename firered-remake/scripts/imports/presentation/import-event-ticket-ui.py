#!/usr/bin/env python3
"""Import only the two original event-ticket icons from authenticated Git blobs."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse
import io
import json
import re
import subprocess
from pathlib import Path

from PIL import Image

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
TICKETS = ("mystic_ticket", "aurora_ticket")


def generate(reference):
    inputs = {}

    def read(path):
        if path not in inputs:
            inputs[path] = subprocess.check_output(
                ["git", "show", COMMIT + ":" + path], cwd=reference)
        return inputs[path]

    items = json.loads(read("src/data/items.json"))["items"]
    constants = read("include/constants/items.h").decode()
    table = read("src/data/item_icon_table.h").decode()
    graphics = read("src/data/graphics/items.h").decode()
    icons, outputs = [], {}
    for short in TICKETS:
        symbol = "ITEM_" + short.upper()
        graphics_name = "".join(word.title() for word in short.split("_"))
        item = next(row for row in items if row["itemId"] == symbol)
        assert item["pocket"] == "POCKET_KEY_ITEMS"
        assert item["fieldUseFunc"] == "FieldUseFunc_OakStopsYou"
        assert re.search(r"\[" + symbol + r"\]\s*=\s*\{gItemIcon_" + graphics_name
                         + r",\s*gItemIconPalette_" + graphics_name + r"\}", table), symbol
        icon_path = "graphics/items/icons/" + short + ".png"
        palette_path = "graphics/items/icon_palettes/" + short + ".pal"
        assert ('gItemIcon_' + graphics_name + '[] = INCBIN_U32("'
                + icon_path.replace(".png", ".4bpp.lz") + '");') in graphics
        assert ('gItemIconPalette_' + graphics_name + '[] = INCBIN_U32("'
                + palette_path.replace(".pal", ".gbapal.lz") + '");') in graphics
        palette_lines = read(palette_path).decode().splitlines()
        assert palette_lines[:3] == ["JASC-PAL", "0100", "16"], palette_path
        palette = [tuple(map(int, line.split())) for line in palette_lines[3:]]
        assert len(palette) == 16 and all(len(rgb) == 3 and all(0 <= c <= 255 for c in rgb) for rgb in palette)
        with Image.open(io.BytesIO(read(icon_path))) as image:
            assert image.mode == "P" and image.size == (24, 24), icon_path
            indices = image.tobytes()
        assert len(indices) == 576 and max(indices) < len(palette)
        # Source palette index zero is transparent; all other source pixels are opaque.
        rgba = bytes(c for index in indices for c in (*palette[index], 0 if index == 0 else 255))
        filename = short + ".rgba"
        outputs[filename] = rgba
        icons.append(dict(item="pret:" + symbol, sourceSymbol=symbol,
                          sourceItemId=int(re.search(r"^#define " + symbol + r"\s+(\d+)", constants, re.M)[1]),
                          name=item["english"], description=item["description_english"].replace("\\n", "\n"),
                          fieldUseFunction=item["fieldUseFunc"], file=filename, width=24, height=24,
                           sourceIcon=icon_path, sourcePalette=palette_path))
    lock = json.loads((_PROJECT_ROOT / 'scripts/recipes/presentation/event-ticket-ui.lock.json').read_text())
    assert lock["schemaVersion"] == 1 and lock["sourceCommit"] == COMMIT
    assert lock["inputs"] == [dict(path=path) for path in sorted(inputs)], "Event-ticket source inputs differ from lock"
    document = dict(schemaVersion=1,  icons=icons)
    outputs["event-ticket-ui.json"] = runtime_json_bytes(document)
    return document, outputs


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    document, outputs = generate(args.reference)
    lock = json.loads((_PROJECT_ROOT / 'scripts/recipes/presentation/event-ticket-ui.lock.json').read_text())
    assert lock["schemaVersion"] == 1 and lock["sourceCommit"] == COMMIT
    for path, data in outputs.items():
        target = args.output / path
        if args.check:
            assert target.read_bytes() == data, path
        else:
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
    print(json.dumps(dict(passed=True, sourceCommit=COMMIT, icons=len(document["icons"]),
                          inputs=len(lock["inputs"]))))
