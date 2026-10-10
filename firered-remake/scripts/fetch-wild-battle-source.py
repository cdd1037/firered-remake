#!/usr/bin/env python3
"""Verify/materialize bounded ordinary wild-battle source. Read fixed Git blobs, never upstream code."""
import argparse
import hashlib
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
LOCK_SHA256 = '44fc538032370c518a48921a06091bb547e57e5b7c7d381a82a57ad64211f482'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(root, name):
    relative = pathlib.PurePosixPath(name)
    if relative.is_absolute() or '..' in relative.parts or not relative.parts:
        raise ValueError('Unsafe source path')
    target = root / relative
    for path in [root, *root.parents, target, *target.parents]:
        if path.is_symlink():
            raise ValueError('Symlink source/output path: ' + str(path))
    return target


def fetch(output, reference_repo=None, tree_path=None, lock_path=None):
    if reference_repo is not None:
        source_root, output_root = reference_repo.resolve(), output.resolve()
        if output_root == source_root or source_root in output_root.parents or output_root in source_root.parents:
            raise ValueError('Output must not overlap the reference repository')
    lock_bytes = (lock_path or ROOT / 'scripts/wild-battle-source.lock.json').read_bytes()
    if sha(lock_bytes) != LOCK_SHA256:
        raise ValueError('Wild battle source lock differs from reviewed pin')
    lock = json.loads(lock_bytes)
    tree_bytes = (tree_path or ROOT / 'content/raw/starter-audit/tree.json').read_bytes()
    if sha(tree_bytes) != lock['treeSnapshotSha256']:
        raise ValueError('Source tree snapshot differs')
    tree = json.loads(tree_bytes)
    if tree['sha'] != lock['commit'] or tree.get('truncated', True):
        raise ValueError('Incomplete/wrong source tree')
    entries = {entry['path']: entry for entry in tree['tree']}
    git_tree = None
    if reference_repo is not None:
        def git(*args):
            return subprocess.check_output(['git', '-C', str(reference_repo), *args])
        if git('rev-parse', lock['commit'] + '^{tree}').decode().strip() != lock['treeSha1']:
            raise ValueError('Reference commit tree differs')
        git_tree = {}
        for row in git('ls-tree', '-rz', lock['commit']).split(b'\0'):
            if row:
                metadata, name = row.split(b'\t')
                git_tree[name.decode()] = tuple(metadata.decode().split())
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        target = safe_path(output, path)
        original = entries[path]
        if (original['type'], original['sha'], original['size']) != ('blob', entry['gitBlobSha1'], entry['byteLength']):
            raise ValueError('Source lock disagrees with fixed source tree: ' + path)
        if git_tree is not None:
            if git_tree.get(path) != ('100644', 'blob', entry['gitBlobSha1']):
                raise ValueError('Not the locked regular Git blob: ' + path)
            data = git('cat-file', 'blob', entry['gitBlobSha1'])
        else:
            data = target.read_bytes()
        blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        if (len(data), blob, sha(data)) != (entry['byteLength'], entry['gitBlobSha1'], entry['sha256']):
            raise ValueError('Pinned source hash mismatch: ' + path)
        verified[path] = data
    # Validate every byte and destination before writing any file.
    safe_path(output, 'manifest.json')
    for path, data in verified.items():
        target = output / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
    manifest = dict(schemaVersion=1, commit=lock['commit'], inputCount=len(verified),
                    lockSha256=LOCK_SHA256, upstreamCodeExecuted=False, romUsed=False)
    (output / 'manifest.json').write_text(json.dumps(manifest, sort_keys=True) + '\n', encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/raw/wild-battle')
    parser.add_argument('--reference-repo', type=pathlib.Path, help='Read fixed Git blobs, never working-tree files.')
    parser.add_argument('--tree', type=pathlib.Path)
    parser.add_argument('--lock', type=pathlib.Path)
    args = parser.parse_args()
    print(json.dumps(fetch(args.output, args.reference_repo, args.tree, args.lock), sort_keys=True))
