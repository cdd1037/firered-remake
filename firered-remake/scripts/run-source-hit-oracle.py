#!/usr/bin/env python3
"""Compile authenticated original animation bodies with bounded native platform shims."""
import argparse
import hashlib
import importlib.util
import json
import re
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('hit_importer', root / 'scripts/import-source-hit-animations.py')
imp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(imp)
p = argparse.ArgumentParser()
p.add_argument('output', type=Path)
p.add_argument('--doubles', action='store_true')
p.add_argument('--stats', action='store_true')
p.add_argument('--motion-vectors', action='store_true')
p.add_argument('--vacant-partner', action='store_true')
p.add_argument('--reference', type=Path, default=Path((root / '.tools/reference-root').read_text().strip()))
a = p.parse_args()
if a.vacant_partner and not a.doubles: p.error("--vacant-partner requires --doubles")
out = a.output.resolve()
out.mkdir(parents=True, exist_ok=False)
raw = imp.authenticated(a.reference)
# The existing authenticated lock also pins this display-scheduler provenance source.
scheduler_path = 'src/battle_main.c'
scheduler_bytes = raw[scheduler_path]
scheduler_sha = hashlib.sha256(scheduler_bytes).hexdigest()
functions = {
    'src/battle_anim.c': ['WaitAnimFrameCount', 'RunAnimScriptCommand', 'Cmd_createsprite', 'Cmd_createvisualtask', 'Cmd_delay', 'Cmd_waitforvisualfinish', 'Cmd_end', 'Cmd_clearmonbg', 'Task_ClearMonBg', 'BattleAnimAdjustPanning'],
    'src/battle_anim_mon_movement.c': ['DoHorizontalLunge', 'ReverseHorizontalLungeDirection', 'AnimTask_ShakeMon', 'AnimTask_ShakeMon_Step', 'AnimTask_ShakeMon2', 'AnimTask_ShakeMon2Step', 'AnimTask_TranslateMonEllipticalRespectSide', 'AnimTask_TranslateMonElliptical', 'AnimTask_TranslateMonElliptical_Step', 'SlideMonToOffset', 'SlideMonToOriginalPos', 'SlideMonToOriginalPos_Step'],
    'src/battle_anim_normal.c': ['AnimHitSplatBasic'],
    'src/battle_anim_fight.c': ['AnimBasicFistOrFoot'],
    'src/trig.c': ['Sin', 'Cos'],
    'src/battle_anim_mons.c': ['TranslateSpriteLinearById', 'TranslateSpriteLinear', 'WaitAnimForDuration', 'RunStoredCallbackWhenAffineAnimEnds', 'RunStoredCallbackWhenAnimEnds', 'SetAnimSpriteInitialXOffset', 'InitSpritePosToAnimTarget', 'InitSpritePosToAnimAttacker', 'AnimSpriteOnMonPos', 'DestroySpriteAndMatrix', 'GetBattlerSpriteSubpriority', 'InitSpriteDataForLinearTranslation', 'TranslateSpriteLinearByIdFixedPoint'],
    'src/battle_anim_effects_1.c': ['AnimCuttingSlice', 'AnimSlice_Step', 'AnimSlashSlice', 'AnimFalseSwipeSlice', 'AnimFalseSwipePositionedSlice', 'AnimFalseSwipeSlice_Step1', 'AnimFalseSwipeSlice_Step2', 'AnimFalseSwipeSlice_Step3'],
    'src/sprite.c': ['CreateSpriteAndAnimate', 'AnimateSprites', 'UpdateOamCoords', 'BuildSpritePriorities', 'SortSprites', 'AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame', 'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop', 'ContinueAnimLoop', 'JumpToTopOfAnimLoop', 'DecrementAnimDelayCounter', 'SetSpriteOamFlipBits', 'StartSpriteAnim', 'BeginAffineAnim', 'ContinueAffineAnim', 'AffineAnimDelay', 'AffineAnimCmd_end', 'AffineAnimCmd_frame', 'AffineAnimStateRestartAnim', 'AffineAnimStateStartAnim', 'ApplyAffineAnimFrameAbsolute', 'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix', 'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame', 'StartSpriteAffineAnim'],
}
if a.doubles:
    functions['src/battle_anim.c'] += ['Cmd_monbg']
    functions['src/battle_anim_mons.c'] += ['GetBattlerSpriteFinal_Y']
if a.stats:
    functions['src/battle_anim.c'] += ['Cmd_loopsewithpan','Task_LoopAndPlaySE']
    functions['src/battle_anim_utility_funcs.c'] = ['AnimTask_Flash','AnimTask_Flash_Step','SetPalettesToColor']
    functions['src/battle_anim_mons.c'] += ['GetBattlePalettesMask','GetBattleMonSpritePalettesMask']
    functions['src/blend_palette.c'] = ['BlendPalette']
bodies, identities = [], []

def identify(path, name, source, start, body, kind):
    identities.append(dict(path=path, name=name, line=source.count('\n', 0, start) + 1,
                           sha256=hashlib.sha256(body.encode()).hexdigest(), kind=kind))

for path, names in functions.items():
    source = raw[path].decode()
    for name in names:
        match = re.search(r'^((?:static )?\w+ ' + name + r'\([^;]*?\))\n\{', source, re.M)
        if match is None:
            raise ValueError('Missing original function: ' + name)
        start, pos, depth = match.start(), match.end(), 1
        while depth:
            if source[pos] == '{': depth += 1
            if source[pos] == '}': depth -= 1
            pos += 1
        body = source[start:pos]
        bodies.append((match[1], body))
        identify(path, name, source, start, body, 'function')

# These identities explain platform scheduling while keeping GPU/palette calls out of the host.
for path, name in [('src/battle_main.c', 'BattleMainCB2'), ('src/battle_anim.c', 'Cmd_monbg'), ('src/battle_anim.c', 'Task_InitUpdateMonBg')]:
    source = raw[path].decode()
    match = re.search(r'^((?:static )?\w+ ' + name + r'\([^;]*?\))\n\{', source, re.M)
    if match is None: raise ValueError('Missing scheduling reference: ' + name)
    pos, depth = match.end(), 1
    while depth:
        if source[pos] == '{': depth += 1
        if source[pos] == '}': depth -= 1
        pos += 1
    identify(path, name, source, match.start(), source[match.start():pos], 'schedulingReference')

constant_names = {
    'src/sprite.c': ['sDummyAnim', 'gDummySpriteAnimTable', 'sDummyAffineAnim', 'gDummySpriteAffineAnimTable'],
    'src/battle_anim_normal.c': ['sAffineAnim_HitSplat_' + str(i) for i in range(4)] + ['sAffineAnims_HitSplat', 'gBasicHitSplatSpriteTemplate'],
    'src/battle_anim_mon_movement.c': ['gHorizontalLungeSpriteTemplate', 'gSlideMonToOffsetSpriteTemplate', 'gSlideMonToOriginalPosSpriteTemplate'],
    'src/battle_anim_effects_1.c': ['sCuttingSliceAnimCmds', 'sCuttingSliceAnimTable', 'gCuttingSliceSpriteTemplate', 'sSlashSliceAnimCmds1', 'sSlashSliceAnimCmds2', 'sSlashSliceAnimTable', 'gSlashSliceSpriteTemplate', 'gFalseSwipeSliceSpriteTemplate', 'gFalseSwipePositionedSliceSpriteTemplate'],
    'src/battle_anim_effects_3.c': ['sScratchAnimCmds', 'sScratchAnimTable', 'gScratchSpriteTemplate'],
}
constant_names['src/battle_anim_fight.c']=['sAnim_Fist','sAnim_FootWide','sAnim_FootTall','sAnim_HandLeft','sAnim_HandRight','sAnims_HandsAndFeet','gFistFootSpriteTemplate']
constant_names['src/trig.c']=['gSineTable']
constants = []
if a.doubles:
    source=raw['src/battle_anim_mons.c'].decode()
    match=re.search(r'^static const struct UCoords8 sBattlerCoords.*?^};',source,re.M|re.S)
    constants.append(match[0])
    identify('src/battle_anim_mons.c','sBattlerCoords',source,match.start(),match[0],'constant')
for path, names in constant_names.items():
    source = raw[path].decode()
    for name in names:
        match = re.search(r'^(?:static )?const [^\n;]*\b' + name + r'(?:\[\])?\s*=\s*\{.*?\};', source, re.M | re.S)
        if match is None:
            raise ValueError('Missing original constant: ' + name)
        constants.append(match[0])
        identify(path, name, source, match.start(), match[0], 'constant')

template_names = ['gBasicHitSplatSpriteTemplate', 'gHorizontalLungeSpriteTemplate', 'gScratchSpriteTemplate', 'gCuttingSliceSpriteTemplate', 'gSlashSliceSpriteTemplate', 'gFalseSwipeSliceSpriteTemplate', 'gFalseSwipePositionedSliceSpriteTemplate', 'gSlideMonToOffsetSpriteTemplate', 'gSlideMonToOriginalPosSpriteTemplate', 'gFistFootSpriteTemplate']
sound_names = ['SE_M_DOUBLE_SLAP', 'SE_M_COMET_PUNCH', 'SE_M_SCRATCH', 'SE_M_CUT', 'SE_M_RAZOR_WIND', 'SE_M_VITAL_THROW', 'SE_M_DOUBLE_TEAM', 'SE_M_VITAL_THROW2', 'SE_M_TAIL_WHIP', 'SE_M_LEER']
task_names = ['AnimTask_ShakeMon', 'AnimTask_ShakeMon2', 'AnimTask_TranslateMonEllipticalRespectSide']
ops = ['loadspritegfx', 'monbg', 'setalpha', 'playsewithpan', 'createsprite', 'createvisualtask', 'delay', 'waitforvisualfinish', 'clearmonbg', 'blendoff', 'end']
if a.stats:
    task_names += ['AnimTask_Flash']
    ops += ['loopsewithpan']
source = raw['data/battle_anim_scripts.s'].decode()
macros = set(re.findall(r'^\s*\.macro (\w+)', raw['asm/macros/battle_anim_script.inc'].decode(), re.M))
arrays = ['struct Command {int args[10];};']
names, moves = [], []

def integer(value):
    return {'ANIM_ATTACKER': 0, 'ANIM_TARGET': 1, 'ANIM_DEF_PARTNER': 3, 'SOUND_PAN_TARGET': 63, 'SOUND_PAN_ATTACKER': -64}.get(value, None) if value in {'ANIM_ATTACKER', 'ANIM_TARGET', 'ANIM_DEF_PARTNER', 'SOUND_PAN_TARGET', 'SOUND_PAN_ATTACKER'} else int(value, 0)

script_ids=[(1, 'POUND'), (10, 'SCRATCH'), (15, 'CUT'), (33, 'TACKLE'), (163, 'SLASH'), (206, 'FALSE_SWIPE'), (68, 'COUNTER'), (233, 'VITAL_THROW')]
if a.stats:script_ids=[(39, 'TAIL_WHIP'), (148, 'FLASH')]
if a.motion_vectors:
    vector_scripts=json.loads((root/'tests/SourceHitAnimation.Oracle/motion-vectors.json').read_text())['scripts']
    script_ids=[(r['moveId'],r['label'][5:]) for r in vector_scripts]
    source='\n'.join(r['label']+':\n'+'\n'.join('\t'+c['op']+' '+', '.join(c['args']) for c in r['commands']) for r in vector_scripts)
for move, label in script_ids:
    name = 'Move_' + label
    match = re.search(r'^' + name + r':\n', source, re.M)
    if match is None: raise ValueError('Missing source script ' + name)
    bytecode, args = [], []
    for line in source[match.end():].splitlines():
        line = line.split('@', 1)[0].strip()
        if not line: continue
        if line.endswith(':'): raise ValueError('Unexpected branch in ' + name)
        op, *tail = line.split(None, 1)
        values = [s.strip() for s in tail[0].split(',')] if tail else []
        if op not in ops or op not in macros: raise ValueError('Unsupported original command ' + op)
        bytecode.append(ops.index(op))
        av = []
        if op == 'setalpha': av = [integer(s) for s in values]
        elif op == 'playsewithpan': av = [sound_names.index(values[0]) + 1, integer(values[1])]
        elif op == 'loopsewithpan':
            sound=sound_names.index(values[0])+1
            payload=[sound&255,sound>>8,integer(values[1])&255,integer(values[2]),integer(values[3])]
            bytecode.extend(payload);args.extend([[]]*len(payload))
        elif op in ['createsprite', 'createvisualtask']:
            if op == 'createsprite':
                pointer = template_names.index(values[0])
                mode = (128 if values[1] == 'ANIM_TARGET' else 0) | (integer(values[2]) & 127)
                tail_values = values[3:]
            else:
                pointer = task_names.index(values[0])
                mode = integer(values[1])
                tail_values = values[2:]
            payload = [pointer, 0, 0, 0, mode, len(tail_values)]
            for value in tail_values:
                number = integer(value) & 65535
                payload += [number & 255, number >> 8]
            bytecode.extend(payload)
            args.extend([[]] * len(payload))
        args.append(av)
        if op in ['delay', 'clearmonbg'] or a.doubles and op == 'monbg':
            bytecode.append(integer(values[0])); args.append([])
        if op == 'end': break
    else: raise ValueError('Unterminated source script ' + name)
    names.append(name); moves.append(move)
    arrays.append('static const u8 ' + name + '[]={' + ','.join(map(str, bytecode)) + '};')
    arrays.append('static const struct Command ' + name + '_args[]={' + ','.join('{{' + (','.join(map(str, v)) or '0') + '}}' for v in args) + '};')
arrays += ['static const TaskFunc tasks[]={' + ','.join(task_names) + '};', 'static const u8 *scripts[]={' + ','.join(names) + '};',
           'static const struct Command *commands[]={' + ','.join(x + '_args' for x in names) + '};',
           'static const int moveIds[]={' + ','.join(map(str, moves)) + '};',
           'static const struct SpriteTemplate *templates[]={' + ','.join('&' + x for x in template_names) + '};',
           'static const char *templateNames[]={' + ','.join(json.dumps(x) for x in template_names) + '};']
# The tags are resource lookup identifiers only; actual tiles are reported relative to each sheet.
tags = sorted(set(re.findall(r'\bANIM_TAG_\w+', '\n'.join(constants))))
source = '\n'.join([('#define STATS_FAMILY 1\n' if a.stats else '')+('#define MOTION_VECTORS 1\n' if a.motion_vectors else '')+('#define DOUBLE_FIXTURE 1\n' if a.doubles else '')+('#define VACANT_FIXTURE 1\n' if a.vacant_partner else '')+'\n'.join('#define ' + tag + ' ' + str(i + 1) for i, tag in enumerate(tags)),
                   (root / 'tests/SourceHitAnimation.Oracle/host.h').read_text(),
                   '\n'.join(sig + ';' for sig, body in bodies), '\n'.join(constants),
                   '\n'.join(body for sig, body in bodies), '\n'.join(arrays),
                   (root / ('tests/SourceHitAnimation.Oracle/double-driver.c' if a.doubles else 'tests/SourceHitAnimation.Oracle/driver.c')).read_text()])
(out / 'oracle.c').write_text(source)
# The pinned PositionedSlice callback contains a redundant chained assignment; preserve it verbatim.
result = subprocess.run(['cc', '-std=c99', '-O0', '-Wall', '-Wextra', '-Werror', '-Wno-error=sequence-point', str(out / 'oracle.c'), '-o', str(out / 'oracle')], capture_output=True, text=True)
(out / 'compile.log').write_text(result.stdout + result.stderr)
if result.returncode: raise SystemExit(result.stderr)
result = subprocess.run([str(out / 'oracle')], cwd=out, capture_output=True, text=True, timeout=10)
(out / 'source-c.csv').write_text(result.stdout)
if result.returncode: raise SystemExit('Source oracle failed: ' + result.stderr)
rows = len(result.stdout.splitlines()) - 1
if rows <= 0: raise SystemExit('Empty source trace')
(out / 'provenance.json').write_text(json.dumps(dict(commit=imp.COMMIT,
    functions=[x for x in identities if x['kind'] == 'function'],
    constantArrays=[x for x in identities if x['kind'] == 'constant'],
    soundIds={str(i + 1): name for i, name in enumerate(sound_names)},
    schedulingReferences=[x for x in identities if x['kind'] == 'schedulingReference'],
    schedulerSource=dict(path=scheduler_path, bytes=len(scheduler_bytes), sha256=scheduler_sha),
    statScripts=a.stats, doubles=a.doubles,vacantPartner=a.vacant_partner,motionBoundaryVectors=a.motion_vectors, displayOffsets='Non-monbg battlers use the pre-task OAM position (nine-bit X wrap normalized for screen rendering); each visible monbg target/partner follows post-task offsets via priority-10 background updates.',
    spriteFields=['slot', 'template', 'x', 'y', 'x2', 'y2', 'tile', 'visible', 'hFlip', 'blend', 'scale', 'subpriority', 'oamRank'],
    spriteIds='Lowest available source sprite slot; fixture battlers reserve slots 0 through 3.' if a.doubles else 'Lowest available source sprite slot; fixture battlers reserve slots 0 and 1.',
    oamOrder='Original persistent 64-slot SortSprites; one extra leading host sentinel safely covers the original documented unused gSpriteOrder[-1] read.',
    coordinates=({'slot0': {'x':32,'rawY':80,'pictureY':92},'slot1': {'x':200,'rawY':40,'pictureY':53},'slot2': {'x':90,'rawY':88,'pictureY':100},'slot3': {'x':152,'rawY':32,'pictureY':45}} if a.doubles else {'player': {'x': 72, 'rawY': 80, 'pictureY': 92}, 'enemy': {'x': 176, 'rawY': 40, 'pictureY': 53}}),
    hostSha256=hashlib.sha256(source.encode()).hexdigest(), rows=rows, originalRuntimeCompared=False), indent=2) + '\n')
print('PASS independently compiled source C callbacks:', len(bodies), 'functions +', len(constants), 'constants,', rows, 'frames')
