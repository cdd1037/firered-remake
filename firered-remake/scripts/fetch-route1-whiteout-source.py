#!/usr/bin/env python3
"""Verify/materialize the bounded Pallet whiteout source; never execute upstream code."""
import argparse
import hashlib
import json
import pathlib
import subprocess

ROOT = pathlib.Path(__file__).resolve().parent.parent
LOCK_SHA256 = '28073ff8287489a2508cb0ca2ce695f469326b0ac040505f6f8cc55dda6a16d6'
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def safe_path(root, name):
    relative = pathlib.PurePosixPath(name)
    if relative.is_absolute() or '..' in relative.parts or not relative.parts or '\\' in name:
        raise ValueError('Unsafe source path: ' + name)
    target = root / relative
    for path in [root, *root.parents, target, *target.parents]:
        if path.is_symlink():
            raise ValueError('Symlink source/output path: ' + str(path))
    return target


def verify_data(entry, data):
    blob = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
    if (len(data), blob, sha(data)) != (entry['byteLength'], entry['gitBlobSha1'], entry['sha256']):
        raise ValueError('Pinned source hash mismatch: ' + entry['path'])


def fetch(output, reference_repo=None, tree_path=None, lock_path=None):
    if reference_repo is not None:
        source_root, output_root = reference_repo.resolve(), output.resolve()
        if output_root == source_root or source_root in output_root.parents or output_root in source_root.parents:
            raise ValueError('Output must not overlap the reference repository')
    lock_bytes = (lock_path or ROOT / 'scripts/route1-whiteout-source.lock.json').read_bytes()
    if sha(lock_bytes) != LOCK_SHA256:
        raise ValueError('Whiteout source lock differs from reviewed pin')
    lock = json.loads(lock_bytes)
    if lock['schemaVersion'] != 1 or lock['repository'] != 'pret/pokefirered' or lock['commit'] != COMMIT:
        raise ValueError('Unsupported whiteout source identity')
    tree_bytes = (tree_path or ROOT / 'content/raw/starter-audit/tree.json').read_bytes()
    if sha(tree_bytes) != lock['treeSnapshotSha256']:
        raise ValueError('Source tree snapshot differs')
    tree = json.loads(tree_bytes)
    if tree['sha'] != COMMIT or tree.get('truncated', True):
        raise ValueError('Incomplete/wrong source tree')
    entries = {entry['path']: entry for entry in tree['tree']}
    git_tree = None
    if reference_repo is not None:
        def git(*args):
            return subprocess.check_output(['git', '-C', str(reference_repo), *args])
        if git('rev-parse', COMMIT + '^{tree}').decode().strip() != lock['treeSha1']:
            raise ValueError('Reference commit tree differs')
        git_tree = {}
        for row in git('ls-tree', '-rz', COMMIT).split(b'\0'):
            if row:
                metadata, name = row.split(b'\t')
                git_tree[name.decode()] = tuple(metadata.decode().split())
    verified = {}
    for entry in lock['files']:
        path = entry['path']
        if path in verified:
            raise ValueError('Duplicate locked source path: ' + path)
        target = safe_path(output, path)
        original = entries[path]
        if (original['type'], original['sha'], original['size']) != ('blob', entry['gitBlobSha1'], entry['byteLength']):
            raise ValueError('Source lock disagrees with fixed source tree: ' + path)
        if git_tree is not None:
            if git_tree.get(path) != ('100644', 'blob', entry['gitBlobSha1']):
                raise ValueError('Not the locked regular Git blob: ' + path)
            data = git('show', COMMIT + ':' + path)
        else:
            data = target.read_bytes()
        verify_data(entry, data)
        verified[path] = data
    if not verified:
        raise ValueError('Empty whiteout source lock')
    manifest = dict(schemaVersion=1, commit=COMMIT, inputCount=len(verified),
                    lockSha256=LOCK_SHA256, upstreamCodeExecuted=False, romUsed=False)
    # All destinations and source bytes must verify before any materialization.
    manifest_path = safe_path(output, 'manifest.json')
    if reference_repo is not None:
        for path, data in verified.items():
            target = output / path
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        manifest_path.write_text(json.dumps(manifest, sort_keys=True) + '\n', encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=pathlib.Path, default=ROOT / 'content/raw/route1-whiteout')
    parser.add_argument('--reference-repo', type=pathlib.Path, help='Export fixed git-show blobs; never read the working tree.')
    parser.add_argument('--tree', type=pathlib.Path)
    parser.add_argument('--lock', type=pathlib.Path)
    args = parser.parse_args()
    print(json.dumps(fetch(args.output, args.reference_repo, args.tree, args.lock), sort_keys=True))
