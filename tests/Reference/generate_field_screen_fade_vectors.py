"""Bounded pinned-C in-place white fade oracle. No upstream scripts or product build.
Reuse maintained extraction/compiler/host definitions. Output is local ignored-ready
numerical evidence; the only adapted source body replaces unreachable GBA HALT with abort.
"""
import argparse,hashlib,json,os,sys,subprocess,tempfile
from pathlib import Path
EXTRA={
'data/scripts/std_msgbox.inc':'090541cfc3a47272ea8203f0f6c18f19f8062d407388a23fcf103e93b196b431',
'src/scrcmd.c':'898dad5a07ce0a125731b998654d86808885a8d48163d607478a5e70c1cac553',
'src/script.c':'1cc4aade1f821e5f218ba7c191b1217e5a3cf942d0bfd0f32f775ce1f808437c',
'src/event_object_lock.c':'e18c4cdf700e7d20a71b2faaedae6dd2f7f33ebbff40562a333668587d95c516',
'src/blend_palette.c':'219a02b2ed9ab3010e203e0131fa8133571dcc271e025d7f6f14bf7993a51090',
'include/palette.h':'3f5ee66bab01538193629ffef3ff3cd6ad7555f7e294f52ae3ccfa36f94ac418',
'asm/macros/event.inc':'848e5e791adeddbe7ae7fe2717729af666a8d8aa163ab7781c74bfd49ce58671',
'data/maps/PokemonTower_5F/scripts.inc':'e8e741918ef55b11a19ca1f62d13841407523a1d3451dc0c56c3e555d696b1f2'}
COLUMNS=['frame','phase','mode','contextStatus','nativeWait','scriptEnableCalls','toWhiteFrame','fromWhiteFrame','healFrame','messageFrame','backgroundBlend','objectBlend','paletteActive','paletteY','paletteObjectToggle','paletteTail','paletteTailCounter','weatherFadeCounter','weatherPaletteState','weatherFadeInActive','weatherReady','scrollOffset','scrollCounter','scrollX','eva','evb','initStep','fogMainCalls','fogCreates','fogDestroys','baselineCopies','bgSample','objectSample','lightenedObjectSample','unfadedBgSample','unfadedObjectSample','unfadedLightenedObjectSample','lockTaskActive','stopPlayerCalls','playerTileTransitionState']
STUBS=r'''
#include <assert.h>
#define ARRAY_COUNT(x) (sizeof(x)/sizeof((x)[0]))
#define AGB_ASSERT_EX(a,b,c) assert(a)
#define ABSPATH(s) s
#define PLTT_ID(n) ((n)*16)
struct RGBColor {u16 r:5,g:5,b:5,pad:1;};
struct PlttData {u16 r:5,g:5,b:5,pad:1;};
struct ScriptContext;
typedef bool8(*ScrCmdFunc)(struct ScriptContext*);
struct ScriptContext {u8 mode,stackDepth;const u8*scriptPtr;bool8(*nativePtr)(void);ScrCmdFunc*cmdTable,*cmdTableEnd;u32 data[4];const u8*stack[20];};
enum{SCRIPT_MODE_STOPPED,SCRIPT_MODE_BYTECODE,SCRIPT_MODE_NATIVE};
enum{CONTEXT_RUNNING,CONTEXT_WAITING,CONTEXT_SHUTDOWN};
struct ScriptContext sGlobalScriptContext;u8 sGlobalScriptContextStatus;
void*gNullScriptPtr=(void*)1;
#define ScriptReadByte(ctx) (*(ctx)->scriptPtr++)
int frame,phase,toWhiteFrame,fromWhiteFrame,healFrame,messageFrame,enableCalls,creates,destroys,stopCalls,waitStopsAt;
void LockPlayerFieldControls(void){} void UnlockPlayerFieldControls(void){}
void FreezeObjectEvents(void){} void StopPlayerAvatar(void){stopCalls++;}
void HandleEnforcedLookDirectionOnPlayerStopMoving(void){}
bool8 IsUpdateLinkStateCBActive(void){return FALSE;}
static void ObserveRow(void);
u16 HealHost(void){healFrame=frame;return 0;}
u16(*const gSpecials[])(void)={HealHost};
u16(*const*gSpecialsEnd)(void)=gSpecials+1;
bool8 ScrCmd_waitmessage(struct ScriptContext*ctx);
bool8 IsFieldMessageBoxHidden(void){return FALSE;}
bool8 MessageHost(struct ScriptContext*ctx){messageFrame=frame;return ScrCmd_waitmessage(ctx);}
'''
MAIN=r'''
void BlendPalette(u16 offset,u16 count,u8 coeff,u16 color)
{if(offset<256)bgBlend=coeff;else objBlend=coeff;SourceBlendPalette(offset,count,coeff,color);}
void FogHorizontal_Main(void){mainCalls++;SourceFogHorizontal_Main();}
void FadeScreen(u8 mode,s8 delay)
{if(mode==FADE_TO_WHITE)toWhiteFrame=frame;else fromWhiteFrame=frame;SourceFadeScreen(mode,delay);}
void ApplyFogBlend(u8 coeff,u16 color)
{bgBlend=objBlend=coeff;SourceApplyFogBlend(coeff,color);}
void ScriptContext_Enable(void){enableCalls++;sGlobalScriptContextStatus=CONTEXT_RUNNING;}
int firstRow;
static void ObserveRow(void)
{
 if(!firstRow)printf(",");firstRow=0;
 int native=sGlobalScriptContext.mode!=SCRIPT_MODE_NATIVE?0:sGlobalScriptContext.nativePtr==IsFreezePlayerFinished?1:sGlobalScriptContext.nativePtr==IsPaletteNotActive?2:3;
 printf("[%d",frame);
 int v[]={phase,sGlobalScriptContext.mode,sGlobalScriptContextStatus,native,enableCalls,toWhiteFrame,fromWhiteFrame,healFrame,messageFrame,bgBlend,objBlend,gPaletteFade.active,gPaletteFade.y,gPaletteFade.objPaletteToggle,gPaletteFade.softwareFadeFinishing,gPaletteFade.softwareFadeFinishingCounter,weatherState.fadeScreenCounter,weatherState.palProcessingState,weatherState.fadeInActive,IsWeatherNotFadingIn(),weatherState.fogHScrollOffset,weatherState.fogHScrollCounter,weatherState.fogHScrollPosX,weatherState.currBlendEVA,weatherState.currBlendEVB,weatherState.initStep,mainCalls,creates,destroys,paletteCopies,gPlttBufferFaded[1],gPlttBufferFaded[257],gPlttBufferFaded[273],gPlttBufferUnfaded[1],gPlttBufferUnfaded[257],gPlttBufferUnfaded[273],FuncIsActiveTask(Task_WaitPlayerStopMoving),stopCalls,gPlayerAvatar.tileTransitionState};
 for(unsigned i=0;i<ARRAY_COUNT(v);i++)printf(",%d",v[i]);printf("]");
}
int main(void)
{
 assert(sizeof(struct RGBColor)==2&&sizeof(struct PlttData)==2);
 ScrCmdFunc cmds[4]={ScrCmd_lockall,ScrCmd_fadescreen,ScrCmd_special,MessageHost};
 const u8 program[]={0,1,FADE_TO_WHITE,2,0,0,1,FADE_FROM_WHITE,3};
 printf("{\"traces\":[");
 for(int c=0;c<4;c++){
  memset(&weatherState,0,sizeof(weatherState));memset(&gPaletteFade,0,sizeof(gPaletteFade));memset(&gPlayerAvatar,0,sizeof(gPlayerAvatar));ResetTasks();
  mainCalls=paletteCopies=creates=destroys=stopCalls=enableCalls=0;toWhiteFrame=fromWhiteFrame=healFrame=messageFrame=-1;
  weatherState.currWeather=weatherState.nextWeather=WEATHER_FOG_HORIZONTAL;weatherState.palProcessingState=WEATHER_PAL_STATE_IDLE;
  Weather_SetBlendCoeffs(16,0);FogHorizontal_InitAll();CreateTask(Task_WeatherMain,80);
  int age=c==1?7:c==2?1020:3;for(int a=0;a<age;a++)Task_WeatherMain(0);
  if(c==3){weatherState.fogHScrollOffset=65535;weatherState.fogHScrollCounter=3;}
  // Synthetic RGB555 probes exercise both copied ordinary and lightened-object baselines.
  for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=((i*17)&31)|(((i*3)&31)<<5)|(((i*7)&31)<<10);
  weatherState.lightenedFogSpritePalsCount=1;weatherState.lightenedFogSpritePals[0]=17;ApplyFogBlend(0,RGB_WHITEALPHA);bgBlend=objBlend=0;
  int startMain=mainCalls;int startOffset=weatherState.fogHScrollOffset;int startCounter=weatherState.fogHScrollCounter;
  InitScriptContext(&sGlobalScriptContext,cmds,cmds+4);SetupBytecodeScript(&sGlobalScriptContext,program);sGlobalScriptContextStatus=CONTEXT_RUNNING;
  waitStopsAt=c==1?4:0;gPlayerAvatar.tileTransitionState=waitStopsAt?1:0;
  if(c)printf(",");printf("{\"name\":\"inplaceWhite%d\",\"age\":%d,\"movementStopsAfterSpriteFrame\":%d,\"initialFog\":[%d,%d,%d],\"rows\":[",c,age,waitStopsAt,startMain,startOffset,startCounter);firstRow=1;
  for(frame=0;frame<100;frame++){
   phase=0;ObserveRow();ScriptContext_RunScript();phase=1;ObserveRow();RunTasks();phase=2;ObserveRow();
   // Only this movement-completion boundary is supplied; lock task and native predicate are original C.
   if(frame==waitStopsAt)gPlayerAvatar.tileTransitionState=0;
   UpdatePaletteFade();phase=3;ObserveRow();
   assert(creates==1&&destroys==0&&weatherState.initStep==2&&mainCalls==startMain+frame+1);
   if(fromWhiteFrame>=0&&frame>=fromWhiteFrame+17)break;
  }
  assert(messageFrame>=0&&frame<100&&enableCalls==0&&paletteCopies==1);printf("]}");
 }
 printf("]}\n");return 0;
}
'''

def generate(reference,helpers,retain):
 sys.path.insert(0,str(helpers))
 import generate_field_fog_vectors as fog,generate_field_warp_vectors as warp
 from generate_field_stair_vectors import extract_function
 from reference_c import COMMIT,compile_c,verify_reference
 verify_reference(reference)
 hashes={**warp.HASHES,**fog.EXTRA_HASHES,**EXTRA};sources={}
 for path,digest in hashes.items():
  raw=(reference/path).read_bytes()
  if hashlib.sha256(raw).hexdigest()!=digest:raise ValueError('Pinned source changed: '+path)
  sources[path]=raw.decode()
 order=extract_function(sources['src/overworld.c'],'OverworldBasic');calls=['ScriptContext_RunScript();','RunTasks();','AnimateSprites();','UpdatePaletteFade();']
 assert [order.index(x) for x in calls]==sorted(order.index(x) for x in calls)
 script=sources['data/maps/PokemonTower_5F/scripts.inc'].split('PokemonTower_5F_EventScript_PurifiedZone::',1)[1].split('\n\n',1)[0]
 std=sources['data/scripts/std_msgbox.inc'].split('Std_MsgboxDefault::',1)[1].split('\n\n',1)[0]
 assert [x.strip() for x in std.splitlines() if x.strip()]==['message 0x0','waitmessage','waitbuttonpress','return']
 assert [x.strip() for x in script.splitlines() if x.strip()][:5]==['lockall','fadescreen FADE_TO_WHITE','special HealPlayerParty','fadescreen FADE_FROM_WHITE','msgbox PokemonTower_5F_Text_PurifiedZoneMonsFullyHealed']
 h=warp.HEADER.replace('typedef int bool8;','typedef uint8_t bool8; typedef int bool32;')
 h=h.replace('enum { WEATHER_PAL_STATE_IDLE, WEATHER_PAL_STATE_SCREEN_FADING_IN };','enum { WEATHER_PAL_STATE_CHANGING_WEATHER, WEATHER_PAL_STATE_SCREEN_FADING_IN, WEATHER_PAL_STATE_SCREEN_FADING_OUT, WEATHER_PAL_STATE_IDLE };')
 h=h.replace('struct Task { s16 data[16]; } gTasks[1];','''
#define NUM_TASKS 16
#define NUM_TASK_DATA 16
#define HEAD_SENTINEL 0xFE
#define TAIL_SENTINEL 0xFF
#define REVISION 0
struct Task {s16 data[16];void(*func)(u8);u8 isActive,prev,next,priority;}gTasks[NUM_TASKS];
typedef void(*TaskFunc)(u8);
''')
 h=h.replace('struct Weather { int fadeInCounter,fadeInActive,currWeather,gammaIndex,gammaTargetIndex,palProcessingState; } weatherState;',fog.WEATHER_STRUCT.replace('} weatherState;','u8 lightenedFogSpritePalsCount,lightenedFogSpritePals[6];} weatherState;'))
 for a in ['void FreezeObjectEvents(void){} ','void LockPlayerFieldControls(void){} void UnlockPlayerFieldControls(void){}','void DestroyTask(int taskId){(void)taskId;complete=1;}','bool8 FadeInScreen_FogHorizontal(void){return FALSE;}']:
  assert a in h;h=h.replace(a,'')
 h=h.replace('void BlendPalette(int offset,int count,int y,int color)\n{ (void)count; (void)color; if(offset<256)bgBlend=y;else objBlend=y; }','void BlendPalette(u16,u16,u8,u16);')
 # Use the actual source bitfield for 15-bit color and source counter widths.
 start=h.index('struct Palette {');end=h.index('} gPaletteFade;',start)+len('} gPaletteFade;')
 p=sources['include/palette.h'];a=p.index('struct PaletteFadeControl\n{');b=p.index('\n};',a)+3
 h=h[:start]+p[a:b]+'\nstruct PaletteFadeControl gPaletteFade;'+h[end:]
 h=h.replace('#define gPaletteFade_delay gPaletteFade.delay','#define gPaletteFade_delay gPaletteFade.multipurpose2').replace('#define gPaletteFade_selectedPalettes gPaletteFade.selectedPalettes','#define gPaletteFade_selectedPalettes gPaletteFade.multipurpose1')
 f=fog.HEADER.replace('int CreateTask(void(*fn)(u8),int priority){(void)fn;(void)priority;return 0;}','u8 CreateTask(TaskFunc,u8);')
 f=f.replace('struct { int objectEventId,spriteId; } gPlayerAvatar;','struct { int objectEventId,spriteId,tileTransitionState; } gPlayerAvatar;')
 f=f.replace('void ApplyFogBlend(u8 coefficient,u16 color){(void)color;bgBlend=objBlend=coefficient;}','void ApplyFogBlend(u8,u16);')
 f=f.replace('void CreateFogHorizontalSprites(void){weatherState.fogHSpritesCreated=TRUE;}','void CreateFogHorizontalSprites(void){weatherState.fogHSpritesCreated=TRUE;creates++;}').replace('void DestroyFogHorizontalSprites(void){weatherState.fogHSpritesCreated=FALSE;}','void DestroyFogHorizontalSprites(void){weatherState.fogHSpritesCreated=FALSE;destroys++;}')
 # Declare counters before fog hardware stubs; other shared host stubs are unused.
 f='extern int creates,destroys;\n'+f
 groups={
 'src/task.c':('ResetTasks','CreateTask','InsertTask','DestroyTask','RunTasks','FindFirstActiveTask','TaskDummy','FuncIsActiveTask'),
 'src/palette.c':('IsSoftwarePaletteFadeFinishing','UpdateNormalPaletteFade','BeginNormalPaletteFade'),
 'src/blend_palette.c':('BlendPalette',),
 'src/field_weather.c':('Weather_SetBlendCoeffs','Weather_SetTargetBlendCoeffs','Weather_UpdateBlend','FadeInScreen_FogHorizontal','FadeInScreenWithWeather','FadeScreen','Task_WeatherMain','LightenSpritePaletteInFog','ApplyFogBlend','IsWeatherNotFadingIn'),
 'src/field_weather_effects.c':('FogHorizontal_InitVars','FogHorizontal_InitAll','FogHorizontal_Main','FogHorizontal_Finish'),
 'src/script.c':('InitScriptContext','SetupBytecodeScript','SetupNativeScript','RunScriptCommand','ScriptReadHalfword','ScriptContext_RunScript'),
 'src/event_object_lock.c':('walkrun_is_standing_still','Task_WaitPlayerStopMoving','IsFreezePlayerFinished','FreezeObjects_WaitForPlayer'),
 'src/scrcmd.c':('IsPaletteNotActive','ScrCmd_fadescreen','ScrCmd_lockall','ScrCmd_special','ScrCmd_waitmessage')}
 bodies=[]
 for path,names in groups.items():
  for name in names:
   body=extract_function(sources[path],name)
   for old,new in [('void BlendPalette(', 'void SourceBlendPalette('),('void FogHorizontal_Main(', 'void SourceFogHorizontal_Main('),('void FadeScreen(', 'void SourceFadeScreen('),('static void ApplyFogBlend(', 'static void SourceApplyFogBlend(')]:body=body.replace(old,new)
   body=body.replace('asm("svc 2");','abort();')
   bodies.append(body)
 code=h+f+STUBS+'void FadeScreen(u8,s8);\n'+''.join(b.split('\n{',1)[0]+';\n' for b in bodies)+'''
struct WeatherFuncs{void(*initVars)(void);void(*main)(void);void(*initAll)(void);bool8(*finish)(void);};
static const struct WeatherFuncs sWeatherFuncs[WEATHER_UNDERWATER+1]={[WEATHER_FOG_HORIZONTAL]={FogHorizontal_InitVars,FogHorizontal_Main,FogHorizontal_InitAll,FogHorizontal_Finish}};
static void(*const sWeatherPalStateFuncs[])(void)={DoNothing,FadeInScreenWithWeather,DoNothing,DoNothing};
'''+''.join(bodies)+MAIN
 if retain:retain.write_text(code)
 results=[]
 with tempfile.TemporaryDirectory() as d:
  src=Path(d)/'white_fade.c';src.write_text(code)
  for opt in ['-O0','-O2']:
   exe=Path(d)/('white_fade.exe' if os.name=='nt' else 'white_fade')
   compile_c(['cc','-std=c11',opt,'-fno-strict-aliasing',str(src),'-o',str(exe)])
   results.append(json.loads(subprocess.check_output([str(exe)],text=True)))
 if results[0]!=results[1]:raise ValueError('Optimization parity failed')
 return dict(sourceCommit=COMMIT,columns=COLUMNS,phaseNames=['beforeScript','afterScript','afterWeatherTasks','afterPalette'],nativeWaitNames=['none','lockPlayer','palette','message'],optimizationParity=['O0','O2'],sources=hashes,sourceFunctions=groups,**results[0])

def validate(r):
 assert len(r['traces'])==4
 summaries=[]
 for t in r['traces']:
  rows=[dict(zip(COLUMNS,x)) for x in t['rows']];end=[x for x in rows if x['phase']==3];by={(x['frame'],x['phase']):x for x in rows};last=end[-1];out=last['toWhiteFrame'];inc=last['fromWhiteFrame'];msg=last['messageFrame']
  movement=t['movementStopsAfterSpriteFrame'];assert out==(movement+3 if movement else 2)
  lockClear=movement+1 if movement else 0
  assert by[(lockClear,1)]['lockTaskActive']==1 and by[(lockClear,2)]['lockTaskActive']==0
  assert by[(lockClear+1,1)]['mode']==1 and by[(lockClear+1,1)]['stopPlayerCalls']==1
  assert inc==out+23 and msg==inc+2 and last['healFrame']==inc
  assert all(x['scriptEnableCalls']==0 and x['fogCreates']==1 and x['fogDestroys']==0 and x['initStep']==2 for x in rows)
  assert by[(out,1)]['paletteObjectToggle']==1 and by[(out,3)]['paletteY']==2
  for d in range(22):
   row=by[(out+d,3)];expectedBg=min(16,2*((d+1)//2));expectedObj=min(16,2*(d//2))
   assert (row['backgroundBlend'],row['objectBlend'])==(expectedBg,expectedObj),(t['name'],d,row)
   assert bool(row['paletteActive'])==(d<21)
  assert by[(out+22,1)]['mode']==1 and by[(out+22,1)]['fromWhiteFrame']==-1
  for d in range(18):
   row=by[(inc+d,3)];assert row['backgroundBlend']==row['objectBlend']==max(0,15-d)
   assert row['weatherReady']==int(d>=16) and row['paletteActive']==0
  assert by[(msg,1)]['backgroundBlend']==14 and by[(msg,3)]['backgroundBlend']==13
  # Weather frame is live continuously; never recreate weather or reset scroll.
  n,offset,counter=t['initialFog']
  for row in end:
   ticks=row['frame']+1;assert row['fogMainCalls']==n+ticks
   assert row['scrollOffset']==(offset+(counter+ticks)//4)%65536
   assert row['scrollCounter']==(counter+ticks)%4
   assert row['scrollX']==(-(offset+(counter+ticks-1)//4))%256
   assert (row['eva'],row['evb'])==(12,8)
  base=by[(out,0)];copied=by[(out,1)]
  for x,y in [('bgSample','unfadedBgSample'),('objectSample','unfadedObjectSample'),('lightenedObjectSample','unfadedLightenedObjectSample')]:assert base[x]==copied[y]
  assert all(x['contextStatus']==0 for x in rows)
  assert by[(msg,1)]['nativeWait']==3
  summaries.append(dict(name=t['name'],toWhite=out,whiteVisible=out+16,paletteInactive=out+21,outNativeReady=out+22,healAndFromWhite=inc,inNativeReady=inc+1,message=msg,visualZero=inc+15,weatherReady=inc+16,rows=len(rows)))
 return summaries

def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--helpers',type=Path,default=Path(__file__).parent);p.add_argument('--output',type=Path,required=True);p.add_argument('--retain-c',type=Path);a=p.parse_args()
 r=generate(a.reference,a.helpers,a.retain_c);r['summary']=validate(r);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(r['summary'],indent=2))
if __name__=='__main__':main()
