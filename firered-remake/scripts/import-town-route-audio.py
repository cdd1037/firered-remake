#!/usr/bin/env python3
"""Authenticate and copy five completed study derivatives; no rendering or upstream execution."""
import argparse
import csv
import hashlib
import io
import json
import os
from pathlib import Path
import re
import shutil
import stat
import struct
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PROFILE = 'firered-town-route-audio-q0-qoa-24-48-v1'
LOCK_BYTES = 7369
LOCK_SHA256 = '7c25b020c9da9a3fce879b624f64c3d880ce8b0ee70c9c1c75bdddbbec434d4a'
MAX_INPUT_BYTES = 2 * 1024 * 1024
IDS = ('MUS_DEX_RATING', 'MUS_ROUTE3', 'MUS_VICTORY_ROAD', 'SE_M_SPIT_UP', 'SE_M_HYPER_BEAM2')


def require(value, message):
    if not value:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def safe_file(root, relative):
    require(isinstance(relative, str) and relative and '\\' not in relative and ':' not in relative
            and not relative.startswith('/') and all(p not in ('', '.', '..') for p in relative.split('/')),
            'Unsafe audio path: ' + str(relative))
    current = Path(os.path.abspath(root))
    for item in [*reversed(current.parents), current]:
        require(not item.is_symlink(), 'Symlink in audio root: ' + str(item))
    for part in relative.split('/'):
        current = current / part
        require(not current.is_symlink(), 'Symlink in audio input: ' + relative)
    return current


def read_pinned(root, entry):
    size = entry['byteLength']
    require(type(size) is int and 0 < size <= MAX_INPUT_BYTES, 'Invalid input size bound')
    path = safe_file(root, entry['path'])
    descriptor = os.open(path, os.O_RDONLY | getattr(os, 'O_NOFOLLOW', 0) | getattr(os, 'O_NONBLOCK', 0))
    with os.fdopen(descriptor, 'rb') as stream:
        before = os.fstat(stream.fileno())
        require(stat.S_ISREG(before.st_mode) and before.st_size == size, 'Pinned size differs: ' + entry['path'])
        data = stream.read(size + 1)
        after = os.fstat(stream.fileno())
        require(len(data) == size and after.st_size == size, 'Input changed during bounded read')
    require(digest(data) == entry['sha256'], 'Pinned digest differs: ' + entry['path'])
    return data


def validate_encoded(data, metadata):
    m = metadata
    require(len(data) == m['byteLength'] and digest(data) == m['encodedSha256'], 'Encoded identity differs')
    if m['codec'] == 'qoa':
        require(len(data) >= 32 and data[:4] == b'qoaf'
                and struct.unpack('>I', data[4:8])[0] == m['logicalFrameCount'], 'Invalid QOA header')
        offset, frames = 8, 0
        while offset < len(data):
            require(offset + 8 <= len(data), 'Truncated QOA frame header')
            channels = data[offset]
            rate = int.from_bytes(data[offset + 1:offset + 4], 'big')
            count, size = struct.unpack('>HH', data[offset + 4:offset + 8])
            require(channels == 1 and rate == m['sampleRate']
                    and count == min(5120, m['logicalFrameCount'] - frames) and count > 0
                    and size == 24 + 8 * ((count + 19) // 20) and offset + size <= len(data), 'Invalid QOA frame')
            offset += size
            frames += count
        require(offset == len(data) and frames == m['logicalFrameCount'], 'Incomplete QOA container')
        return
    require(m['codec'] == 'vorbis' and len(data) >= 58 and data[:6] == b'OggS\0\2'
            and data[26:35] == b'\x01\x1e\x01vorbis' and data[35:39] == bytes(4)
            and data[39] == 1 and struct.unpack('<I', data[40:44])[0] == m['sampleRate'], 'Invalid Vorbis header')
    offset, eos, granule = 0, False, -1
    while offset < len(data):
        require(not eos and len(data) - offset >= 27 and data[offset:offset + 5] == b'OggS\0', 'Invalid Ogg page')
        count = data[offset + 26]
        require(len(data) - offset >= 27 + count, 'Truncated Ogg lacing')
        size = 27 + count + sum(data[offset + 27:offset + 27 + count])
        require(len(data) - offset >= size, 'Truncated Ogg payload')
        granule = struct.unpack('<q', data[offset + 6:offset + 14])[0]
        eos = bool(data[offset + 5] & 4)
        offset += size
    require(eos and granule == m['logicalFrameCount'], 'Ogg terminal granule differs')


def prepare(study, reference):
    lock_bytes = read_pinned(ROOT, dict(path='content/town-route-audio-source-lock.json',
                                      byteLength=LOCK_BYTES, sha256=LOCK_SHA256))
    lock = json.loads(lock_bytes)
    require(lock['profile'] == PROFILE and lock['schemaVersion'] == 1 and tuple(lock['songIds']) == IDS,
            'Source profile differs')
    commit = lock['source']['commit']
    originals = {}
    for entry in lock['sourceFiles']:
        require(type(entry['byteLength']) is int and 0 < entry['byteLength'] <= MAX_INPUT_BYTES, 'Invalid source size bound')
        obj = commit + ':' + entry['path']
        size = int(subprocess.check_output(['git', '-C', str(reference), 'cat-file', '-s', obj]))
        require(size == entry['byteLength'], 'Source Git object size differs')
        data = subprocess.check_output(['git', '-C', str(reference), 'cat-file', 'blob', obj])
        require(len(data) == size and digest(data) == entry['sha256']
                and hashlib.sha1(f'blob {size}\0'.encode() + data).hexdigest() == entry['gitBlobSha1'],
                'Source Git object differs: ' + entry['path'])
        originals[entry['path']] = data
    inputs = {e['path']: read_pinned(study, e) for e in lock['studyFiles']}
    manifest, inventory, summary = [json.loads(inputs[p]) for p in ['manifest.json', 'inventory.json', 'RESULT.json']]
    require(manifest['sourceCommit'] == inventory['source']['commit'] == commit
            and manifest['romSha1'] == inventory['source']['romSha1'] == lock['source']['romSha1']
            and summary['manifestSha256'] == digest(inputs['manifest.json']) and summary['allAssetHashesVerified']
            and summary['status'] == 'complete-basic-library' and not summary['audibleOutputVerified'], 'Study identity differs')
    require(re.search(rb'\[FANFARE_DEX_EVAL\]\s*=\s*\{\s*MUS_DEX_RATING,\s*196\s*\}', originals['src/sound.c']),
            'Source fanfare counter differs')
    require(b'PlaySE(SE_M_SPIT_UP);' in originals['src/region_map.c']
            and b'PlaySE(SE_M_HYPER_BEAM2);' in originals['src/region_map.c'], 'Town Map source sound identities differ')
    require(json.loads(originals['data/maps/Route22/map.json'])['music'] == 'MUS_ROUTE3'
            and json.loads(originals['data/maps/Route23/map.json'])['music'] == 'MUS_VICTORY_ROAD', 'Source route music differs')
    song_table = re.findall(rb'^\s*song\s+(\w+),\s*(\d+),\s*(\d+)', originals['sound/song_table.inc'], re.M)
    clips, generated = [], {'source-lock.json': lock_bytes}
    for identity in IDS:
        clip, = [c for c in manifest['clips'] if c['id'] == identity]
        song, = [c for c in inventory['songs'] if c['symbol'] == identity]
        result = json.loads(inputs[f'results/{identity}.json'])
        require(all(clip[k] == v for k, v in result.items()) and clip['songId'] == song['songId']
                and song['constantAliases'] == [identity] and clip['status'] == 'converted'
                and not clip['audibleOutputVerified'] and not clip['seamlessLoopVerified'], 'Selected study entry differs')
        require(re.search(rb'^#define\s+' + identity.encode() + rb'\s+' + str(song['songId']).encode() + rb'\s',
                          originals['include/constants/songs.h'], re.M), 'Song constant differs')
        require(song_table[song['songId']] == (song['asmSymbol'].encode(), str(song['player']).encode(),
                                             str(song['musicStatusPlayer']).encode()), 'Song table entry differs')
        require(re.search(rb'^' + re.escape(Path(song['sourcePath']).name.encode()) + rb':\s+'
                          + re.escape(song['mid2agbOptions'].encode()) + rb'\s*$', originals['sound/songs/midi/midi.cfg'], re.M),
                'MIDI compile options differ')
        counter = 196 if identity == 'MUS_DEX_RATING' else None
        require(clip['sourceFanfareCounterVBlanks'] == song['fanfareCounterFrames'] == counter, 'Fanfare counter differs')
        trace = inputs[f'traces/{identity}.csv']
        require(digest(trace) == clip['traceSha256'], 'Source trace digest differs')
        rows = [{k: int(v) for k, v in row.items()} for row in csv.DictReader(io.StringIO(trace.decode()))]
        require(len(rows) == clip['capture']['sourceVBlanks'] and rows[-1]['sample_end'] == clip['capturedFrames']
                and clip['capture']['sampleRate'] == 48000 and clip['capture']['sourceStereo'] == 0,
                'Source capture differs')
        stopped = next((r['frame'] for r in rows if r['clock'] > 0 and r['status'] & 0x80000000), None)
        require(stopped == clip['sourceSequenceStoppedVBlank'], 'Source sequence stop differs')
        source_loop = clip.get('sourceLoop')
        if source_loop:
            a = next(r for r in rows if r['clock'] >= source_loop['selectedClockBegin'] + 1)
            b = next(r for r in rows if r['clock'] >= source_loop['selectedClockEnd'] + 1)
            require(a['sample_begin'] == clip['sourceLoopStartFrame'] and b['sample_begin'] == clip['sourceLoopEndFrame']
                    and a['frame'] == source_loop['renderVBlankBegin'] and b['frame'] == source_loop['renderVBlankEnd'],
                    'Source loop trace endpoints differ')
        rate = clip['sampleRate']
        quantize = lambda n: None if n is None else (n * rate + 24000) // 48000
        require(rate == (24000 if clip['sourceEnergyAbove12kHzPercent'] <= 1 else 48000)
                and clip['logicalFrames'] == quantize(clip['sourceFrames'])
                and clip['loopStart'] == quantize(clip.get('sourceLoopStartFrame'))
                and clip['loopEnd'] == quantize(clip.get('sourceLoopEndFrame')), 'Target-frame quantization differs')
        music = identity.startswith('MUS_')
        require(clip['codec'] == ('vorbis-q0' if music else 'qoa'), 'Codec differs')
        m = dict(id=identity, songId=song['songId'], sourceCommit=commit, sourceSymbol=identity,
            sourcePath=song['sourcePath'], sourceMidiSha256=digest(originals[song['sourcePath']]),
            sourcePlayer=song['player'], priority=song['priority'], sourceCounter=counter,
            renderer=lock['renderer']['name'], rendererSourceSha256=lock['renderer']['sourceSha256'],
            rendererBinarySha256=lock['renderer']['binarySha256'],
            encoder='ffmpeg/libvorbis q0 (study run_study.py)' if music else 'Godot 4.6.3-stable official AudioStreamWAV QOA',
            codec='vorbis' if music else 'qoa', sourcePcmSha256=clip['sourcePcmSha256'],
            sourceSampleRate=48000, sourceChannels=1, sourceFrameCount=clip['sourceFrames'],
            sourceSequenceStoppedVBlank=stopped, sourceLoopStartFrame=clip.get('sourceLoopStartFrame'),
            sourceLoopEndFrame=clip.get('sourceLoopEndFrame'),
            sourceLoopVBlankBegin=source_loop['renderVBlankBegin'] if source_loop else None,
            sourceLoopVBlankEnd=source_loop['renderVBlankEnd'] if source_loop else None,
            frameRateNumerator=16777216, frameRateDenominator=280896, sampleRate=rate, channels=1,
            logicalFrameCount=clip['logicalFrames'], loopStartFrame=clip['loopStart'], loopEndFrame=clip['loopEnd'],
            path='assets/' + identity.lower() + ('.ogg' if music else '.qoa'),
            encodedSha256=clip['sha256'], byteLength=clip['bytes'], decodedPcmSha256=None,
            sourceEnergyAbove12kHzPercent=clip['sourceEnergyAbove12kHzPercent'], resampled=rate != 48000,
            normalized=False, losslessToSourceChannel=False, audibleOutputVerified=False,
            sampleExactLoopSeamVerified=False, originalScriptWaitVerified=False)
        data = inputs[clip['path']]
        validate_encoded(data, m)
        generated[m['path']] = data
        clips.append(m)
    runtime = dict(schemaVersion=1, profile=PROFILE, sourceLockSha256=LOCK_SHA256,
        studyManifestSha256=digest(inputs['manifest.json']), studyInventorySha256=digest(inputs['inventory.json']),
        romSha1=manifest['romSha1'], audioBytes=sum(c['byteLength'] for c in clips), clips=clips, limitations=lock['limitations'])
    generated['manifest.json'] = (json.dumps(runtime, indent=2) + '\n').encode()
    return generated


def install(directory, files):
    directory = Path(os.path.abspath(directory))
    for item in [*reversed(directory.parents), directory]:
        require(not item.is_symlink(), 'Symlink output directory')
    if directory.exists():
        actual = {p.relative_to(directory).as_posix() for p in directory.rglob('*') if p.is_file() or p.is_symlink()}
        require(actual == set(files), 'Existing output inventory differs: ' + str(directory))
        for name, data in files.items():
            read_pinned(directory, dict(path=name, byteLength=len(data), sha256=digest(data)))
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
    parser.add_argument('--output', type=Path, default=ROOT / 'content/generated/town-route-audio')
    parser.add_argument('--receipt', type=Path)
    args = parser.parse_args()
    generated = prepare(args.study_dir, args.reference)
    install(args.output, generated)
    receipt = dict(status='passed', profile=PROFILE, assetCount=5,
        encodedBytes=sum(len(v) for k, v in generated.items() if k.startswith('assets/')),
        runtimeFiles=len(generated), runtimeBytes=sum(map(len, generated.values())),
        manifestSha256=digest(generated['manifest.json']), manifestBytes=len(generated['manifest.json']),
        sourceLockSha256=LOCK_SHA256, sourceFiles=12, studyInputs=20, rerendered=False, reencoded=False,
        downloaded=False, upstreamExecuted=False, audibleOutputVerified=False, sampleExactLoopSeamVerified=False)
    if args.receipt:
        args.receipt.parent.mkdir(parents=True, exist_ok=True)
        args.receipt.write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
