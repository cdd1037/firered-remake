#!/usr/bin/env python3
"""Compile bounded original callbacks in a disclosed C host; emit one compact callback oracle."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,pathlib,re,json,subprocess
p=argparse.ArgumentParser();p.add_argument('--project-root',type=pathlib.Path,default=_PROJECT_ROOT);p.add_argument('--output',type=pathlib.Path,required=True);a=p.parse_args()
r=a.project_root;out=a.output;out.mkdir(parents=True,exist_ok=False)
lock=json.loads((r/'scripts/recipes/presentation/six-island-presentation.lock.json').read_text());inputs=[]
def source(path):
 data=(r/'content/raw/pokefirered/037335f4'/path).read_bytes();row=next(v for v in lock['files'] if v['path']==path)
 inputs.append(path);return data.decode()
def func(text,name):
 m=re.search(r'^(?:static )?(?:bool8|u8|void) '+name+r'\([^;]*?\)\n\{',text,re.M);assert m,name
 i=m.end();depth=1
 while depth:
  depth+=(text[i]=='{')-(text[i]=='}');i+=1
 return text[m.start():i]+'\n'
palette=source('src/palette.c');field=source('src/field_effect.c');weather=source('src/field_weather.c');special=source('src/field_specials.c');transition=source('src/field_fadetransition.c')
# The host observes the same RunTasks order: fall priority0, weather80; then palette update.
order=source('src/overworld.c');assert order.index('RunTasks();',order.index('static void OverworldBasic'))<order.index('UpdatePaletteFade();',order.index('static void OverworldBasic'))
code=r'''
#include <stdio.h>
#include <stdint.h>
#include <stdbool.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int bool8;
#define TRUE 1
#define FALSE 0
#define PALETTE_FADE_STATUS_DONE 0
#define PALETTE_FADE_STATUS_ACTIVE 1
#define OBJ_PLTT_OFFSET 256
#define NORMAL_FADE 0
#define HARDWARE_FADE 1
#define PLTT_SIZE 0
#define PLTT NULL
#define SUBSPRITES_IGNORE_PRIORITY 0
#define SE_FALL 1
#define SE_M_STRENGTH 2
#define PLAYER_AVATAR_FLAG_SURFING 0
#define HELPCONTEXT_SURFING 0
#define VAR_TEMP_1 0
#define CpuCopy32(a,b,c) ((void)0)
#define WEATHER_RAIN 1
#define WEATHER_RAIN_THUNDERSTORM 2
#define WEATHER_DOWNPOUR 3
#define WEATHER_SNOW 4
#define WEATHER_SHADE 5
#define WEATHER_DROUGHT 6
#define WEATHER_FOG_HORIZONTAL 7
#define WEATHER_VOLCANIC_ASH 8
#define WEATHER_SANDSTORM 9
#define WEATHER_FOG_DIAGONAL 10
#define WEATHER_UNDERWATER 11
#define WEATHER_PAL_STATE_IDLE 0
#define WEATHER_PAL_STATE_SCREEN_FADING_IN 1
struct Palette {int active,deltaY,delayCounter,delay,y,targetY,blendColor,mode,yDec,bufferTransferDisabled,objPaletteToggle,softwareFadeFinishing,softwareFadeFinishingCounter;u32 selectedPalettes;} gPaletteFade;
#define gPaletteFade_delay gPaletteFade.delay
#define gPaletteFade_selectedPalettes gPaletteFade.selectedPalettes
int sPlttBufferTransferPending,bgBlend,objBlend;u16 gPlttBufferFaded[512];
void BlendPalette(int offset,int count,int y,int color){(void)count;(void)color;if(offset<256)bgBlend=y;else objBlend=y;}
void UpdateBlendRegisters(void){}
static u8 UpdateNormalPaletteFade(void);static bool8 IsSoftwarePaletteFadeFinishing(void);
u8 UpdatePaletteFade(void){return UpdateNormalPaletteFade();}
struct Task {s16 data[16];} tasks[1];
struct ObjectEvent {int invisible,fixedPriority,triggerGroundEffectsOnMove,triggerGroundEffectsOnStop,landingJump;};
struct Sprite {int y,y2,centerToCornerVecY,subspriteMode;struct {int priority;}oam;};
struct ObjectEvent gObjectEvents[1];struct Sprite gSprites[1];
struct {int objectEventId,spriteId,preventStep;}gPlayerAvatar;
struct Weather {int fadeInCounter,fadeInActive,currWeather,gammaIndex,gammaTargetIndex,palProcessingState;}weatherState;
struct Weather *gWeatherPtr=&weatherState;
int gSpriteCoordOffsetY,camY,fallCue,landCue,complete;
void CameraObjectReset2(void){}void CameraObjectReset1(void){camY=0;}
int GetPlayerFacingDirection(void){return 1;}int GetFaceDirectionMovementAction(int a){return a;}
void ObjectEventSetHeldMovement(struct ObjectEvent*a,int b){(void)a;(void)b;}
bool8 IsWeatherNotFadingIn(void){return gWeatherPtr->palProcessingState!=WEATHER_PAL_STATE_SCREEN_FADING_IN;}
bool8 FadeInScreen_RainShowShade(void){return FALSE;}bool8 FadeInScreen_Drought(void){return FALSE;}bool8 FadeInScreen_FogHorizontal(void){return FALSE;}
void PlaySE(int se){if(se==SE_FALL)fallCue++;else landCue++;}
void SetCameraPanningCallback(void *p){(void)p;}void SetCameraPanning(int x,int y){(void)x;camY=y;}
void UnlockPlayerFieldControls(void){}void UnfreezeObjectEvents(void){}void InstallCameraPanAheadCallback(void){}
void PlayerGetDestCoords(s16*x,s16*y){*x=*y=0;}int MapGridGetMetatileBehaviorAt(int x,int y){(void)x;(void)y;return 0;}
int MetatileBehavior_IsSurfableInSeafoamIslands(int b){(void)b;return 0;}void VarSet(int a,int b){(void)a;(void)b;}
void SetPlayerAvatarTransitionFlags(int a){(void)a;}void SetHelpContext(int a){(void)a;}
void Task_FallWarpFieldEffect(u8 t){(void)t;}int FindTaskIdByFunc(void(*f)(u8)){(void)f;return 0;}void DestroyTask(int a){(void)a;complete=1;}
'''
for name in ['IsSoftwarePaletteFadeFinishing','UpdateNormalPaletteFade','BeginNormalPaletteFade']:code+=func(palette,name)
code+=func(weather,'FadeInScreenWithWeather')
for name in ['FallWarpEffect_'+str(i) for i in range(1,8)]:code+=func(field,name)
code+='\n#define gTasks tasks\nint gSpecialVar_0x8004=3,gSpecialVar_0x8005=0,gSpecialVar_0x8006=12,gSpecialVar_0x8007=3;\nint CreateTask(void(*fn)(u8),int priority){(void)fn;(void)priority;return 0;}\nvoid ScriptContext_Enable(void){}\nstatic void Task_ShakeScreen(u8);static void Task_EndScreenShake(u8);\n'
shake_start=special.index('void ShakeScreen(void)')
for macro in ['tXtrans','tTimer','tNremain','tDuration','tYtrans']:
 code+=re.findall(r'^#define '+macro+r'[^\n]+',special[:shake_start],re.M)[-1]+'\n'
for name in ['ShakeScreen','Task_ShakeScreen','Task_EndScreenShake']:code+=func(special,name)
for macro in ['tXtrans','tTimer','tNremain','tDuration','tYtrans']:code+='#undef '+macro+'\n'
code+='\nint musicReady;\nvoid FreezeObjectEvents(void){}void LockPlayerFieldControls(void){}void WarpIntoMap(void){}void CB2_LoadMap(void){}void SetMainCallback2(void(*f)(void)){(void)f;}\nint WaitWarpFadeOutScreen(void){return gPaletteFade.active;}int BGMusicStopped(void){return musicReady;}\n'
code+=func(transition,'Task_Teleport2Warp')
code+=r'''
static bool8(*callbacks[])(struct Task*)={FallWarpEffect_1,FallWarpEffect_2,FallWarpEffect_3,FallWarpEffect_4,FallWarpEffect_5,FallWarpEffect_6,FallWarpEffect_7};
int main(void){
  printf("{\"landings\":[");
  int bottoms[]={64,80,88,96,112};
  for(int n=0;n<5;n++){
    memset(&gPaletteFade,0,sizeof(gPaletteFade));memset(tasks,0,sizeof(tasks));memset(gSprites,0,sizeof(gSprites));memset(gObjectEvents,0,sizeof(gObjectEvents));memset(&weatherState,0,sizeof(weatherState));
    camY=fallCue=landCue=complete=0;bgBlend=objBlend=16;gSprites[0].y=bottoms[n]-16;gSprites[0].centerToCornerVecY=-16;
    weatherState.palProcessingState=WEATHER_PAL_STATE_SCREEN_FADING_IN;BeginNormalPaletteFade(0xffffffff,0,16,0,0);
    if(n)printf(",");printf("{\"spriteBottom\":%d,\"frames\":[",bottoms[n]);
    for(int frame=1;frame<=100;frame++){
      while(callbacks[tasks[0].data[0]](&tasks[0]));
      if(frame>1)FadeInScreenWithWeather();
      UpdatePaletteFade();
      if(frame>1)printf(",");printf("[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]",frame,bgBlend,objBlend,gPaletteFade.active,!gObjectEvents[0].invisible,gSprites[0].y2,gObjectEvents[0].fixedPriority,camY,fallCue,landCue,complete);
      if(complete)break;
    }printf("]}");
  }printf("],\"departures\":[");
  int readyAt[]={0,64};
  for(int n=0;n<2;n++){
    memset(&gPaletteFade,0,sizeof(gPaletteFade));memset(tasks,0,sizeof(tasks));bgBlend=objBlend=complete=0;musicReady=readyAt[n]==0;
    BeginNormalPaletteFade(0xffffffff,0,0,16,0);Task_Teleport2Warp(0);
    if(n)printf(",");printf("{\"musicReadyAt\":%d,\"frames\":[",readyAt[n]);
    for(int frame=1;frame<=100;frame++){
      Task_Teleport2Warp(0);UpdatePaletteFade();musicReady=frame>=readyAt[n];
      if(frame>1)printf(",");printf("[%d,%d,%d,%d,%d,%d]",frame,bgBlend,objBlend,gPaletteFade.active,tasks[0].data[0],complete);
      if(complete)break;
    }printf("]}");
  }printf("],\"shake\":[0");camY=0;ShakeScreen();for(int frame=1;frame<=36;frame++){Task_ShakeScreen(0);printf(",%d",camY);}printf("]}\n");return 0;
}
'''
(out/'source-motion.c').write_text(code)
cmd=['cc','-std=c11','-O0','-Wall','-Wextra',str(out/'source-motion.c'),'-o',str(out/'source-motion')]
build=subprocess.run(cmd,text=True,capture_output=True);(out/'build.log').write_text(build.stdout+build.stderr);assert build.returncode==0,build.stderr
run=subprocess.run([str(out/'source-motion')],text=True,capture_output=True,check=True);data=json.loads(run.stdout);(out/'oracle.json').write_text(json.dumps(data,separators=(',',':'))+'\n')
assert len(data['landings'])==5 and all(x['frames'][-1][-1]==1 for x in data['landings'])
report={'passed':True,'sourceFiles':inputs,'commands':[cmd],'scope':'Extracted original palette, teleport departure task, seven fall callbacks and ShakeScreen. Weather initialization is the disclosed first-callback harness boundary; no native or audio acceptance.'}
(out/'report.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps({'passed':True,'departureFrameCounts':[len(x['frames']) for x in data['departures']],'frameCounts':[len(x['frames']) for x in data['landings']],'firstVisible':[next(f[0] for f in x['frames'] if f[4]) for x in data['landings']]}))
