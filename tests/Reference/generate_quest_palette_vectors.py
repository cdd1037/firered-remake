"""Extract pinned RGB555 Quest Log tint; emit small exhaustive SHA-256 fixtures.

No upstream scripts execute.  Each digest covers all 32768 ascending RGB555
colors, serialized explicitly as little-endian uint16, for one coefficient.
The source's u32 gray intermediate is preserved.  RGB2 casts operands to u32
before shifting to avoid signed-left-shift UB on portable host C; the final
uint16 bits are unchanged.  Every output is also checked against an independent
signed-floor formulation, and coefficient 16 against the extracted grayscale.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

from reference_c import COMMIT, compile_c, verify_reference


HASHES = {
    'src/palette.c': 'd406f56ba0a815d184753396e7e05120921c651a7bb2f5efff642816e64e7dc6',
    'include/global.h': '044325b90e7c79146cc0cf73d31b6be3e4840059e2e778b63d9b5448fe538429',
    'include/constants/rgb.h': 'e4b500db58dceafbd1eaa196fad0670f485ab2283a49ab5a295e94535d0833eb',
}


def extract_function(source, name):
    match = re.search(r'^void ' + re.escape(name) + r'\([^;]+?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing extracted function: ' + name)
    start, end, depth = match.start(), match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


HEADER = r'''
#include <stdint.h>
#include <stdio.h>
#ifdef _WIN32
#include <fcntl.h>
#include <io.h>
#endif
typedef uint8_t u8;
typedef uint16_t u16;
typedef int16_t s16;
typedef int32_t s32;
typedef uint32_t u32;
#define RGB2(r,g,b) (((u32)(b)<<10)|((u32)(g)<<5)|(u32)(r))
'''

MAIN = r'''
static int floor_div_16(int n) { return n >= 0 ? n / 16 : -((-n + 15) / 16); }
int main(void)
{
    unsigned tone, color;
#ifdef _WIN32
    if (_setmode(_fileno(stdout), _O_BINARY) == -1) return 10;
#endif
    if (Q_8_8(0.3) != 76 || Q_8_8(0.59) != 151 || Q_8_8(0.1133) != 29) return 11;
    for (tone = 0; tone <= 16; ++tone)
        for (color = 0; color < 32768; ++color)
        {
            u16 source = (u16)color, result = 0, gray = source;
            int r = GET_R(source), g = GET_G(source), b = GET_B(source);
            int weighted = (76*r + 151*g + 29*b) / 256;
            u16 expected = (u16)RGB2(r+floor_div_16((int)tone*(weighted-r)),
                g+floor_div_16((int)tone*(weighted-g)), b+floor_div_16((int)tone*(weighted-b)));
            CopyPaletteInvertedTint(&source, &result, 1, (u8)tone);
            TintPalette_GrayScale(&gray, 1);
            if (result != expected || (tone == 16 && result != gray)) return 12;
            if (putchar(result & 255) == EOF || putchar(result >> 8) == EOF) return 13;
        }
    return fflush(stdout) == 0 ? 0 : 14;
}
'''


def generate(reference):
    verify_reference(reference)
    for relative, expected in HASHES.items():
        if hashlib.sha256((reference / relative).read_bytes()).hexdigest() != expected:
            raise ValueError('Quest palette reference revision mismatch: ' + relative)
    global_text = (reference / 'include/global.h').read_text()
    rgb_text = (reference / 'include/constants/rgb.h').read_text()
    macros = [re.search(r'^#define Q_8_8\(n\).*$', global_text, re.M).group()]
    macros += [re.search(r'^#define ' + name + r'\(color\).*$', rgb_text, re.M).group()
               for name in ('GET_R', 'GET_G', 'GET_B')]
    palette = (reference / 'src/palette.c').read_text()
    c = HEADER + '\n' + '\n'.join(macros) + '\n'
    c += extract_function(palette, 'TintPalette_GrayScale') + '\n'
    c += extract_function(palette, 'CopyPaletteInvertedTint') + '\n' + MAIN
    with tempfile.TemporaryDirectory() as directory:
        source = Path(directory) / 'quest_palette.c'
        executable = Path(directory) / ('quest_palette.exe' if os.name == 'nt' else 'quest_palette')
        source.write_text(c)
        compile_c(['cc', '-std=c11', '-O2', str(source), '-o', str(executable)])
        output = subprocess.check_output([str(executable)])
    if len(output) != 17 * 32768 * 2:
        raise ValueError('Incomplete RGB555 oracle output')
    samples = [0, 1, 2, 15, 16, 30, 31, 32, 992, 1024, 31744, 32767,
               0x4210, 0x03ff, 0x7c1f, 0x7fe0, 0x2c67, 0x56b5]
    rows = []
    for coefficient in range(17):
        block = output[coefficient * 65536:(coefficient + 1) * 65536]
        rows.append({'tone': coefficient, 'sha256': hashlib.sha256(block).hexdigest(),
                     'samples': [{'source': source, 'result': int.from_bytes(block[source*2:source*2+2], 'little')}
                                 for source in samples]})
    return {'sourceCommit': COMMIT, 'colorCount': 32768, 'encoding': 'ascending-rgb555-little-endian-uint16',
            'tones': rows}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    fixture = generate(args.reference)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print('17 source tone hashes; 557056 exhaustive RGB555 comparisons; 306 diagnostic rows')


if __name__ == '__main__':
    main()
