#!/usr/bin/env python3
"""Download only locked text inputs. Never execute source code or acquire a ROM."""
import hashlib,json,pathlib,urllib.request
ROOT=pathlib.Path(__file__).resolve().parent.parent
lock=json.loads((ROOT/'scripts/fetch-pokemon.lock.json').read_text(encoding='utf-8'))
if lock['commit']!=json.loads((ROOT/'content/source-lock.json').read_text(encoding='utf-8'))['source']['sourceCommit']:
    raise ValueError('Pokemon source commit must match project source lock')
for entry in lock['files']:
    path=pathlib.PurePosixPath(entry['path'])
    if path.is_absolute() or '..' in path.parts: raise ValueError('Unsafe input path')
    dest=ROOT/'content/raw/pokefirered/037335f4'/path
    data=dest.read_bytes() if dest.exists() else urllib.request.urlopen(f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{path}',timeout=60).read()
    dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
print(f'Verified {len(lock["files"])} pinned Pokemon source texts; no upstream execution.')
