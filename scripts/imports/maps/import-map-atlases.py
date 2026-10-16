#!/usr/bin/env python3
"""Compile map-render PNGs into regional 16x16 RGBA pages and tile-index streams.

Run after the ordinary map/border importers. Inputs are their generated runtime-map
and border artifact lists, never screenshots or upstream code. Original PNGs and
animated/interactive patch assets are left intact. --check regenerates and compares.
--verify reports original-pixel comparisons separately from packed-only consistency.
--prune verifies first, backs up outside the project, then removes packed map PNGs.
Requires Pillow, already used by the map importers.
"""
from __future__ import annotations
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
from array import array
from collections import defaultdict
import io
import json
import os
from pathlib import Path
import shutil
import sys
import tempfile
import zlib
from PIL import Image

ROOT = _PROJECT_ROOT
TILE = 16
COLUMNS = 64
PER_PAGE = COLUMNS * COLUMNS


def inventory(generated: Path):
    images = {}
    documents = []
    for path in sorted((generated / 'maps/regions').rglob('*.json')):
        document = json.loads(path.read_bytes())
        if isinstance(document, dict) and document.get('schemaVersion') == 3 and 'blocks' in document:
            documents.append(path)
    documents += sorted((generated / 'maps/borders').rglob('border.json'))
    for document in documents:
        for artifact in json.loads(document.read_text())['artifacts']:
            if not any(artifact['path'] == name or artifact['path'].endswith('.' + name) for name in ('bottom.png', 'middle.png', 'top.png', 'map.png', 'border-bottom.png', 'border-middle.png', 'border-top.png', 'border.png')):
                continue  # Opening map lists also contain player sprite sheets.
            path = document.parent / artifact['path']
            relative = path.relative_to(generated).as_posix()
            if path.suffix != '.png' or '..' in Path(artifact['path']).parts:
                raise ValueError('Unsupported map artifact: ' + relative)
            dimensions = (artifact['width'], artifact['height'])
            if relative in images and images[relative] != dimensions:
                raise ValueError('Inconsistent map artifact dimensions: ' + relative)
            images[relative] = dimensions
    if not images:
        raise ValueError('No generated map artifacts found')
    return dict(sorted(images.items()))


from image_io import encode_png


def decode_indices(data: bytes, count: int):
    raw = zlib.decompress(data)
    if len(raw) != count * 2:
        raise ValueError('Tile index length differs from image dimensions')
    indices = array('H')
    indices.frombytes(raw)
    if sys.byteorder != 'little':
        indices.byteswap()
    return indices


class AtlasReader:
    """One regional page cohort cached while traversing logical image paths."""
    def __init__(self, output: Path):
        self.output = output
        self.catalog = json.loads((output / 'catalog.json').read_text())
        c = self.catalog
        if (c['schemaVersion'], c['tileSize'], c['pageColumns'], c['tilesPerPage']) != (1, TILE, COLUMNS, PER_PAGE):
            raise ValueError('Unsupported map atlas format')
        self.indices = (output / 'indices.bin').read_bytes()
        self.pack = None
        self.tiles = []

    def read(self, path):
        entry = self.catalog['images'][path]
        pack = entry['pack']
        if pack != self.pack:
            self.tiles = []
            descriptors = self.catalog['pages'][pack]
            for page_index, descriptor in enumerate(descriptors):
                name = descriptor['path']
                if Path(name).name != name:
                    raise ValueError('Atlas page path must be a flat filename')
                with Image.open(self.output / name) as source:
                    page = source.convert('RGBA')
                count = descriptor['tileCount']
                if (page.size != (descriptor['width'], descriptor['height']) or
                    page.width != COLUMNS * TILE or page.height % TILE or max(page.size) > 1024 or
                    not 0 < count <= PER_PAGE or count > page.width * page.height // TILE**2 or
                    page_index < len(descriptors) - 1 and count != PER_PAGE):
                    raise ValueError('Atlas page dimensions/count differ: ' + name)
                for n in range(count):
                    x, y = n % COLUMNS * TILE, n // COLUMNS * TILE
                    self.tiles.append(page.crop((x, y, x + TILE, y + TILE)))
            self.pack = pack
        width, height = entry['width'], entry['height']
        if width <= 0 or height <= 0 or width % TILE or height % TILE:
            raise ValueError('Invalid map dimensions: ' + path)
        start, length = entry['offset'], entry['length']
        if start < 0 or length <= 0 or start + length > len(self.indices):
            raise ValueError('Invalid map index span: ' + path)
        indices = decode_indices(self.indices[start:start + length], width * height // TILE**2)
        image = Image.new('RGBA', (width, height))
        columns = width // TILE
        for n, tile in enumerate(indices):
            if tile >= len(self.tiles):
                raise ValueError('Tile reference exceeds regional pages: ' + path)
            image.paste(self.tiles[tile], (n % columns * TILE, n // columns * TILE))
        return image


def input_image(generated, path, previous):
    if (generated / path).is_file():
        with Image.open(generated / path) as source:
            return source.convert('RGBA')
    if previous is None or path not in previous.catalog['images']:
        raise ValueError('Neither fresh PNG nor packed image exists: ' + path)
    return previous.read(path)


def verify(generated: Path, output: Path, expected=None, previous=None):
    packed = AtlasReader(output)
    expected = inventory(generated) if expected is None else expected
    if set(packed.catalog['images']) != set(expected):
        raise ValueError('Map atlas inventory differs from generated map artifacts')
    pixels = original_images = original_pixels = recovered_images = 0
    for path, dimensions in expected.items():
        reconstructed = packed.read(path)
        if reconstructed.size != dimensions:
            raise ValueError('Map image dimensions differ: ' + path)
        fresh = (generated / path).is_file()
        if fresh or previous is not None:
            original = input_image(generated, path, previous)
            if reconstructed.size != original.size or reconstructed.tobytes() != original.tobytes():
                raise ValueError('Reconstructed RGBA pixels differ: ' + path)
            if fresh:
                original_images += 1
                original_pixels += original.width * original.height
            else:
                recovered_images += 1
        pixels += reconstructed.width * reconstructed.height
    return dict(images=len(expected), packs=len(packed.catalog['pages']), decodedPixels=pixels,
                originalImagesCompared=original_images, originalPixelsCompared=original_pixels,
                recoveredImagesCompared=recovered_images, packedOnlyImages=len(expected)-original_images,
                originalPixelParity=original_images == len(expected),
                sourcePngBytes=sum((generated / p).stat().st_size for p in expected if (generated / p).is_file()),
                atlasPngBytes=sum(p.stat().st_size for p in output.glob('*.png')),
                indexBytes=len(packed.indices), catalogBytes=(output / 'catalog.json').stat().st_size)


def compile_atlases(generated: Path, output: Path, images, previous=None):
    output.mkdir(parents=True, exist_ok=True)
    catalog = dict(schemaVersion=1, tileSize=TILE, pageColumns=COLUMNS, tilesPerPage=PER_PAGE,
                   pages={}, images={})
    packs = defaultdict(list)
    for path, dimensions in images.items():
        parts = path.split('/')
        pack = parts[2] if parts[:2] == ['maps', 'regions'] else 'borders'
        packs[pack].append((path, dimensions))
    indices_blob = bytearray()
    streams = {}
    for pack in sorted(packs):
        ids, tiles = {}, []
        for path, dimensions in packs[pack]:
            image = input_image(generated, path, previous)
            if image.size != dimensions or image.width % TILE or image.height % TILE:
                raise ValueError('Map image is not a whole metatile grid: ' + path)
            indices = array('H')
            for y in range(0, image.height, TILE):
                for x in range(0, image.width, TILE):
                    tile = image.crop((x, y, x + TILE, y + TILE)).tobytes()
                    identity = ids.get(tile)
                    if identity is None:
                        identity = len(tiles)
                        if identity > 65535:
                            raise ValueError('Regional atlas exceeds UInt16 tile identity: ' + pack)
                        ids[tile] = identity
                        tiles.append(tile)
                    indices.append(identity)
            if sys.byteorder != 'little':
                indices.byteswap()
            encoded = zlib.compress(indices.tobytes(), 9)
            offset = streams.get(encoded)
            if offset is None:
                offset = len(indices_blob)
                streams[encoded] = offset
                indices_blob.extend(encoded)
            catalog['images'][path] = dict(pack=pack, width=image.width, height=image.height,
                offset=offset, length=len(encoded))
        pages = []
        for start in range(0, len(tiles), PER_PAGE):
            batch = tiles[start:start + PER_PAGE]
            page = Image.new('RGBA', (COLUMNS * TILE, ((len(batch) + COLUMNS - 1) // COLUMNS) * TILE))
            for n, pixels in enumerate(batch):
                page.paste(Image.frombytes('RGBA', (TILE, TILE), pixels),
                           (n % COLUMNS * TILE, n // COLUMNS * TILE))
            name = f'{pack}-{start // PER_PAGE:03d}.png'
            (output / name).write_bytes(encode_png(page))
            pages.append(dict(path=name, width=page.width, height=page.height, tileCount=len(batch)))
        catalog['pages'][pack] = pages
    (output / 'indices.bin').write_bytes(indices_blob)
    (output / 'catalog.json').write_text(json.dumps(catalog, sort_keys=True, separators=(',', ':')) + '\n')


def prune_originals(root: Path, generated: Path, output: Path, images, backup_directory=None):
    # Verify installed pages before planning any removal. Only actual remaining PNGs
    # are compared to originals; absent inputs are reported as packed consistency.
    verified = verify(generated, output, images)
    removals = [generated / path for path in images if (generated / path).is_file()]
    updates = []
    for manifest in sorted(generated.rglob('manifest.json')):
        document = json.loads(manifest.read_text())
        changed = False
        for key in ('outputs', 'files', 'artifacts'):
            rows = document.get(key)
            if not isinstance(rows, list):
                continue
            kept = []
            for row in rows:
                path = row if isinstance(row, str) else row.get('path') if isinstance(row, dict) else None
                logical = (manifest.parent / path).relative_to(generated).as_posix() if path else None
                if logical in images:
                    changed = True
                else:
                    kept.append(row)
            document[key] = kept
            if key == 'outputs' and 'outputCount' in document and len(kept) != len(rows):
                document['outputCount'] = len(kept)
        if changed:
            updates.append((manifest, (json.dumps(document, sort_keys=True, separators=(',', ':')) + '\n').encode()))
    if not removals and not updates:
        return dict(prunedImages=0, manifestsUpdated=0, backup=None, **verified)
    backup = (backup_directory or Path(tempfile.mkdtemp(prefix='firered-map-png-backup-'))).resolve()
    if backup == root or root in backup.parents:
        raise ValueError('Prune backup must be outside the project')
    if backup.exists() and any(backup.iterdir()):
        raise ValueError('Prune backup directory must be empty')
    backup.mkdir(parents=True, exist_ok=True)
    originals = removals + [path for path, _ in updates]
    for path in originals:
        target = backup / path.relative_to(root)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, target)
    try:
        for path, data in updates:
            path.write_bytes(data)
        for path in removals:
            path.unlink()
    except Exception:
        for path in originals:
            shutil.copy2(backup / path.relative_to(root), path)
        raise
    return dict(prunedImages=len(removals), manifestsUpdated=len(updates), backup=str(backup), **verified)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=ROOT)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--backup-dir', type=Path, help='Empty directory outside project, used only with --prune')
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--check', action='store_true')
    mode.add_argument('--verify', action='store_true')
    mode.add_argument('--prune', action='store_true', help='Regenerate, verify, back up, then remove packed map PNGs')
    args = parser.parse_args()
    root = args.root.resolve()
    generated = root / 'content/generated'
    output = args.output or generated / 'maps/atlases'
    if args.backup_dir and not args.prune:
        parser.error('--backup-dir requires --prune')
    if args.prune and output.resolve() != (generated / 'maps/atlases').resolve():
        parser.error('--prune requires the canonical content/generated/maps/atlases output')
    images = inventory(generated)
    if args.verify:
        print(json.dumps(dict(verified=True, **verify(generated, output, images)), sort_keys=True))
        return
    previous = AtlasReader(output) if (output / 'catalog.json').is_file() else None
    with tempfile.TemporaryDirectory(prefix='firered-map-atlases-') as temporary:
        staging = Path(temporary)
        compile_atlases(generated, staging, images, previous)
        result = verify(generated, staging, images, previous)
        produced = {p.name for p in staging.iterdir()}
        if args.check:
            existing = {p.name for p in output.iterdir()} if output.exists() else set()
            if existing != produced or any((output / name).read_bytes() != (staging / name).read_bytes() for name in produced):
                raise ValueError('Generated map atlases differ; rerun importer after map generation')
        else:
            output.mkdir(parents=True, exist_ok=True)
            for name in sorted(produced):
                temp = output / ('.' + name + '.tmp')
                shutil.copyfile(staging / name, temp)
                os.replace(temp, output / name)
            for file in output.iterdir():
                if file.is_file() and file.name not in produced:
                    file.unlink()
        if args.prune:
            result = prune_originals(root, generated, output, images, args.backup_dir)
        print(json.dumps(dict(checked=args.check, internalConsistency=True, **result), sort_keys=True))


if __name__ == '__main__':
    main()
