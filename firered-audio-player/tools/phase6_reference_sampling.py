#!/usr/bin/env python3
"""Five bounded original-driver song references using existing GCC/libmGBA only.

No dependency installation, new runtime implementation, core edits, or recorded
fallback. Fresh initialization for every render; original gain is never fitted.
"""
import array
import hashlib
import json
import os
import pathlib
import platform
import shlex
import struct
import subprocess
import time
import wave

ROOT = pathlib.Path(__file__).resolve().parents[1]
REF = ROOT.parent / 'audio-reference-build/pokefirered'
STUDY = ROOT.parent / 'runtime-audio-study'
LIB = ROOT.parent / 'audio-reference-build/emulator/libmgba-gcc-local'
OUT = ROOT / 'results/phase6-reference'
OUT.mkdir(parents=True, exist_ok=True)
ROM = REF / 'pokefirered.gba'
PACK = STUDY / 'audio.pack'
PROBE = ROOT / 'tools/phase6-reference-probe.c'
BINARY = OUT / 'reference-probe'
SELECTION = ROOT / 'results/phase5-music/selection.json'
NAMES = ['se_pc_on', 'se_door', 'mus_route1', 'mus_pewter', 'mus_vs_wild']
EXPECTED = {
    'se_pc_on': (4, 0x086b56f4, 1),
    'se_door': (241, 0x086bc670, 1),
    'mus_route1': (291, 0x086d1ddc, 0),
    'mus_pewter': (314, 0x086e8810, 0),
    'mus_vs_wild': (298, 0x086da810, 0),
}


def sha(path, algorithm='sha256'):
    return hashlib.new(algorithm, path.read_bytes()).hexdigest()


def command(args):
    return shlex.join([str(arg) for arg in args])


before = {'romSha256': sha(ROM), 'packSha256': sha(PACK),
          'originalProbeSha256': sha(STUDY / 'probe.c'),
          'bootstrapSha256': sha(STUDY / 'boot.bin')}
assert sha(ROM, 'sha1') == '41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc'
assert before['packSha256'] == '595af58c91458c6a57080cc4f0066caf0c42da84665b7476b3de45e38ccfd065'
assert before['originalProbeSha256'] == '2adb12d7bf0132b4f2ec51d2b70e39ed22aea190681d5760e8334bb13df71324'
assert before['bootstrapSha256'] == '0bb1c0735535250697d3746859fd111254b027d7e78d1ea16163d787f42d9dd2'
compiler = subprocess.check_output(['gcc', '--version'], text=True).splitlines()[0]
compile_args = ['gcc', '-O2', '-Wall', '-Wextra', '-Werror', '-D_GNU_SOURCE',
                '-I' + str(LIB / 'include'), str(PROBE), '-L' + str(LIB / 'lib'),
                '-Wl,-rpath,' + str(LIB / 'lib'), '-lmgba', '-lm', '-o', str(BINARY)]
subprocess.run(compile_args, check=True)
rom = ROM.read_bytes()
selection = json.loads(SELECTION.read_text())
assert selection['fixedRevision'] == '037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert selection['romSha1'] == sha(ROM, 'sha1')
selected = {row['name']: row for row in selection['songs']}


def invoke(row, mode):
    suffix = {'stripped': '', 'full': '-full-control', 'unguarded': '-unguarded-control'}[mode]
    path = OUT / ('reference-' + row['name'] + suffix + '.wav')
    args = [str(BINARY), str(PACK), str(ROM) if mode == 'full' else '-',
            str(path), str(row['songId']), '480']
    env = dict(os.environ, GUARD_ROM='0' if mode == 'unguarded' else '1')
    started = time.perf_counter()
    result = subprocess.run(args, env=env, capture_output=True, text=True, check=True)
    elapsed = time.perf_counter() - started
    metrics = json.loads(result.stdout)
    assert metrics['mailbox'] == 0 and metrics['emulatedFrames'] == 480
    assert metrics['songId'] == row['songId'] and metrics['songAddress'] == f'0x{row["songAddress"]:08x}'
    assert metrics['originalPlayer'] == row['originalPlayer']
    assert metrics['guarded'] == (mode != 'unguarded')
    assert (metrics['romChecks'] > 0) == (mode != 'unguarded')
    assert (metrics['initialReverb'], metrics['finalReverb'], metrics['pcmMaxChannels'],
            metrics['pcmMasterVolume'], metrics['pcmSamplesPerVBlank'], metrics['pcmFreq']) == (0, 50, 5, 12, 224, 13379)
    with wave.open(str(path), 'rb') as f:
        assert (f.getnchannels(), f.getsampwidth(), f.getframerate()) == (2, 2, 48000)
        pcm = f.readframes(f.getnframes())
        assert f.getnframes() == metrics['frames'] == 385752
    values = array.array('h', pcm)
    if __import__('sys').byteorder != 'little':
        values.byteswap()
    peak = max(map(abs, values))
    nonzero = sum(v != 0 for v in values)
    assert peak > 0 and nonzero > 0
    return {'path': str(path.relative_to(ROOT)),
            'command': 'GUARD_ROM=' + env['GUARD_ROM'] + ' ' + command(args),
            'sha256': sha(path), 'pcmSha256': hashlib.sha256(pcm).hexdigest(),
            'byteCount': path.stat().st_size, 'wholeProcessWallSeconds': elapsed,
            'stdoutMetrics': metrics, 'stderr': result.stderr.strip(),
            'pcm': {'peakAbs': peak, 'nonzeroValues': nonzero,
                    'rms': (sum(v * v for v in values) / len(values)) ** .5,
                    'saturatedValues': sum(v in (-32768, 32767) for v in values)}}


renders = []
for name in NAMES:
    row = selected[name]
    assert (row['songId'], row['songAddress'], row['originalPlayer']) == EXPECTED[name]
    assert row['songTableAddress'] == 0x084a32cc + row['songId'] * 8
    assert struct.unpack_from('<IHH', rom, row['songTableAddress'] - 0x08000000) == (row['songAddress'], row['originalPlayer'], row['originalPlayer'])
    assert sha(REF / row['sourcePath']) == row['sourceSha256']
    stripped = invoke(row, 'stripped')
    full = invoke(row, 'full')
    unguarded = invoke(row, 'unguarded')
    assert stripped['sha256'] == full['sha256'] == unguarded['sha256']
    renders.append({**{k: row[k] for k in ['name', 'songId', 'songAddress', 'songTableAddress',
                        'originalPlayer', 'trackCount', 'priority', 'reverb', 'voicegroup',
                        'sourcePath', 'sourceSha256', 'potentialLeafTypes']},
                    'stripped': stripped, 'fullDataControl': full,
                    'unguardedControl': unguarded, 'controlsByteIdentical': True})
    print(name, stripped['sha256'], 'stripped/full/unguarded exact', flush=True)

negative = []
for label, song_id, frames in [('unknown-song', '5', '480'),
                             ('raw-address', '0x086b56f4', '480'),
                             ('negative-song', '-1', '480'),
                             ('malformed-song', '4junk', '480'),
                             ('zero-duration', '4', '0'),
                             ('extended-duration', '4', '481')]:
    output = OUT / ('reference-invalid-' + label + '.wav')
    assert not output.exists()
    args = [str(BINARY), str(PACK), '-', str(output), song_id, frames]
    result = subprocess.run(args, env=dict(os.environ, GUARD_ROM='1'), capture_output=True, text=True)
    assert result.returncode == 2 and not output.exists()
    negative.append({'label': label, 'command': command(args), 'exitCode': result.returncode,
                     'stderr': result.stderr.strip(), 'createdOutputFile': False})

after = {'romSha256': sha(ROM), 'packSha256': sha(PACK),
         'originalProbeSha256': sha(STUDY / 'probe.c'),
         'bootstrapSha256': sha(STUDY / 'boot.bin')}
assert before == after
metadata = {
    'scope': 'Five fixed original-driver song references for PSG/mixer coverage; no native synthesis implementation or runtime/core edits.',
    'source': {'repository': 'pret/pokefirered', 'fixedRevision': selection['fixedRevision'],
               'romSha1': selection['romSha1'], 'inputsBefore': before, 'inputsAfter': after,
               'unchangedInputs': True, 'selectionPath': str(SELECTION.relative_to(ROOT)),
               'selectionSha256': sha(SELECTION), 'originalProbe': str(STUDY / 'probe.c')},
    'build': {'compiler': compiler, 'command': command(compile_args),
              'sourcePath': str(PROBE.relative_to(ROOT)), 'sourceSha256': sha(PROBE),
              'scriptPath': 'tools/phase6_reference_sampling.py', 'scriptSha256': sha(pathlib.Path(__file__)),
              'binaryPath': str(BINARY.relative_to(ROOT)), 'binarySha256': sha(BINARY),
              'binaryBytes': BINARY.stat().st_size, 'libmgbaVersion': '0.10.5',
              'libmgbaSourceTag': '26b7884bc25a5933960f3cdcd98bac1ae14d42e2',
              'libmgbaSha256': sha(LIB / 'lib/libmgba.so.0.10.5'), 'host': platform.platform()},
    'settings': {'sampleRate': 48000, 'channels': 2, 'pcmBits': 16, 'audioBufferCapacity': 8192,
                 'freshEmulatorPerRun': True, 'warmupFrames': 60, 'captureEmulatedFrames': 480,
                 'actualCapturedPcmFrames': 385752, 'actualCaptureSeconds': 385752 / 48000,
                 'm4aSongNumStartEntrypoint': '0x081dd0f5', 'songTableAddress': '0x084a32cc',
                 'bootstrapStereoArgument': 1, 'mGbaHostVolume': 256,
                 'originalPcmMasterVolume': 12, 'originalPcmMaxChannels': 5,
                 'originalPcmFrequency': 13379, 'originalPcmSamplesPerVBlank': 224,
                 'initialGlobalReverb': 0, 'songGlobalReverb': 50,
                 'gainFit': False, 'timeStretch': False, 'phaseChasing': False},
    'methodNotes': [
        'Only IDs4,241,291,314,298 and exactly480 capture frames are admitted by the probe. The linked original table entries, header track/priority/reverb/voicegroup data and track pointers are checked before WAV creation.',
        'The fixed pack bootstrap calls original m4aSoundInit and SetPokemonCryStereo(1), then polls SoundVSync and SoundMain as in earlier references. No cry setup calls, custom master volume, gain compensation or new original-driver settings were introduced.',
        'm4aSongNumStart chooses each original song-table player. Every process starts fresh, so SE are standalone and BGM/SE concurrency is outside this sample. All selected headers set original global reverb50.',
        'Stripped: only pack segments in a zero-filled16MiB ROM image. Full control: fixed baseline ROM with the same pack/bootstrap overlaid only in memory. Unguarded control: same stripped run with read/branch guard hooks disabled. All three output WAVs match byte-for-byte for every song.',
        'ROM guards cover CPU data loads and active-region changes, not every internal DMA fast path/sequential instruction fetch. Passing guards is empirical resource-closure evidence, not a proof for arbitrary songs.',
        '480 emulated capture frames produce385752 PCM frames here, including onset/buffering. This is8.0365s rather than an exact8s file. Compare a shared overlap after one fixed onset alignment; preserve original gain and do not chase PSG phase or noise samples.',
        'These are isolated original-driver references, not full-game scheduling or physical GBA output. Standalone native comparisons and subjective/device acceptance remain separate.',
        'CPU seconds/realtime factors cover the capture loop, synchronous PCM writes and optional guards. Whole-process wall seconds also include boot/setup. Raw getrusage maxRSS can inherit runner high-water state; /proc VmRSS/VmHWM is a separate pre-teardown process snapshot, not native-runtime memory usage.'
    ],
    'renders': renders, 'negativeTests': negative,
}
(OUT / 'reference-metadata.json').write_text(json.dumps(metadata, indent=2) + '\n')
print('Wrote', OUT / 'reference-metadata.json')
