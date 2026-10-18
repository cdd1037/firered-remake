#!/usr/bin/env python3
"""Compile only authored bounded glue and verbatim pinned-source extracts."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PINS = {
    'include/constants/battle_anim.h': '87437751121d8f9dbcd62e2ba67122b9c7c68ce2255b55096982c7ca8c3cf48e',
    'src/battle_anim_mons.c': 'fcb002acc48b8cd6b8cf8b7eddcb3034d721ff5887eb6bdeedfe183b7b3908bb',
    'src/battle_anim_effects_3.c': 'bdf8ba956af35ca232d7e5101090e2ff31a35f0ae91286a21b216e7787ba6c1f',
    'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae',
    'src/task.c': '8bdd5205ec396be7b66d6e384a82309b3cab59783d46fb8c4c991b2172d4868b',
    'src/battle_anim.c': '5883d9ee0483120ef67461952f5880499f322fb1c706cc213ca13c0c2b0e88c5',
    'src/battle_main.c': 'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928',
    'include/battle_anim.h': '15d0a815fec54ed669e1cf7130d323070d799ecfba0f3a935da4b9c28d6f09a7',
    'include/sprite.h': 'a77aa1c837dfb58cf60b3eac299c7cc29a23ce27735ba710b9e8eeed102bf773',
}

def digest(data):
    return hashlib.sha256(data).hexdigest()

def extract_function(text, name):
    match = re.search(r'^(?:static )?\w+ ' + re.escape(name) + r'\([^;]*?\)\n\{', text, re.M)
    if match is None:
        raise ValueError('Missing source function: ' + name)
    end = match.end()
    depth = 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return match.start(), end

def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--repository', type=Path, required=True)
    p.add_argument('--output', type=Path, default=HERE / 'glare-source-vectors.json')
    a = p.parse_args()
    if a.output.exists():
        raise SystemExit('Refuse overwriting an existing oracle. Generate a new candidate path.')
    if not a.output.resolve().is_relative_to(HERE):
        raise SystemExit('Output must remain inside the glare stage directory.')
    raw = a.repository / 'content/raw/pokefirered/037335f4'
    inputs, source = [], {}
    for relative, expected in PINS.items():
        data = (raw / relative).read_bytes()
        actual = digest(data)
        if actual != expected:
            raise ValueError(f'Pinned source mismatch: {relative}: {actual}')
        inputs.append({'path': relative, 'sha256': actual, 'bytes': len(data), 'url': f'https://github.com/pret/pokefirered/blob/{COMMIT}/{relative}'})
        source[relative] = data.decode('utf-8')
    extracts = []
    def record(relative, name, start, end):
        text = source[relative][start:end]
        extracts.append({'path': relative, 'name': name,
                         'firstLine': source[relative][:start].count('\n') + 1,
                         'lastLine': source[relative][:end].count('\n') + 1,
                         'sha256': digest(text.encode()), 'bytes': len(text.encode())})
        return text
    def function(relative, name):
        return record(relative, name, *extract_function(source[relative], name))
    effects = source['src/battle_anim_effects_3.c']
    start = effects.index('#define IDX_ACTIVE_SPRITES 10')
    end = effects.index('#undef sActiveSpritesIdx', start) + len('#undef sActiveSpritesIdx')
    glare = record('src/battle_anim_effects_3.c', 'glare macros + four complete functions + undefines', start, end)
    template_start = effects.index('const struct SpriteTemplate gGlareEyeDotSpriteTemplate =')
    template_end = effects.index('\n};', template_start) + len('\n};')
    template = record('src/battle_anim_effects_3.c', 'gGlareEyeDotSpriteTemplate', template_start, template_end)
    create = function('src/sprite.c', 'CreateSprite')
    animate = function('src/sprite.c', 'AnimateSprites')
    find = function('src/task.c', 'FindFirstActiveTask')
    run = function('src/task.c', 'RunTasks')
    destroy = function('src/battle_anim.c', 'DestroyAnimVisualTask')
    battle = function('src/battle_main.c', 'BattleMainCB2')
    # Record source init semantics and unreachable contest predicate without invoking script decoding.
    function('src/battle_anim.c', 'Cmd_createvisualtask')
    function('src/battle_anim.c', 'IsContest')
    mons = source['src/battle_anim_mons.c']
    coords_start = mons.index('static const struct UCoords8 sBattlerCoords')
    coords_end = mons.index('\n};', coords_start) + len('\n};')
    coords = record('src/battle_anim_mons.c', 'sBattlerCoords', coords_start, coords_end)
    constants = []
    for relative, name in [('include/constants/battle_anim.h', 'ANIM_SPRITES_START'), ('include/constants/battle_anim.h', 'ANIM_TAG_SMALL_RED_EYE'), ('include/sprite.h', 'MAX_SPRITES')]:
        match = re.search(r'^#define ' + name + r'[^\n]*', source[relative], re.M)
        if match is None: raise ValueError(name)
        constants.append(record(relative, name, match.start(), match.end()))
    generated = HERE / 'glare-harness.generated.c'
    prefix = (HERE / 'harness_prefix.c').read_text()
    suffix = (HERE / 'harness_suffix.c').read_text()
    c = '\n'.join(constants) + '\n' + prefix + '\n' + coords + '\n#define AnimGlareEyeDot ObservedGlareEyeDot\n' + template + '\n#undef AnimGlareEyeDot\n'
    c += '\n#define CreateSprite Source_CreateSprite\n' + create + '\n#undef CreateSprite\n'
    c += '\n' + glare + '\n' + animate + '\n' + find + '\n' + run + '\n' + destroy + '\n' + battle + '\n' + suffix
    generated.write_text(c)
    spec = importlib.util.spec_from_file_location('reference_c', a.repository / 'tests/Reference/reference_c.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    module.verify_reference(raw)
    outputs, commands = [], []
    for optimization in ('-O0', '-O2'):
        executable = HERE / ('glare-harness-' + optimization[1:])
        command = ['cc', '-std=c11', optimization, '-fwrapv', '-Wall', '-Wextra', '-Wno-unused-parameter', str(generated), '-o', str(executable)]
        module.compile_c(command)
        commands.append([*command[:-3], generated.name, "-o", executable.name])
        outputs.append(subprocess.check_output([str(executable)], text=True))
    if outputs[0] != outputs[1]:
        raise AssertionError('O0 and O2 source observations differ')
    actual = json.loads(outputs[1])
    assertions = validate(actual)
    result = {'schema': 'firered.glare.source-oracle.v1', 'referenceCommit': COMMIT,
              'sourceInputs': inputs, 'sourceExtracts': extracts,
              'harness': {'generatorSha256': digest(Path(__file__).read_bytes()),
                          'prefixSha256': digest((HERE / 'harness_prefix.c').read_bytes()),
                          'suffixSha256': digest((HERE / 'harness_suffix.c').read_bytes()),
                          'generatedCSha256': digest(generated.read_bytes()),
                          'commands': commands, 'optimizationParity': ['O0', 'O2'],
                          'assertions': assertions, 'referenceCHelperSha256': digest((a.repository / 'tests/Reference/reference_c.py').read_bytes())},
              'semantics': {
                  'frameZero': 'Direct source initializer, matching Cmd_createvisualtask immediate init; first RunTasks call is numbered frame 1.',
                  'phaseOrder': ['AnimateSprites callbacks', 'BuildOamBuffer observation', 'RunTasks callbacks'],
                  'spriteStateFields': ['slot', 'instance', 'x', 'y', 'x2', 'y2', 'timer', 'parentTask', 'parentCounterIndex', 'subpriority'],
                  'taskDataFields': {'0': 'state', '1': 'timer', '2': 'pairNum', '5': 'pairMax', '6': 'dotOffset', '7': 'isContest', '10': 'activeChildren', '11': 'startX', '12': 'startY', '13': 'endX', '14': 'endY'},
                  'callFields': ['slot', 'instance', 'timerBefore', 'timerAfterOrAtDestroyEntry', 'parentCountBefore', 'parentCountAfter', 'destroyed'],
                  'allocationFields': ['attempt', 'pairNum', 'siblingIndex', 'x', 'y', 'subpriority', 'resultSlotOr64', 'instanceOrZero'],
                  'readFields': ['kind: 0=side 1=coordinate 2=attribute', 'battler', 'selector', 'value'],
                  'layoutCoverage': 'All 16 ordered actor/target IDs for both source coordinate tables; same-side and self pairs are helper probes, not claims that a move script targets those pairs.',
                  'stubs': ['Controlled battler side/coordinate/height reads', 'CreateSpriteAt performs zeroed sprite allocation, can fail a chosen attempt; tile/palette/OAM loading is outside scope', 'DestroySprite records the timer at entry, zeroes/frees the slot, and does not mutate the global animation count', 'DestroyTask records task teardown', 'BuildOamBuffer observes live child slots; does not claim rendered pixel or native OAM sorting correctness', 'AnimateSprite and unrelated battle rendering callbacks are inert'],
                  'verbatimFunctions': ['AnimTask_GlareEyeDots', 'AnimTask_GlareEyeDots_Step', 'GetGlareEyeDotCoords', 'AnimGlareEyeDot', 'CreateSprite', 'AnimateSprites', 'FindFirstActiveTask', 'RunTasks', 'DestroyAnimVisualTask', 'BattleMainCB2'],
                  'instrumentation': 'Preprocessor aliases route CreateSprite through a return-value observer and the source sprite template through a callback observer. Both observers call the verbatim source function once, without eager child callbacks.',
                  'contest': 'Normal initializer forcibly writes FALSE; explicit forced-state cases reach the otherwise unreachable offset branch. They are coverage probes, not reachable FireRed behavior.',
                  'integerModel': 'C11 fixed-width s16/u8, 32-bit int, arithmetic signed right shift and GNU native signed-left-shift behavior, -fwrapv; complete O0/O2 output equality checked. Negative signed left shift in the original source is not portable ISO C.'},
              **actual}
    a.output.write_text(json.dumps(result, separators=(',', ':')) + '\n')
    print(json.dumps({'output': str(a.output), 'sha256': digest(a.output.read_bytes()), 'scenarios': len(actual['scenarios']), 'frameRows': sum(len(s['frames']) for s in actual['scenarios']), 'coordinateRows': len(actual['coordinates']), 'assertions': assertions}))

def validate(data):
    scenarios = data['scenarios']
    assert scenarios and data['coordinates']
    for scenario in scenarios:
        frames = scenario['frames']
        assert [f['frame'] for f in frames] == list(range(len(frames)))
        assert not frames[-1]['taskAlive'] and frames[-1]['visualCount'] == 0
        assert frames[-1]['taskData'][10] == 0
        assert sum(len(f['allocations']) for f in frames) == 26
        assert [f['frame'] for f in frames if f['allocations']] == list(range(4, 53, 4))
        first_calls = {}
        created = {}
        for frame in frames:
            assert frame['taskData'][10] == len(frame['children'])
            assert frame['visualCount'] == int(frame['taskAlive'])
            for allocation in frame['allocations']:
                if allocation[6] != 64:
                    created[allocation[7]] = frame['frame']
            for call in frame['callbacks']:
                first_calls.setdefault(call[1], frame['frame'])
                assert call[3] == call[2] + 1
                if call[6]:
                    assert call[3] == 37
                    assert call[5] == call[4] - 1
                    assert frame['frame'] == created[call[1]] + 37
            # No raw child callbacks occur on its allocation frame.
            assert all(row[1] not in {a[7] for a in frame['allocations'] if a[6] != 64} for row in frame['oamChildren'])
        assert all(first_calls[instance] == frame + 1 for instance, frame in created.items())
        if scenario['occupiedSlots'] == 0:
            assert frames[-1]['frame'] == 89
            assert max(len(f['children']) for f in frames) == 20
        elif scenario['occupiedSlots'] == 64:
            assert frames[-1]['frame'] == 53
            assert not created
        elif scenario['occupiedSlots'] == 63:
            assert frames[-1]['frame'] == 81
            assert len(created) == 2
        assert frames[0]['taskData'][7] == int(scenario['forceContestBranch'])
    assert len({(s['layout'], s['attacker'], s['target']) for s in scenarios[:32]}) == 32
    assert all(s['contestAfterInit'] == 0 for s in scenarios)
    failed = next(s for s in scenarios if s['name'] == 'first_sibling_allocation_failure')
    assert failed['frames'][4]['allocations'][0][6] == 64
    assert failed['frames'][4]['allocations'][1][6] == 0
    assert failed['frames'][4]['children'][0][4:6] == [3, 3]
    contest = next(s for s in scenarios if s['forceContestBranch'])
    assert [r[4:6] for r in contest['frames'][4]['children']] == [[-3, 3], [3, -3]]
    assert any(c['endX'] < c['startX'] for c in data['coordinates'])
    assert any(c['startX'] < 0 for c in data['coordinates'])
    assert any(c['startX'] == -32768 and c['endX'] == 32767 for c in data['coordinates'])
    return ['nonempty observations', 'all 32 ordered actor/target layout combinations', 'contest initializer overwrites seed TRUE with FALSE', 'failed first sibling leaves successful second sibling +3/+3', 'forced contest branch produces -3/+3 and +3/-3', 'all frame rows contiguous', 'all 26 allocation attempts at frames 4..52', 'raw children have no eager callback or allocation-frame OAM sample', 'first child callback at birth+1', 'child expiry at birth+37 and timer=37', 'parent live count equals live children every frame', 'raw children never decrement global animation task count', 'normal task termination frame 89 and peak 20 children', 'full saturation termination frame 53', 'single-free-slot partial allocation termination frame 81', 'negative displacement and full s16 endpoint coordinate probes', 'O0/O2 byte-identical output']

if __name__ == '__main__':
    main()
