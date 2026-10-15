#!/usr/bin/env python3
"""Read-only publication triage, not copyright clearance. JSON goes to stdout only."""
from __future__ import annotations
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse
import collections
import json
import os
from pathlib import Path
import re
import subprocess
import sys

LIMIT = 2 * 1024 * 1024
PRIVATE = re.compile(rb'(?:/workspace/|/home/|/Users/|/tmp/|[A-Za-z]:\\Users\\)')
LOCAL_PREFIXES = ('content/raw/', 'content/generated/', 'src/Game.Godot/Assets/Reference/',
                  '.play/', 'exports/', 'evidence/local-checkpoints/')
MEDIA = {'.png', '.jpg', '.jpeg', '.wav', '.ogg', '.mp3', '.mid', '.midi', '.pal', '.4bpp', '.pcm'}
BINARY = {'.gba', '.elf', '.sav', '.srm', '.ss0', '.ss1', '.state', '.zip', '.gz', '.7z', '.tar', '.pck'}


def classify(path, data=b'', mode='100644', skipped=False):
    """Return evidence categories only; no result means unclassified, never cleared."""
    p = path.lower()
    ext = Path(p).suffix
    result = []
    def add(code, reason):
        result.append({'category': code, 'reason': reason})
    if mode == '120000':
        add('symlink', 'Link target needs review; auditor does not follow it.')
    if mode == '160000':
        add('submodule', 'Separate repository and history are outside this audit.')
    if path.startswith(LOCAL_PREFIXES):
        add('local_only_boundary', 'Path belongs to a raw/generated/reference/runtime output boundary.')
    if ext in BINARY or data.startswith((b'\x7fELF', b'PK\x03\x04', b'\x1f\x8b')):
        add('opaque_binary_or_archive', 'ROM/build/save/archive candidate; archives are not expanded.')
    if ext in MEDIA or data.startswith((b'\x89PNG\r\n\x1a\n', b'RIFF', b'OggS', b'MThd')):
        add('media_or_screenshot', 'Image/audio/palette needs individual origin and distribution review.')
    if ext in {'.ttf', '.otf', '.ttc'} or data.startswith((b'ttcf', b'OTTO')):
        add('font_license_review', 'Distinguish source-game glyphs from independently licensed fonts.')
    if 'evidence/' in p or ext in {'.log', '.jsonl', '.stderr'}:
        add('evidence_review', 'Logs/traces may embed dialogue, saves, screenshots or local paths.')
    if any(x in p for x in ('fixture', 'golden', 'checkpoint', 'save')) and ext not in {'.csproj'}:
        add('fixture_provenance_review', 'Name indicates fixture/save/golden; synthetic origin must be established.')
    if data.startswith(b'version https://git-lfs.github.com/spec/v1\n'):
        add('lfs_pointer', 'Only the pointer was read; local/remote LFS payloads were not inspected.')
    if PRIVATE.search(data):
        add('private_path_marker', 'Text includes a machine-local absolute path; values are not echoed.')
    if re.search(rb'"(?:ScriptSource|TextSource|textSource|scriptSource|rawSource|base64)"\s*:', data):
        add('embedded_source_marker', 'Structured content may carry source text or encoded assets.')
    if re.search(rb'"(?:party|Party|playerName|PlayerName|saveVersion|SaveVersion)"\s*:', data):
        add('save_or_game_data_marker', 'Structured game/player state requires synthetic/private-data review.')
    if 'license' in p or 'copying' in p or 'third-party' in p:
        add('license_record_review', 'License text is evidence for its stated component, not global asset clearance.')
    if skipped:
        add('content_not_scanned', 'Blob exceeds byte cap; only path and size were reviewed.')
    if not result:
        add('unclassified_review', 'No heuristic matched. Authorship, copied excerpts and licensing remain unreviewed.')
    return result


def self_test():
    cases = [
        ('content/generated/a.json', b'{}', 'local_only_boundary'),
        ('plain.data', b'\x89PNG\r\n\x1a\n', 'media_or_screenshot'),
        ('image.png', b'', 'media_or_screenshot'),
        ('data.txt', b'\x7fELF', 'opaque_binary_or_archive'),
        ('asset', b'version https://git-lfs.github.com/spec/v1\n', 'lfs_pointer'),
        ('report.json', b'{"path":"/home/example/file"}', 'private_path_marker'),
        ('world.json', b'{"TextSource":"source"}', 'embedded_source_marker'),
        ('save.json', b'{"party":[]}', 'save_or_game_data_marker'),
        ('font', b'ttcf', 'font_license_review'),
        ('original.cs', b'class Example {}', 'unclassified_review'),
    ]
    for path, data, expected in cases:
        assert expected in {x['category'] for x in classify(path, data)}, (path, expected)
    assert classify('link', mode='120000')[0]['category'] == 'symlink'
    assert classify('module', mode='160000')[0]['category'] == 'submodule'
    assert classify('large', skipped=True)[0]['category'] == 'content_not_scanned'
    print(json.dumps({'selfTest': 'passed', 'cases': len(cases) + 3, 'filesystemWrites': False}))


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--repo', type=Path, default=_PROJECT_ROOT)
    ap.add_argument('--history', action='store_true', help='Also inspect locally reachable objects under --all; no fetch.')
    ap.add_argument('--max-bytes', type=int, default=LIMIT, help='Per-blob content cap; larger blobs remain unreviewed.')
    ap.add_argument('--self-test', action='store_true', help='Pure in-memory classification fixtures; no Git or filesystem writes.')
    args = ap.parse_args()
    if args.self_test:
        self_test(); return 0
    if args.max_bytes <= 0:
        ap.error('--max-bytes must be positive')
    env = dict(os.environ, GIT_OPTIONAL_LOCKS='0', GIT_NO_LAZY_FETCH='1', GIT_NO_REPLACE_OBJECTS='1', GIT_TERMINAL_PROMPT='0')
    base = ['git', '-c', 'core.fsmonitor=false', '-C', str(args.repo)]
    def git(*a):
        return subprocess.check_output(base + list(a), env=env)
    tracked = []
    for row in git('ls-files', '--stage', '-z').split(b'\0'):
        if not row: continue
        meta, name = row.split(b'\t', 1)
        mode, oid, stage = meta.decode().split()
        tracked.append((name.decode('utf-8', 'surrogateescape'), oid, mode, stage))
    objects = [(p, oid, mode, 'index:' + stage) for p, oid, mode, stage in tracked]
    if args.history:
        for row in git('rev-list', '--objects', '--all').splitlines():
            oid, _, name = row.partition(b' ')
            objects.append((name.decode('utf-8', 'surrogateescape') or '(unnamed object)', oid.decode(), '', 'history'))
    findings = []
    counts = collections.Counter()
    scanned_blobs = set()
    # --batch-check avoids reading large blobs. --batch uses no filters, textconv or external diff.
    check = subprocess.Popen(base + ['cat-file', '--batch-check'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, env=env)
    reader = subprocess.Popen(base + ['cat-file', '--batch'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, env=env)
    try:
        for path, oid, mode, scope in objects:
            if scope == 'history' and oid in scanned_blobs: continue
            if mode == '160000':
                reasons = classify(path, mode=mode)
                counts['submodule'] += 1
                findings.append({'path': path, 'object': oid, 'scope': scope, 'bytes': None, 'findings': reasons})
                continue
            check.stdin.write((oid + '\n').encode()); check.stdin.flush()
            header = check.stdout.readline().decode().strip().split()
            if len(header) != 3:
                raise RuntimeError('Missing local Git object; no fetching attempted: ' + oid)
            _, kind, size = header; size = int(size)
            if kind != 'blob':
                if mode == '160000':
                    reasons = classify(path, mode=mode)
                else: continue
            else:
                scanned_blobs.add(oid)
                data = b''
                if size <= args.max_bytes:
                    reader.stdin.write((oid + '\n').encode()); reader.stdin.flush()
                    actual = reader.stdout.readline().decode().split()
                    if len(actual) != 3 or actual[1] != 'blob' or int(actual[2]) != size:
                        raise RuntimeError('Unexpected Git blob response')
                    data = reader.stdout.read(size)
                    if len(data) != size or reader.stdout.read(1) != b'\n':
                        raise RuntimeError('Truncated Git blob')
                reasons = classify(path, data, mode, size > args.max_bytes)
            for reason in reasons: counts[reason['category']] += 1
            findings.append({'path': path, 'object': oid, 'scope': scope, 'bytes': size, 'findings': reasons})
    finally:
        for proc in (check, reader):
            proc.stdin.close(); proc.stdout.close(); proc.wait()
    untracked = git('ls-files', '--others', '--exclude-standard', '-z').split(b'\0')
    report = {
        'schemaVersion': 1, 'status': 'manual_review_required', 'publicReady': False,
        'head': git('rev-parse', 'HEAD').decode().strip(),
        'trackedIndexEntries': len(tracked), 'uniqueBlobsInspectedOrCapped': len(scanned_blobs),
        'historyRequested': args.history,
        'localReachableCommitCount': int(git('rev-list', '--all', '--count')) if args.history else None,
        'shallowRepository': git('rev-parse', '--is-shallow-repository').decode().strip() == 'true',
        'untrackedNonIgnoredCountNotInspected': sum(bool(x) for x in untracked),
        'categoryCounts': dict(sorted(counts.items())), 'entries': findings,
        'limits': [
            'Index bytes are scanned, not unstaged working-tree changes; untracked and ignored files are not scanned.',
            'History scans locally reachable --all objects only; reflogs, unreachable objects and remote-only refs are excluded.',
            'rev-list may show one representative path per object; all historical rename paths are not reconstructed.',
            'Git LFS payloads, submodules and archive contents require separate review; no network is used.',
            'Signatures, paths and content markers are triage, not authorship, secret detection or legal clearance.',
            'Reports expose filenames and object IDs. Keep raw reports private until reviewed.',
        ]}
    print(json.dumps(report, ensure_ascii=True, indent=2))
    return 2  # A scan is never a release authorization.


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print(json.dumps({'status': 'audit_incomplete', 'error': str(error)}), file=sys.stderr)
        sys.exit(1)
