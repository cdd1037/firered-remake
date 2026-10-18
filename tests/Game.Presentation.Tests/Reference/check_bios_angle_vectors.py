#!/usr/bin/env python3
"""Check integer BIOS angle octants and the compact focused fixture.

Behavioral references: mGBA src/gba/bios.c _ArcTan/_ArcTan2 and
https://retrointernals.github.io/gba-bios-reference/swi_0A_arctan2/.
Only independently authored arithmetic is executed. Published hardware values
are anchors; this script does not claim to run original GBA hardware.
"""
import csv
import argparse
import io
from pathlib import Path

ROOT = Path(__file__).resolve().parent
COEFFICIENTS = (912, 2332, 4022, 5802, 8321, 13905, 41721)

def s16(n):
    return ((n + 32768) & 65535) - 32768

def divide(n, d):
    quotient = abs(n) // abs(d)
    return -quotient if (n < 0) != (d < 0) else quotient

def polynomial(n):
    t = -(n * n >> 14)
    value = 169
    for coefficient in COEFFICIENTS:
        value = (value * t >> 14) + coefficient
    return n * value >> 16

def reference(x, y):
    x, y = s16(x), s16(y)
    if not y:
        return 0 if x >= 0 else 32768
    if not x:
        return 16384 if y >= 0 else 49152
    if y >= 0:
        if x >= 0:
            if x >= y:
                return polynomial(divide(y << 14, x)) & 65535
        elif -x >= y:
            return (polynomial(divide(y << 14, x)) + 32768) & 65535
        return (16384 - polynomial(divide(x << 14, y))) & 65535
    if x <= 0:
        if -x > -y:
            return (polynomial(divide(y << 14, x)) + 32768) & 65535
    elif x >= -y:
        return (polynomial(divide(y << 14, x)) + 65536) & 65535
    return (49152 - polynomial(divide(x << 14, y))) & 65535

def compact(x, y):
    x, y = s16(x), s16(y)
    if y == 0:
        return 32768 if x < 0 else 0
    if x == 0:
        return 49152 if y < 0 else 16384
    if abs(x) >= abs(y):
        return (polynomial(divide(y << 14, x)) + (32768 if x < 0 else 65536 if y < 0 else 0)) & 65535
    return ((49152 if y < 0 else 16384) - polynomial(divide(x << 14, y))) & 65535

# Published hardware-measured examples in the clean-room reference, plus its
# independently measured ArcTan asymmetry pair (input +/-0x1000).
HARDWARE = {
    (0, 0): 0x0000, (1, 0): 0x0000, (-1, 0): 0x8000,
    (0, 1): 0x4000, (0, -1): 0xC000,
    (1, 1): 0x2000, (-1, 1): 0x6000,
    (-1, -1): 0xA000, (1, -1): 0xE000,
    (100, 50): 0x12E4, (50, 100): 0x2D1C,
    (4096, 12288): 0x32E5,
}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--fixture', type=Path, default=ROOT.parent / 'Fixtures/bios-angle-vectors.csv')
    parser.add_argument('--output', type=Path, help='Write independent focused vectors after all arithmetic checks')
    args = parser.parse_args()
    for vector, expected in HARDWARE.items():
        assert reference(*vector) == compact(*vector) == expected, vector
    assert polynomial(4096) == 0x09FB
    assert polynomial(-4096) == -0x09FC

    # Every polynomial input ArcTan2 can produce, checked for intermediate int32
    # safety; this does not claim support for arbitrary direct ArcTan arguments.
    max_product = 0
    for n in range(-16384, 16385):
        t = -(n * n >> 14)
        value = 169
        for coefficient in COEFFICIENTS:
            max_product = max(max_product, abs(value * t))
            assert -(1 << 31) <= value * t < (1 << 31)
            value = (value * t >> 14) + coefficient
        max_product = max(max_product, abs(value * n))
        assert -(1 << 31) <= value * n < (1 << 31)

    # Covers all quotient values in every octant; for quotient zero use nonzero
    # minor axis / 32767 so the division path, not axis shortcut, is exercised.
    count = 0
    for q in range(16385):
        major, minor = (16384, q) if q else (32767, 1)
        for sx in (-1, 1):
            for sy in (-1, 1):
                for swap in (False, True):
                    x, y = (minor, major) if swap else (major, minor)
                    x, y = sx * x, sy * y
                    expected = reference(x, y)
                    assert compact(x, y) == expected, (x, y)
                    count += 1

    # Signed-16 extrema and axis/diagonal boundaries at every signed-16 k.
    boundary_count = 0
    for k in range(-32768, 32768):
        for vector in ((k, 0), (0, k), (k, k), (k, -k), (k, k + 1), (k, k - 1),
                       (-32768, k), (k, -32768), (32767, k), (k, 32767)):
            assert compact(*vector) == reference(*vector), vector
            boundary_count += 1

    special = [(0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)]
    for x, y in ((1, 1), (4, 1), (3, 1), (100, 50), (50, 100),
                 (129, 57), (120, 40), (73, 29), (32767, 1),
                 (32767, 32766), (16384, 16383), (16384, 16385)):
        for sx, sy in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
            special.append((sx * x, sy * y))
    special.extend([(-32768, -32768), (-32768, 32767), (32767, -32768),
                    (-32768, 1), (1, -32768), (65535, 1), (65536, 65536)])
    with io.StringIO(newline='') as f:
        writer = csv.writer(f)
        writer.writerow(('input_x', 'input_y', 's16_x', 's16_y', 'arctan2_hex',
                         'arctan2neg_hex', 'needle_rotation_hex', 'matrix_angle_u8'))
        for x, y in special:
            a = reference(x, y)
            n = -a & 65535
            rotation = (n + 0xC000) & 65535
            writer.writerow((x, y, s16(x), s16(y), f'0x{a:04X}', f'0x{n:04X}',
                             f'0x{rotation:04X}', rotation >> 8))
        candidate = f.getvalue().encode()
        if args.output is not None:
            if args.output.exists() and args.output.read_bytes() != candidate:
                raise SystemExit('Existing BIOS vectors differ; review a new candidate path: ' + str(args.output))
            args.output.parent.mkdir(parents=True, exist_ok=True)
            args.output.write_bytes(candidate)
        else:
            assert candidate == args.fixture.read_bytes(), 'Focused BIOS vectors differ'
    report = (f'Published hardware anchors: {len(HARDWARE)} + 2 ArcTan asymmetry checks passed\n'
              f'Polynomial domain: 32769 inputs, max absolute product {max_product}; no int32 overflow\n'
              f'All quotient/octant states: {count} passed\n'
              f'Signed16 axes, diagonals, adjacent boundaries, extrema: {boundary_count} passed\n'
              f'Focused vectors checked: {len(special)}\n'
              'C# product and staged helper were not compiled or executed.\n'
              'Checks establish the compact dispatch equivalence; hardware is cited, not executed here.\n')
    print(report,end='')

if __name__ == '__main__':
    main()
