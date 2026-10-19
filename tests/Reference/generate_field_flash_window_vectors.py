"""Extract the pinned Flash window raster into a bounded, portable C oracle.

Only the two original boundary functions execute; no upstream script runs.
Every radius in the public 0..200 range is sampled, including all source level
radii and every illumination step. The source y=160 write is kept separately
from the 160 visible lines, with a canary after it to catch an invalid write.
Normal managed tests read the ignored local output and never compile C.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

from reference_c import COMMIT, compile_c

SOURCE_HASH = '2b4e9e17b4528008ce36a38bab6108cb396f0aff2ef35cf6028bad49820c88e6'


def extract_function(source, name):
    match = re.search(r'^(?:static )?void ' + re.escape(name) + r'\([^;]*?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing Flash boundary function: ' + name)
    end, depth = match.end(), 1
    while depth:
        if end == len(source):
            raise ValueError('Unterminated Flash boundary function: ' + name)
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end] + '\n'


HEADER = r'''
#include <stdint.h>
#include <stdio.h>
typedef uint16_t u16;
typedef uint32_t u32;
typedef int32_t s32;
'''

MAIN = r'''
int main(void)
{
    printf("{\"radii\":[");
    for (int radius = 0; radius <= 200; radius++)
    {
        u16 boundaries[162] = {0};
        boundaries[161] = 0x5aa5;
        SetFlashScanlineEffectWindowBoundaries(boundaries, 120, 80, radius);
        if (boundaries[161] != 0x5aa5) return 2;
        if (radius) printf(",");
        printf("{\"radius\":%d,\"left\":[", radius);
        for (int y = 0; y < 160; y++)
        {if (y) printf(","); printf("%u", (unsigned)(boundaries[y] >> 8));}
        printf("],\"right\":[");
        for (int y = 0; y < 160; y++)
        {if (y) printf(","); printf("%u", (unsigned)(boundaries[y] & 255));}
        printf("],\"sourceLine160\":[%u,%u]}",
            (unsigned)(boundaries[160] >> 8), (unsigned)(boundaries[160] & 255));
    }
    printf("]}\n");
    return fflush(stdout) == 0 ? 0 : 3;
}
'''


def generate(reference):
    raw = (reference / 'src/field_screen_effect.c').read_bytes()
    if hashlib.sha256(raw).hexdigest() != SOURCE_HASH:
        raise ValueError('Flash window reference revision mismatch: src/field_screen_effect.c')
    source = raw.decode()
    code = HEADER + ''.join(extract_function(source, name) for name in (
        'SetFlashScanlineEffectWindowBoundary', 'SetFlashScanlineEffectWindowBoundaries')) + MAIN
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / 'field_flash_window.c'
        binary = Path(directory) / ('field_flash_window.exe' if os.name == 'nt' else 'field_flash_window')
        path.write_text(code, encoding='utf-8')
        compile_c(['cc', '-std=c11', '-O0', str(path), '-o', str(binary)])
        result = json.loads(subprocess.check_output([str(binary)], text=True))
    radii = result['radii']
    if [row['radius'] for row in radii] != list(range(201)):
        raise ValueError('Incomplete Flash radius coverage')
    for row in radii:
        if len(row['left']) != 160 or len(row['right']) != 160 or len(row['sourceLine160']) != 2:
            raise ValueError('Incomplete Flash visible scanlines')
        if any(not 0 <= value <= 255 for key in ('left', 'right', 'sourceLine160') for value in row[key]):
            raise ValueError('Flash boundary outside the hardware register range')
    return {'sourceCommit': COMMIT, 'center': [120, 80], 'visibleSize': [240, 160], **result}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = generate(args.reference)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, separators=(',', ':')) + '\n', encoding='utf-8')
    print(json.dumps({'radii': len(result['radii']), 'visibleScanlines': 201 * 160,
                      'endpoints': 201 * 160 * 2, 'sourceLine160': 'separate, not visible'}))


if __name__ == '__main__':
    main()
