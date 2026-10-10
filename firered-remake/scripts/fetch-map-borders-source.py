#!/usr/bin/env python3
"""Read four pinned field rendering files as data. Never execute upstream programs."""
import argparse
import hashlib
import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent.parent
LOCK_SHA256 = 'd065353dc19a1efb4d867ba08f5d855375bdfb2d493c4768a90c4574ee49e33e'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def verify(data, entry):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
    if len(data) != entry['byteLength'] or blob != entry['gitBlobSha1'] or sha(data) != entry['sha256']:
        raise ValueError('Pinned source hash mismatch: ' + entry['path'])


def fetch(lock_path, destination, cache=None, tree_path=None):
    data = lock_path.read_bytes()
    if sha(data) != LOCK_SHA256:
        raise ValueError('Map-border source lock differs from reviewed pin')
    lock = json.loads(data)
    tree = None
    if tree_path is not None:
        data = tree_path.read_bytes()
        if sha(data) != lock['treeSnapshotSha256']:
            raise ValueError('Source tree snapshot differs')
        document = json.loads(data)
        if document['sha'] != lock['commit'] or document.get('truncated', True):
            raise ValueError('Incomplete or wrong source tree')
        tree = {e['path']: e for e in document['tree']}
    if cache is not None and tree is None:
        raise ValueError('--cache requires --tree to verify reference checkout bytes before copying')
    for entry in lock['files']:
        path = pathlib.PurePosixPath(entry['path'])
        if path.is_absolute() or '..' in path.parts:
            raise ValueError('Unsafe source path')
        target = destination / path
        if cache is not None:
            original = tree[entry['path']]
            if (original['type'], original['sha'], original['size']) != ('blob', entry['gitBlobSha1'], entry['byteLength']):
                raise ValueError('Source lock disagrees with fixed Git tree')
            data = (cache / path).read_bytes()
        elif target.is_file():
            data = target.read_bytes()
        else:
            url = f'https://raw.githubusercontent.com/{lock["repository"]}/{lock["commit"]}/{path}'
            data = urllib.request.urlopen(url, timeout=60).read()
        verify(data, entry)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    print(f'Verified {len(lock["files"])} pinned map-border source files; no upstream code executed.')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/map-borders-source.lock.json')
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/raw/map-borders')
    parser.add_argument('--cache', type=pathlib.Path)
    parser.add_argument('--tree', type=pathlib.Path)
    args = parser.parse_args()
    fetch(args.lock, args.output, args.cache, args.tree)


if __name__ == '__main__':
    main()
