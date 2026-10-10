#!/usr/bin/env python3
"""Authenticate and normalize the frozen 24/48 kHz q0/QOA candidate. Never re-encode."""
from __future__ import annotations
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import stat
import struct
import tempfile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('original_audio', ROOT / 'scripts/import-early-audio.py')
original = importlib.util.module_from_spec(spec)
spec.loader.exec_module(original)
require, sha, strict_json = original.require, original.sha, original.strict_json
CANDIDATE_SHA256 = 'bc517cd9e0063068b743cb5e236a698125d43254cd3a7b837517a0a27912ab57'
PROFILE = 'firered-early-delivery-q0-qoa-24-48-v1'
AUDIO_BYTES = 6258027
# These independently frozen normalized identities are checked on every import.
MANIFEST_SHA256 = 'bd9e9027ad41aa16b10d7bea0a3aebed77a0901f593c531ff002a7ec8bc4360d'
TIMING_SHA256 = '67c8996d160bb302f27ace4fd2de5e7a16db14b1caa2a88fd4cc15e02f84182f'
FROZEN_SHA256 = '0f278728df3bce2cb665abbee10511c06559694f1751ee5f6ed53391945e6c00'


def read_pinned(root, relative, expected_sha, expected_size=None, maximum=2*1024*1024):
    """Bound allocation before reading, then authenticate the same open file description."""
    path=original.safe_file(root,relative)
    with os.fdopen(os.open(path,os.O_RDONLY|getattr(os,'O_NOFOLLOW',0)), 'rb') as stream:
        before=os.fstat(stream.fileno())
        require(stat.S_ISREG(before.st_mode) and 0 < before.st_size <= maximum, 'Pinned file size/type limit: '+relative)
        require(expected_size is None or before.st_size == expected_size, 'Pinned encoded length mismatch: '+relative)
        # At most the authenticated length plus one sentinel byte, never read-to-EOF.
        data=stream.read(before.st_size+1)
        after=os.fstat(stream.fileno())
        require(len(data)==before.st_size and after.st_size==before.st_size
                and after.st_mtime_ns==before.st_mtime_ns and after.st_ctime_ns==before.st_ctime_ns,
                'Pinned file changed during bounded read: '+relative)
    require(sha(data)==expected_sha, 'Pinned file digest mismatch: '+relative)
    return data


def json_bytes(value):
    return (json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False) + '\n').encode()


def validate_qoa(data, rate, channels, frames):
    require(0 < len(data) <= 16 * 1024 * 1024 and 0 < frames <= rate * 600, 'QOA size/duration limit')
    require(len(data) >= 32 and data[:4] == b'qoaf' and int.from_bytes(data[4:8], 'big') == frames, 'Invalid QOA file header')
    offset = 8; consumed = 0
    while offset < len(data):
        require(len(data) - offset >= 8, 'Truncated QOA frame header')
        h = data[offset:offset+8]
        n, size = int.from_bytes(h[4:6], 'big'), int.from_bytes(h[6:8], 'big')
        require(h[0] == channels and int.from_bytes(h[1:4], 'big') == rate
                and n == min(5120, frames-consumed) and n > 0, 'QOA frame layout mismatch')
        require(size == 8 + 16*channels + 8*channels*((n+19)//20) and offset+size <= len(data), 'QOA frame size mismatch')
        consumed += n; offset += size
    require(offset == len(data) and consumed == frames, 'QOA total frame mismatch')


def validate_ogg(data, rate, channels, frames):
    require(len(data) >= 58 and data[:6] == b'OggS\x00\x02' and data[26:35] == b'\x01\x1e\x01vorbis'
            and struct.unpack_from('<I', data,35)[0] == 0 and data[39] == channels
            and struct.unpack_from('<I',data,40)[0] == rate, 'Vorbis identification mismatch')
    offset=0; eos=False; granule=-1
    while offset < len(data):
        require(not eos and len(data)-offset >= 27 and data[offset:offset+5] == b'OggS\x00', 'Ogg page mismatch')
        count=data[offset+26]; require(len(data)-offset >= 27+count, 'Ogg lacing truncated')
        length=27+count+sum(data[offset+27:offset+27+count]); require(offset+length <= len(data), 'Ogg payload truncated')
        granule=struct.unpack_from('<q',data,offset+6)[0]; eos=bool(data[offset+5]&4); offset+=length
    require(eos and granule == frames, 'Ogg logical frame count mismatch')


def normalize(candidate, sources):
    require(candidate['schemaVersion'] == 1 and candidate['profile'] == 'firered-stable-q0-24-48-v1'
            and candidate['sourceManifestSha256'] == original.MANIFEST_SHA256
            and candidate['sourceLockSha256'] == original.SOURCE_LOCK_SHA256
            and candidate['clipCount'] == 134 and candidate['totalBytes'] == AUDIO_BYTES, 'Candidate identity mismatch')
    clips=[]; timing=[]
    for c in candidate['clips']:
        s=c['source']; original.validate_clip(s)
        require(s == sources.get(c['id']), 'Candidate original metadata mismatch')
        vorbis=c['codec']=='vorbis'; rate=c['sampleRate']
        require(c['codec'] in ('vorbis','qoa') and vorbis == c['id'].startswith('MUS_')
                and rate in (24000,48000) and c['channels']==1, 'Candidate format mismatch')
        div=48000//rate
        require(c['path']=='assets/'+c['id']+('.ogg' if vorbis else '.qoa'), 'Unsafe candidate path')
        source_frames=s['loopEndFrame'] if vorbis and s['loopEndFrame'] is not None else s['frameCount']
        require(c['logicalFrames']==(source_frames+div-1)//div if vorbis else c['logicalFrames']==source_frames//div,
                'Candidate frame quantization mismatch')
        begin=(s['loopStartFrame']+div-1)//div if s['loopStartFrame'] is not None else None
        end=(s['loopEndFrame']+div-1)//div if s['loopEndFrame'] is not None else None
        require((c['loopStart'],c['loopEnd'])==(begin,end) and c['loopOffsetSeconds']==(begin/rate if begin is not None else None),
                'Candidate loop quantization mismatch')
        clips.append(dict(id=c['id'],path='assets/'+c['id'].lower()+('.ogg' if vorbis else '.qoa'),codec=c['codec'],
            byteLength=c['bytes'],sha256=c['sha256'],sampleRate=rate,channels=1,logicalFrameCount=c['logicalFrames'],
            loopStartFrame=begin,loopEndFrame=end,loopOffsetSeconds=c['loopOffsetSeconds'],
            transform=dict(channelSelection='left; L=R verified over every source frame',resample=rate!=48000,normalization=False,
                tailTrim='exclusive source loop end; target-frame quantization' if vorbis and end is not None else 'target-frame quantization only',
                encoder='ffmpeg 7.1.5 / libvorbis q0' if vorbis else 'Godot 4.7.2 AudioStreamWAV QOA'),source=s,
            acceptance=dict(encodedHashVerified=True,decodedFrameCountVerified=True,losslessToSourceChannel=False,
                godotPlaybackVerified=False,audibleOutputVerified=False,seamlessLoopVerified=False),pcmSha256=None))
        # Preserve the research's measured errors, along with exact integer source and target bounds.
        timing.append(dict(id=c['id'],sourceFrames=s['frameCount'],sourceSampleRate=48000,sampleRate=rate,
            logicalFrames=c['logicalFrames'],sourceLoopStart=s['loopStartFrame'],sourceLoopEnd=s['loopEndFrame'],
            loopStart=begin,loopEnd=end,measurements=c['timing']))
    require(len(clips)==len({c['id'] for c in clips})==len(sources)==134, 'Incomplete candidate inventory')
    manifest=dict(schemaVersion=1,profile=PROFILE,sourceManifestSha256=original.MANIFEST_SHA256,
        sourceLockSha256=original.SOURCE_LOCK_SHA256,sourceFrozenSha256=original.FROZEN_SHA256,sampleRate=0,
        frameRateNumerator=16777216,frameRateDenominator=280896,audioBytes=AUDIO_BYTES,clips=clips,
        limitations=['Mixed 24/48 kHz: use each clip sampleRate; manifest sampleRate=0 denotes mixed rates.',
        'All derivatives are lossy: Vorbis q0 music and QOA effects/cries; no derivative PCM hash or lossless claim.',
        'Resampling quantizes lengths/loop points; exact original metadata and measured timing errors are retained.',
        'Only 134 early-region clips; not full-game sound coverage.',
        'Physical-device listening and original source loop-seam fidelity remain unverified.'])
    timing_doc=dict(schemaVersion=1,candidateManifestSha256=CANDIDATE_SHA256,
        roundingPolicy='Music duration and loop points round up to target frame; QOA duration rounds down. 24/48 kHz only.',clips=timing)
    return manifest,timing_doc


def import_audio(candidate_dir, provenance_dir, output_dir, receipt_path=None):
    candidate_dir,provenance_dir,output_dir=map(lambda p:Path(p).absolute(),(candidate_dir,provenance_dir,output_dir))
    require(output_dir not in (candidate_dir,provenance_dir) and not output_dir.is_symlink(), 'Unsafe destination')
    raw=read_pinned(candidate_dir,'manifest.json',CANDIDATE_SHA256); candidate=strict_json(raw)
    source=read_pinned(provenance_dir,'source-manifest.json',original.MANIFEST_SHA256)
    lock=read_pinned(provenance_dir,'source-lock.json',original.SOURCE_LOCK_SHA256)
    source_frozen=read_pinned(provenance_dir,'source-FROZEN.json',original.FROZEN_SHA256)
    sources={c['id']:c for c in strict_json(source)['clips']}
    manifest,timing=normalize(candidate,sources); mb,tb=json_bytes(manifest),json_bytes(timing)
    frozen=json_bytes(dict(schemaVersion=1,profile=PROFILE,manifestSha256=sha(mb),timingSha256=sha(tb),
        candidateManifestSha256=CANDIDATE_SHA256,audioBytes=AUDIO_BYTES,clipCount=134))
    require((sha(mb),sha(tb),sha(frozen))==(MANIFEST_SHA256,TIMING_SHA256,FROZEN_SHA256),'Normalized frozen identity mismatch')
    output_dir.parent.mkdir(parents=True,exist_ok=True)
    staging=Path(tempfile.mkdtemp(prefix='.small-audio-delivery-',dir=output_dir.parent))
    try:
        for name,data in [('manifest.json',mb),('timing.json',tb),('FROZEN.json',frozen),('source-manifest.json',source),
                          ('source-lock.json',lock),('source-FROZEN.json',source_frozen)]: (staging/name).write_bytes(data)
        total=0
        for c,m in zip(candidate['clips'],manifest['clips']):
            data=read_pinned(candidate_dir,c['path'],c['sha256'],c['bytes'],maximum=16*1024*1024)
            (validate_ogg if c['codec']=='vorbis' else validate_qoa)(data,c['sampleRate'],c['channels'],c['logicalFrames'])
            target=staging/m['path']; target.parent.mkdir(exist_ok=True);target.write_bytes(data);total+=len(data)
        require(total==AUDIO_BYTES,'Total audio bytes mismatch')
        if output_dir.exists():
            actual={p.relative_to(output_dir).as_posix() for p in output_dir.rglob('*') if p.is_file() or p.is_symlink()}
            planned={p.relative_to(staging).as_posix() for p in staging.rglob('*') if p.is_file()}
            require(actual==planned,'Existing runtime inventory differs; use a fresh directory')
            for name in planned:
                expected=(staging/name).read_bytes()
                read_pinned(output_dir,name,sha(expected),len(expected),maximum=16*1024*1024)
        else: staging.rename(output_dir)
        result=dict(status='passed',profile=PROFILE,clips=134,vorbis=31,lossyQoa=103,audioBytes=total,
            packageBytes=sum(p.stat().st_size for p in output_dir.rglob('*') if p.is_file()),manifestSha256=sha(mb),timingSha256=sha(tb),
            frozenSha256=sha(frozen),candidateManifestSha256=CANDIDATE_SHA256,audibleVerified=False,sourceSeamFidelityVerified=False)
        if receipt_path:
            receipt_path=Path(receipt_path);receipt_path.parent.mkdir(parents=True,exist_ok=True)
            receipt_path.write_bytes(json_bytes(result|dict(candidateDirectory=str(candidate_dir),provenanceDirectory=str(provenance_dir),
                originalDerivativeInputs=[dict(id=c['id'],path=c['derivativeInputPath'],sha256=c['sha256']) for c in candidate['clips']])))
        return result
    finally:
        if staging.exists():shutil.rmtree(staging)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--candidate-dir',type=Path,required=True)
    parser.add_argument('--provenance-dir',type=Path,default=ROOT/'content/generated/audio-delivery')
    parser.add_argument('--output-dir',type=Path,default=ROOT/'content/generated/small-audio-delivery')
    parser.add_argument('--receipt',type=Path)
    args=parser.parse_args(); print(json.dumps(import_audio(args.candidate_dir,args.provenance_dir,args.output_dir,args.receipt),indent=2))

if __name__=='__main__':main()
