"""Compile bounded, pinned original field-jump C; emit callback-count vectors.

No upstream scripts execute. Row zero is the pre-initializer presentation pose.
The first completed callback invokes the actual Step0 initializer, which itself
calls Step1: it is not an extra frame. All later rows call the original Step1.
Each action wrapper is cross-checked against its extracted primitive callback.
Animation/ground-effect rendering is stubbed and is not claimed by these vectors.
Only direction/coordinate changes and original jump state/callbacks execute.
Run beside the existing tests/Reference/reference_c.py helper (or add that folder
to PYTHONPATH). Normal managed tests consume the committed numerical JSON only.
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

HASHES = {
    'src/event_object_movement.c': '920f96b45569e702dd00997e7984effb3a7677ba2b68512b939eda69c2294254',
    'src/data/object_events/movement_action_func_tables.h': 'e7b83d35e64a2b53d7b8bd123b9b76dc6b4f0e524378b453283677a256d1faf3',
    'include/constants/global.h': '7ecf4a63c7be65328135a75f8620786e40343bbe7688e7017e6d780526fd6a0c',
}
DIRECTIONS = [('Down', 'down', (0, 1)), ('Up', 'up', (0, -1)),
              ('Left', 'left', (-1, 0)), ('Right', 'right', (1, 0))]


def extract_function(source, name):
    match = re.search(r'^(?:static )?(?:void|u8|bool8|s16) ' + re.escape(name) + r'\([^;]+?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing original function: ' + name)
    start, end, depth = match.start(), match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def extract_array(source, name):
    match = re.search(r'^static const [^\n]+\b' + re.escape(name) + r'\[\][^=]*=\s*\{.*?^\};', source, re.M | re.S)
    if match is None:
        raise ValueError('Missing original array: ' + name)
    return match.group()


HEADER = r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8;
typedef int8_t s8;
typedef uint16_t u16;
typedef int16_t s16;
typedef uint32_t u32;
typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define NELEMS(a) (sizeof(a)/sizeof((a)[0]))
#define JUMP_HALFWAY 1
#define JUMP_FINISHED ((u8)-1)
enum { JUMP_DISTANCE_IN_PLACE, JUMP_DISTANCE_NORMAL, JUMP_DISTANCE_FAR };
enum { JUMP_TYPE_HIGH, JUMP_TYPE_LOW, JUMP_TYPE_NORMAL };
struct Coords16 { s16 x, y; };
struct Sprite { s16 x, y, y2, data[8]; u8 animPaused; };
struct ObjectEvent {
    struct Coords16 currentCoords, previousCoords;
    u8 facingDirection, movementDirection, previousMovementDirection;
    u8 facingDirectionLocked, triggerGroundEffectsOnMove, disableCoveringGroundEffects;
    u8 triggerGroundEffectsOnStop, landingJump, hasShadow;
};
/* Rendering-only dependencies cannot affect the numerical primitive under test. */
static u8 GetMoveDirectionAnimNum(u8 direction) { return direction; }
static u8 GetJumpSpecialDirectionAnimNum(u8 direction) { return direction; }
static void SetStepAnimHandleAlternation(struct ObjectEvent *o, struct Sprite *s, u8 a) { (void)o; (void)s; (void)a; }
static void SetStepAnim(struct ObjectEvent *o, struct Sprite *s, u8 a) { (void)o; (void)s; (void)a; }
static void DoShadowFieldEffect(struct ObjectEvent *o) { (void)o; }
static void StartSpriteAnim(struct Sprite *s, u8 a) { (void)s; (void)a; }
#define tDirection data[3]
#define sJumpDistance data[4]
#define sJumpType data[5]
#define sTimer data[6]
'''

MAIN = r'''
struct Case {
    bool8 (*start)(struct ObjectEvent *, struct Sprite *);
    bool8 (*step)(struct ObjectEvent *, struct Sprite *);
    u8 direction, distance, type, special;
};
static const struct Case cases[] = { CASES };
int main(void)
{
    unsigned c;
    for (c = 0; c < NELEMS(cases); c++)
    {
        struct ObjectEvent object = {0};
        struct Sprite action = {0}, primitive = {0};
        unsigned ordinal = 0;
        u8 phase = 0;
        bool8 finished;
        const struct Case *test = &cases[c];
        SetJumpSpriteData(&primitive, test->direction, test->distance, test->type);
        printf("%u 0 0 0 0 0\n", c);
        do {
            ordinal++;
            if (ordinal > 64) return 10;
            finished = ordinal == 1 ? test->start(&object, &action) : test->step(&object, &action);
            phase = test->special ? DoJumpSpecialSpriteMovement(&primitive) : DoJumpSpriteMovement(&primitive);
            if (action.x != primitive.x || action.y != primitive.y || action.y2 != primitive.y2 ||
                action.data[6] != primitive.data[6] || finished != (phase == JUMP_FINISHED)) return 11;
            if (finished && (!action.animPaused || action.data[2] != 2)) return 12;
            printf("%u %u %d %d %d %u\n", c, ordinal, action.x, action.y, action.y2, phase);
        } while (!finished);
    }
    return fflush(stdout) == 0 ? 0 : 13;
}
'''


def generate(reference):
    for relative, expected in HASHES.items():
        if hashlib.sha256((reference / relative).read_bytes()).hexdigest() != expected:
            raise ValueError('Field jump reference revision mismatch: ' + relative)
    movement = (reference / 'src/event_object_movement.c').read_text()
    tables = (reference / 'src/data/object_events/movement_action_func_tables.h').read_text()
    constants = (reference / 'include/constants/global.h').read_text()
    # The named wrappers are the bounded admission surface, not prefix matching.
    cases = []
    for family, prefix in [('Jump2', 'jump_2_'), ('JumpInPlace', 'jump_in_place_'),
                           ('JumpSpecial', 'jump_special_'), ('JumpSpecialWithEffect', 'jump_special_with_effect_')]:
        for title, suffix, vector in DIRECTIONS:
            cases.append({'action': prefix + suffix, 'wrapper': family + title, 'vector': vector})
    for title, suffix, vector in DIRECTIONS:
        other = {'Down': 'Up', 'Up': 'Down', 'Left': 'Right', 'Right': 'Left'}[title]
        cases.append({'action': 'jump_in_place_' + suffix + '_' + other.lower(),
                      'wrapper': 'JumpInPlace' + title + other, 'vector': vector})
    functions = ['Step1', 'GetJumpY', 'SetJumpSpriteData', 'DoJumpSpriteMovement',
                 'DoJumpSpecialSpriteMovement', 'SetObjectEventDirection', 'MoveCoordsInDirection',
                 'ShiftObjectEventCoords', 'ShiftStillObjectEventCoords', 'GetOppositeDirection',
                 'InitJump', 'InitJumpRegular', 'UpdateJumpAnim', 'DoJumpAnimStep',
                 'DoJumpSpecialAnimStep', 'DoJumpAnim', 'DoJumpSpecialAnim', 'DoJumpInPlaceAnim', 'InitJumpSpecial']
    cases_c = []
    for case in cases:
        root = 'MovementAction_' + case['wrapper']
        functions += [root + '_Step0', root + '_Step1']
        initializer = extract_function(movement, root + '_Step0')
        match = re.search(r'InitJumpRegular\(objectEvent, sprite, (DIR_\w+), (JUMP_DISTANCE_\w+), (JUMP_TYPE_\w+)\)', initializer)
        special = match is None
        if special:
            match = re.search(r'InitJumpSpecial\(objectEvent, sprite, (DIR_\w+)\)', initializer)
            if match is None:
                raise ValueError('Unknown jump initializer: ' + root)
            direction = match[1]
            init = extract_function(movement, 'InitJumpSpecial')
            match2 = re.search(r'InitJump\(objectEvent, sprite, direction, (JUMP_DISTANCE_\w+), (JUMP_TYPE_\w+)\)', init)
            distance, jump_type = match2.groups()
        else:
            direction, distance, jump_type = match.groups()
        case.update(distance=distance, jumpType=jump_type, special=special)
        cases_c.append('{%s_Step0,%s_Step1,%s,%s,%s,%d}' % (root, root, direction, distance, jump_type, special))
    bodies = [extract_function(movement, name) for name in functions]
    declarations = '\n'.join(body.partition('\n{')[0] + ';' for body in bodies)
    c = HEADER + '\n' + '\n'.join(re.findall(r'^#define DIR_.*$', constants, re.M)) + '\n'
    c += '\n'.join(extract_array(movement, name) for name in ('sDirectionToVectors', 'gOppositeDirections', 'sJumpY_High', 'sJumpY_Low', 'sJumpY_Normal', 'sJumpYTable')) + '\n'
    c += '\n'.join(extract_array(tables, name) for name in ('sJumpInitDisplacements', 'sJumpDisplacements')) + '\n'
    c += declarations + '\n' + '\n'.join(bodies) + '\n' + MAIN.replace('CASES', ',\n'.join(cases_c))
    with tempfile.TemporaryDirectory() as directory:
        source = Path(directory) / 'field_jump.c'
        executable = Path(directory) / ('field_jump.exe' if os.name == 'nt' else 'field_jump')
        source.write_text(c)
        compile_c(['cc', '-std=c11', '-O2', str(source), '-o', str(executable)])
        output = subprocess.check_output([str(executable)], text=True)
    for case in cases:
        case['rows'] = []
    for line in output.splitlines():
        case_index, *row = map(int, line.split())
        if len(row) != 5 or not 0 <= case_index < len(cases):
            raise ValueError('Malformed C oracle output')
        cases[case_index]['rows'].append(row)
    high = [-4,-6,-8,-10,-11,-12,-12,-12,-11,-10,-9,-8,-6,-4,0,0]
    normal = [-2,-4,-6,-8,-9,-10,-10,-10,-9,-8,-6,-5,-3,-2,0,0]
    for case in cases:
        far = case['distance'] == 'JUMP_DISTANCE_FAR'
        stationary = case['distance'] == 'JUMP_DISTANCE_IN_PLACE'
        duration = (32 if far else 16) * (2 if case['special'] else 1)
        if len(case['rows']) != duration + 1:
            raise ValueError('Incomplete C callback output: ' + case['action'])
        arc = normal if case['jumpType'] == 'JUMP_TYPE_NORMAL' else high
        for count, row in enumerate(case['rows']):
            pixels = 0 if stationary else (count + 1) // 2 if case['special'] else count
            shift = int(far) + int(case['special'])
            y2 = 0 if count in (0, duration) else arc[(count - 1) >> shift]
            phase = 255 if count == duration else 1 if count == duration // 2 else 0
            expected = [count, pixels * case['vector'][0], pixels * case['vector'][1], y2, phase]
            if row != expected:
                raise ValueError('Independent closed-form check differs: ' + str((case['action'], row, expected)))
        case['duration'] = duration
        for key in ('wrapper', 'vector', 'special', 'distance', 'jumpType'):
            del case[key]
    return {'sourceCommit': COMMIT,
            'clock': 'completed movement callbacks; row 0 is pre-initializer; row 1 includes Step0 and its immediate Step1',
            'columns': ['callbackCount', 'x', 'y', 'y2', 'phase'],
            'phases': {'moving': 0, 'halfway': 1, 'finished': 255},
            'cases': cases}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    fixture = generate(args.reference)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(fixture, separators=(',', ':')) + '\n')
    print('%d source action cases; %d pre-initializer/callback rows; extracted wrappers and independent primitive/closed-form parity' %
          (len(fixture['cases']), sum(len(case['rows']) for case in fixture['cases'])))


if __name__ == '__main__':
    main()
