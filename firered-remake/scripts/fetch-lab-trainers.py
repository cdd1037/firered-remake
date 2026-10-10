#!/usr/bin/env python3
"""Fetch the bounded, pinned trainer research texts. No ROM or upstream execution."""
import hashlib
import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
lock = json.loads((ROOT / 'scripts/lab-trainers.lock.json').read_text(encoding='utf-8'))
if (lock['schemaVersion'] != 1 or lock['commit'] != COMMIT or
        lock['repository'] != 'pret/pokefirered' or lock['completeSourceRoster'] is not False or
        lock['commit'] != json.loads((ROOT / 'content/source-lock.json').read_text(encoding='utf-8'))['source']['sourceCommit']):
    raise ValueError('Unexpected trainer source identity or scope')
seen = set()
for entry in lock['files']:
    path = pathlib.PurePosixPath(entry['path'])
    if path.is_absolute() or '..' in path.parts or str(path) in seen:
        raise ValueError('Unsafe or duplicate source path')
    seen.add(str(path))
    dest = ROOT / 'content/raw/lab-trainers' / path
    data = dest.read_bytes() if dest.exists() else urllib.request.urlopen(
        f'https://raw.githubusercontent.com/{lock["repository"]}/{COMMIT}/{path}', timeout=60).read()
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode('utf-8') + b'\0' + data).hexdigest()
    if (len(data) != entry['byteLength'] or blob != entry['gitBlobSha1'] or
            hashlib.sha256(data).hexdigest() != entry['sha256']):
        raise ValueError('Source hash mismatch: ' + str(path))
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_bytes(data)
if not seen:
    raise ValueError('Empty source manifest')
print(f'Verified {len(seen)} pinned source texts for three lab trainer parties; no upstream execution.')
