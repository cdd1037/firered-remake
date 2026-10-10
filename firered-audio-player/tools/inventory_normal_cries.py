#!/usr/bin/env python3
"""Audit fixed FireRed ordinary cry mapping and bounded source-asset decoding.

Reads only pret source, matching ELF/map/ROM, and original .bin/.wav assets.
No audio rendering or third-party player implementation is used. Output is a
research manifest, not runtime data. Only Python's standard library is needed.
"""
import argparse
import hashlib
import json
import pathlib
import re
import struct
import subprocess
import wave

ROOT = pathlib.Path(__file__).resolve().parents[1]
REVISION = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
ROM_SHA1 = '41cb23d8dccc8ebd7c649cd8fbb58eeace6e2fdc'
DELTA = [0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def decode(payload, samples):
    """Forward MP2K DPCM; no implicit padding or reads beyond the asset."""
    output = bytearray()
    value = 0
    for index in range(samples):
        block, within = divmod(index, 64)
        base = block * 33
        position = base if within == 0 else base + 1 + within // 2
        if position >= len(payload):
            raise ValueError(f'Truncated DPCM at decoded index {index}')
        if within == 0:
            value = payload[position]
        else:
            packed = payload[position]
            nibble = packed & 15 if within & 1 else packed >> 4
            value = (value + DELTA[nibble]) & 255
        output.append(value)
    return bytes(output)


def elf_symbols(path):
    """Read ELF32 little-endian symbols, including static C mapping arrays."""
    data = path.read_bytes()
    assert data[:6] == b'\x7fELF\x01\x01'
    section_offset = struct.unpack_from('<I', data, 32)[0]
    section_size, section_count = struct.unpack_from('<HH', data, 46)
    sections = [struct.unpack_from('<10I', data, section_offset + i * section_size)
                for i in range(section_count)]
    result = {}
    for section in sections:
        if section[1] != 2:  # SHT_SYMTAB
            continue
        strings_section = sections[section[6]]
        strings = data[strings_section[4]:strings_section[4] + strings_section[5]]
        for offset in range(section[4], section[4] + section[5], section[9]):
            name_offset, value, size, _, _, _ = struct.unpack_from('<IIIBBH', data, offset)
            name = strings[name_offset:strings.find(b'\0', name_offset)].decode('ascii')
            if name:
                result[name] = (value, size)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=pathlib.Path,
                        default=ROOT.parent / 'audio-reference-build/pokefirered')
    parser.add_argument('--out', type=pathlib.Path,
                        default=ROOT / 'results/phase4/inventory.json')
    args = parser.parse_args()
    ref = args.reference.resolve()
    revision = subprocess.check_output(['git', '-C', str(ref), 'rev-parse', 'HEAD'], text=True).strip()
    assert revision == REVISION, 'Unexpected reference revision'
    rom = (ref / 'pokefirered.gba').read_bytes()
    assert hashlib.sha1(rom).hexdigest() == ROM_SHA1, 'Unexpected reference ROM'
    link_map = (ref / 'pokefirered.map').read_text()
    elf = elf_symbols(ref / 'pokefirered.elf')

    def symbol(name):
        found = re.search(r'0x([0-9a-f]{8})\s+' + re.escape(name) + r'\s*$', link_map, re.M)
        assert found, f'Missing link-map symbol: {name}'
        address = int(found[1], 16)
        assert elf[name][0] == address, f'ELF/map disagree: {name}'
        return address

    def read(address, count):
        offset = address - 0x08000000
        assert 0 <= offset <= len(rom) and 0 <= count <= len(rom) - offset
        return rom[offset:offset + count]

    species_source = (ref / 'include/constants/species.h').read_text()
    species_constants = {name: int(value) for name, value in re.findall(
        r'^#define SPECIES_(\w+)\s+(\d+)\s*$', species_source, re.M)}
    ordinary = {value: name for name, value in species_constants.items()
                if 1 <= value <= 411 and not name.startswith('OLD_UNOWN_')}
    assert set(ordinary) == set(range(1, 252)) | set(range(277, 412))
    national_source = (ref / 'include/constants/pokedex.h').read_text().split('enum {', 1)[1].split('};', 1)[0]
    national_names = re.findall(r'\bNATIONAL_DEX_(\w+)\s*,', national_source)
    national_ids = {name: index for index, name in enumerate(national_names)}
    assert national_ids['DEOXYS'] == 386
    national_addr, national_bytes = elf['sSpeciesToNationalPokedexNum']
    assert national_bytes == 411 * 2
    national_rom = struct.unpack('<411H', read(national_addr, national_bytes))
    for internal_id, name in ordinary.items():
        assert national_rom[internal_id - 1] == national_ids[name]
    assert {national_ids[name] for name in ordinary.values()} == set(range(1, 387))

    cry_constants = {name: int(value) for name, value in re.findall(
        r'\bCRY_(\w+)\s*=\s*(\d+)', (ref / 'include/constants/hoenn_cries.h').read_text())}
    mapping_source = (ref / 'src/data/pokemon/cry_ids.h').read_text()
    hoenn_map = {species_constants[name]: cry_constants[cry] for name, cry in re.findall(
        r'\[SPECIES_(\w+) - HOENN_MON_SPECIES_START\]\s*=\s*CRY_(\w+)', mapping_source)}
    assert set(hoenn_map) == set(range(277, 412))
    mapping_addr, mapping_bytes = elf['sHoennSpeciesIdToCryId']
    assert mapping_bytes == 135 * 2
    assert struct.unpack('<135H', read(mapping_addr, mapping_bytes)) == tuple(
        hoenn_map[i] for i in range(277, 412))

    cry_table = symbol('gCryTable')
    table_source = (ref / 'sound/cry_tables.inc').read_text()
    table_names = re.findall(r'^\s*cry (\w+)\s*$', table_source, re.M)
    assert len(table_names) == 388 and len(set(table_names)) == 388
    assert symbol('gCryTable_Reverse') - cry_table == len(table_names) * 12
    data_source = (ref / 'sound/direct_sound_data.inc').read_text()
    # Every relevant physical extent comes from a specific .incbin file. The
    # next source declaration only verifies alignment, never determines length.
    declarations = list(re.finditer(r'(\w+)::\s*\.incbin "([^"]+)"', data_source))
    declarations_by_name = {match[1]: (index, match) for index, match in enumerate(declarations)}
    assets = []
    for cry_index, name in enumerate(table_names):
        tone_address = cry_table + cry_index * 12
        tone = read(tone_address, 12)
        type_, key, length, pan, address, attack, decay, sustain, release = struct.unpack('<4BI4B', tone)
        assert (type_, key, length, pan, attack, decay, sustain, release) == (32, 60, 0, 0, 255, 0, 255, 0)
        assert address == symbol(name)
        declaration_index, declaration = declarations_by_name[name]
        path = pathlib.Path(declaration[2])
        asset = (ref / path).read_bytes()
        assert len(asset) >= 17 and read(address, len(asset)) == asset
        wave_type, status, frequency, loop_start, sample_count = struct.unpack('<HHIII', asset[:16])
        assert (wave_type, status, frequency, loop_start) == (1, 0, 10512 * 1024, 0)
        payload = asset[16:]
        next_declaration = declarations[declaration_index + 1]
        next_address = symbol(next_declaration[1])
        source_gap = data_source[declaration.end():next_declaration.start()].strip()
        assert source_gap == '.align 2', f'Unexpected physical layout after {name}: {source_gap}'
        alignment_bytes = (-len(asset)) % 4
        assert next_address == address + len(asset) + alignment_bytes
        assert read(address + len(asset), alignment_bytes) == bytes(alignment_bytes)
        with wave.open(str((ref / path).with_suffix('.wav')), 'rb') as source:
            assert (source.getnchannels(), source.getsampwidth(), source.getframerate()) == (1, 1, 10512)
            wav_frames = source.getnframes()
            assert wav_frames == sample_count + 1
            expected = bytes((value - 128) & 255 for value in source.readframes(wav_frames))
        decoded_playable = decode(payload, sample_count)
        assert decoded_playable == expected[:sample_count], f'Playable WAV mismatch: {name}'
        boundary = None
        try:
            decoded = decode(payload, sample_count + 1)
            guard_stored = True
            guard_policy = 'encoded_in_payload'
        except ValueError:
            # The final guard sits at an even within-block index: its high
            # nibble was staged but never emitted by wav2agb --no-pad. Prove
            # both the source guard and the ORIGINAL ROM's consumed nibble.
            block, within = divmod(sample_count, 64)
            assert within > 0 and within % 2 == 0
            assert block * 33 + 1 + within // 2 == len(payload)
            assert expected[-1] == decoded_playable[-1]
            if alignment_bytes:
                boundary_byte = 0
                boundary_kind = 'source_declared_alignment_zero'
            else:
                next_asset = (ref / next_declaration[2]).read_bytes()
                assert read(next_address, len(next_asset)) == next_asset
                boundary_byte = next_asset[0]
                boundary_kind = 'next_source_incbin_header'
            assert boundary_byte in (0, 1) and DELTA[boundary_byte >> 4] == 0
            assert read(address + len(asset), 1)[0] == boundary_byte
            decoded = decoded_playable + decoded_playable[-1:]
            guard_stored = False
            guard_policy = 'repeat_last_playable_verified_against_source_wav_and_rom_layout'
            boundary = {'kind': boundary_kind, 'byte': boundary_byte,
                        'address': address + len(asset), 'nextSymbol': next_declaration[1]}
        assert decoded == expected, f'Guard/source WAV mismatch: {name}'
        required_samples = sample_count + int(guard_stored)
        try:
            decode(payload[:-1], required_samples)
        except ValueError:
            pass
        else:
            raise AssertionError(f'One-byte-truncated payload accepted: {name}')
        assets.append({
            'symbol': name, 'cryIndex': cry_index, 'toneAddress': tone_address,
            'toneHex': tone.hex(' '), 'type': type_, 'key': key, 'adsr': [attack, decay, sustain, release],
            'waveAddress': address, 'waveType': wave_type, 'waveStatus': status,
            'frequencyWord': frequency, 'sampleRate': 10512, 'loopStart': loop_start,
            'sampleCount': sample_count, 'sourceWavFrames': wav_frames,
            'payloadBytes': len(payload), 'assetBytes': len(asset), 'headerBytes': 16,
            'assetEndExclusive': address + len(asset), 'alignmentBytesAfter': alignment_bytes,
            'nextSymbol': next_declaration[1], 'nextSymbolAddress': next_address,
            'sourceAsset': path.as_posix(), 'sourceWav': path.with_suffix('.wav').as_posix(),
            'sourceIncbinLine': data_source.count('\n', 0, declaration.start()) + 1,
            'guardStored': guard_stored, 'guardSignedValue': decoded[-1] if decoded[-1] < 128 else decoded[-1] - 256,
            'reconstructedGuardPolicy': guard_policy, 'originalGuardBoundary': boundary,
            'nominalSeconds': sample_count / 10512,
            'assetSha256': sha(asset), 'payloadSha256': sha(payload),
            'sourceWavSha256': sha((ref / path).with_suffix('.wav').read_bytes()),
            'decodedPlayableSha256': sha(decoded_playable), 'decodedWithGuardSha256': sha(decoded),
            'validation': {'romAssetIdentical': True, 'sourceWavAllSamplesIdentical': True,
                           'comparedSamples': len(decoded), 'differingSamples': 0,
                           'oneByteTruncatedPayloadRejected': True}
        })

    species = []
    for internal_id, constant_name in sorted(ordinary.items()):
        cry_index = internal_id - 1 if internal_id <= 251 else hoenn_map[internal_id]
        asset = assets[cry_index]
        assert asset['symbol'][4:].replace('_', '').upper() == constant_name.replace('_', '')
        species.append({
            'internalId': internal_id, 'name': asset['symbol'][4:],
            'speciesConstant': 'SPECIES_' + constant_name, 'nationalDexId': national_ids[constant_name],
            **{key: asset[key] for key in ('cryIndex', 'toneAddress', 'waveAddress', 'payloadBytes',
               'sampleCount', 'type', 'guardStored', 'reconstructedGuardPolicy', 'guardSignedValue',
               'frequencyWord', 'sampleRate', 'assetBytes', 'sourceAsset', 'sourceWav', 'nominalSeconds')}
        })
    used = {row['cryIndex'] for row in species}
    assert len(species) == len(used) == 386
    assert len({row['waveAddress'] for row in species}) == 386
    unused = [{'cryIndex': index, 'symbol': table_names[index]} for index in range(388) if index not in used]
    assert unused == [{'cryIndex': 265, 'symbol': 'Cry_Unused265'}, {'cryIndex': 268, 'symbol': 'Cry_Unused268'}]
    for asset in assets:
        asset['ordinarySpecies'] = asset['cryIndex'] in used
    missing_guards = sum(not row['guardStored'] for row in species)
    assert missing_guards == 190
    shortest = min(species, key=lambda row: row['sampleCount'])
    longest = max(species, key=lambda row: row['sampleCount'])
    result = {
        'schemaVersion': 1,
        'provenance': {'repository': 'pret/pokefirered', 'fixedRevision': revision, 'romSha1': ROM_SHA1,
                       'thirdPartyPlayerSourceUsed': False, 'runtimeModified': False,
                       'sourceWavComparisonScope': 'asset decoder and verified interpolation guard; not synthesized output accuracy'},
        'summary': {'ordinarySpeciesCount': len(species), 'internalSpeciesSlotCountExcludingNoneAndEgg': 411,
                    'oldUnownPlaceholderCount': 25, 'cryTableEntries': len(assets),
                    'ordinaryDistinctCryIndices': len(used), 'ordinaryUniquePhysicalAssets': len(used),
                    'ordinaryStoredGuards': len(species) - missing_guards,
                    'ordinaryReconstructedGuards': missing_guards,
                    'ordinaryToneTypes': sorted({row['type'] for row in species}),
                    'ordinarySampleRates': sorted({row['sampleRate'] for row in species}),
                    'allTableReconstructedGuards': sum(not asset['guardStored'] for asset in assets),
                    'ordinaryPlayableSamplesCompared': sum(row['sampleCount'] for row in species),
                    'ordinarySamplesWithGuardsCompared': sum(row['sampleCount'] + 1 for row in species),
                    'allTableSamplesWithGuardsCompared': sum(asset['sourceWavFrames'] for asset in assets),
                    'differingSamples': 0, 'unusedCryEntries': unused,
                    'shortest': {key: shortest[key] for key in ('name', 'internalId', 'sampleCount', 'nominalSeconds')},
                    'longest': {key: longest[key] for key in ('name', 'internalId', 'sampleCount', 'nominalSeconds')}},
        'symbols': {'gCryTable': cry_table, 'gCryTableReverse': symbol('gCryTable_Reverse'),
                    'sHoennSpeciesIdToCryId': mapping_addr, 'sSpeciesToNationalPokedexNum': national_addr,
                    'gPokemonCrySongTemplate': symbol('gPokemonCrySongTemplate')},
        'normalParameters': {'mode': 0, 'volume': 120, 'pan': 0, 'pitch': 15360, 'lengthTicks': 140,
                             'release': 0, 'progress': 0, 'chorus': 0, 'priority': 10,
                             'reverse': False, 'trackCount': 1, 'stereoForResearch': 1,
                             'panIsCallerSupplied': True, 'stereoIsGlobalNotSpeciesParameter': True},
        'excludedInputs': {'none': 0, 'oldUnownInternalIds': [252, 276], 'egg': 412,
                           'additionalUnownFormIds': [413, 439], 'oldUnownCryIndex': 200,
                           'notes': '386 ordinary species only. Do not pass NONE, EGG, or form IDs directly through unchecked SpeciesToCryId.'},
        'species': species,
        'assets': assets,
    }
    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(result, indent=2) + '\n')
    print(json.dumps({'out': str(args.out), 'summary': result['summary']}, indent=2))


if __name__ == '__main__':
    main()
