#!/usr/bin/env python3
"""Bounded original-C oracle for the Metronome and Sleep Talk caller preludes."""
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
p.add_argument('--output', type=Path, default=HERE.parent.parent/'Game.Presentation.Tests/Fixtures/caller-prelude-source-vectors.json')
p.add_argument('--reference-helper', type=Path, default=HERE.parent/'reference_c.py')
p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a = p.parse_args()
if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
sys.path.insert(0, str(a.reference_helper.resolve().parent))
from reference_c import compile_c, verify_reference
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function, array, macros, enum_containing, identity, COMMIT, verify
r = a.reference.resolve()
out = (a.build_dir or Path(tempfile.mkdtemp(prefix='caller-prelude-reference-'))).resolve()
out.mkdir(parents=True, exist_ok=True)
paths = ['src/battle_anim_effects_1.c', 'src/battle_anim_effects_3.c', 'src/battle_anim_mons.c', 'src/battle_anim.c', 'src/sprite.c', 'src/trig.c', 'include/sprite.h', 'src/data/battle_anim.h', 'include/gba/types.h', 'include/gba/io_reg.h', 'include/gba/defines.h', 'include/global.h', 'include/battle.h', 'include/battle_anim.h', 'include/palette.h', 'include/constants/songs.h', 'include/constants/battle_anim.h', 'include/constants/battle.h', 'include/constants/pokemon.h', 'data/battle_anim_scripts.s']
verify_reference(r)
verify(r, paths, Path(__file__).resolve().parent / 'pinned-inputs.json')
s = {p: (r/p).read_text() for p in paths}
header = s['include/sprite.h']
types = header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
oam = s['include/gba/types.h']
types = types.replace('union AffineAnimCmd\n{', 'union AffineAnimCmd\n{\n    u32 portableArmAbiAlignment;')
types += '\n' + oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]

engine = ['AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame', 'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop', 'ContinueAnimLoop', 'JumpToTopOfAnimLoop', 'BeginAffineAnim', 'ContinueAffineAnim', 'AffineAnimDelay', 'AffineAnimCmd_loop', 'BeginAffineAnimLoop', 'ContinueAffineAnimLoop', 'JumpToTopOfAffineAnimLoop', 'AffineAnimCmd_jump', 'AffineAnimCmd_end', 'AffineAnimCmd_frame', 'CopyOamMatrix', 'GetSpriteMatrixNum', 'SetSpriteOamFlipBits', 'AffineAnimStateRestartAnim', 'AffineAnimStateReset', 'AffineAnimStateStartAnim', 'ApplyAffineAnimFrameAbsolute', 'DecrementAnimDelayCounter', 'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix', 'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame', 'AnimateSprites', 'CreateSprite', 'CreateSpriteAndAnimate', 'StartSpriteAnim', 'StartSpriteAffineAnim', 'SpriteCallbackDummy', 'ResetSprite', 'CalcCenterToCornerVec', 'FreeSpriteOamMatrix', 'DestroySprite']
names = {
 'src/battle_anim_effects_1.c': ['SetSpriteNextToMonHead','AnimThoughtBubble','AnimThoughtBubble_Step','AnimMetronomeFinger','AnimMetronomeFinger_Step'],
 'src/battle_anim_effects_3.c': ['AnimLetterZ'],
 'src/battle_anim_mons.c': ['SetSpriteCoordsToAnimAttackerCoords','SetAnimSpriteInitialXOffset','GetBattlerSide','GetBattlerPosition','GetBattlerSpriteCoordAttr','RunStoredCallbackWhenAnimEnds','RunStoredCallbackWhenAffineAnimEnds','DestroySpriteAndMatrix','GetBattlerSpriteCoord','GetBattlerSpriteCoord2','GetBattlerSpriteFinal_Y','GetBattlerSpriteSubpriority'],
 'src/battle_anim.c': ['DestroyAnimSprite','Cmd_createsprite'],
 'src/trig.c': ['Sin'],
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
 'src/trig.c':['gSineTable'],
 'src/battle_anim_mons.c':['sBattlerCoords'],
 'src/battle_anim_effects_1.c':['sMetronomeThroughtBubbleAnimCmds1','sMetronomeThroughtBubbleAnimCmds2','sMetronomeThroughtBubbleAnimCmds3','sMetronomeThroughtBubbleAnimCmds4','sMetronomeThroughtBubbleAnimTable','sMetronomeFingerAffineAnimCmds1','sMetronomeFingerAffineAnimCmds2','sMetronomeFingerAffineAnimTable'],
 'src/battle_anim_effects_3.c':['sLetterZAnimCmds','sLetterZAnimTable','sLetterZAffineAnimCmds','sLetterZAffineAnimTable']}.items():
 for n in ns:add_array(path,n)
for n in ['gOamData_AffineOff_ObjNormal_32x32','gOamData_AffineNormal_ObjNormal_32x32','gOamData_AffineDouble_ObjNormal_32x32']:add_struct('src/data/battle_anim.h',n,'OamData')
for path, ns in {'src/battle_anim_effects_1.c':['gThoughtBubbleSpriteTemplate','gMetronomeFingerSpriteTemplate'],'src/battle_anim_effects_3.c':['gLetterZSpriteTemplate']}.items():
 for n in ns:add_struct(path,n,'SpriteTemplate')
defs = macros(list(s.values()), ['ANIM_TAG_LETTER_Z','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_THOUGHT_BUBBLE','ANIM_TAG_FINGER','ANIM_TAG_FINGER_2','ANIM_TAG_RED_HEART','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','SPRITE_NONE','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDCNT_TGT1_BG1','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_BG1HOFS','REG_OFFSET_BG1VOFS','SE_M_METRONOME','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','BG_PLTT_ID','PLTT_SIZE_4BPP','MON_PIC_HEIGHT','TAG_NONE','Q_8_8','BG_ANIM_PAL_1','ANIMSPRITE_IS_TARGET','IS_DOUBLE_BATTLE','MAX_BATTLERS_COUNT','B_POSITION_PLAYER_RIGHT','TILE_SIZE_4BPP'])
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
kindNames=['thought-bubble','metronome-finger','letter-z']
cases=[]
def add(kind,d,b,t,args,geometry=0,variant='authored'):
 args=list(args)+[0]*(8-len(args));rows=run([kind,d,b,t,geometry,*args]);assert not rows[-1]['alive']
 inputs=rows[0]['inputs'];root=214 if kind==2 else 118
 cases.append(dict(id=f'{kindNames[kind]}-{len(cases):03}',kind=kindNames[kind],kindId=kind,root=root,doubles=bool(d),attacker=b,target=t,sourceWidths=inputs['widths'],sourceHeights=inputs['heights'],sourceYOffsets=inputs['yOffsets'],sourceElevations=inputs['elevations'],battlerCoordinates=inputs['coordinates'],initialSubpriority=rows[0]['sprite']['subpriority'],initialArgs=args,variant=variant,rows=rows))
for d in [0,1]:
 for b in range(4 if d else 2):
  add(0,d,b,b^1,[0,100]);add(1,d,b,b^1,[0])
  add(1,d,b,b^1,[1],geometry=1,variant='target-selector-other-geometry')
  for velocity in [-1,-5,-3]:add(2,d,b,b^1,[0,20,5,velocity])
  for t in ([b,b^2,b^3] if d else [b]):add(2,d,b,t,[12,-123,5,-3],geometry=1,variant='head-order-offset-unused-y')
for b,offset in [(0,166),(0,168),(1,174),(1,176)]:add(2,0,b,b^1,[offset,20,5,-3],variant='unsigned-horizontal-edge')
for b in [0,1]:add(2,0,b,b^1,[0,32767,5,-32768],variant='signed16-y-wrap-unused-y-offset')
semantics={
 'frame0':'Original Cmd_createsprite calls CreateSpriteAndAnimate: native initializer then native frame/affine player. Later rows execute AnimateSprites.',
 'geometry':'Original battler coordinate switch, final-Y equation and attribute switch run with explicit dimensions/y-offsets/zero elevations replacing species lookup. Inputs are observed C values.',
 'scriptBoundary':'T2_READ_32 maps bounded template tokens to host pointers. Native createsprite flag, selected subpriority clamp, argument write, native callback and visual count run unchanged.',
 'callbackStorage':'Portable IDs replace only host function-pointer splitting: 1 DestroyAnimSprite, 2 AnimThoughtBubble_Step, 3 AnimMetronomeFinger_Step, 4 DestroySpriteAndMatrix.',
 'armAbi':'A u32 union member gives AffineAnimCmd the ARM stride8. Metronome affine1 uses an identical guarded copy with inert END at index-1 for the original loop-top probe. Original commands and control flow remain unchanged.',
 'matrixBoundary':'ObjAffineSet records zero-rotation diagonal inputs only; shared BIOS matrix tests independently cover coefficients. This oracle compares full native affine x/y/rotation state.',
 'destruction':'Observer snapshot captures sprite fields before exact source DestroySprite resets the native cell. alive=false identifies retired state.',
 'letter':'AnimLetterZ ignores arg1, applies args2/3 sign by attacker side, updates signed16 accumulators immediately, uses C truncating division and unsigned16 X edge check. It does not use the Rest SleepLetterZ callback.',
 'stubs':['CreateSpriteAt initializes consumed tag-backed source fields and assigns deterministic affine matrix4; original ascending CreateSprite, callback, AnimateSprites and frame/affine programs retained.','No contest, palette fade, battler body motion, graphics decompression or audio playback is simulated by this callback-only harness.']}
def representative(c):
    # Full player-side recipes, opposite-side double geometry/selector,
    # partner head ordering, and both sides of each integer boundary.
    if c['variant'] in ['unsigned-horizontal-edge', 'signed16-y-wrap-unused-y-offset']:
        return True
    if not c['doubles'] and c['attacker'] == 0:
        return True
    return c['doubles'] and c['attacker'] == 3 and (
        c['kindId'] < 2 or c['initialArgs'][3] == -3 and c['target'] in [2, 1])

if not a.full:
    cases = [c for c in cases if representative(c)]
result=dict(profile='full' if a.full else 'representative',schema='firered-caller-prelude-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,extractedFunctions=extracted,extractedData=extractedData,optimization_parity=['O0','O2'],compiler_flags=['-std=c11','-fwrapv','-fno-strict-aliasing'],semantics=semantics,cases=cases)
if a.output.exists():raise FileExistsError('Refusing to overwrite reviewed fixture: '+str(a.output))
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n')
verification=dict(schema=result['schema'],fixture=a.output.name,fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest(),generatedCSha256=hashlib.sha256(c.encode()).hexdigest(),optimizationParity=['O0','O2'],cases=len(cases),rows=sum(len(c['rows']) for c in cases),terminalFrames={name:sorted(set(c['rows'][-1]['frame'] for c in cases if c['kind']==name)) for name in kindNames})
(out/'verification.json').write_text(json.dumps(verification,indent=2)+'\n');print(json.dumps(verification,indent=2))
