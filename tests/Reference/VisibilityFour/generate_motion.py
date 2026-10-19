#!/usr/bin/env python3
"""Bounded original-C oracle for native visibility flags, fades, palettes, clones and shrinking ring."""
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
p.add_argument('--output', type=Path, default=HERE.parent.parent/'Game.Presentation.Tests/Fixtures/visibility-four-source-vectors.json')
p.add_argument('--reference-helper', type=Path, default=HERE.parent/'reference_c.py')
p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a = p.parse_args()
sys.path.insert(0, str(a.reference_helper.resolve().parent))
from reference_c import compile_c, verify_reference
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function, array, macros, enum_containing, identity, COMMIT, verify
r = a.reference.resolve()
out = (a.build_dir or Path(tempfile.mkdtemp(prefix='visibility-four-reference-'))).resolve()
out.mkdir(parents=True, exist_ok=True)
paths = ['include/constants/rgb.h', 'src/battle_main.c', 'src/battle_anim_effects_2.c', 'src/battle_anim_dark.c', 'src/battle_anim_normal.c', 'src/battle_anim_utility_funcs.c', 'src/battle_anim_effects_3.c', 'src/battle_anim_mons.c', 'src/battle_anim.c', 'src/sprite.c', 'src/trig.c', 'include/sprite.h', 'src/data/battle_anim.h', 'include/gba/types.h', 'include/gba/io_reg.h', 'include/gba/defines.h', 'include/global.h', 'include/battle.h', 'include/battle_anim.h', 'include/palette.h', 'include/constants/songs.h', 'include/constants/battle_anim.h', 'include/constants/battle.h', 'include/constants/pokemon.h', 'data/battle_anim_scripts.s']
verify_reference(r)
verify(r, paths, Path(__file__).resolve().parent / 'pinned-inputs.json')
s = {p: (r/p).read_text() for p in paths}
header = s['include/sprite.h']
types = header[header.index('struct AnimFrameCmd'):header.index('\nenum\n{\n    SUBSPRITES_OFF')]
oam = s['include/gba/types.h']
types = types.replace('union AffineAnimCmd\n{', 'union AffineAnimCmd\n{\n    u32 portableArmAbiAlignment;')
types += '\n' + oam[oam.index('struct OamData'):oam.index('#define ST_OAM_HFLIP')]
types += '\n' + re.search(r'struct BattleAnimBgData\s*\{[\s\S]*?\n\};', s['include/battle_anim.h'])[0]
engine = ['AnimateSprite', 'BeginAnim', 'ContinueAnim', 'AnimCmd_frame', 'AnimCmd_end', 'AnimCmd_jump', 'AnimCmd_loop', 'BeginAnimLoop', 'ContinueAnimLoop', 'JumpToTopOfAnimLoop', 'BeginAffineAnim', 'ContinueAffineAnim', 'AffineAnimDelay', 'AffineAnimCmd_loop', 'BeginAffineAnimLoop', 'ContinueAffineAnimLoop', 'JumpToTopOfAffineAnimLoop', 'AffineAnimCmd_jump', 'AffineAnimCmd_end', 'AffineAnimCmd_frame', 'CopyOamMatrix', 'GetSpriteMatrixNum', 'SetSpriteOamFlipBits', 'AffineAnimStateRestartAnim', 'AffineAnimStateReset', 'AffineAnimStateStartAnim', 'ApplyAffineAnimFrameAbsolute', 'DecrementAnimDelayCounter', 'DecrementAffineAnimDelayCounter', 'ApplyAffineAnimFrameRelativeAndUpdateMatrix', 'ConvertScaleParam', 'GetAffineAnimFrame', 'ApplyAffineAnimFrame', 'AnimateSprites', 'CreateSprite', 'CreateSpriteAndAnimate', 'StartSpriteAnim', 'StartSpriteAffineAnim', 'SpriteCallbackDummy', 'ResetSprite', 'CalcCenterToCornerVec', 'FreeSpriteOamMatrix', 'DestroySprite']
names = {
 'src/battle_anim_normal.c':['UnpackSelectedBattlePalettes'],
 'src/battle_anim_dark.c':['AnimTask_AttackerFadeToInvisible','AnimTask_AttackerFadeToInvisible_Step','AnimTask_AttackerFadeFromInvisible','AnimTask_AttackerFadeFromInvisible_Step','AnimTask_InitAttackerFadeFromInvisible'],
 'src/battle_anim_utility_funcs.c':['AnimTask_SetCamouflageBlend','AnimTask_BlendBattleAnimPalExclude','StartBlendAnimSpriteColor','AnimTask_BlendSpriteColor_Step2','AnimTask_SetAllNonAttackersInvisiblity','AnimTask_SetAttackerInvisibleWaitForSignal','AnimTask_WaitAndRestoreVisibility'],
 'src/battle_anim_effects_3.c':['AnimTask_OdorSleuthMovement','AnimTask_OdorSleuthMovementWaitFinish','MoveOdorSleuthClone'],
 'src/battle_anim_mons.c':['GetBattlerSide','GetBattlerPosition','IsBattlerSpritePresent','GetAnimBattlerSpriteId','CloneBattlerSpriteWithBlend','DestroySpriteWithActiveSheet','PrepareBattlerSpriteForRotScale','GetSpritePalIdxByBattler','GetBattlerSpriteBGPriorityRank','GetBattlePalettesMask','SetAnimSpriteInitialXOffset','DestroySpriteAndMatrix','AnimSpriteOnMonPos','InitSpritePosToAnimAttacker','InitSpritePosToAnimTarget','GetBattlerSpriteCoord','GetBattlerSpriteCoord2','GetBattlerSpriteFinal_Y','GetBattlerSpriteSubpriority'],
 'src/battle_anim.c':['IsBattlerSpriteVisible','Cmd_visible','Cmd_invisible','DestroyAnimSprite','Cmd_createsprite'],
 'src/trig.c':['Sin','Cos'], 'src/sprite.c':engine,
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
for path, ns in {'src/trig.c':['gSineTable'],'src/battle_anim_mons.c':['sBattlerCoords'],'src/battle_anim_effects_2.c':['sThinRingShrinkingAffineAnimCmds','sThinRingShrinkingAffineAnimTable']}.items():
 for n in ns:add_array(path,n)
add_struct('src/data/battle_anim.h','gOamData_AffineDouble_ObjBlend_64x64','OamData')
add_struct('src/battle_anim_effects_2.c','gThinRingShrinkingSpriteTemplate','SpriteTemplate')
defs = macros(list(s.values()), ['ST_OAM_AFFINE_ON_MASK','ANIM_TAG_THIN_RING','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BIT_FLANK','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','ANIM_DEF_PARTNER','SPRITE_NONE','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDCNT_TGT1_BG1','BLDCNT_TGT1_BG2','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','BG_PLTT_ID','PLTT_SIZE_4BPP','MON_PIC_HEIGHT','TAG_NONE','Q_8_8','BG_ANIM_PAL_1','BG_ANIM_PAL_2','ANIMSPRITE_IS_TARGET','IS_DOUBLE_BATTLE','MAX_BATTLERS_COUNT','B_POSITION_PLAYER_RIGHT','B_POSITION_OPPONENT_RIGHT','TILE_SIZE_4BPP','RGB','MON_DATA_HP','BATTLE_TERRAIN_GRASS','BATTLE_TERRAIN_LONG_GRASS','BATTLE_TERRAIN_SAND','BATTLE_TERRAIN_UNDERWATER','BATTLE_TERRAIN_WATER','BATTLE_TERRAIN_POND','BATTLE_TERRAIN_MOUNTAIN','BATTLE_TERRAIN_CAVE','BATTLE_TERRAIN_BUILDING','BATTLE_TERRAIN_PLAIN'])
defs += '\n'.join(enum_containing(s['include/battle_anim.h'], n) for n in ['BATTLER_COORD_X'])
defs += '\n' + oam[oam.index('#define ST_OAM_HFLIP'):oam.index('struct BgAffineSrcData')]
defs += '\n' + s['src/sprite.c'][s['src/sprite.c'].index('#define DUMMY_OAM_DATA'):s['src/sprite.c'].index('#define ANIM_END')]
c = (HERE/'motion-prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',protos).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(bodies))
(out/'motion.c').write_text(c)
for opt in ['O0','O2']:
 compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(out/'motion.c'),'-o',str(out/('motion-'+opt))])
def run(args):
 results = [subprocess.check_output([str(out/('motion-'+opt)), *map(str,args)]) for opt in ['O0','O2']]
 assert results[0] == results[1], ('O0/O2 mismatch',args)
 return [json.loads(row) for row in results[0].splitlines()]
kinds=['fade-out','fade-in','fade-init','logical-wait','all-others','camouflage','exclude-palette','odor-clones','visibility-commands','prepare-affine','shrinking-ring']
cases=[]
def add(kind,d,actor,target,present,logical,sprite,args=(),terrain=0,capacity=60,geometry=0):
 args=list(args)+[0]*(8-len(args));rows=run([kind,d,actor,target,present,logical,sprite,terrain,capacity,geometry,*args])
 cases.append(dict(id=f'{kinds[kind]}-{len(cases):03}',kind=kinds[kind],kindId=kind,doubles=bool(d),attacker=actor,target=target,presentMask=present,logicalMask=logical,spriteMask=sprite,terrain=terrain,capacity=capacity,geometry=geometry,initialArgs=args,rows=rows))
for d in [0,1]:
 for actor in range(4 if d else 2):
  target=actor^1;present=15 if d else 3
  for logical,sprite in [(0,0),(0,present),(present,0),(present,present)]:
   for k in [0,1]:add(k,d,actor,target,present,logical,sprite,[1])
   for k in [2,3,7,8,9]:add(k,d,actor,target,present,logical,sprite)
   for value in [0,1]:add(4,d,actor,target,present,logical,sprite,[value])
  for delay in [0,4]:add(0,d,actor,target,present,0,0,[delay])
  for k in [10]:add(k,d,actor,target,present,0,0,[0,0,0,0])
add(0,0,0,1,3,0,0,[-1])
for terrain in range(10):
 for delay,initial,final in [(3,0,14),(0,0,0)]:add(5,1,2,1,15,0,0,[2,delay,initial,final],terrain)
for selector in range(8):
 for logical,sprite,present in [(0,0,15),(15,0,15),(0,15,15),(15,15,15),(0,0,5)]:add(6,1,0,1,present,logical,sprite,[selector,0,0,16,0])
for capacity in [0,1,2,3]:
 for logical in [0,15]:add(7,1,3,2,15,logical,15,capacity=capacity,geometry=1)
for present in [0,1,4,5,14]:
 for k in [7,8,9]:add(k,1,0,1,present,15,15)
def representative(c):
    # The wrap-delay trace belongs to full (all native frames, no thinning).
    if c['kindId'] == 0 and c['initialArgs'][0] == -1:
        return False
    # Every dual-invisibility truth-table combination on the player side;
    # reversed double-side callbacks also exercise different OAM ownership.
    if not c['doubles'] and c['attacker'] == 0:
        return True
    if c['doubles'] and c['attacker'] == 3 and c['logicalMask'] == c['spriteMask'] == 0:
        return True
    # Every terrain's native palette and one immediate no-change fade.
    if c['kindId'] == 5:
        return c['initialArgs'][1] == 3 or c['terrain'] == 0
    # All selectors; independently retain all flag/presence truth-table
    # branches for selector zero instead of their Cartesian product.
    if c['kindId'] == 6:
        return c['logicalMask'] == c['spriteMask'] == 0 and c['presentMask'] == 15 or c['initialArgs'][0] == 0
    # Every bounded allocation capacity, both logical visibility states,
    # and all explicit missing-battler command/clone probes.
    return c['capacity'] < 60 or c['presentMask'] not in [3, 15]

if not a.full:
    cases = [c for c in cases if representative(c)]
result=dict(profile='full' if a.full else 'representative',schemaVersion=1,source=identity(r,paths),extractedFunctions=extracted,extractedData=extractedData,semantics={'frame0':'Direct source initializer and initial immediate callbacks; subsequent rows run source sprite callbacks, latch OAM invisibility, then source task callbacks. Full root tests independently use the actual shared script scheduler.','presence':'Native IsBattlerSpritePresent is original and consumes explicit party HP and position. Runtime launch presence uses event-time FaintShown ownership; primitive vectors map the resulting presence bit, never infer native occupancy from committed final HP.','clone':'Native CloneBattlerSpriteWithBlend scans all 64 cells and copies the entire source struct. Fixed reservations map by free-cell rank; extra occupied cells retain their contents.','visibility':'Present AND (!logicalInvisible OR !spriteInvisible). Rendered OAM uses spriteInvisible only; tasks run after its capture.','logicalWait':'Signal is written before sprite phase on frame5; original callback restores saved logical bit and uses DestroyTask without decrementing the visual count.','ring':'Original sprite engine, original affine command byte stride, original AnimSpriteOnMonPos and source template.'},cases=cases)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n')
(out/'verification.json').write_text(json.dumps(dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest()),indent=2)+'\n')
print(json.dumps(dict(cases=len(cases),rows=sum(len(c['rows']) for c in cases),output=str(a.output))))
