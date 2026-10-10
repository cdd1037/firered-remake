#!/usr/bin/env python3
"""Offline development reference renders; existing GCC/libmGBA, no downloads.

Run from any directory. Inputs are cryptographically pinned. The original
source, ROM and audio pack are read-only; binary and PCM files are gitignored.
This does not render the C# player or form part of its runtime dependency set.
"""
import hashlib
import json
import pathlib
import platform
import re
import shlex
import struct
import subprocess
import time
import wave

ROOT = pathlib.Path(__file__).resolve().parents[1]
REF = ROOT.parent / 'audio-reference-build/pokefirered'
STUDY = ROOT.parent / 'runtime-audio-study'
LIB = ROOT.parent / 'audio-reference-build/emulator/libmgba-gcc-local'
OUT = ROOT / 'results/phase4'
OUT.mkdir(parents=True, exist_ok=True)
ROM = REF / 'pokefirered.gba'
PACK = STUDY / 'audio.pack'
PROBE = ROOT / 'tools/phase4-reference-probe.c'
BINARY = OUT / 'reference-probe'


def sha(path, algorithm='sha256'):
    return hashlib.new(algorithm, path.read_bytes()).hexdigest()


def command(args):
    return shlex.join([str(arg) for arg in args])


before = {'romSha256': sha(ROM), 'packSha256': sha(PACK),
          'originalProbeSha256': sha(STUDY / 'probe.c')}
assert sha(ROM, 'sha1') == '41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc'
assert before['packSha256'] == '595af58c91458c6a57080cc4f0066caf0c42da84665b7476b3de45e38ccfd065'
assert before['originalProbeSha256'] == '2adb12d7bf0132b4f2ec51d2b70e39ed22aea190681d5760e8334bb13df71324'
compiler = subprocess.check_output(['gcc', '--version'], text=True).splitlines()[0]
compile_args = ['gcc', '-O2', '-Wall', '-Wextra', '-Werror', '-D_GNU_SOURCE',
                '-I' + str(LIB / 'include'), str(PROBE), '-L' + str(LIB / 'lib'),
                '-Wl,-rpath,' + str(LIB / 'lib'), '-lmgba', '-lm', '-o', str(BINARY)]
subprocess.run(compile_args, check=True)
rom = ROM.read_bytes()
constants = (REF / 'include/constants/species.h').read_text()
cry_source = (REF / 'sound/cry_tables.inc').read_text().split('gCryTable_Reverse')[0]
cry_names = re.findall(r'^\s*cry\s+(Cry_\w+)', cry_source, re.M)
assert len(cry_names) == 388


def invoke(name, tone, full=False):
    path = OUT / ('reference-' + name + ('-full-control' if full else '') + '.wav')
    args = [str(BINARY), str(PACK), str(ROM) if full else '-', str(path), hex(tone), '240']
    started = time.perf_counter()
    result = subprocess.run(args, capture_output=True, text=True, check=True)
    elapsed = time.perf_counter() - started
    metrics = json.loads(result.stdout)
    assert metrics['mailbox'] == 0 and metrics['frames'] == 192879
    assert metrics['romChecks'] > 0 and metrics['emulatedFrames'] == 240
    with wave.open(str(path), 'rb') as f:
        assert f.getnchannels() == 2 and f.getsampwidth() == 2 and f.getframerate() == 48000
        pcm = f.readframes(f.getnframes())
        assert f.getnframes() == metrics['frames']
    return {'path': str(path.relative_to(ROOT)), 'command': command(args),
            'sha256': sha(path), 'pcmSha256': hashlib.sha256(pcm).hexdigest(),
            'byteCount': path.stat().st_size, 'wholeProcessWallSeconds': elapsed,
            'stdoutMetrics': metrics, 'stderr': result.stderr.strip()}


renders = []
for name in ['Bulbasaur', 'Pikachu', 'Mewtwo', 'Treecko', 'Wailord', 'Deoxys']:
    index = cry_names.index('Cry_' + name)
    tone = 0x0848c914 + index * 12
    type_, key, length, pan, sample, attack, decay, sustain, release = struct.unpack_from('<4BI4B', rom, tone - 0x08000000)
    wt, ws, frequency, loop, count = struct.unpack_from('<HHIII', rom, sample - 0x08000000)
    assert (type_, key, length, pan, attack, decay, sustain, release) == (0x20, 60, 0, 0, 255, 0, 255, 0)
    assert (wt, ws, frequency, loop) == (1, 0, 10764288, 0)
    species = int(re.search(r'^#define SPECIES_' + name.upper() + r'\s+(\d+)\b', constants, re.M)[1])
    stripped = invoke(name.lower(), tone)
    full = invoke(name.lower(), tone, full=True)
    assert stripped['sha256'] == full['sha256'], 'stripped/full reference mismatch'
    renders.append({'speciesId': species, 'name': name, 'cryIndex': index,
                    'toneAddress': hex(tone), 'sampleAddress': hex(sample),
                    'toneType': hex(type_), 'sourcePlaybackHz': frequency / 1024,
                    'decodedPlayableSamples': count,
                    'nominalUnfilteredSourceSeconds': count / (frequency / 1024),
                    'stripped': stripped, 'fullDataControl': full,
                    'strippedEqualsFullDataControl': True})
    print(name, stripped['sha256'], 'stripped/full exact', flush=True)

# Same inputs as phase3 must retain its exact existing reference WAV.
charmander = invoke('charmander-check', 0x0848c938)
assert charmander['sha256'] == sha(STUDY / 'cry-stripped.wav')

# Invalid addresses/counts fail before any WAV is created.
negative = []
for label, tone, frames in [
    ('reverse-table', '0x0848db44', '240'),
    ('unaligned-tone', '0x0848c915', '240'),
    ('outside-rom', '0xffffffff', '240'),
    ('malformed-tone', '0x848c914junk', '240'),
    ('zero-duration', '0x0848c914', '0'),
    ('unbounded-duration', '0x0848c914', '3601')]:
    output = OUT / ('reference-invalid-' + label + '.wav')
    assert not output.exists(), 'Unexpected stale negative-test output'
    args = [str(BINARY), str(PACK), '-', str(output), tone, frames]
    test = subprocess.run(args, capture_output=True, text=True)
    assert test.returncode == 2 and not output.exists()
    negative.append({'label': label, 'command': command(args),
                     'exitCode': test.returncode, 'stderr': test.stderr.strip(),
                     'createdOutputFile': False})

after = {'romSha256': sha(ROM), 'packSha256': sha(PACK),
         'originalProbeSha256': sha(STUDY / 'probe.c')}
assert before == after
metadata = {
    'scope': 'Developer-only six sampled ordinary normal cries; original fixed driver in existing mGBA. No runtime C# edits, downloads, installations, agbplay or third-party-player implementation.',
    'source': {'repository': 'pret/pokefirered',
               'fixedRevision': '037335f4c725d7c9aecdac87066f2002b4bd7e14',
               'romSha1': sha(ROM, 'sha1'), 'inputsBefore': before, 'inputsAfter': after,
               'unchangedInputs': before == after,
               'originalProbe': str(STUDY / 'probe.c'),
               'normalTableAddress': '0x0848c914', 'normalTableEndExclusive': '0x0848db44',
               'normalRecordBytes': 12, 'normalTableEntries': 388},
    'build': {'compiler': compiler, 'command': command(compile_args),
              'sourcePath': str(PROBE.relative_to(ROOT)), 'sourceSha256': sha(PROBE),
              'scriptPath': 'tools/phase4_reference_sampling.py',
              'scriptSha256': sha(pathlib.Path(__file__)),
              'binaryPath': str(BINARY.relative_to(ROOT)), 'binarySha256': sha(BINARY),
              'binaryBytes': BINARY.stat().st_size,
              'libmgbaVersion': '0.10.5', 'libmgbaSourceTag': '26b7884bc25a5933960f3cdcd98bac1ae14d42e2',
              'libmgbaSha256': sha(LIB / 'lib/libmgba.so.0.10.5'),
              'host': platform.platform()},
    'settings': {'sampleRate': 48000, 'channels': 2, 'pcmBits': 16,
                 'audioBufferCapacity': 8192, 'emulatedFrames': 240,
                 'warmupFrames': 60, 'normalParameterInvocationFrames': 8,
                 'volume': 120, 'pan': 0, 'pitch': 15360, 'lengthTicks': 140,
                 'release': 0, 'progress': 0, 'chorus': 0, 'priority': 10,
                 'stereo': True, 'freshGlobalReverb': 0, 'romClosureGuards': True,
                 'setPokemonCryToneEntrypoint': '0x081de639'},
    'methodNotes': [
        'ROM/pack inputs are hash-pinned before use and hashed again afterward. Each selected tone is validated as an aligned fixed normal-table record with the expected compressed normal tone/wave headers before output creation.',
        'Stripped uses only original audio.pack segments in a zero-filled 16 MiB image. Full-data controls read the fixed original ROM and overlay the same pack/bootstrap only in process memory. Both use the original driver.',
        'This isolated bootstrap is an original-driver reference, not full-game scheduling/hardware-bit-perfect validation. No game-level BGM ducking is involved in these isolated cries.',
        'Guard hooks cover CPU loads and active-region changes, not every internal DMA fast path or sequential instruction fetch; they are empirical closure evidence, not a proof for all uses.',
        'Some original cry assets do not physically store an interpolation guard sample. The original driver may read surrounding ROM padding; bounded native decoder tail handling is separately audited. No native/reference equality is assumed.',
        'Capture audio length is 4.0183125 s (240 emulated frames). This includes onset delay, the 140-tick sequence wait and silence. Nominal source duration is reported separately.',
        'CPU seconds and realtime factor measure only the capture loop, including synchronous WAV data writes and ROM guard overhead. Whole-process wall time includes boot and setup.',
        'Linux getrusage maxRssKiB is raw diagnostic logging and can reflect inherited runner high-water state; it is not accepted as isolated engine-memory usage. procVmRssKiB and procVmHwmKiB are a separate /proc/self/status snapshot before teardown. These are host reference-tool metrics, not native runtime/device performance.'
    ],
    'renders': renders,
    'phase3RollbackReference': {'existingPath': str(STUDY / 'cry-stripped.wav'),
                                'existingSha256': sha(STUDY / 'cry-stripped.wav'),
                                'newRender': charmander, 'byteIdentical': True},
    'negativeTests': negative,
}
(OUT / 'reference-metadata.json').write_text(json.dumps(metadata, indent=2) + '\n')
print('Wrote', OUT / 'reference-metadata.json')
