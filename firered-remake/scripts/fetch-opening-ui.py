#!/usr/bin/env python3
"""Fetch pinned source text for the bounded typed script interpreter; no upstream execution."""
import pathlib,json,hashlib,urllib.request
ROOT=pathlib.Path(__file__).resolve().parent.parent
lock=json.loads((ROOT/'scripts/fetch-opening-ui.lock.json').read_text(encoding='utf-8'))
assert lock['commit']==json.loads((ROOT/'content/source-lock.json').read_text(encoding='utf-8'))['source']['sourceCommit']
for entry in lock['files']:
 path=pathlib.PurePosixPath(entry['path'])
 if path.is_absolute() or '..' in path.parts:raise ValueError('Unsafe source path')
 dest=ROOT/'content/raw/opening-ui'/path
 data=dest.read_bytes() if dest.exists() else urllib.request.urlopen(f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{path}',timeout=60).read()
 blob=hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()
 if len(data)!=entry['byteLength'] or blob!=entry['gitBlobSha1'] or hashlib.sha256(data).hexdigest()!=entry['sha256']:raise ValueError('Pinned source hash mismatch: '+str(path))
 dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
print(f'Verified {len(lock["files"])} opening PC menu source files; no upstream scripts executed.')
