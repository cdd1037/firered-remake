#!/usr/bin/env python3
import tempfile
import argparse,json,re,subprocess,sys
from pathlib import Path
sys.dont_write_bytecode=True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from source_extract import function,array,macros,identity,COMMIT,verify
HERE=Path(__file__).resolve().parent
p=argparse.ArgumentParser();p.add_argument('reference',type=Path);p.add_argument('--build-dir',type=Path);p.add_argument('--output',type=Path,default=(HERE.parents[1]/ 'Game.Presentation.Tests/Fixtures')/'weather-motion-source-vectors.json');p.add_argument('--full', action='store_true', help='Emit the complete original-C corpus instead of semantic representatives');a=p.parse_args();r=a.reference;out=(a.build_dir or Path(tempfile.mkdtemp(prefix='weather-motion-reference-'))).resolve();out.mkdir(parents=True,exist_ok=True)
paths=['src/battle_anim_rock.c','src/battle_anim_fire.c','src/battle_anim_utility_funcs.c','src/battle_anim_mons.c','src/battle_anim_normal.c','src/blend_palette.c','src/sprite.c','include/sprite.h','include/gba/types.h','include/constants/battle.h','include/constants/battle_anim.h','include/palette.h','data/battle_anim_scripts.s']
verify(r,paths, Path(__file__).resolve().parent / 'pinned-inputs.json')
s={p:(r/p).read_text() for p in paths};header=s['include/sprite.h'];types=header[header.index('enum\n{\n    SUBSPRITES_OFF'):header.index('struct Sprite;')];oam=s['include/gba/types.h'];types+='\n'+oam[oam.index('struct PlttData'):oam.index('struct BgAffineSrcData')]
funcs={'src/battle_anim_rock.c':['AnimFlyingSandCrescent'],'src/battle_anim_fire.c':['AnimTask_MoveHeatWaveTargets','AnimTask_MoveHeatWaveTargets_Step','AnimTask_BlendBackground'],'src/battle_anim_utility_funcs.c':['AnimTask_BlendBattleAnimPal','AnimTask_BlendParticle','StartBlendAnimSpriteColor','AnimTask_BlendSpriteColor_Step2'],'src/battle_anim_mons.c':['GetAnimBattlerSpriteId','GetBattleAnimBg1Data','GetBattlePalettesMask','GetBattleMonSpritePalettesMask'],'src/battle_anim_normal.c':['UnpackSelectedBattlePalettes'],'src/blend_palette.c':['BlendPalette'],'src/sprite.c':['SetSubspriteTables','AddSubspritesToOamBuffer']}
bodies=[]
for path,ns in funcs.items():
 for n in ns:
  f=function(s[path],n, signed_byte=False)
  if n=='BlendPalette':f=f.replace('void BlendPalette(','void SourceBlendPalette(')
  bodies.append(f)
protos='\n'.join(f[:f.index('{')].strip()+';' for f in bodies)
arrays='\n'.join(array(s['src/battle_anim_rock.c'],n) for n in ['sFlyingSandSubsprites','sFlyingSandSubspriteTable'])+'\n'+array(s['src/sprite.c'],'sOamDimensions')+'\nstatic const struct SubspriteTable sNullSubspriteTable[]={{0,NULL}};\n'
defs=macros(list(s.values()),['B_SIDE_PLAYER','BIT_FLANK','ANIM_TARGET','ANIM_DEF_PARTNER','ANIM_ATTACKER','ANIM_ATK_PARTNER','SPRITE_NONE','BATTLE_PARTNER','BG_ANIM_PAL_1','BG_ANIM_PAL_2','BG_PLTT_ID','B_POSITION_PLAYER_LEFT','B_POSITION_PLAYER_RIGHT','B_POSITION_OPPONENT_LEFT','B_POSITION_OPPONENT_RIGHT','F_PAL_BG','F_PAL_BATTLERS_2'])
c=(HERE/'motion-prefix.c.in').read_text().replace('@TYPES@',types).replace('@DEFINES@',defs).replace('@PROTOTYPES@',protos).replace('@ARRAYS@',arrays).replace('@FUNCTIONS@','\n'.join(bodies));(out/'motion.c').write_text(c)
for opt in ['O0','O2']:subprocess.run(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(out/'motion.c'),'-o',str(out/('motion-'+opt))],check=True)
def run(args):
 data=[subprocess.check_output([str(out/('motion-'+opt)),*map(str,args)]) for opt in ['O0','O2']];assert data[0]==data[1];return [json.loads(x) for x in data[0].splitlines()]
recipes=[[10,2304,96],[90,2048,96],[50,2560,96],[20,2304,96],[70,1984,96],[0,2816,96],[60,2560,96]]
crescents=[dict(attacker=attacker,args=[*recipe,flag],rows=run([0,attacker,*recipe,flag])) for attacker in range(4) for flag in [0,1] for recipe in recipes]
heat=[dict(attacker=attacker,target=target,visibleMask=mask,rows=run([1,attacker,target,mask])) for attacker in range(4) for target in range(4) for mask in [3,15]]
blend=[dict(kind=kind,visibleMask=mask,attacker=attacker,paletteIndex=index,rows=run([2,kind,mask,attacker,index])) for kind in range(6) for mask in [3,15] for attacker in [0,1] for index in ([0,3,15] if kind==4 else [0])]
abi=[]
for x,y in [(-64,-16),(-32,0),(-1,-1),(0,0),(16,16),(239,159),(256,256),(304,255),(511,511)]:
 for flip in [0,8,16,24]:
  for mode in [1,2]:
   for tile in [0,1018]:
    args=[3,x,y,flip,tile,3,mode,0,128,0];abi.append(dict(center=[x,y],flip=flip,tile=tile,priority=3,mode=mode,initialIndex=0,limit=128,nullTable=False,row=run(args)[0]))
for initial,limit,null in [(0,0,0),(0,1,0),(62,64,0),(63,64,0),(64,64,0),(126,128,0),(127,128,0),(128,128,0),(0,128,1),(127,128,1)]:
 abi.append(dict(center=[0,0],flip=0,tile=0,priority=3,mode=1,initialIndex=initial,limit=limit,nullTable=bool(null),row=run([3,0,0,0,0,3,1,initial,limit,null])[0]))
for c in crescents:assert c['rows'][-1]['alive']==0 and c['rows'][-2]['callback']==1 and c['rows'][-2]['alive']==1
for c in heat:assert len(c['rows'])==130 and c['rows'][-1]['alive']==0
def representative_crescent(c):
    # Every script recipe; sign/flip branch on the opposing side.
    return (c['attacker'], c['args'][3]) in [(0, 0), (1, 1)]

def representative_heat(c):
    # Both increment signs, partner present/absent, and selected ally.
    return (c['attacker'], c['target'], c['visibleMask']) in [
        (0, 1, 3), (0, 1, 15), (1, 0, 3), (1, 0, 15), (2, 0, 15), (3, 1, 15)]

def representative_blend(c):
    # All palette operations plus high-bank index and double-side mask.
    return c['attacker'] == 0 and c['visibleMask'] == 3 and c['paletteIndex'] == 0 or c['kind'] in [0, 2] and c['attacker'] == 1 and c['visibleMask'] == 15 or c['kind'] == 4 and c['attacker'] == 1 and c['visibleMask'] == 15 and c['paletteIndex'] in [3, 15]

if not a.full:
    crescents = [c for c in crescents if representative_crescent(c)]
    heat = [c for c in heat if representative_heat(c)]
    blend = [c for c in blend if representative_blend(c)]
    # abi stays complete: material fragmentRenders refer to its stable indices.
result=dict(profile='full' if a.full else 'representative',schema='firered-weather-motion-palette-abi-source-v1',commit=COMMIT,sources=identity(r,paths),source_functions=funcs,optimization_parity=['O0','O2'],compiler_flags=['-std=c11','-fwrapv','-fno-strict-aliasing'],semantics={'blendKinds':['sunlight-fade-in','sunlight-fade-out','rain-fade-in','rain-fade-out','heat-particle-red','heat-background-red'],'paletteRowBanks':[1,2,3,8,16,17,18,19,31],'oamPieceFields':['x_u9','y_u8','shape','size','tile_u10','priority','palette','matrixNum','objMode','mosaic'],'abiBoundary':'Verbatim AddSubspritesToOamBuffer with exact source OamData and Subsprite bitfields; no global OAM sorting or matrix BIOS claim. center is logical parent center, source oam origin starts center minus16/minus8.','stubs':['visibility is supplied four-bit mask','battler position-to-id map is identity','sprite palette slot is supplied explicitly','stored source tasks run after an explicit immediate initializer','DestroyAnimSprite marks inUse false and updates count; fields retained for inspection']},crescents=crescents,heat=heat,blends=blend,abi=abi)
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(result,separators=(',',':'))+'\n');print('motion',len(crescents),len(heat),len(blend),len(abi))
