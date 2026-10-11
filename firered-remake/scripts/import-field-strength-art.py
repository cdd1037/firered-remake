#!/usr/bin/env python3
"""Rebuild private Strength dust from authenticated git blobs; executes no reference scripts."""
import argparse, hashlib, io, json, pathlib, subprocess
from PIL import Image

args = argparse.ArgumentParser()
args.add_argument('--reference-root', required=True, type=pathlib.Path)
args.add_argument('--check', action='store_true')
options = args.parse_args()
root = pathlib.Path(__file__).resolve().parents[1]
lock = json.loads((root / 'scripts/field-strength-art.lock.json').read_text())
commit = lock['sourceCommit']
tree = subprocess.check_output(['git', 'rev-parse', commit + '^{tree}'], cwd=options.reference_root, text=True).strip()
if tree != lock['sourceTree']:
    raise SystemExit('Source tree differs')
blobs = {}
for row in lock['files']:
    data = subprocess.check_output(['git', 'show', commit + ':' + row['path']], cwd=options.reference_root)
    git_hash = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
    if len(data) != row['byteLength'] or hashlib.sha256(data).hexdigest() != row['sha256'] or git_hash != row['gitBlobSha1']:
        raise SystemExit('Source differs: ' + row['path'])
    blobs[row['path']] = data
image = Image.open(io.BytesIO(blobs['graphics/field_effects/pics/ground_impact_dust.png']))
if image.mode != 'P' or image.size != (16, 24):
    raise SystemExit('Dust source dimensions or indexed palette differ')
palette = [tuple(map(int, line.split())) for line in blobs['graphics/field_effects/palettes/general_0.pal'].decode().splitlines()[3:]]
if len(palette) != 16:
    raise SystemExit('Dust palette differs')
palette = [tuple(((c >> 3) << 3) | ((c >> 3) >> 2) for c in rgb) for rgb in palette]
rgba = bytes(c for index in image.getdata() for c in ((*palette[index], 255) if index else (0, 0, 0, 0)))
output = lock['output']
if len(rgba) != output['byteLength'] or hashlib.sha256(rgba).hexdigest() != output['sha256']:
    raise SystemExit('Dust output differs')
path = root / output['path']
if options.check:
    if not path.is_file() or path.read_bytes() != rgba:
        raise SystemExit('Stored dust output differs')
else:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(rgba)
print('Verified 8 pinned source blobs and 1536 source RGBA bytes')
