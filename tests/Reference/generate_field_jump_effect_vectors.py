"""Execute bounded original field-effect callbacks and sprite animation to emit test vectors.

Only explicitly extracted pinned C functions run, with rendering allocation/copy stubs.
This authored harness does not run any scripts from the reference repository.
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
    'src/field_effect_helpers.c': 'bd19b5a73def76a864e37bf4c406035d5eae2890893ad7983386ff4a27b944b4',
    'src/metatile_behavior.c': 'a1491016f9b6ee56c38fd66bf890dfcb5d37c7dc0dfda9fe7c0a5282376b9042',
    'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae',
    'src/event_object_movement.c': '920f96b45569e702dd00997e7984effb3a7677ba2b68512b939eda69c2294254',
    'src/data/field_effects/field_effect_objects.h': 'cfdb0c9dbac21ea91964e76a2b9de7ddc104cfdd048d3a517739b9fe7350a98d',
    'include/constants/metatile_behaviors.h': '88955bf5c4b8d702d01d28161017362a2617ee37dce7ef77c0e288bda379da75',
}


def function(source, name):
    match = re.search(r'^(?:static )?(?:void|u8|bool8|s16) ' + re.escape(name) + r'\([^;]+?\)\s*\{', source, re.M)
    if not match:
        raise ValueError('Missing original function: '+name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end]


def array(source, name):
    match = re.search(r'^static const [^\n]+\b' + re.escape(name) + r'\[\][^=]*=\s*\{.*?^\};', source, re.M | re.S)
    if not match:
        raise ValueError('Missing original array: '+name)
    return match.group()


HEADER = r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
typedef uint8_t u8; typedef int8_t s8; typedef uint16_t u16;
typedef int16_t s16; typedef uint32_t u32; typedef uint8_t bool8;
#define TRUE 1
#define FALSE 0
#define MAX_SPRITES 64
#define NELEMS(a) (sizeof(a)/sizeof((a)[0]))
#define ST_OAM_AFFINE_ON_MASK 1
#define FLDEFF_SHADOW 1
#define GROUND_EFFECT_FLAG_LAND_IN_TALL_GRASS 2
#define GROUND_EFFECT_FLAG_LAND_IN_LONG_GRASS 5
#define GROUND_EFFECT_FLAG_LAND_IN_SHALLOW_WATER 3
#define GROUND_EFFECT_FLAG_LAND_IN_DEEP_WATER 4
#define GROUND_EFFECT_FLAG_LAND_ON_NORMAL_GROUND 1
union AnimCmd { struct { s16 imageValue; u8 duration,hFlip,vFlip; } frame; s16 type; };
#define ANIMCMD_FRAME(i,d) {.frame={i,d,0,0}}
#define ANIMCMD_END {.type=-1}
struct Sprite {
    s16 x,y,data[8];
    u8 inUse,animEnded,animBeginning,affineAnimBeginning,animPaused,animNum,animCmdIndex,animDelayCounter,animLoopCounter,usingSheet;
    u16 sheetTileStart;
    struct { u8 affineMode,priority; u16 tileNum; } oam;
    const union AnimCmd *const *anims;
    void *images;
    void (*callback)(struct Sprite *);
};
struct ObjectEvent { u8 active,hasShadow,currentMetatileBehavior,previousMetatileBehavior,spriteId,landingJump,disableJumpLandingGroundEffect; };
static struct Sprite gSprites[MAX_SPRITES];
static struct ObjectEvent gObjectEvents[1];
static const u8 gAffineAnimsDisabled=TRUE;
static int imageIndex;
static u8 TryGetObjectEventIdByLocalIdAndMap(s16 a,s16 b,s16 c,u8 *id) { *id=0; return 0; }
static void FieldEffectStop(struct Sprite *s,int effect) { s->inUse=0; }
static void UpdateObjectEventSpriteInvisibility(struct Sprite *s,u8 invisible) { }
static void SetObjectSubpriorityByElevation(int elevation,struct Sprite *s,int offset) { }
static void SetSpriteOamFlipBits(struct Sprite *s,u8 h,u8 v) { }
static void RequestSpriteFrameImageCopy(s16 image,u16 tile,void *images) { imageIndex=image; }
static void UnsupportedCommand(struct Sprite *s) { abort(); }
'''

MAIN = r'''
int main(void) {
    unsigned b,kind; int age;
    for (b=0;b<NUM_METATILE_BEHAVIORS;b++) {
        struct Sprite shadow={0}; struct ObjectEvent *o=&gObjectEvents[0];
        o->active=TRUE; o->hasShadow=TRUE; o->spriteId=1;
        o->previousMetatileBehavior=0; o->currentMetatileBehavior=b; shadow.inUse=TRUE;
        UpdateShadowFieldEffect(&shadow); printf("S 0 %u %u\n",b,shadow.inUse);
        o->previousMetatileBehavior=b; o->currentMetatileBehavior=0; shadow.inUse=TRUE;
        UpdateShadowFieldEffect(&shadow); printf("S %u 0 %u\n",b,shadow.inUse);
        o->currentMetatileBehavior=b; o->landingJump=TRUE; kind=0;
        GetGroundEffectFlags_JumpLanding(o,&kind); printf("L %u %u\n",b,kind);
    }
    memset(gSprites,0,sizeof(gSprites));
    gSprites[0].inUse=TRUE;gSprites[0].animBeginning=TRUE;
    gSprites[0].anims=dustAnims;gSprites[0].callback=UpdateJumpImpactEffect;
    for(age=0;age<27;age++) {
        AnimateSprites();
        printf("D %d %d %u\n",age,gSprites[0].inUse?imageIndex:-1,gSprites[0].animEnded);
    }
    return fflush(stdout)==0?0:2;
}
'''


def generate(reference):
    for name, digest in HASHES.items():
        if hashlib.sha256((reference/name).read_bytes()).hexdigest()!=digest:
            raise ValueError('Field jump effect reference mismatch: '+name)
    read=lambda name:(reference/name).read_text()
    helpers=read('src/field_effect_helpers.c'); behaviors=read('src/metatile_behavior.c')
    movement=read('src/event_object_movement.c'); sprite=read('src/sprite.c')
    body=[function(behaviors,n) for n in ['MetatileBehavior_IsPokeGrass','MetatileBehavior_IsReflective','MetatileBehavior_IsSurfable','MetatileBehavior_IsTallGrass','MetatileBehavior_IsLongGrass','MetatileBehavior_IsPuddle','MetatileBehavior_IsShallowFlowingWater','MetatileBehavior_IsATile']]
    body += [function(helpers,n) for n in ['UpdateShadowFieldEffect','UpdateJumpImpactEffect']]
    body += [function(movement,'GetGroundEffectFlags_JumpLanding')]
    body += [function(sprite,n) for n in ['BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','DecrementAnimDelayCounter','AnimateSprite','AnimateSprites']]
    declarations='\n'.join(re.split(r'\s*\{',b,1)[0]+';' for b in body)
    constants='\n'.join(re.findall(r'^#define (?:MB_|NUM_METATILE_BEHAVIORS)[^\n]+',read('include/constants/metatile_behaviors.h'),re.M))
    surfable=re.search(r'^static const bool8 sBehaviorSurfable\[NUM_METATILE_BEHAVIORS\] = \{.*?^\};',behaviors,re.M|re.S).group()
    dust=array(read('src/data/field_effects/field_effect_objects.h'),'sAnim_GroundImpactDust')
    tables='''
static void (*sAnimFuncs[])(struct Sprite *)={ContinueAnim,BeginAnim};
static void (*sAffineAnimFuncs[])(struct Sprite *)={UnsupportedCommand,UnsupportedCommand};
static void (*sAnimCmdFuncs[])(struct Sprite *)={UnsupportedCommand,UnsupportedCommand,AnimCmd_end,AnimCmd_frame};
static const union AnimCmd *const dustAnims[]={sAnim_GroundImpactDust};
'''
    c=HEADER+'\n'+constants+'\n'+surfable+'\n'+declarations+'\n'+dust+'\n'+tables+'\n'+'\n'.join(body)+MAIN
    with tempfile.TemporaryDirectory() as directory:
        source=Path(directory)/'jump_effects.c';exe=Path(directory)/('jump_effects.exe' if os.name=='nt' else 'jump_effects')
        source.write_text(c);compile_c(['cc','-std=c11','-O2',str(source),'-o',str(exe)])
        output=subprocess.check_output([str(exe)],text=True)
    data={'sourceCommit':COMMIT,'shadowPairs':[],'landings':[],'dust':[]}
    keys={'S':'shadowPairs','L':'landings','D':'dust'}
    for line in output.splitlines():
        tag,*values=line.split();data[keys[tag]].append(list(map(int,values)))
    if len(data['shadowPairs'])!=480 or len(data['landings'])!=240 or len(data['dust'])!=27:
        raise ValueError('Incomplete source output')
    if [r[1] for r in data['dust']] != [0]*8+[1]*8+[2]*9+[-1]*2:
        raise ValueError('Source callback-before-animation dust lifetime differs')
    return data


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,required=True);parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();data=generate(args.reference)
    args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(data,separators=(',',':'))+'\n')
    print('480 source shadow gates; 240 landing selectors; 27 sprite callback frames')

if __name__=='__main__':main()
