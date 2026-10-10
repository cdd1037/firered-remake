#!/usr/bin/env python3
"""Import the exact approved q6 delivery candidate without changing original-source authentication."""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import struct
import tempfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('original_audio', ROOT / 'scripts/import-early-audio.py')
original = importlib.util.module_from_spec(spec)
spec.loader.exec_module(original)
MANIFEST_SHA256 = 'aa6e6835be96879f119568ba0300463763658287e39f201492efc6da174eeb3c'
FROZEN_SHA256 = 'e90535a189542babe1fc87a9d4333aa24aa86f1def9b3d241730f71628577b71'
PROFILE = 'firered-early-delivery-q6-mono-v1'
AUDIO_BYTES = 25964468
require, sha, read_pinned, strict_json = original.require, original.sha, original.read_pinned, original.strict_json


def validate_clip(c):
    s = c['source']; original.validate_clip(s)
    vorbis = c['codec'] == 'vorbis'
    require(c['codec'] in ('vorbis', 'pcm_s16le_wav') and vorbis == c['id'].startswith('MUS_'), 'Unexpected delivery codec')
    require(c['id'] == s['id'] and c['sampleRate'] == 48000 and c['channels'] == 1 and c['byteLength'] > 0,
            'Invalid delivery identity/format')
    require(c['path'] == 'assets/' + c['id'].lower() + ('.ogg' if vorbis else '.wav'), 'Invalid delivery path')
    begin, end = c['loopStartFrame'], c['loopEndFrame']
    require((begin, end) == (s['loopStartFrame'], s['loopEndFrame']), 'Delivery loop differs from source')
    require(c['logicalFrameCount'] == (end if vorbis and end is not None else s['frameCount']), 'Invalid delivery frame count')
    require(c['loopOffsetSeconds'] == (begin / 48000 if begin is not None else None), 'Invalid exact loop offset')
    require(not vorbis or end is None or s['frameCount'] == end + 1, 'Unexpected loop-end trim')
    t = c['transform']
    require(t == {'channelSelection': 'left; L=R verified over every source frame', 'resample': False,
                  'normalization': False, 'tailTrim': 'one excluded frame at source loopEnd' if vorbis and end is not None else 'none',
                  'encoder': 'ffmpeg 7.1.5 / libvorbis q6' if vorbis else 'Python wave PCM16 mono'}, 'Unsupported transformation')
    require(c['acceptance'] == {'encodedHashVerified': True, 'decodedFrameCountVerified': True,
        'losslessToSourceChannel': not vorbis, 'godotPlaybackVerified': False, 'audibleOutputVerified': False,
        'seamlessLoopVerified': False}, 'Changed candidate acceptance claims')


def import_audio(candidate_dir, source_dir, output_dir, project_root=ROOT):
    candidate_dir, source_dir, output_dir = map(lambda p: Path(p).absolute(), (candidate_dir, source_dir, output_dir))
    require(output_dir not in (candidate_dir, source_dir) and not output_dir.is_symlink(), 'Unsafe delivery destination')
    manifest_bytes = read_pinned(candidate_dir, 'manifest.json', MANIFEST_SHA256)
    frozen_bytes = read_pinned(candidate_dir, 'FROZEN.json', FROZEN_SHA256)
    source_bytes = read_pinned(source_dir, 'manifest.json', original.MANIFEST_SHA256)
    lock_bytes = read_pinned(source_dir, 'source-lock.json', original.SOURCE_LOCK_SHA256)
    source_frozen = read_pinned(source_dir, 'FROZEN.json', original.FROZEN_SHA256)
    read_pinned(project_root, 'content/early-audio-source-lock.json', original.SOURCE_LOCK_SHA256)
    m, s = strict_json(manifest_bytes), strict_json(source_bytes)
    require((m['schemaVersion'], m['profile'], m['sourceManifestSha256'], m['sourceLockSha256'], m['sourceFrozenSha256'],
             m['sampleRate'], m['frameRateNumerator'], m['frameRateDenominator'], m['audioBytes']) ==
            (1, PROFILE, original.MANIFEST_SHA256, original.SOURCE_LOCK_SHA256, original.FROZEN_SHA256,
             48000, 16777216, 280896, AUDIO_BYTES), 'Delivery manifest identity differs')
    originals = {c['id']: c for c in s['clips']}
    require(len(m['clips']) == len(originals) == len({c['id'] for c in m['clips']}) == 134
            and {c['id'] for c in m['clips']} == set(originals), 'Incomplete delivery inventory')
    output_dir.parent.mkdir(parents=True, exist_ok=True)
    staging = Path(tempfile.mkdtemp(prefix='.audio-delivery-', dir=output_dir.parent))
    total = 0
    try:
        for name, data in [('manifest.json', manifest_bytes), ('FROZEN.json', frozen_bytes),
                           ('source-manifest.json', source_bytes), ('source-lock.json', lock_bytes), ('source-FROZEN.json', source_frozen)]:
            (staging / name).write_bytes(data)
        for c in m['clips']:
            validate_clip(c)
            require(c['source'] == originals[c['id']], 'Embedded original metadata differs')
            data = read_pinned(candidate_dir, c['path'], c['sha256'])
            require(len(data) == c['byteLength'], 'Delivery byte length differs')
            source = c['source']
            wav = read_pinned(source_dir, source['wavPath'], source['wavSha256'])
            original.validate_wav(source, wav)
            pcm = memoryview(wav)[44:]
            require(pcm[0::4] == pcm[2::4] and pcm[1::4] == pcm[3::4], 'Source channels differ')
            if c['codec'] == 'pcm_s16le_wav':
                require(len(data) == 44 + source['frameCount'] * 2, 'PCM sample count differs')
                require(struct.unpack('<4sI4s4sIHHIIHH4sI', data[:44]) ==
                        (b'RIFF', len(data)-8, b'WAVE', b'fmt ', 16, 1, 1, 48000, 96000, 2, 16, b'data', len(data)-44),
                        'Invalid delivery PCM header')
                require(sha(data[44:]) == c['pcmSha256'] and data[44::2] == pcm[0::4]
                        and data[45::2] == pcm[1::4] and any(data[44:]), 'Delivery PCM differs from every original sample')
            else:
                require(data[:4] == b'OggS' and b'\x01vorbis' in data[:64], 'Invalid Vorbis container')
            target = staging / c['path']; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
            total += len(data)
        require(total == AUDIO_BYTES, 'Delivery size differs')
        read_pinned(candidate_dir, 'manifest.json', MANIFEST_SHA256)
        read_pinned(source_dir, 'manifest.json', original.MANIFEST_SHA256)
        if output_dir.exists():
            actual = {p.relative_to(output_dir).as_posix() for p in output_dir.rglob('*') if p.is_file() or p.is_symlink()}
            planned = {p.relative_to(staging).as_posix() for p in staging.rglob('*') if p.is_file()}
            require(actual == planned, 'Existing delivery inventory differs; use a fresh directory')
            for relative in sorted(planned):
                require(original.safe_file(output_dir, relative).read_bytes() == (staging / relative).read_bytes(),
                        'Existing delivery differs: ' + relative)
        else:
            staging.rename(output_dir)
        return {'status':'passed', 'clips':134, 'vorbisMusic':31, 'losslessMonoPcm':103,
                'audioBytes':total, 'packageBytes':sum(p.stat().st_size for p in output_dir.rglob('*') if p.is_file()),
                'manifestSha256':MANIFEST_SHA256, 'sourceManifestSha256':original.MANIFEST_SHA256,
                'godotVerified':False, 'audibleVerified':False, 'seamlessLoopVerified':False}
    finally:
        if staging.exists(): shutil.rmtree(staging)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate-dir', type=Path, required=True)
    parser.add_argument('--source-dir', type=Path, required=True, help='Frozen original mono master render; read only')
    parser.add_argument('--output-dir', type=Path, default=ROOT / 'content/generated/audio-delivery')
    args = parser.parse_args()
    print(json.dumps(import_audio(args.candidate_dir, args.source_dir, args.output_dir), indent=2))

if __name__ == '__main__': main()
