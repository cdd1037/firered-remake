#!/usr/bin/env python3
"""Bounded original-C oracle for Helping Hand, Imprison and Teeter Dance."""
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
out = (a.build_dir or Path(tempfile.mkdtemp(prefix='support-status-reference-'))).resolve()
out.mkdir(parents=True, exist_ok=True)
if a.output is None:
 a.output = out/'support-status-source-vectors.json' if a.full else HERE.parent.parent/'Game.Presentation.Tests/Fixtures/support-status-source-vectors.json'
if a.full and a.output.resolve().is_relative_to((HERE.parent.parent/'Game.Presentation.Tests/Fixtures').resolve()):
 p.error('--full output must stay outside the local representative Fixtures directory')
paths = ['src/battle_main.c', 'src/battle_anim_effects_1.c', 'src/battle_anim_psychic.c', 'src/battle_anim_effects_3.c', 'src/battle_anim_mons.c', 'src/battle_anim.c', 'src/sprite.c', 'src/trig.c', 'include/sprite.h', 'src/data/battle_anim.h', 'include/gba/types.h', 'include/gba/io_reg.h', 'include/gba/defines.h', 'include/global.h', 'include/battle.h', 'include/battle_anim.h', 'include/palette.h', 'include/constants/songs.h', 'include/constants/battle_anim.h', 'include/constants/battle.h', 'include/constants/pokemon.h', 'data/battle_anim_scripts.s']
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
 'src/battle_anim_effects_1.c': ['AnimFlyingMusicNotes','AnimFlyingMusicNotes_Step'],
 'src/battle_anim_effects_3.c': ['AnimHelpingHandClap','AnimHelpingHandClap_Step','AnimTask_HelpingHandAttackerMovement','AnimTask_HelpingHandAttackerMovement_Step','AnimTask_TeeterDanceMovement','AnimTask_TeeterDanceMovement_Step'],
 'src/battle_anim_psychic.c': ['AnimTask_ImprisonOrbs','AnimTask_ImprisonOrbs_Step'],
 'src/battle_anim_mons.c': ['GetBattlerSide','GetBattlerPosition','GetBattlerSpriteCoordAttr','GetAnimBattlerSpriteId','DestroySpriteAndMatrix','GetBattlerSpriteCoord','GetBattlerSpriteCoord2','GetBattlerSpriteFinal_Y','GetBattlerSpriteSubpriority'],
 'src/battle_anim.c': ['DestroyAnimSprite','Cmd_createsprite'],
 'src/trig.c': ['Sin','Cos'],
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
 'src/battle_anim_effects_1.c':['sWavyMusicNotesAnimCmds'+str(i) for i in range(1,9)]+['gMusicNotesAnimTable','sWavyMusicNotesAffineAnimCmds','sMusicNotesAffineAnimTable']}.items():
 for n in ns:add_array(path,n)
for n in ['gOamData_AffineOff_ObjNormal_32x32','gOamData_AffineOff_ObjBlend_16x16','gOamData_AffineDouble_ObjNormal_16x16']:
 add_struct('src/data/battle_anim.h',n,'OamData')
for path,ns in {'src/battle_anim_effects_1.c':['gFastFlyingMusicNotesSpriteTemplate'],'src/battle_anim_effects_3.c':['gHelpingHandClapSpriteTemplate'],'src/battle_anim_psychic.c':['sImprisonOrbSpriteTemplate']}.items():
 for n in ns:add_struct(path,n,'SpriteTemplate')
defs = macros(list(s.values()), ['sTransl_InitX','sTransl_InitY','sTransl_DestX','sTransl_DestY','sTransl_Speed','sTransl_Duration','ST_OAM_AFFINE_ON_MASK','ANIM_TAG_TAG_HAND','ANIM_TAG_HOLLOW_ORB','ANIM_TAG_MUSIC_NOTES','B_SIDE_PLAYER','B_SIDE_OPPONENT','BATTLE_PARTNER','BIT_SIDE','BATTLE_TYPE_DOUBLE','B_POSITION_PLAYER_LEFT','B_POSITION_OPPONENT_LEFT','GET_BATTLER_SIDE2','GET_BATTLER_POSITION','ANIM_ATTACKER','ANIM_TARGET','ANIM_ATK_PARTNER','SPRITE_NONE','BLDCNT_EFFECT_BLEND','BLDCNT_TGT2_ALL','BLDCNT_TGT1_BG1','BLDALPHA_BLEND','REG_OFFSET_BLDCNT','REG_OFFSET_BLDALPHA','REG_OFFSET_BG1HOFS','REG_OFFSET_BG1VOFS','SE_M_METRONOME','SOUND_PAN_ATTACKER','SOUND_PAN_TARGET','BG_PLTT_ID','PLTT_SIZE_4BPP','MON_PIC_HEIGHT','TAG_NONE','Q_8_8','BG_ANIM_PAL_1','ANIMSPRITE_IS_TARGET','IS_DOUBLE_BATTLE','MAX_BATTLERS_COUNT','B_POSITION_PLAYER_RIGHT','TILE_SIZE_4BPP'])
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
kindNames=['helping-clap','flying-note','helping-movement','imprison-orbs','teeter-movement','helping-integrated','teeter-integrated','note-frame-affine']
roots=[270,298,270,286,298,270,298,298]
cases,taskCases,integratedCases,directCases,hiddenCases=[],[],[],[],[]

# Reviewable branch representatives, never positional/every-Nth sampling.
REPRESENTATIVE = {
 # Both hands/sides; note normal/long duration, selectors 6/7 and signed velocities.
 'cases': 'helping-clap-000 helping-clap-019 helping-clap-082 helping-clap-099 flying-note-002 flying-note-024 flying-note-088 flying-note-107'.split(),
 # Both movement directions; Imprison side/geometry and capacity 0/1/4/5/full.
 'taskCases': 'helping-movement-000 helping-movement-033 teeter-movement-002 teeter-movement-035 imprison-orbs-001 imprison-orbs-034 imprison-orbs-036 imprison-orbs-039 imprison-orbs-044 imprison-orbs-047'.split(),
 # All eight note selectors, including duration 41 and affine VFlip suppression.
 'directCases': 'note-frame-affine-000 note-frame-affine-001 note-frame-affine-002 note-frame-affine-003 note-frame-affine-004 note-frame-affine-005 note-frame-affine-006 note-frame-affine-007'.split(),
 # Both sides of each complete clap/note schedule and moving base-X interaction.
 'integratedCases': 'helping-integrated-000 helping-integrated-022 teeter-integrated-001 teeter-integrated-023'.split(),
 # All five source form shapes and all four hidden actor slots; rank mapping only.
 'hiddenCases': 'imprison-orbs-000 imprison-orbs-005 imprison-orbs-010 imprison-orbs-015 imprison-orbs-016'.split(),
}
caseNumbers = {group: 0 for group in REPRESENTATIVE}
def add(kind,d,b,t,args=(),geometry=0,capacity=60,hidden=False):
 group='hiddenCases' if hidden else 'cases' if kind<2 else 'taskCases' if kind<5 else 'integratedCases' if kind<7 else 'directCases'
 caseId=f'{kindNames[kind]}-{caseNumbers[group]:03}';caseNumbers[group]+=1
 if not a.full and caseId not in REPRESENTATIVE[group]:return
 args=list(args)+[0]*(8-len(args));rows=run([kind,d,b,t,geometry,capacity,int(hidden),*args]);inputs=rows[0]['inputs']
 if kind!=7:assert rows[-1]['alive']==0,(kind,d,b,t)
 dest=hiddenCases if hidden else cases if kind<2 else taskCases if kind<5 else integratedCases if kind<7 else directCases
 dest.append(dict(id=caseId,kind=kindNames[kind],kindId=kind,root=roots[kind],doubles=bool(d),attacker=b,target=t,hiddenActor=hidden,geometry=geometry,initialArgs=args,initialSubpriority=max(3,inputs['subpriorities'][b]-(40 if kind==0 else 2)),sourceWidths=inputs['widths'],sourceHeights=inputs['heights'],sourceYOffsets=inputs['yOffsets'],battlerCoordinates=inputs['coordinates'],additionalFreeSlots=capacity,rows=rows))
for d in [0,1]:
 for b in range(4 if d else 2):
  for geometry in [0,1]:
   for hand in [0,1]:add(0,d,b,b^1,[hand],geometry)
   for args in [[0,16,-2],[0,0,-2],[0,-16,-2],[1,-8,-2],[2,8,-2],[6,-11,3],[7,7,-3]]:add(1,d,b,b^1,args,geometry)
   for kind in [2,3,4,5,6]:add(kind,d,b,b^1,geometry=geometry)
for capacity in range(6):
 for b in [0,3]:add(3,1,b,b^1,geometry=1,capacity=capacity)
for geometry in range(2,7):
 for b in [0,1,2,3]:
  add(3,1,b,b^1,geometry=geometry)
  add(3,1,b,b^1,geometry=geometry,hidden=True)
for anim in range(8):add(7,0,0,1,[anim])
semantics={
 'frame0':'Source Cmd_createsprite executes initializer then AnimateSprite immediately; task frame0 is its initializer only. Later frames execute original AnimateSprites before task callbacks. Integrated notes are spawned before that frame sprite phase, as callback1 precedes BattleMainCB2.',
 'integrated':'Helping movement plus both authored clap selectors; Teeter movement plus five authored note arguments at frames0/24/48/72/96. Other script commands are covered by resolver-backed complete-root tests, not duplicated in this native harness.',
 'sourceBaseX':'Every task row preserves source battler x and x2 independently. Teeter notes read original GetBattlerSpriteCoord, not mutable gSprites.x; rendered displacement is baseX-initialBaseX+x2 for this authored closure only.',
 'capacity':'All four battler slots reserved; additionalFreeSlots begin at4, other slots are live nonmatching fillers. Original CreateSprite ascending allocation unchanged. Imprison stores all five attempts including MAX_SPRITES; raw children do not add visual count. Fifth stored ID overwrites radiusdata12 only after its final use.',
 'oam':'hFlip/vFlip are control fields. Effective nonaffine rendered flips are matrixNum bits3/4. Affine matrices use matrixNum as index, and BeginAnim/AnimCmd_frame explicitly bypass flip writes. Note selectors6/7 retain frame VFlip commands without rendered OAM flips.',
 'dummy':'Original dummy frame table starts at END, so BeginAnim leaves raw Helping Hand OAM flip and callback tile increment untouched. Runtime dummy frame representation is not allowed to overwrite those raw OAM fields.',
 'coordinates':'Original source coordinate/attribute functions retained; species lookup alone replaced by explicit dimensions/yOffsets. Metadata copied from observed C inputs. Sprite-sheet allocation maps to deterministic local slots, not a second allocator.',
 'destroy':'Original DestroySprite/ResetSprite execute. Observer retains pre-reset destroyed rows and alivefalse; retired affine mode may already be cleared by FreeSpriteOamMatrix.',
 'hardware':'GPU writes are recorded. Source sprite frame/affine engines and zero-rotation affine matrices execute. No actual hardware/audio/assets are run.',
 'events':{'1':['SetGpuReg','offset','value'],'3':['CreateSprite','slot-or64','templateId','x','y','subpriority','attempt'],'4':['DestroySprite','slot','instance','data0','animEnded','templateId'],'6':['FreeOamMatrix','matrix'],'10':['DestroyAnimVisualTask','taskId'],'18':['FrameOrder','1sprites/2fade/3tasks']}}

def keep(value, names):
 return {name: value[name] for name in names.split()}
def sprite_expectation(value, group, kind):
 if group == 'directCases': fields = 'tile animDelay animEnded oamMatrixNum xScale yScale rotation'
 else:
  fields = 'alive position tile visible priority subpriority objMode affineMode oamMatrixNum'
  if (group == 'cases' and kind == 1) or (group == 'integratedCases' and kind == 6):
   fields += ' animEnded animDelay xScale yScale rotation affineEnded'
 return keep(value, fields)
def representative_case(case, group):
 # Preserve only fields consumed by semantic replay, without copying row.inputs or raw engine diagnostics.
 names = 'id kindId root doubles attacker target sourceWidths sourceHeights sourceYOffsets battlerCoordinates initialArgs initialSubpriority additionalFreeSlots'
 result = {name: case[name] for name in names.split() if name in case}
 rows = []
 kind = case['kindId']
 for row in case['rows']:
  expected = keep(row, 'frame')
  if group == 'directCases': expected['sprite'] = sprite_expectation(row['sprite'], group, kind)
  elif group == 'cases':
   expected['args'] = row['args']
   expected['sprite'] = sprite_expectation(row['sprite'], group, kind)
  else:
   expected['visualCount'] = row['visualCount']
   expected['sprites'] = [sprite_expectation(sprite, group, kind) | ({'slot': sprite['slot']} if group != 'hiddenCases' else {}) for sprite in row['sprites']]
   if group != 'hiddenCases':
    expected['args'] = row['args']
    expected['battler'] = keep(row['battler'], 'position')
   if group == 'taskCases':
    expected.update(alive=row['alive'], data=row['data'][:3] if kind == 3 else row['data'][:1], events=[event for event in row['events'] if event[0] == 3])
    if kind == 3: expected['registers'] = row['registers'][:2]
  rows.append(expected)
 result['rows'] = rows
 return result
result=dict(profile='full' if a.full else 'representative',schema='firered-support-status-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=names,extractedFunctions=extracted,extractedData=extractedData,optimization_parity=['O0','O2'],compiler_flags=['-std=c11','-fwrapv','-fno-strict-aliasing'],semantics=semantics,cases=cases,taskCases=taskCases,integratedCases=integratedCases,directCases=directCases,hiddenCases=hiddenCases)
if not a.full:
 for group, ids in REPRESENTATIVE.items():
  selected = result[group]
  assert set(case['id'] for case in selected) == set(ids), (group, 'missing representative')
  result[group] = [representative_case(case, group) for case in selected]
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result, separators=(',', ':'))+'\n')
verification=dict(schema=result['schema'],fixtureSha256=hashlib.sha256(a.output.read_bytes()).hexdigest(),generatedCSha256=hashlib.sha256(c.encode()).hexdigest(),optimizationParity=['O0','O2'],groups={key:{'cases':len(result[key]),'rows':sum(len(case['rows']) for case in result[key])} for key in ['cases','taskCases','integratedCases','directCases','hiddenCases']},terminalFrames={name:sorted(set(case['rows'][-1]['frame'] for group in [cases,taskCases,integratedCases,directCases,hiddenCases] for case in group if case['kind']==name)) for name in kindNames})
(out/'verification.json').write_text(json.dumps(verification,indent=2)+'\n');print(json.dumps(verification,indent=2))
