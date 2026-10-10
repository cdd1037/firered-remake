#!/usr/bin/env python3
"""Authenticate and copy only three completed normal-cry derivatives; never render or play raw samples."""
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
PROFILE = 'firered-route22-normal-cry-qoa24-v1'
LOCK_BYTES = 6560
LOCK_SHA256 = '963f4ba1f7d2477d2777996685751667cd8c8be0bfa1886f14e4a3412eebdc77'
MAX_INPUT_BYTES = 2 * 1024 * 1024
IDS = ('CRY_SPEAROW_NORMAL', 'CRY_FEAROW_NORMAL', 'CRY_MANKEY_NORMAL')


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


def validate_container(data, metadata):
    m = metadata
    require(32 <= len(data) <= 16 * 1024 and data[:4] == b'qoaf'
            and struct.unpack('>I', data[4:8])[0] == m['logicalFrameCount'], 'Invalid QOA header')
    offset, frames = 8, 0
    while offset < len(data):
        require(offset + 8 <= len(data), 'Truncated QOA frame header')
        channels = data[offset]
        rate = int.from_bytes(data[offset + 1:offset + 4], 'big')
        count, size = struct.unpack('>HH', data[offset + 4:offset + 8])
        require(channels == m['channels'] == 1 and rate == m['sampleRate'] == 24000
                and count == min(5120, m['logicalFrameCount'] - frames) and count > 0
                and size == 24 + 8 * ((count + 19) // 20) and offset + size <= len(data), 'Invalid QOA frame')
        offset += size
        frames += count
    require(offset == len(data) and frames == m['logicalFrameCount'], 'Incomplete QOA container')


def validate_encoded(data, metadata):
    require(len(data) == metadata['byteLength'] and digest(data) == metadata['encodedSha256'], 'Encoded identity differs')
    validate_container(data, metadata)


def prepare(study, reference, renderer=None):
    study = Path(study)
    renderer = Path(renderer) if renderer is not None else study.parent / 'audio-reference-build/render'
    lock_bytes = read_pinned(ROOT, dict(path='content/route22-cry-audio-source-lock.json',
                                      byteLength=LOCK_BYTES, sha256=LOCK_SHA256))
    lock = json.loads(lock_bytes)
    require(lock['profile'] == PROFILE and lock['schemaVersion'] == 1 and tuple(lock['cryIds']) == IDS,
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
    renders = {e['path']: read_pinned(renderer, e) for e in lock['rendererFiles']}
    require(digest(renders['render_audio.c']) == lock['renderer']['sourceSha256']
            and digest(renders['render_audio']) == lock['renderer']['binarySha256'], 'Renderer identity differs')
    symbols = json.loads(renders['symbols.json'])
    require(symbols['PlayCry_ByMode'] == 134684256 and symbols['gPokemonCryMusicPlayers'] == 50360176,
            'Original ROM cry entry points differ')
    require(b"function=SY['PlayCry_ByMode']" in inputs['run_study.py']
            and b"arg=c['speciesId']" in inputs['run_study.py']
            and b"player=SY['gPokemonCryMusicPlayers']" in inputs['run_study.py'], 'Study cry invocation differs')
    manifest, inventory, runtime, cries, summary = [json.loads(inputs[p]) for p in
        ['manifest.json', 'inventory.json', 'runtime-manifest.json', 'cry-inventory.json', 'RESULT.json']]
    require(manifest['sourceCommit'] == inventory['source']['commit'] == commit
            and manifest['romSha1'] == inventory['source']['romSha1'] == lock['source']['romSha1']
            and summary['manifestSha256'] == digest(inputs['manifest.json'])
            and summary['runtimeManifestSha256'] == digest(inputs['runtime-manifest.json'])
            and summary['allAssetHashesVerified'] and summary['status'] == 'complete-basic-library'
            and summary['categorySummary']['cry']['count'] == 386
            and not summary['audibleOutputVerified'] and not summary['renderFailures']
            and not summary['incompleteCaptures'] and inventory['source']['romCryTablesVerified']
            and inventory['source']['compressedCrySamplesMatchRom'], 'Study identity differs')
    mode, = [m for m in inventory['cryModes'] if m['mode'] == 0]
    require(all(mode[k] == v for k, v in lock['cryMode'].items()) and mode['toneTable'] == 'gCryTable',
            'Normal cry mode differs')
    sound = originals['src/sound.c']
    require(re.search(rb'case CRY_MODE_NORMAL:\s*break;', sound)
            and b'species--;' in sound and b'species = SpeciesToCryId(species);' in sound
            and re.search(rb'if \(species < SPECIES_OLD_UNOWN_B - 1\)\s*return species;', originals['src/pokemon.c']),
            'Source cry dispatch differs')
    for name, value in [('CRY_MODE_NORMAL', 0), ('CRY_VOLUME', 120), ('CRY_PRIORITY_NORMAL', 10)]:
        require(re.search(rb'^#define\s+' + name.encode() + rb'\s+' + str(value).encode() + rb'\s',
                          originals['include/constants/sound.h'], re.M), 'Source cry constant differs')
    normal_table = originals['sound/cry_tables.inc'].split(b'gCryTable_Reverse::')[0]
    tones = re.findall(rb'^\s*cry\s+(\w+)\s*$', normal_table, re.M)
    clips, generated = [], {'source-lock.json': lock_bytes}
    for identity, species_id in zip(IDS, (21, 22, 56), strict=True):
        clip, = [c for c in manifest['clips'] if c['id'] == identity]
        result = json.loads(inputs[f'results/{identity}.json'])
        run, = [c for c in runtime['clips'] if c['id'] == identity]
        planned, = [c for c in cries if c['speciesId'] == species_id]
        species, = [c for c in inventory['speciesCries'] if c['speciesId'] == species_id]
        sample, = [c for c in inventory['crySamples'] if c['speciesId'] == species_id]
        require(clip == result and all(clip[k] == v for k, v in run.items())
                and all(clip[k] == v for k, v in planned.items()) and clip['speciesId'] == species_id
                and clip['mode'] == 0 and clip['category'] == 'cry' and clip['status'] == 'converted'
                and clip['codec'] == 'qoa' and not clip['audibleOutputVerified'] and not clip['seamlessLoopVerified'],
                'Selected study entry differs')
        require(species['cryTableIndex'] == clip['cryIndex'] == species_id - 1
                and all(sample[k] == v for k, v in species.items()) and not sample['isUnused']
                and tones[clip['cryIndex']].decode() == species['sampleSymbol']
                and sample['sourceSha256'] == digest(originals[species['sourcePath']])
                and sample['sourceBytes'] == len(originals[species['sourcePath']])
                and sample['normalToneType'] == 32 and not sample['loopEnabled'], 'Cry source mapping differs')
        require(re.search(rb'^#define\s+' + species['species'].encode() + rb'\s+' + str(species_id).encode() + rb'\s',
                          originals['include/constants/species.h'], re.M), 'Species constant differs')
        capture = clip['capture']
        require(capture == dict(sampleRate=48000, channels=2, frames=289313, sourceVBlanks=360,
                                sourceStereo=0, soundCntH=13058, audibleOutputVerified=False), 'Capture metadata differs')
        trace = inputs[f'traces/{identity}.csv']
        require(digest(trace) == clip['traceSha256'], 'Source trace digest differs')
        rows = [{k: int(v) for k, v in row.items()} for row in csv.DictReader(io.StringIO(trace.decode()))]
        require(len(rows) == capture['sourceVBlanks'] and rows[-1]['sample_end'] == clip['capturedFrames'] == capture['frames']
                and all(r['frame'] == i for i, r in enumerate(rows)), 'Source capture differs')
        stopped = next((r['frame'] for r in rows if r['clock'] > 0 and r['status'] & 0x80000000), None)
        require(stopped == clip['sourceSequenceStoppedVBlank'] == 142, 'Shared background-sequence trace differs')
        require(clip['sampleRate'] == 24000 and 0 <= clip['sourceEnergyAbove12kHzPercent'] <= 1
                and clip['logicalFrames'] == (clip['sourceFrames'] * 24000 + 24000) // 48000
                and clip['loopStart'] is None and clip['loopEnd'] is None, 'Target delivery differs')
        m = dict(id=identity, speciesId=species_id, cryTableIndex=clip['cryIndex'], cryMode=0,
            sourceCommit=commit, sourceSymbol=species['species'], sourcePath=species['sourcePath'],
            sourceWavSha256=sample['sourceSha256'], sourceCompressedSha256=sample['compressedSha256'],
            sourcePlayer=4, priority=10, sourceCounter=None, sourceFunction='PlayCry_ByMode',
            sourceFunctionAddress=symbols['PlayCry_ByMode'], renderer=lock['renderer']['name'],
            rendererSourceSha256=lock['renderer']['sourceSha256'], rendererBinarySha256=lock['renderer']['binarySha256'],
            encoder='Godot 4.6.3-stable official AudioStreamWAV QOA', codec='qoa',
            sourcePcmSha256=clip['sourcePcmSha256'], sourceSampleRate=48000, sourceChannels=1,
            sourceFrameCount=clip['sourceFrames'], studyBackgroundSequenceStoppedVBlank=stopped,
            directCryStoppedVBlank=None, frameRateNumerator=16777216, frameRateDenominator=280896,
            sampleRate=24000, channels=1, logicalFrameCount=clip['logicalFrames'],
            loopStartFrame=None, loopEndFrame=None, path='assets/' + identity.lower() + '.qoa',
            encodedSha256=clip['sha256'], byteLength=clip['bytes'], decodedPcmSha256=None,
            sourceEnergyAbove12kHzPercent=clip['sourceEnergyAbove12kHzPercent'], resampled=True,
            normalized=False, losslessToSourceChannel=False, audibleOutputVerified=False,
            originalScriptWaitVerified=False)
        data = inputs[clip['path']]
        validate_encoded(data, m)
        generated[m['path']] = data
        clips.append(m)
    output = dict(schemaVersion=1, profile=PROFILE, sourceLockSha256=LOCK_SHA256,
        studyManifestSha256=digest(inputs['manifest.json']), studyInventorySha256=digest(inputs['inventory.json']),
        studyRuntimeManifestSha256=digest(inputs['runtime-manifest.json']), romSha1=manifest['romSha1'],
        audioBytes=sum(c['byteLength'] for c in clips), clips=clips, limitations=lock['limitations'])
    generated['manifest.json'] = (json.dumps(output, indent=2) + '\n').encode()
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
    parser.add_argument('--renderer-dir', type=Path)
    parser.add_argument('--output', type=Path, default=ROOT / 'content/generated/route22-cry-audio')
    parser.add_argument('--receipt', type=Path)
    args = parser.parse_args()
    generated = prepare(args.study_dir, args.reference, args.renderer_dir)
    install(args.output, generated)
    receipt = dict(status='passed', profile=PROFILE, assetCount=3,
        encodedBytes=sum(len(v) for k, v in generated.items() if k.startswith('assets/')),
        runtimeFiles=len(generated), runtimeBytes=sum(map(len, generated.values())),
        manifestSha256=digest(generated['manifest.json']), manifestBytes=len(generated['manifest.json']),
        sourceLockSha256=LOCK_SHA256, sourceFiles=8, studyInputs=16, rendererInputs=3,
        rerendered=False, reencoded=False, downloaded=False, upstreamExecuted=False,
        rawSampleDirectPlayback=False, audibleOutputVerified=False, originalScriptWaitVerified=False)
    if args.receipt:
        args.receipt.parent.mkdir(parents=True, exist_ok=True)
        args.receipt.write_text(json.dumps(receipt, indent=2) + '\n')
    print(json.dumps(receipt))


if __name__ == '__main__':
    main()
