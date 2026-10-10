#!/usr/bin/env python3
"""Read-only, fixed FireRed Charmander-cry audit from pret source/ROM/assets.

The format equations here were derived from src/m4a_1.s SoundMainRAM_Unk2;
no third-party audio-player implementation is used. Does not render audio.
"""
import argparse
import hashlib
import json
import pathlib
import re
import struct
import wave

ROOT = pathlib.Path(__file__).resolve().parents[1]
P = argparse.ArgumentParser(description=__doc__)
P.add_argument('--reference', type=pathlib.Path,
               default=ROOT.parent / 'audio-reference-build/pokefirered')
P.add_argument('--out', type=pathlib.Path, default=ROOT / 'specs/phase3-cry-inventory.json')
args = P.parse_args()
ref = args.reference
rom = (ref / 'pokefirered.gba').read_bytes()
assert hashlib.sha1(rom).hexdigest() == '41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc', \
    'This inventory accepts only the fixed matching FireRed ROM'
link_map = (ref / 'pokefirered.map').read_text()

def symbol(name):
    result = re.search(r'0x([0-9a-f]{8})\s+' + re.escape(name) + r'(?:\s*=\s*\.)?\s*$',
                       link_map, re.M)
    if not result:
        raise ValueError('Symbol absent: ' + name)
    return int(result[1], 16)

def read(address, count):
    offset = address - 0x08000000
    if offset < 0 or count < 0 or offset + count > len(rom):
        raise ValueError('Out-of-bounds ROM read')
    return rom[offset:offset + count]

def sha(data):
    return hashlib.sha256(data).hexdigest()

DELTA = [0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1]

def decode(payload, samples):
    """Decode only requested indices; never read padding of a partial block."""
    output = bytearray()
    value = 0
    for index in range(samples):
        block, within = divmod(index, 64)
        base = block * 33
        if within == 0:
            if base >= len(payload):
                raise ValueError('Truncated DPCM block seed')
            value = payload[base]
        else:
            position = base + 1 + within // 2
            if position >= len(payload):
                raise ValueError('Truncated DPCM delta byte')
            packed = payload[position]
            nibble = packed & 15 if within & 1 else packed >> 4
            value = (value + DELTA[nibble]) & 255
        output.append(value)
    return bytes(output)

cry_table = symbol('gCryTable')
tone_address = cry_table + 3 * 12
assert tone_address == 0x0848C938
tone_bytes = read(tone_address, 12)
type_, key, length, pan, wave_address, attack, decay, sustain, release = \
    struct.unpack('<4BI4B', tone_bytes)
assert (type_, key, length, pan, wave_address, attack, decay, sustain, release) == \
    (0x20, 60, 0, 0, 0x08513038, 255, 0, 255, 0)
wave_type, wave_status, frequency_word, loop_start, decoded_count = \
    struct.unpack('<HHIII', read(wave_address, 16))
assert (wave_type, wave_status, frequency_word, loop_start, decoded_count) == \
    (1, 0, 10764288, 0, 7133)
asset_path = ref / 'sound/direct_sound_samples/cries/charmander.bin'
asset = asset_path.read_bytes()
assert len(asset) == 3695
assert read(wave_address, len(asset)) == asset
payload = asset[16:]
assert len(payload) == 3679
decoded = decode(payload, decoded_count + 1)
source_wav_path = ref / 'sound/direct_sound_samples/cries/charmander.wav'
with wave.open(str(source_wav_path), 'rb') as source:
    assert source.getnchannels() == 1 and source.getsampwidth() == 1
    assert source.getframerate() == 10512
    source_sample_count = source.getnframes()
    assert source_sample_count == 7134
    expected = bytes((sample - 128) & 255 for sample in source.readframes(source_sample_count))
assert decoded == expected, 'Decoded samples differ from source WAV'
assert decoded[-1] == 254  # signed -2, interpolation guard, not playable sample 7134
try:
    decode(payload[:-1], decoded_count + 1)
except ValueError:
    truncated_payload_rejected = True
else:
    raise AssertionError('Truncated payload was accepted')

original_template = read(symbol('gPokemonCrySongTemplate'), 52)
# Demonstration addresses are the actual first original cry-slot allocation.
# A native player can relocate these into its own bounded, separate address domain.
slot_base = symbol('gPokemonCrySongs')
track_owner = symbol('gPokemonCryTracks')
assert slot_base == 0x03006F00 and track_owner == 0x03007180
patched = bytearray(original_template)
patched[0] = 1     # chorus 0: one active track
patched[2] = 10    # priority
struct.pack_into('<I', patched, 4, tone_address)
struct.pack_into('<I', patched, 8, slot_base + 0x11)
struct.pack_into('<I', patched, 12, slot_base + 0x18)
struct.pack_into('<I', patched, 0x14, slot_base + 0x1A)
patched[0x1D] = 120 & 127
patched[0x28] = (0 + 64) & 127
pitch_b = 15360 + 0x80
old_chorus_delta = (patched[0x19] - patched[0x12]) & 255
patched[0x2A] = (pitch_b >> 8) & 127
patched[0x12] = (pitch_b >> 1) & 127
patched[0x19] = (old_chorus_delta + ((pitch_b >> 1) & 127)) & 127
struct.pack_into('<H', patched, 0x2E, 140)
patched[0x26] = 0
struct.pack_into('<I', patched, 0x20, 0)
assert patched[0x12] == 64 and patched[0x2A] == 60
signed = lambda b: b if b < 128 else b - 256
result = {
    'provenance': {
        'source_repository': 'pret/pokefirered',
        'fixed_revision': '037335f4c725d7c9aecdac87066f2002b4bd7e14',
        'rom_sha1': hashlib.sha1(rom).hexdigest(),
        'decoder_source': 'src/m4a_1.s:SoundMainRAM_Unk2',
        'source_wav_sha256': sha(source_wav_path.read_bytes()),
        'third_party_player_source_used': False,
    },
    'tone': {'address': hex(tone_address), 'raw_hex': tone_bytes.hex(' '),
             'type': type_, 'key': key, 'length': length, 'pan_sweep': pan,
             'wave_pointer': hex(wave_address), 'adsr': [attack, decay, sustain, release],
             'compressed': True, 'fixed_rate': False, 'reversed': False},
    'wave': {'address': hex(wave_address), 'type': wave_type, 'status': wave_status,
             'frequency_word': frequency_word, 'c60_hz': frequency_word / 1024,
             'loop_start': loop_start, 'looped': bool(wave_status & 0xC000),
             'decoded_playable_samples': decoded_count,
             'decoded_samples_with_guard': len(decoded), 'guard_signed_value': signed(decoded[-1]),
             'encoded_payload_bytes': len(payload), 'header_and_payload_bytes': len(asset),
             'rom_span_with_next_alignment': symbol('Cry_Charmeleon') - wave_address,
             'complete_blocks': 111, 'last_partial_block_encoded_bytes': 16,
             'last_partial_block_playable_samples': 29, 'last_partial_block_with_guard_samples': 30,
             'asset_sha256': sha(asset), 'payload_sha256': sha(payload),
             'decoded_playable_sha256': sha(decoded[:decoded_count]),
             'decoded_with_guard_sha256': sha(decoded),
             'first64_signed': [signed(v) for v in decoded[:64]],
             'last16_including_guard_signed': [signed(v) for v in decoded[-16:]]},
    'dynamic_song': {'original_template_address': hex(symbol('gPokemonCrySongTemplate')),
                     'original_template_size': 52, 'original_template_hex': original_template.hex(' '),
                     'example_actual_slot_base': hex(slot_base), 'original_track0_owner': hex(track_owner),
                     'patched_template_hex': patched.hex(' '),
                     'volume': 120, 'pan': 0, 'pitch': 15360, 'tie_key': 60, 'tune_command_byte': 64,
                     'tie_velocity': 127, 'length_ticks': 140, 'release': 0, 'progress': 0,
                     'chorus': 0, 'priority': 10, 'stereo': 1,
                     'note_on_tick': 0, 'eot_and_fine_tick': 140,
                     'score_seconds_at_gba_clock': 140 * 280896 / 16777216,
                     'nominal_sample_seconds': decoded_count / (frequency_word / 1024)},
    'validation': {'rom_asset_identical': True, 'original_wav_all_samples_identical': True,
                   'compared_sample_count': len(decoded), 'differing_sample_count': 0,
                   'truncated_payload_rejected': truncated_payload_rejected,
                   'decoded_tail_is_bounded': True},
}
args.out.parent.mkdir(parents=True, exist_ok=True)
args.out.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'out': str(args.out), 'validation': result['validation'],
                  'wave': {k: result['wave'][k] for k in ['encoded_payload_bytes', 'decoded_playable_samples',
                            'decoded_samples_with_guard', 'decoded_with_guard_sha256']}}, indent=2))
