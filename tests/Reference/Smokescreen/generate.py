#!/usr/bin/env python3
"""Extract original Smokescreen raw helper, callbacks and frame interpreter.

Only authored cases/generator are tracked. Trajectories are explicit local data.
The resource shim records native load/free calls; it does not invent refcounts.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference,out):
 helper=module('reference_c',root/'tests/Reference/reference_c.py');helper.verify_reference(reference)
 ex=module('source_extract',root/'tests/Reference/source_extract.py');engine=module('sprite_harness',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 pins=json.loads((Path(__file__).parent/'pinned-inputs.json').read_text());source={p:(reference/p).read_text() for p in pins if p.endswith(('.c','.h','.s'))}
 for path,digest in pins.items():
  if hashlib.sha256((reference/path).read_bytes()).hexdigest()!=digest:raise ValueError('Pinned source changed: '+path)
 names={'src/battle_anim_smokescreen.c':['SmokescreenImpact','SpriteCB_SmokescreenImpactMain','SpriteCB_SmokescreenImpact'],'src/util.c':['CreateInvisibleSpriteWithCallback'],'src/battle_anim_effects_3.c':['AnimTask_SmokescreenImpact','AnimBlackSmoke','AnimBlackSmoke_Step'],'src/sprite.c':['StartSpriteAnim'],'src/battle_anim_mons.c':['GetBattlerSpriteFinal_Y','AnimThrowProjectile','AnimThrowProjectile_Step','InitSpritePosToAnimAttacker','SetAnimSpriteInitialXOffset','InitAnimArcTranslation','TranslateAnimHorizontalArc','InitAnimLinearTranslation','AnimTranslateLinear'],'src/trig.c':['Sin']}
 funcs=[ex.function(source[p],n) for p,ns in names.items() for n in ns]
 smoke=source['src/battle_anim_smokescreen.c'];decls=smoke[smoke.index('#define TAG_SMOKESCREEN'):smoke.index('const struct CompressedSpriteSheet gSpriteSheet_EnemyShadow')]
 extra=r'''
#define ST_OAM_AFFINE_OFF 0
#define ST_OAM_OBJ_NORMAL 0
#define ST_OAM_4BPP 0
#define SPRITE_SHAPE(x) 0
#define SPRITE_SIZE(x) 1
#define sActiveSprites data[0]
#define sPersist data[1]
#define sMainSpriteId data[0]
#define BATTLER_COORD_X 0
#define BATTLER_COORD_Y 1
#define sTransl_Speed data[0]
#define sTransl_InitX data[1]
#define sTransl_DestX data[2]
#define sTransl_InitY data[3]
#define sTransl_DestY data[4]
#define sTransl_ArcAmpl data[5]
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define B_SIDE_PLAYER 0
#define DISPLAY_HEIGHT 160
#define MON_PIC_HEIGHT 64
#define IS_DOUBLE_BATTLE() doubles
static int doubles,yoff,elevation,coordX,coordY;static u8 gBattleAnimAttacker,gBattleAnimTarget;static s16 gBattleAnimArgs[8];
static const struct {u8 x,y;} sBattlerCoords[2][4]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};
static int GetBattlerSide(u8 id){return id&1;}static u8 GetBattlerPosition(u8 id){return id;}
static u8 GetBattlerYDelta(u8 id,u16 species){return yoff;}static u8 GetBattlerElevation(u8 id,u16 species){return id&1?elevation:0;}
static u8 GetBattlerSpriteCoord(u8 id,u8 coord){return (coord&1)==0?sBattlerCoords[doubles][id].x:GetBattlerSpriteFinal_Y(id,1,TRUE);}
static u8 GetBattlerSpriteCoord2(u8 id,u8 coord){return GetBattlerSpriteCoord(id,coord);}
static int visualCount,tilesLoaded,paletteLoaded,sheetLoads,paletteLoads,sheetFrees,paletteFrees;static u32 gSmokescreenImpactTiles[1],gSmokescreenImpactPalette[1];
static void DestroySprite(struct Sprite*s){s->inUse=FALSE;}
static void DestroyAnimVisualTask(u8 id){visualCount--;}
static void SpriteCallbackDummy(struct Sprite*s){}
static const union AnimCmd dummyAnim={.type=-1};static const union AnimCmd*const gDummySpriteAnimTable[]={&dummyAnim};
static const union AffineAnimCmd dummyAffine={.type=0x7fff};static const union AffineAnimCmd*const gDummySpriteAffineAnimTable[]={&dummyAffine};
static const struct OamData gDummyOamData={.y=160,.x=304,.priority=3};
static u16 GetSpriteTileStartByTag(u16 tag){assert(tag==55019);return tilesLoaded?0:0xffff;}
static void LoadCompressedSpriteSheetUsingHeap(const struct CompressedSpriteSheet*s){if(s->tag!=55019)return;assert(s->size==0x180);tilesLoaded=1;sheetLoads++;}
static void LoadCompressedSpritePaletteUsingHeap(const struct CompressedSpritePalette*s){if(s->tag!=55019)return;paletteLoaded=1;paletteLoads++;}
static void FreeSpriteTilesByTag(u16 tag){assert(tag==55019);tilesLoaded=0;sheetFrees++;}
static void FreeSpritePaletteByTag(u16 tag){assert(tag==55019);paletteLoaded=0;paletteFrees++;}
static u8 CreateSprite(const struct SpriteTemplate*t,s16 x,s16 y,u8 priority){int i;for(i=0;i<MAX_SPRITES&&gSprites[i].inUse;i++);assert(i<MAX_SPRITES);struct Sprite*s=&gSprites[i];memset(s,0,sizeof(*s));s->inUse=TRUE;s->animBeginning=TRUE;s->usingSheet=TRUE;s->oam=*t->oam;s->x=x;s->y=y;s->subpriority=priority;s->anims=t->anims;s->affineAnims=t->affineAnims;s->callback=t->callback;s->template=t;return i;}
'''
 invisible=source['src/util.c'];start=invisible.index('static const struct SpriteTemplate gInvisibleSpriteTemplate');end=invisible.index('};',start)+2
 extra+='#define Q_8_8(x) ((s16)((x)*256))\n'+ex.array(source['src/trig.c'],'gSineTable')+'\n'+invisible[start:end]+'\n'+decls
 h=engine.sprite_harness(reference,funcs,extra).replace('#define MAX_SPRITES 1','#define MAX_SPRITES 64')
 definitions=r'''
struct OamData {int y,affineMode,objMode,mosaic,bpp,shape,x,matrixNum,size,tileNum,priority,paletteNum,affineParam;};
struct SpriteTemplate;
struct CompressedSpriteSheet {const void*data;u16 size,tag;};
struct CompressedSpritePalette {const void*data;u16 tag;};
'''
 h=h.replace('struct Sprite;\ntypedef',definitions+'\nstruct Sprite;\ntypedef',1)
 h=h.replace('struct {u8 affineMode;int tileNum;} oam;','struct OamData oam;bool8 invisible;int subpriority;const struct SpriteTemplate*template;')
 template='struct SpriteTemplate {u16 tileTag,paletteTag;const struct OamData*oam;const union AnimCmd*const*anims;const void*images;const union AffineAnimCmd*const*affineAnims;SpriteCallback callback;};\n'
 h=h.replace('struct ObjAffineSrcData',template+'struct ObjAffineSrcData',1)
 h=h.replace('static void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;}','static int visualCount;\nstatic void DestroyAnimSprite(struct Sprite*s){s->inUse=FALSE;visualCount--;}')
 h+=r'''
static void print(int tick){printf("%d,%d,%d,%d,%d,%d,%d,%d",tick,tilesLoaded,paletteLoaded,sheetLoads,paletteLoads,sheetFrees,paletteFrees,visualCount);for(int i=0;i<64;i++){struct Sprite*s=&gSprites[i];if(!s->inUse||s->callback==SpriteCallbackDummy&&s->template==NULL)continue;int kind=s->template==&sSmokescreenImpactSpriteTemplate?1:s->template==&gInvisibleSpriteTemplate?0:s->template->tileTag==10017?3:2;printf(";%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",i,kind,s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->invisible,s->hFlip,s->vFlip,s->subpriority,s->oam.priority,s->animEnded,s->animDelayCounter,s->data[0],s->data[1],s->data[2]);}puts("");}
static int live(void){for(int i=0;i<64;i++)if(gSprites[i].inUse&&gSprites[i].template)return 1;return 0;}
int main(int argc,char**argv){assert(argc>=3);int mode=atoi(argv[1]);if(mode==0){assert(argc>=11);doubles=atoi(argv[2]);gBattleAnimAttacker=atoi(argv[3]);gBattleAnimTarget=atoi(argv[4]);yoff=atoi(argv[5]);elevation=atoi(argv[6]);int persist=atoi(argv[7]);tilesLoaded=paletteLoaded=atoi(argv[8]);int occupied=atoi(argv[9]);assert(argc==10+occupied);for(int i=0;i<(doubles?4:2);i++){gSprites[i].inUse=1;gSprites[i].callback=SpriteCallbackDummy;}for(int i=0;i<occupied;i++){int slot=atoi(argv[10+i]);gSprites[slot].inUse=1;gSprites[slot].callback=SpriteCallbackDummy;}for(int i=0;i<64;i++)if(gSprites[i].inUse){gSprites[i].anims=gDummySpriteAnimTable;gSprites[i].animBeginning=TRUE;}if(persist)SmokescreenImpact(GetBattlerSpriteCoord(gBattleAnimTarget,2)+8,GetBattlerSpriteCoord(gBattleAnimTarget,3)+8,TRUE);else{visualCount=1;AnimTask_SmokescreenImpact(0);}print(-1);for(int frame=0;frame<40;frame++){AnimateSprites();print(frame);if(!live()||persist&&!tilesLoaded)return 0;}abort();}
 assert(argc==9);coordX=atoi(argv[2]);coordY=atoi(argv[3]);for(int i=0;i<5;i++)gBattleAnimArgs[i]=atoi(argv[4+i]);static const struct SpriteTemplate smoke={.oam=&gDummyOamData,.anims=gDummySpriteAnimTable,.affineAnims=gDummySpriteAffineAnimTable,.callback=AnimBlackSmoke};u8 id=CreateSprite(&smoke,coordX,coordY,4);gSprites[id].oam.priority=2;visualCount=1;gSprites[id].callback(&gSprites[id]);AnimateSprite(&gSprites[id]);print(-1);for(int frame=0;frame<33000;frame++){AnimateSprites();print(frame);if(!live())return 0;}abort();}
'''
 # Native impact argument count includes the explicit occupied-count field.
 h=h.replace('assert(argc>=11)','assert(argc>=10)')
 path=out/'smokescreen.c';path.write_text(h);executables=[]
 for opt in ['O0','O2']:
  exe=out/('smokescreen-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(exe)]);executables.append(exe)
 cases=json.loads((Path(__file__).parent/'cases.json').read_text())
 for name,rows in cases.items():
  for case in rows:
   args=[0,int(case['doubles']),case['actor'],case['target'],case['yOffset'],case['elevation'],int(case['persist']),int(case['preloaded']),len(case['occupied']),*case['occupied']] if name=='impact' else [1,case['x'],case['y'],*case['args']]
   outputs=[subprocess.check_output([str(exe),*map(str,args)],text=True) for exe in executables]
   if outputs[0]!=outputs[1]:raise ValueError('Native optimization parity differs: '+str(case))
   case['rows']=[{'state':list(map(int,parts[0].split(','))),'sprites':[list(map(int,p.split(','))) for p in parts[1:]]} for line in outputs[0].splitlines() for parts in [line.split(';')]]
 schedule_names=['RunAnimScriptCommand','WaitAnimFrameCount','Cmd_loadspritegfx','Cmd_delay','Cmd_waitforvisualfinish'];schedule_funcs=[ex.function(source['src/battle_anim.c'],n) for n in schedule_names]
 script_text=source['data/battle_anim_scripts.s'];match=re.search(r'^Move_SMOKESCREEN:\n(.*?)(?=^\w+:)',script_text,re.M|re.S);assert match
 data=[]
 def word(n):return [n&255,(n>>8)&255]
 for line in match[1].splitlines():
  line=line.strip()
  if not line:continue
  op,_,tail=line.partition(' ');args=[a.strip() for a in tail.split(',')] if tail else []
  if op=='loadspritegfx':data.extend([0,0,0])
  elif op=='createsprite':
   assert args[0] in ['gBlackBallSpriteTemplate','gBlackSmokeSpriteTemplate'] and args[1]=='ANIM_TARGET';values=list(map(int,args[3:]));data.extend([2,int(args[0]=='gBlackBallSpriteTemplate'),int(args[2]),len(values),*[b for v in values for b in word(v)]])
  elif op=='createvisualtask':assert args==['AnimTask_SmokescreenImpact','2'];data.append(3)
  elif op=='delay':data.extend([4,int(args[0])])
  elif op=='waitforvisualfinish':data.append(5)
  elif op=='playsewithpan':data.extend([6,0 if args[0]=='SE_M_DOUBLE_TEAM' else 1])
  elif op=='end':data.append(8)
  else:raise ValueError(op)
 schedule=h[:h.index('int main(')]+r'''
#define gAnimVisualTaskCount visualCount
#define T1_READ_16(p) ((u16)((p)[0]|((p)[1]<<8)))
#define GET_TRUE_SPRITE_INDEX(x) 0
static const struct CompressedSpriteSheet gBattleAnimPicTable[1]={{.tag=10016}};static const struct CompressedSpritePalette gBattleAnimPaletteTable[1]={{.tag=10016}};static void AddSpriteIndex(u16 index){}
static const u8*sBattleAnimScriptPtr;static s8 sAnimFramesToWait;static int gAnimScriptActive=1,frame;static void(*gAnimScriptCallback)(void);
'''+ '\n'.join(f[:f.index('{')].strip()+';' for f in schedule_funcs)+r'''
static void Sprite(void){sBattleAnimScriptPtr++;int ball=*sBattleAnimScriptPtr++,priority=*sBattleAnimScriptPtr++,count=*sBattleAnimScriptPtr++;for(int i=0;i<count;i++){gBattleAnimArgs[i]=T1_READ_16(sBattleAnimScriptPtr);sBattleAnimScriptPtr+=2;}static const struct SpriteTemplate smoke={.tileTag=10016,.oam=&gDummyOamData,.anims=gDummySpriteAnimTable,.affineAnims=gDummySpriteAffineAnimTable,.callback=AnimBlackSmoke};static const struct SpriteTemplate projectile={.tileTag=10017,.oam=&gDummyOamData,.anims=gDummySpriteAnimTable,.affineAnims=gDummySpriteAffineAnimTable,.callback=AnimThrowProjectile};int sub[]={30,40,20,50};u8 id=CreateSprite(ball?&projectile:&smoke,GetBattlerSpriteCoord(gBattleAnimTarget,2),GetBattlerSpriteCoord(gBattleAnimTarget,3),sub[gBattleAnimTarget]-priority);gSprites[id].oam.priority=2;gSprites[id].callback(&gSprites[id]);AnimateSprite(&gSprites[id]);visualCount++;printf("E,%d,0,%d,%d\n",frame,id,ball);}
static void Task(void){sBattleAnimScriptPtr++;AnimTask_SmokescreenImpact(0);visualCount++;printf("E,%d,1,0,0\n",frame);}
static void Sound(void){sBattleAnimScriptPtr++;int sound=*sBattleAnimScriptPtr++;printf("E,%d,2,%d,0\n",frame,sound);}
static void End(void){if(visualCount){sAnimFramesToWait=1;return;}gAnimScriptActive=0;printf("E,%d,3,0,0\n",frame);}
static void(*const sScriptCmdTable[9])(void)={[0]=Cmd_loadspritegfx,[2]=Sprite,[3]=Task,[4]=Cmd_delay,[5]=Cmd_waitforvisualfinish,[6]=Sound,[8]=End};
'''+ '\n'.join(schedule_funcs)+'\nstatic const u8 script[]={'+','.join(map(str,data))+'};'+r'''
int main(int argc,char**argv){assert(argc==4);doubles=atoi(argv[1]);gBattleAnimAttacker=atoi(argv[2]);gBattleAnimTarget=atoi(argv[3]);yoff=8;for(int i=0;i<(doubles?4:2);i++){gSprites[i].inUse=TRUE;gSprites[i].callback=SpriteCallbackDummy;gSprites[i].anims=gDummySpriteAnimTable;gSprites[i].animBeginning=TRUE;}sBattleAnimScriptPtr=script;gAnimScriptCallback=RunAnimScriptCommand;for(frame=0;gAnimScriptActive&&frame<200;frame++){gAnimScriptCallback();AnimateSprites();print(frame);}assert(!gAnimScriptActive&&!tilesLoaded&&!paletteLoaded);return 0;}
'''
 path=out/'smokescreen-schedule.c';path.write_text(schedule);executables=[]
 for opt in ['O0','O2']:
  exe=out/('smokescreen-schedule-'+opt);helper.compile_c(['cc','-std=c11','-'+opt,'-fwrapv','-fno-strict-aliasing',str(path),'-o',str(exe)]);executables.append(exe)
 schedules=[]
 for doubles,actor,target in [(0,0,1),(0,1,0),(1,0,3),(1,1,2),(1,2,1),(1,3,0)]:
  outputs=[subprocess.check_output([str(exe),str(doubles),str(actor),str(target)],text=True) for exe in executables]
  if outputs[0]!=outputs[1]:raise ValueError('Native schedule optimization parity differs')
  events=[];rows=[]
  for line in outputs[0].splitlines():
   if line.startswith('E,'):events.append(list(map(int,line[2:].split(','))));continue
   parts=line.split(';');rows.append(dict(state=list(map(int,parts[0].split(','))),sprites=[list(map(int,p.split(','))) for p in parts[1:]]))
  schedules.append(dict(doubles=bool(doubles),actor=actor,target=target,events=events,rows=rows))
 return dict(schema='firered-smokescreen-source-v1',sourceSha256=pins,optimizationParity=['O0','O2'],sourceFunctions=names,scheduleFunctions=schedule_names,schedules=schedules,cases=cases,semantics='Tick -1 records native initialization. Tick 0 and later use original AnimateSprites and frame interpreter. Resource shim records load/free-by-tag; unchecked native allocation is bounded by explicit cases. Source persist/preloaded rows document helper behavior beyond the admitted single-owner nonpersistent invocation. No managed code produces expected rows.')

if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--build-dir',type=Path);a=p.parse_args()
 if a.output.exists():raise FileExistsError('Refusing to overwrite local fixture: '+str(a.output))
 out=a.build_dir or Path(tempfile.mkdtemp(prefix='smokescreen-reference-'));out.mkdir(exist_ok=True,parents=True);r=build(a.root.resolve(),a.reference or a.root/'content/raw/pokefirered/037335f4',out);a.output.parent.mkdir(exist_ok=True,parents=True);a.output.write_text(json.dumps(r,separators=(',',':'))+'\n');print(json.dumps(dict(cases=sum(len(c) for c in r['cases'].values()),rows=sum(len(c['rows']) for rows in r['cases'].values() for c in rows))))
