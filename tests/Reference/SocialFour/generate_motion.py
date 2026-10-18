#!/usr/bin/env python3
"""Bounded original-C oracle for Taunt, Follow Me, Attract and Torment."""
import argparse
import hashlib
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path
sys.dont_write_bytecode = True
HERE = Path(__file__).resolve().parent
p = argparse.ArgumentParser()
p.add_argument('reference', type=Path)
p.add_argument('--build-dir', type=Path)
p.add_argument('--output', type=Path, help='Plain JSON destination; --full defaults to the temporary build directory.')
p.add_argument('--full', action='store_true', help='Run every original configuration and retain every raw C observation.')
p.add_argument('--reference-helper', type=Path, default=HERE.parent/'reference_c.py')
a = p.parse_args()
sys.path.insert(0, str(a.reference_helper.resolve().parent))
from reference_c import compile_c, verify_reference
from source_extract import function, array, macros, enum_containing, identity, COMMIT, verify
r = a.reference.resolve()
out = (a.build_dir or Path(tempfile.mkdtemp(prefix='social-four-reference-'))).resolve()
out.mkdir(parents=True, exist_ok=True)
if a.output is None:
 a.output = out/'social-four-source-vectors.json' if a.full else HERE.parent.parent/'Game.Presentation.Tests/Fixtures/social-four-source-vectors.json'
if a.full and a.output.resolve().is_relative_to((HERE.parent.parent/'Game.Presentation.Tests/Fixtures').resolve()):
 p.error('--full output must stay outside the local representative Fixtures directory')
paths = ['src/battle_main.c', 'src/battle_anim_effects_1.c', 'src/battle_anim_effects_2.c', 'src/battle_anim_effects_3.c', 'src/battle_anim_mons.c', 'src/battle_anim.c', 'src/sprite.c', 'src/trig.c', 'include/sprite.h', 'src/data/battle_anim.h', 'include/gba/types.h', 'include/gba/io_reg.h', 'include/gba/defines.h', 'include/global.h', 'include/battle.h', 'include/battle_anim.h', 'include/palette.h', 'include/constants/songs.h', 'include/constants/battle_anim.h', 'include/constants/battle.h', 'include/constants/pokemon.h', 'data/battle_anim_scripts.s']
verify_reference(r)
verify(r, paths)
s = {p: (r/p).read_text() for p in paths}
header = s['include/sprite.h']
types = header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
oam = s['include/gba/types.h']
types = types.replace('union AffineAnimCmd\n{', 'union AffineAnimCmd\n{\n    u32 portableArmAbiAlignment;')
types += '\n' + oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
types += '\n' + re.search(r'struct BattleAnimBgData\s*\{[\s\S]*?\n\};', s['include/battle_anim.h'])[0]
engine = ['AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame', 'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop', 'ContinueAnimLoop', 'JumpToTopOfAnimLoop', 'BeginAffineAnim', 'ContinueAffineAnim', 'AffineAnimDelay', 'AffineAnimCmd_loop', 'BeginAffineAnimLoop', 'ContinueAffineAnimLoop', 'JumpToTopOfAffineAnimLoop', 'AffineAnimCmd_jump', 'AffineAnimCmd_end', 'AffineAnimCmd_frame', 'CopyOamMatrix', 'GetSpriteMatrixNum', 'SetSpriteOamFlipBits', 'AffineAnimStateRestartAnim', 'AffineAnimStateReset', 'AffineAnimStateStartAnim', 'ApplyAffineAnimFrameAbsolute', 'DecrementAnimDelayCounter', 'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix', 'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame', 'AnimateSprites', 'CreateSprite', 'CreateSpriteAndAnimate', 'StartSpriteAnim', 'StartSpriteAffineAnim', 'SpriteCallbackDummy', 'ResetSprite', 'CalcCenterToCornerVec', 'FreeSpriteOamMatrix', 'DestroySprite']
names = {
 'src/battle_anim_effects_1.c': ['SetSpriteNextToMonHead', 'AnimThoughtBubble', 'AnimThoughtBubble_Step', 'AnimMetronomeFinger_Step', 'AnimFollowMeFinger', 'AnimFollowMeFinger_Step1', 'AnimFollowMeFinger_Step2', 'AnimTauntFinger', 'AnimTauntFinger_Step1', 'AnimTauntFinger_Step2'],
 'src/battle_anim_effects_2.c': ['AnimRedHeartProjectile', 'AnimRedHeartProjectile_Step', 'AnimRedHeartRising', 'AnimRedHeartRising_Step', 'AnimTask_HeartsBackground', 'AnimTask_HeartsBackground_Step'],
 'src/battle_anim_effects_3.c': ['AnimTask_TormentAttacker', 'TormentAttacker_Step', 'TormentAttacker_Callback'],
 'src/battle_anim_mons.c': ['InitAnimLinearTranslation', 'AnimTranslateLinear', 'SetAnimSpriteInitialXOffset', 'InitSpritePosToAnimAttacker', 'GetBattlerSide', 'GetBattlerPosition', 'GetBattlerSpriteCoordAttr', 'GetAnimBattlerSpriteId', 'WaitAnimForDuration', 'RunStoredCallbackWhenAnimEnds', 'RunStoredCallbackWhenAffineAnimEnds', 'DestroySpriteAndMatrix', 'PrepareAffineAnimInTaskData', 'RunAffineAnimFromTaskData', 'SetSpriteRotScale', 'ShouldRotScaleSpeciesBeFlipped', 'PrepareBattlerSpriteForRotScale', 'ResetSpriteRotScale', 'SetBattlerSpriteYOffsetFromYScale', 'GetBattleAnimBg1Data', 'GetBattlerSpriteCoord', 'GetBattlerSpriteCoord2', 'GetBattlerSpriteFinal_Y', 'GetBattlerSpriteSubpriority'],
 'src/battle_anim.c': ['DestroyAnimSprite', 'BattleAnimAdjustPanning', 'Cmd_createsprite'],
 'src/trig.c': ['Sin', 'Cos'],
 'src/sprite.c': engine,
}
bodies, extracted = [], []
def record(path, name, original, adaptation=None):
 start = s[path].index(original.rstrip())
 return dict(path=path, name=name, startLine=s[path][:start].count('\n')+1, endLine=s[path][:start+len(original.rstrip())].count('\n')+1, sha256=hashlib.sha256(original.encode()).hexdigest(), adaptation=adaptation)
for path, ns in names.items():
 for n in ns:
  original = function(s[path], n)
  f, adaptation = original, None
  if n == 'CreateSprite':
   f = f.replace('u8 CreateSprite(', 'u8 SourceCreateSprite(')
   adaptation = 'symbol renamed to SourceCreateSprite for allocation instrumentation wrapper'
  if n == 'DestroySprite':
   f = f.replace('void DestroySprite(', 'void SourceDestroySprite(')
   adaptation = 'symbol renamed for pre-reset observer snapshot and destruction event wrapper; source reset behavior unchanged'
  if n == 'GetBattlerSpriteCoord':
   begin = f.index('        if (GetBattlerSide(battlerId) != B_SIDE_PLAYER)')
   end = f.index('        if (coordType == BATTLER_COORD_Y_PIC_OFFSET)', begin)
   f = f[:begin] + '        species = 0; /* explicit coordinate inputs */\n' + f[end:]
   adaptation = 'species lookup boundary replaced by explicit y-offset/elevation callbacks; original coordinate switch and original GetBattlerSpriteFinal_Y retained'
  if n == 'GetBattlerSpriteCoord2':
   begin = f.index('        spriteInfo =')
   end = f.index('        if (coordType == BATTLER_COORD_Y_PIC_OFFSET)', begin)
   f = f[:begin] + '        species = 0; /* explicit coordinate inputs */\n' + f[end:]
   adaptation = 'species lookup boundary replaced by explicit y-offset/elevation callbacks; original coordinate branch and original GetBattlerSpriteFinal_Y retained'
  if n == 'GetBattleAnimBg1Data':
   f = f.replace('void GetBattleAnimBg1Data(', 'void SourceGetBattleAnimBg1Data(')
   adaptation = 'symbol renamed for external-call event instrumentation; original fixed BG1 palette8/tilesOffset512 retained'
  if n == 'RunAffineAnimFromTaskData':
   f = f.replace('sAnimTaskAffineAnim = LoadPointerFromVars(task->data[13], task->data[14]) + (task->data[7] << 3);', 'sAnimTaskAffineAnim = (const union AffineAnimCmd *)(LoadPointerFromVars(task->data[13], task->data[14]) + (task->data[7] << 3));')
   adaptation = 'portable pointer representation: LoadPointerFromVars returns byte pointer instead of GNU void-pointer arithmetic; explicit command-pointer cast only'
  if n == 'GetBattlerSpriteCoordAttr':
   switch = f.index('    switch (attr)')
   f = f[:f.index('{')+1] + '\n    s32 ret;\n    const struct MonCoords *coords = &explicitCoords[battlerId];\n' + f[switch:]
   adaptation = 'species/personality table lookup replaced by explicit source dimensions; original attribute switch retained verbatim'
  bodies.append(f)
  extracted.append(record(path, n, original, adaptation))
protos = '\n'.join(f[:f.index('{')].strip()+';' for f in bodies)
arrays, extractedData = '', []
def add_array(path, name):
 global arrays
 original = array(s[path], name)
 arrays += original + '\n'
 adaptation = None
 if name == 'sMetronomeFingerAffineAnimCmds2':
  guarded = original.replace(name, name+'_portableGuarded', 1)
  guarded = guarded.replace('{\n', '{\n    AFFINEANIMCMD_END, /* bounded inert command[-1] probe at loop top */\n', 1)
  arrays += guarded + '\n#define '+name+' ('+name+'_portableGuarded + 1)\n'
  adaptation = 'original array retained; table points to equivalent guarded copy with one END prefix so source loop-top command[-1] probe is in-bounds; the probe value is inert when index0 breaks'
 extractedData.append(record(path, name, original, adaptation))
def add_struct(path, name, typ):
 global arrays
 original = re.search(r'(?:static )?const struct '+typ+' '+name+r'\s*=\s*\{[\s\S]*?\n\};', s[path])[0] + '\n'
 arrays += original + '\n'
 extractedData.append(record(path, name, original))
# Dummy values are literal source sentinel equivalents and sDummySprite is original.
arrays += 'static const union AnimCmd sDummyAnim={.type=-1}; const union AnimCmd *const gDummySpriteAnimTable[]={&sDummyAnim};\nstatic const union AffineAnimCmd sDummyAffineAnim={.type=32767};const union AffineAnimCmd *const gDummySpriteAffineAnimTable[]={&sDummyAffineAnim};\nconst struct OamData gDummyOamData=DUMMY_OAM_DATA;extern const struct SpriteTemplate gDummySpriteTemplate;\n'
add_struct('src/sprite.c', 'sDummySprite', 'Sprite')
add_struct('src/sprite.c', 'gDummySpriteTemplate', 'SpriteTemplate')
for n in ['sAnimFuncs', 'sAffineAnimFuncs', 'sAnimCmdFuncs', 'sAffineAnimCmdFuncs', 'sCenterToCornerVecTable']:
 add_array('src/sprite.c', n)
for path, ns in {
 'src/trig.c': ['gSineTable'],
 'src/battle_anim_mons.c': ['sBattlerCoords'],
 'src/battle_anim_effects_1.c': ['sMetronomeThroughtBubbleAnimCmds1', 'sMetronomeThroughtBubbleAnimCmds2', 'sMetronomeThroughtBubbleAnimCmds3', 'sMetronomeThroughtBubbleAnimCmds4', 'sMetronomeThroughtBubbleAnimTable', 'sMetronomeFingerAffineAnimCmds1', 'sMetronomeFingerAffineAnimCmds2', 'sMetronomeFingerAffineAnimTable', 'sTauntFingerAnimCmds1', 'sTauntFingerAnimCmds2', 'sTauntFingerAnimCmds3', 'sTauntFingerAnimCmds4', 'sTauntFingerAnimTable'],
 'src/battle_anim_effects_3.c': ['sAffineAnims_Torment']}.items():
 for n in ns: add_array(path,n)
for n in ['gOamData_AffineOff_ObjNormal_16x16', 'gOamData_AffineOff_ObjNormal_32x32', 'gOamData_AffineNormal_ObjNormal_32x32']:
 add_struct('src/data/battle_anim.h', n, 'OamData')
for path, ns in {
 'src/battle_anim_effects_1.c': ['gThoughtBubbleSpriteTemplate','gTauntFingerSpriteTemplate','gFollowMeFingerSpriteTemplate'],
 'src/battle_anim_effects_2.c': ['gRedHeartProjectileSpriteTemplate','gRedHeartRisingSpriteTemplate']}.items():
 for n in ns: add_struct(path, n, 'SpriteTemplate')
defs = macros(list(s.values()), ['sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY','sTransl_Speed','sTransl_Duration','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_THOUGHT_BUBBLE','ANIM_TAG_FINGER','ANIM_TAG_FINGER_2','ANIM_TAG_RED_HEART','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','SPRITE_NONE','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDCNT_TGT1_BG1','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_BG1HOFS','REG_OFFSET_BG1VOFS','SE_M_METRONOME','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','BG_PLTT_ID','PLTT_SIZE_4BPP','MON_PIC_HEIGHT','TAG_NONE','Q_8_8','BG_ANIM_PAL_1','ANIMSPRITE_IS_TARGET','IS_DOUBLE_BATTLE','MAX_BATTLERS_COUNT','B_POSITION_PLAYER_RIGHT','TILE_SIZE_4BPP'])
defs += '\n'.join(enum_containing(s['include/battle_anim.h'], n) for n in ['BATTLER_COORD_X','BATTLER_COORD_ATTR_HEIGHT','BG_ANIM_PRIORITY'])
defs += '\n' + oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
# Preserve source multiline DUMMY_OAM_DATA macro rather than the single-line macro parser.
defs += '\n' + s['src/sprite.c'][s['src/sprite.c'].index('#define DUMMY_OAM_DATA'):s['src/sprite.c'].index('#define ANIM_END')]
c = (HERE/'motion-prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',protos).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(bodies))
(out/'motion.c').write_text(c)
for opt in ['O0','O2']:
 compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(out/'motion.c'),'-o',str(out/('motion-'+opt))])
def run(args):
 results = [subprocess.check_output([str(out/('motion-'+opt)), *map(str,args)]) for opt in ['O0','O2']]
 assert results[0] == results[1], ('O0/O2 mismatch',args)
 return [json.loads(row) for row in results[0].splitlines()]
kindNames = ['thought-bubble','taunt-finger','follow-me-finger','red-heart-projectile','red-heart-rising','hearts-background','torment-attacker']
roots = [269,269,266,213,213,213,259]
coordinates = [[[72,80,72,88],[176,40,176,40],[48,40,48,48],[112,80,112,80]],[[32,80,32,88],[200,40,200,40],[90,88,90,96],[152,32,152,32]]]
subpriorities = [30,40,20,50]
cases, taskCases, directCases, integratedCases = [], [], [], []

# Reviewable branch representatives, never positional/every-Nth sampling.
REPRESENTATIVE = {
 # Both sides/selectors; hold 45/1/2; top clamp; partner destination; rising-heart priority clamp.
 'cases': 'thought-bubble-000 thought-bubble-004 thought-bubble-019 thought-bubble-122 thought-bubble-124 taunt-finger-001 taunt-finger-016 taunt-finger-089 taunt-finger-112 follow-me-finger-002 follow-me-finger-017 follow-me-finger-123 follow-me-finger-125 red-heart-projectile-003 red-heart-projectile-018 red-heart-projectile-087 red-heart-rising-007 red-heart-rising-121'.split(),
 # BG1 lifecycle; Torment side/geometry, capacity 0/1/5/full, foreign adoption below/above six.
 'taskCases': 'hearts-background-000 torment-attacker-001 torment-attacker-005 torment-attacker-018 torment-attacker-019 torment-attacker-021 torment-attacker-022 torment-attacker-023 torment-attacker-024 torment-attacker-025'.split(),
 # Every frame selector/base-flip combination and both finite affine programs.
 'directCases': 'frame-0-0-baseflip0 frame-0-0-baseflip1 frame-0-1-baseflip0 frame-0-1-baseflip1 frame-0-2-baseflip0 frame-0-2-baseflip1 frame-0-3-baseflip0 frame-0-3-baseflip1 frame-1-0-baseflip0 frame-1-0-baseflip1 frame-1-1-baseflip0 frame-1-1-baseflip1 frame-1-2-baseflip0 frame-1-2-baseflip1 frame-1-3-baseflip0 frame-1-3-baseflip1 follow-affine-0 follow-affine-1'.split(),
 # All eight recipes plus BG1, with clamped and unclamped subpriority.
 'integratedCases': 'hearts-overlay-00 hearts-overlay-03'.split(),
}
caseNumbers = {group: 0 for group in REPRESENTATIVE}
def add(kind,d,b,t,args=(),geometry=0,capacity=60,preexisting=0,variant='source'):
 group = 'cases' if kind < 5 else 'taskCases'
 caseId = f'{kindNames[kind]}-{caseNumbers[group]:03}'
 caseNumbers[group] += 1
 if not a.full and caseId not in REPRESENTATIVE[group]: return
 args = list(args)+[0]*(8-len(args))
 rows = run([kind,d,b,t,geometry,capacity,preexisting,*args])
 assert not rows[-1]['alive'], (kind,d,b,t,variant)
 inputs = rows[0]['inputs']
 assert inputs['subpriorities'] == subpriorities
 dest = cases if kind < 5 else taskCases
 dest.append(dict(id=caseId, kind=kindNames[kind],kindId=kind,root=roots[kind],doubles=bool(d),attacker=b,target=t,visibleMask=15 if d else 3,geometry=geometry,sourceWidths=inputs['widths'],sourceHeights=inputs['heights'],sourceYOffsets=inputs['yOffsets'],sourceElevations=inputs['elevations'],battlerCoordinates=inputs['coordinates'],variant=variant,initialArgs=args,initialPosition=inputs['coordinates'][t][2:],initialSubpriority=max(3,subpriorities[b]-[11,12,2,3,40][kind]) if kind<3 or kind==4 else max(3,subpriorities[t]-3),additionalFreeSlots=capacity,preexistingMatchingSlots=list(range(4,4+preexisting)),rows=rows))
for d in [0,1]:
 for b in range(4 if d else 2):
  for t in [b^1]+([b^3,b^2] if d else []):
   for kind,args in [(0,[0,45]),(1,[0]),(2,[0]),(3,[20,-8])]: add(kind,d,b,t,args)
  for kind in [0,1,2]:
   add(kind,d,b,b^1,[1,1] if kind==0 else [1],geometry=1,variant='target-selector-alternate-dimensions')
  for recipe in [[16,256,0],[224,240,15],[126,272,30],[80,224,45],[170,272,60],[40,256,75],[112,256,90],[200,272,90]]:
   add(4,d,b,b^1,recipe)
  add(5,d,b,b^1)
  add(6,d,b,b^1)
  add(6,d,b,b^1,geometry=1,variant='alternate-dimensions')
for n in [0,1,3,5]: add(6,0,0,1,capacity=n,variant=f'capacity-{n}')
for n,pre in [(0,2),(3,2),(6,2),(6,6)]: add(6,1,1,0,capacity=n,preexisting=pre,variant=f'capacity-{n}-preexisting-{pre}')
for selector in [0,1]:
 add(0,1,3,2,[selector,2],geometry=1,variant='short-hold')
 add(2,1,3,1,[selector],geometry=1,variant='top-clamp')
# Explicit raw frame / affine vectors exercise the tables independently of callbacks.
for kind in [0,1]:
 for anim in range(4):
  for h in [0,1]:
   rows = run([7+kind,0,0,1,0,60,0,anim,h,1,0,0,0,0,0])
   directCases.append(dict(id=f'frame-{kind}-{anim}-baseflip{h}',kind=kindNames[kind],kindId=kind,anim=anim,baseHFlip=h,baseVFlip=1,rows=rows))
for anim in [0,1]:
 rows = run([9,0,0,1,0,60,0,anim,0,0,0,0,0,0,0])
 directCases.append(dict(id=f'follow-affine-{anim}',kind='follow-me-finger',kindId=2,affineAnim=anim,rows=rows))
for index,(d,b,t) in enumerate([(0,0,1),(0,1,0),(1,2,3),(1,3,2)]):
 caseId = f'hearts-overlay-{index:02}'
 if not a.full and caseId not in REPRESENTATIVE['integratedCases']: continue
 rows=run([10,d,b,t,0,60,0,*([0]*8)])
 integratedCases.append(dict(id=caseId,kind='hearts-overlay',root=213,doubles=bool(d),attacker=b,target=t,recipes=[[16,256,0],[224,240,15],[126,272,30],[80,224,45],[170,272,60],[40,256,75],[112,256,90],[200,272,90]],rows=rows))
semantics = {
 'kindNames':kindNames,
 'frame0':'Sprite CreateSpriteAndAnimate calls initializer then frame/affine engine. Task frame0 is immediate initializer only. Later integrated/Torment frames use source AnimateSprites, inactive palette-fade boundary, then source task callback, matching BattleMainCB2 battle_main.c1447-1453.',
 'creation':'Source Cmd_createsprite supplies selected target X_2,Y_PIC_OFFSET even when subpriority uses attacker. Bubbles/fingers override positions; rising hearts use explicit screen coordinates. Subpriority offsets are 11/12/2/3/40, with native signed clamp to minimum3 for rising hearts.',
 'flipFields':'hFlip/vFlip are raw base sprite flags; nonaffine effective flips are oamMatrixNum bits3/4 after source frame engine XOR. Affine matrixNum is a matrix index.',
 'coordinates':'Each case stores observed source battlerCoordinates and row0.inputs; original GetBattlerSpriteFinal_Y applies explicit YOffset/elevation, player+8 and u8 wrap/cap104. All fixture metadata is copied from observed C input records.',
 'battlerSubpriority':subpriorities,
 'torment':'Only task increments visual count. Six raw source CreateSprite calls are attempted even when full; each emits sound. No child initializer executes. Cleanup scans global template pointers in ascending slots, adopts at most six including live preexisting matching-template sprites, rewrites data0=taskId,data1=6, anim2,callback. Child callback decrements task.data6 and uses DestroySprite without decrementing visual count. Actual destruction source ResetSprite clears template; no stale inactive matching-template states are synthesized. Preexisting sprites are live raw creations with dummy callbacks, as produced by another overlapping Torment task. Existing producer is outside this bounded slice.',
 'capacity':'Slots0..3 reserve battlers; live matching-template preexisting slots begin4; the next additionalFreeSlots are free; all remaining slots contain live nonmatching filler sprites. Source CreateSprite ascending-slot allocator is unchanged.',
 'affineLoopGuard':'Original JumpToTopOfAffineAnimLoop probes command[-1] at index0 before its bounds break. FollowMe affine1 table uses an identical command copy with an END prefix; its public pointer starts after that prefix. This makes the source probe valid host memory without changing source control flow or commands. The probe value is inert because index0 either skips the while or immediately breaks; both reach the same decrement/restart.',
 'armAbi':'AffineAnimCmd gains a portable u32 alignment member so sizeof is8, matching ARM structure layout and native task-affine byte-stride8. The extracted source task body has only an explicit pointer cast for portable typed-byte arithmetic.',
 'scriptBoundary':'Original Cmd_createsprite is executed using bounded source bytecode records. T2_READ_32 maps deterministic template tokens to host pointers; no pointer bits are truncated. The remaining flag/arg/subpriority clamp, original initializer/engine sequence, and visual counter increment execute unchanged.',
 'callbackStorage':'Portable callback IDs in data6/data7 replace host-address splitting only. ID map:1 DestroyAnimSprite,2 AnimThoughtBubble_Step,3 AnimTauntFinger_Step2,4 AnimFollowMeFinger_Step1,5 DestroySpriteAndMatrix,6 AnimRedHeartRising_Step. Task affine pointer data13/data14 maps to stable ID1. Original callbacks remain unchanged; task-affine body has a portable pointer cast only.',
 'hearts':'BG1 starts with attribute priority2,size2,charbase2 and scroll[19,-7]; BG2 sentinel attributes[2,0,0,3,2,0,0] and scroll[37,-23] stay unchanged. Original task sets BG1 size0,priority3,charbase1,scroll0; fades64,holds141,fades64; cleanup BG1 at270, final reset/destroy271. External graphics/palette calls are events, no asset content.',
 'taskAffine':'Original task-affine scale/rotation state and SetSpriteRotScale/SetBattlerSpriteYOffsetFromYScale run. Explicit source y-offsets replace species lookup. BIOS ObjAffineSet boundary computes exact zero-rotation diagonal input values needed by Torment; nonzero rotation matrix coefficients are intentionally unmodeled and irrelevant to FollowMe callback/frame state.',
 'destroyedRows':'Destroyed sprites retain their pre-reset state in observer snapshots; source live slot is reset by exact ResetSprite. destroyedSnapshot=true marks that observer state and alive remains false.',
 'events':{'1':['SetGpuReg','offset','value'], '2':['SetAnimBgAttribute','bg','attribute','value'], '3':['CreateSprite','slot-or64','templateId','x','y','subpriority','attempt'], '4':['DestroySprite','slot','instance','data0','animEnded','templateId'], '5':['GetBattleAnimBg1Data','bg','palette','tilesOffset'], '6':['FreeOamMatrix','matrix'], '7':['PlaySE12WithPanning','sound','pan'], '10':['DestroyAnimVisualTask','taskId'], '11':['AnimLoadCompressedBgTilemap','bg','assetId'], '12':['AnimLoadCompressedBgGfx','bg','assetId','tilesOffset'], '13':['LoadCompressedPalette','assetId','offset','size'], '14':['InitBattleAnimBg','bg'], '15':['StoreCallback','slot','callbackId'], '18':['FrameOrder','phase1sprites/2fade/3tasks']},
 'stubs':['Sprite sheet resources are available; source CreateSprite/AnimateSprites/frame/affine/ResetSprite retained. CreateSpriteAt is bounded initialization equivalent for tag-backed sprites with deterministic matrix indices.','Palette fade is inactive in this slice and instrumented at its native execution boundary; no unsupported palette-fade algorithm is substituted.','Graphics/audio calls are instrumented, no asset decompression or hardware execution.','Non-contest; battle positions identity; all selected battlers visible.','Battler dimensions, y-offsets and zero elevation replace species-dependent table lookup only; original coordinate switches, GetBattlerSpriteFinal_Y, coordinate-attribute and y-scale equations execute.','Direct table cases stop on source animEnded/affineAnimEnded and retain two additional terminal frames to expose stable sentinels.']
}

def keep(value, names):
 return {name: value[name] for name in names.split()}
def sprite_expectation(value, group, kind):
 if group == 'directCases':
  fields = 'xScale yScale rotation affineEnded' if kind == 2 else 'tile oamMatrixNum animDelay animEnded'
 else:
  fields = 'alive position tile visible priority subpriority objMode affineMode oamMatrixNum'
  if group == 'cases' and kind < 2: fields += ' animEnded animDelay'
  if group == 'cases' and kind == 2: fields += ' xScale yScale rotation affineEnded'
 return keep(value, fields)
def representative_case(case, group):
 # Metadata is copied from observed C inputs once; row.inputs and unused raw registers are diagnostics.
 names = 'id kindId root doubles attacker target visibleMask sourceWidths sourceHeights sourceYOffsets initialArgs initialSubpriority additionalFreeSlots preexistingMatchingSlots recipes anim affineAnim baseHFlip baseVFlip'
 result = {name: case[name] for name in names.split() if name in case}
 rows = []
 kind = case.get('kindId')
 for row in case['rows']:
  expected = keep(row, 'frame')
  if group == 'directCases': expected['sprite'] = sprite_expectation(row['sprite'], group, kind)
  elif group == 'cases':
   expected.update(keep(row, 'args visualCount'))
   expected['sprite'] = sprite_expectation(row['sprite'], group, kind)
  else:
   expected.update(keep(row, 'args alive visualCount'))
   expected['sprites'] = [dict(slot=sprite['slot'], **sprite_expectation(sprite, group, kind)) for sprite in row['sprites']]
   if kind == 5 or group == 'integratedCases':
    expected.update(registers=row['registers'][:2], bg1Attributes=row['bg1Attributes'][:5])
   else:
    expected.update(data=row['data'][:7], battler=keep(row['battler'], 'position affineMode'), battlerMatrix=row['battlerMatrix'], events=[event for event in row['events'] if event[0] in (3,7)])
  rows.append(expected)
 result['rows'] = rows
 return result
result=dict(profile='full' if a.full else 'representative',schema='firered-social-four-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,extractedFunctions=extracted,extractedData=extractedData,optimization_parity=['O0','O2'],compiler_flags=['-std=c11','-fwrapv','-fno-strict-aliasing'],semantics=semantics,cases=cases,taskCases=taskCases,directCases=directCases,integratedCases=integratedCases)
if not a.full:
 for group, ids in REPRESENTATIVE.items():
  selected = result[group]
  assert set(case['id'] for case in selected) == set(ids), (group, 'missing representative')
  result[group] = [representative_case(case, group) for case in selected]
a.output.parent.mkdir(parents=True,exist_ok=True)
a.output.write_text(json.dumps(result, separators=(',', ':'))+'\n')
verification=dict(schema=result['schema'],fixture=a.output.name,fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest(),generatedCSha256=hashlib.sha256(c.encode()).hexdigest(),optimizationParity=['O0','O2'],groups={key:{'cases':len(result[key]),'rows':sum(len(case['rows']) for case in result[key])} for key in ['cases','taskCases','directCases','integratedCases']},terminalFrames={name:sorted(set(case['rows'][-1]['frame'] for case in cases+taskCases if case['kind']==name)) for name in kindNames})
(out/'verification.json').write_text(json.dumps(verification,indent=2)+'\n')
print(json.dumps(verification,indent=2))
