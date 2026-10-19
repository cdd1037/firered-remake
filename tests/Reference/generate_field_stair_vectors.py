"""Trace pinned on-foot stair tasks, signed offsets, walk frames and palette gates.

The local host extracts the original task callbacks, movement actions, sprite
animation callbacks and palettes. It stubs hardware/audio and selects a fresh
standard-player animation seed, NONE/SUNNY weather and no map preview. No
upstream script runs. Output belongs in the already ignored presentation fixture
directory. Task callbacks precede weather, sprites and palette updates.
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
from generate_field_warp_vectors import HEADER as WARP_HEADER, HASHES as WARP_HASHES
from generate_field_door_vectors import extract_array

HASHES = {**WARP_HASHES,
    'src/field_control_avatar.c': '305daa295f88ce682b2b1868700d2f76012c659cb0df2905e6177a51ee4e9b8d',
    'src/event_object_movement.c': '920f96b45569e702dd00997e7984effb3a7677ba2b68512b939eda69c2294254',
    'src/sprite.c': '2a804302eb5a89d31c3ec2f33dc645a80a0c645113d4b33b9bd571064c0286ae',
    'src/metatile_behavior.c': 'a1491016f9b6ee56c38fd66bf890dfcb5d37c7dc0dfda9fe7c0a5282376b9042',
    'src/data/object_events/object_event_anims.h': 'a484f38af198398f763c1d5f239dddd4397bb4cc7eab48c2b2e992552d767b0e',
}


def extract_function(source, name):
    match = re.search(r'^[^\n;#]*\b' + re.escape(name) + r'\([^;]*?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing source function: ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end] + '\n'


HEADER = r'''
typedef int32_t s32;
#define MB_UP_RIGHT_STAIR_WARP 0x6c
#define MB_UP_LEFT_STAIR_WARP 0x6d
#define MB_DOWN_RIGHT_STAIR_WARP 0x6e
#define MB_DOWN_LEFT_STAIR_WARP 0x6f
#define DIR_WEST 3
#define DIR_EAST 4
#define SE_EXIT 1
#define ST_OAM_AFFINE_ON_MASK 1
#define ANIMCMD_FRAME(image, ticks, ...) {.frame={.imageValue=(image),.duration=(ticks),__VA_ARGS__}}
#define ANIMCMD_JUMP(where) {.jump={.type=-2,.target=(where)}}
union AnimCmd { int type; struct { int imageValue,duration,hFlip,vFlip; } frame; struct { int type,target; } jump; };
struct Sprite { s16 data[8],x2,y2; int animPaused,animNum,animCmdIndex,animDelayCounter;
    int animBeginning,animEnded,usingSheet,sheetTileStart; const void *images;
    const union AnimCmd *const *anims; struct { int priority,affineMode,tileNum; } oam; } gSprites[1];
struct ObjectEvent { int heldMovementFinished,heldMovementActive,movementActionId,inanimate,facingDirection; } gObjectEvents[1];
struct { int objectEventId,spriteId; } gPlayerAvatar;
struct StepAnimTable { const union AnimCmd *const *anims; int animPos[4]; };
int sourceBehavior,musicStopRequested,displayedFrame;
void CameraObjectReset1(void){} void CameraObjectReset2(void){}
void Overworld_PlaySpecialMapMusic(void){}
void FieldCB_DefaultWarpExit(void){} void (*gFieldCallback)(void);
int CreateTask(void(*fn)(u8),int priority){(void)fn;(void)priority;return 0;}
void TryFadeOutOldMapMusic(void){musicStopRequested=1;}
void PlayRainStoppingSoundEffect(void){} void PlaySE(int id){(void)id;}
void PlayerGetDestCoords(s16*x,s16*y){*x=0;*y=0;}
int MapGridGetMetatileBehaviorAt(int x,int y){(void)x;(void)y;return sourceBehavior;}
int GetPlayerFacingDirection(void){return (sourceBehavior&1)?DIR_WEST:DIR_EAST;}
int GetWalkInPlaceNormalMovementAction(int direction){return 10+direction;}
int GetWalkInPlaceFastMovementAction(int direction){return 20+direction;}
int GetMoveDirectionAnimNum(int direction){(void)direction;return 0;}
int GetMoveDirectionFastAnimNum(int direction){(void)direction;return 1;}
void SetObjectEventDirection(struct ObjectEvent*o,int direction){o->facingDirection=direction;}
bool8 ObjectEventIsMovementOverridden(struct ObjectEvent*o){return o->heldMovementActive;}
int ObjectEventClearHeldMovementIfFinished(struct ObjectEvent*o)
{if(!o->heldMovementFinished)return FALSE;o->heldMovementActive=0;o->heldMovementFinished=0;return TRUE;}
void ObjectEventForceSetHeldMovement(struct ObjectEvent*o,int action)
{o->heldMovementActive=1;o->heldMovementFinished=0;o->movementActionId=action;gSprites[0].data[2]=0;}
void SetSpriteOamFlipBits(struct Sprite*s,int h,int v){(void)s;(void)h;(void)v;}
void RequestSpriteFrameImageCopy(int image,int tile,const void*images)
{(void)tile;(void)images;displayedFrame=image==7?1:image==8?3:0;}
void WarpFadeInScreen(void); void WarpFadeOutScreen(void);
'''

SHIMS = r'''
void WarpFadeOutScreen(void){BeginNormalPaletteFade(0xffffffff,0,0,16,0);}
void WarpFadeInScreen(void)
{weatherState.palProcessingState=WEATHER_PAL_STATE_SCREEN_FADING_IN;BeginNormalPaletteFade(0xffffffff,0,16,0,0);}
void AnimateSpritesHost(void)
{
    struct ObjectEvent *o=&gObjectEvents[0];struct Sprite *s=&gSprites[0];
    if(o->heldMovementActive&&!o->heldMovementFinished)
    {
        if(s->data[2]==0)
            o->heldMovementFinished=o->movementActionId>=20?
                MovementAction_WalkInPlaceFastLeft_Step0(o,s):MovementAction_WalkInPlaceNormalLeft_Step0(o,s);
        else o->heldMovementFinished=MovementAction_WalkInPlace_Step1(o,s);
    }
    if(o->heldMovementActive)ContinueAnim(s);
}
void row(int frame)
{printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",frame,gTasks[0].data[0],bgBlend,objBlend,
    gPaletteFade.active,complete,gSprites[0].x2,gSprites[0].y2,displayedFrame,musicStopRequested);}
int main(void)
{
    const char* names[]={"departureReady","departureDelayed","arrival"};
    printf("{\"traces\":[");int trace=0;
    for(sourceBehavior=0x6c;sourceBehavior<=0x6f;sourceBehavior++)for(int kind=0;kind<3;kind++)
    {
        memset(gTasks,0,sizeof(gTasks));memset(&gPaletteFade,0,sizeof(gPaletteFade));
        memset(&weatherState,0,sizeof(weatherState));memset(gSprites,0,sizeof(gSprites));memset(gObjectEvents,0,sizeof(gObjectEvents));
        gSprites[0].anims=hostAnims;complete=musicStopRequested=displayedFrame=0;
        bgBlend=objBlend=kind==2?16:0;
        if(kind==2)WarpFadeInScreen();else DoStairWarp(sourceBehavior,0);
        if(trace++)printf(",");printf("{\"name\":\"%s\",\"behavior\":%d,\"frames\":[",names[kind],sourceBehavior);row(0);
        for(int frame=1;frame<=100;frame++)
        {
            musicReady=kind==0||frame>=65;
            if(kind==2){Task_ExitStairs(0);if(frame>1)FadeInScreenWithWeather();}
            else Task_StairWarp(0);
            AnimateSpritesHost();UpdatePaletteFade();printf(",");row(frame);
            if(complete)break;
        }
        if(!complete)return 2;printf("]}");
    }
    printf("]}\n");return 0;
}
'''


def generate(reference):
    sources = {}
    for path, digest in HASHES.items():
        raw = (reference / path).read_bytes()
        if hashlib.sha256(raw).hexdigest() != digest:
            raise ValueError('Stair reference revision mismatch: ' + path)
        sources[path] = raw.decode()
    caller = extract_function(sources['src/field_control_avatar.c'], 'TryArrowWarp')
    if 'delay = 0;' not in caller or 'delay = 12;' not in caller or 'DoStairWarp(metatileBehavior, delay);' not in caller:
        raise ValueError('Stair on-foot/bike caller changed')
    order = extract_function(sources['src/overworld.c'], 'OverworldBasic')
    if not order.index('RunTasks();') < order.index('AnimateSprites();') < order.index('UpdatePaletteFade();'):
        raise ValueError('Source callback order changed')
    anims = sources['src/data/object_events/object_event_anims.h']
    if not re.search(r'\.anims = sAnimTable_RedGreenNormal,\s*\.animPos = \{1, 3, 0, 2\}', anims):
        raise ValueError('Player step alternation table changed')
    code = WARP_HEADER + HEADER
    for name in ('sAnim_GoWest', 'sAnim_GoFastWest'):
        code += extract_array(anims, name)
    code += 'static const union AnimCmd *const hostAnims[]={sAnim_GoWest,sAnim_GoFastWest};\n'
    code += 'static const struct StepAnimTable sStepAnimTables[]={{hostAnims,{1,3,0,2}},{NULL,{0}}};\n'
    groups = {
        'src/palette.c': ('IsSoftwarePaletteFadeFinishing','UpdateNormalPaletteFade','BeginNormalPaletteFade'),
        'src/field_weather.c': ('FadeInScreenWithWeather',),
        'src/metatile_behavior.c': ('MetatileBehavior_IsDirectionalUpRightStairWarp','MetatileBehavior_IsDirectionalUpLeftStairWarp','MetatileBehavior_IsDirectionalDownRightStairWarp','MetatileBehavior_IsDirectionalDownLeftStairWarp'),
        'src/sprite.c': ('DecrementAnimDelayCounter','ContinueAnim','AnimCmd_frame','AnimCmd_jump','SeekSpriteAnim'),
        'src/event_object_movement.c': ('GetStepAnimTable','SetStepAnimHandleAlternation','InitMoveInPlace','MovementAction_WalkInPlace_Step1','MovementAction_WalkInPlaceNormalLeft_Step0','MovementAction_WalkInPlaceFastLeft_Step0'),
        'src/field_fadetransition.c': ('DoStairWarp','Task_StairWarp','UpdateStairsMovement','ForceStairsMovement','GetStairsMovementDirection','Task_ExitStairs','ExitStairsMovement','WaitStairExitMovementFinished'),
    }
    bodies = [extract_function(sources[path],name) for path,names in groups.items() for name in names]
    code += ''.join(body.split('\n{',1)[0]+';\n' for body in bodies)
    code += 'static void(*const sAnimCmdFuncs[])(struct Sprite*)={NULL,AnimCmd_jump,NULL,AnimCmd_frame};\n'
    code += ''.join(bodies) + SHIMS
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory)/'field_stair.c'
        binary = Path(directory)/('field_stair.exe' if os.name=='nt' else 'field_stair')
        path.write_text(code)
        compile_c(['cc','-std=c11','-O0',str(path),'-o',str(binary)])
        result = json.loads(subprocess.check_output([str(binary)],text=True))
    ends = [trace['frames'][-1][0] for trace in result['traces']]
    if ends != [36,66,24]*4:
        raise ValueError('Unexpected stair phase boundaries: '+str(ends))
    return {'sourceCommit':COMMIT,'columns':['frame','taskPhase','backgroundBlend','objectBlend','paletteActive','complete','offsetX','offsetY','walkFrame','musicFadeRequested'],**result}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();output=generate(args.reference)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(json.dumps({'traces':len(output['traces']),'rows':sum(len(t['frames']) for t in output['traces'])}))


if __name__=='__main__':
    main()
