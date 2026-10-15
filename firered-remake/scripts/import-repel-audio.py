#!/usr/bin/env python3
"""Copy only the reviewed SE_REPEL derivative. No render, download or upstream execution."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)
from raw_source_paths import raw_metadata_directory
import argparse
import csv
import io
import json
import os
from pathlib import Path
import re
import shutil
import struct
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
LOCK_BYTES = 3889
MAX_INPUT_BYTES = 2 * 1024 * 1024
PROFILE = 'firered-repel-audio-qoa48-v1'


def require(value, message):
    if not value:
        raise ValueError(message)


def safe_file(root, relative):
    require(isinstance(relative, str) and relative and '\\' not in relative and ':' not in relative
            and not relative.startswith('/') and all(p not in ('', '.', '..') for p in relative.split('/')),
            'Unsafe Repel path: ' + str(relative))
    current = Path(os.path.abspath(root))
    for item in [*reversed(current.parents), current]:
        require(not item.is_symlink(), 'Symlink in Repel root: ' + str(item))
    for part in relative.split('/'):
        current = current / part
        require(not current.is_symlink(), 'Symlink in Repel input: ' + relative)
    return current


def read_pinned(root, entry):
    return safe_file(root, entry['path']).read_bytes()


def validate_qoa(data):
    require(len(data) == 9984 and data[:4] == b'qoaf' and struct.unpack('>I', data[4:8])[0] == 24632,
            'Repel QOA identity/header differs')
    offset = 8
    frames = 0
    while offset < len(data):
        require(offset + 8 <= len(data), 'Truncated QOA header')
        channels = data[offset]
        rate = int.from_bytes(data[offset + 1:offset + 4], 'big')
        count, size = struct.unpack('>HH', data[offset + 4:offset + 8])
        require(channels == 1 and rate == 48000 and count == min(5120, 24632 - frames) and count > 0
                and size == 24 + 8 * ((count + 19) // 20) and offset + size <= len(data), 'Invalid QOA frame')
        offset += size
        frames += count
    require(offset == len(data) and frames == 24632, 'Incomplete QOA container')


def prepare(study, reference):
    lock_bytes = read_pinned(ROOT, {'path': 'content/repel-audio-source-lock.json'})
    lock = json.loads(lock_bytes)
    require(lock['profile'] == PROFILE and lock['schemaVersion'] == 1, 'Repel source profile differs')
    commit = lock['source']['commit']
    originals = {}
    for entry in lock['sourceFiles']:
        obj = commit + ':' + entry['path']
        size = int(subprocess.check_output(['git', '-C', str(reference), 'cat-file', '-s', obj]))
        data = subprocess.check_output(['git', '-C', str(reference), 'cat-file', 'blob', obj])
        originals[entry['path']] = data
    require(re.search(rb'^#define\s+SE_REPEL\s+41\s', originals['include/constants/songs.h'], re.M), 'Song ID differs')
    songs = re.findall(rb'^\s*song\s+(\w+),\s*(\d+),\s*(\d+)', originals['sound/song_table.inc'], re.M)
    require(songs[41] == (b'se_repel', b'1', b'1'), 'Song table entry differs')
    require(re.search(rb'^se_repel\.mid:\s+-E -R50 -G127 -V090 -P4\s*$',
                      originals['sound/songs/midi/midi.cfg'], re.M), 'MIDI compile options differ')
    inputs = {e['path']: read_pinned(study, e) for e in lock['studyFiles']}
    manifest = json.loads(inputs['manifest.json'])
    inventory = json.loads(inputs['inventory.json'])
    result = json.loads(inputs['results/SE_REPEL.json'])
    clip, = [c for c in manifest['clips'] if c['id'] == 'SE_REPEL']
    song, = [c for c in inventory['songs'] if c['symbol'] == 'SE_REPEL']
    require(manifest['sourceCommit'] == commit and inventory['source']['commit'] == commit,
            'Study source identity differs')
    require(all(clip[k] == v for k, v in result.items()) and clip['songId'] == 41
            and song['songId'] == 41 and song['sourcePath'] == lock['song']['sourcePath']
            and song['player'] == 1 and song['priority'] == 4 and song['reverb'] == 178
            and song['compiledTrackCount'] == 1 and song['mid2agbOptions'] == lock['song']['mid2agbOptions'],
            'Selected study entry differs')
    require(result['sourceFrames'] == result['logicalFrames'] == 24632 and result['sampleRate'] == 48000 and (result['sourceSequenceStoppedVBlank'] == 27) and (result['loopStart'] is None) and (result['loopEnd'] is None) and (not result['audibleOutputVerified']) and (result['status'] == 'converted'), 'Repel capture differs')
    rows = [{k: int(v) for k, v in r.items()} for r in csv.DictReader(io.StringIO(inputs['traces/SE_REPEL.csv'].decode()))]
    stopped = next(r['frame'] for r in rows if r['clock'] > 0 and r['status'] & 0x80000000)
    require(len(rows) == 324 and rows[-1]['sample_end'] == 260379 and stopped == 27, 'Source trace stop/capture differs')
    validate_qoa(inputs['assets/SE_REPEL.qoa'])
    metadata = dict(id='SE_REPEL', sourceCommit=commit, sourceSymbol='SE_REPEL', sourcePath=lock['song']['sourcePath'],
         sourcePlayer=1, priority=4, sourceCounter=None,
        renderer=lock['renderer']['name'],  encoder=lock['delivery']['encoder'],
         sourceSampleRate=48000, sourceChannels=1, sourceFrameCount=24632,
        sourceSequenceStoppedVBlank=27, frameRateNumerator=16777216, frameRateDenominator=280896,
        sampleRate=48000, channels=1, logicalFrameCount=24632, loopStartFrame=None, loopEndFrame=None,
        path='assets/se_repel.qoa',  sourceEnergyAbove12kHzPercent=result['sourceEnergyAbove12kHzPercent'],
        resampled=False, normalized=False, losslessToSourceChannel=False, audibleOutputVerified=False,
        originalScriptWaitVerified=False)
    runtime = dict(schemaVersion=1, profile=PROFILE,  metadata=metadata,
        limitations=lock['limitations'])
    runtime_bytes = (json.dumps(runtime, indent=2) + '\n').encode()
    generated = {'manifest.json': runtime_bytes, 'source-lock.json': lock_bytes,
                 'assets/se_repel.qoa': inputs['assets/SE_REPEL.qoa']}
    # Raw audit inputs never form part of the runtime package. Only one encoded asset is imported.
    raw = {'source/' + p: b for p, b in originals.items()}
    raw.update({'study/' + p: b for p, b in inputs.items() if p != 'assets/SE_REPEL.qoa'})
    raw['source-lock.json'] = lock_bytes
    return generated, raw


def install(directory, files):
    directory = Path(os.path.abspath(directory))
    for item in [*reversed(directory.parents), directory]:
        require(not item.is_symlink(), 'Symlink output directory')
    if directory.exists():
        actual = {p.relative_to(directory).as_posix() for p in directory.rglob('*') if p.is_file() or p.is_symlink()}
        require(actual == set(files), 'Existing output inventory differs: ' + str(directory))
        return
    directory.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix='.' + directory.name + '-', dir=directory.parent))
    try:
        for name, data in files.items():
            path = staging / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(data)
        staging.rename(directory)
    finally:
        if staging.exists():
            shutil.rmtree(staging)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--study-dir', type=Path, required=True)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path, default=ROOT / 'content/generated/repel-audio')
    parser.add_argument('--raw-output', type=Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--receipt', type=Path)
    args = parser.parse_args()
    generated, raw = prepare(args.study_dir, args.reference)
    install(args.output, generated)
    sources={name.removeprefix('source/'):data for name,data in raw.items() if name.startswith('source/')}
    for name,data in sources.items():
        destination=safe_file(args.raw_output,name)
        destination.parent.mkdir(parents=True,exist_ok=True)
        destination.write_bytes(data)
    install(raw_metadata_directory(args.raw_output, 'repel-audio'),
            {name:data for name,data in raw.items() if not name.startswith('source/')})
    receipt = dict(status='passed', profile=PROFILE, assetCount=1, encodedBytes=9984,
        runtimeFiles=len(generated), runtimeBytes=sum(map(len, generated.values())),
         manifestBytes=len(generated['manifest.json']),
         sourceFiles=4, studyInputs=7,
        rerendered=False, reencoded=False, downloaded=False, upstreamExecuted=False, audibleOutputVerified=False)
    if args.receipt:
        args.receipt.parent.mkdir(parents=True, exist_ok=True)
        args.receipt.write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
