#!/usr/bin/env python3
"""Extract original Stockpile/Swallow callbacks with shared native sprite/task players."""
import argparse,hashlib,importlib.util,json,re,subprocess
from pathlib import Path
CONFIGS=[[[x,y,13,0] for x,y in [(55,55),(-55,-55),(0,55),(0,-55),(55,-34),(55,34),(-55,-34),(-55,34)]]+[[x,y,d,0] for x,y,d in [(0,0,1),(127,-128,2),(-128,127,127)]],[[x,-8,0,0] for x in [0,-24,16,-16,24]]+[[127,127,0,0],[-128,-128,0,0]],[[0,0,0,hidden] for hidden in [0,1]],[[0,0,0,hidden] for hidden in [0,1]]]
SPRITE_COLUMNS=['slot','x','y','x2','y2','invisible','tile','hflip','vflip','subpriority','a','b','c','d','affineMode','objMode','priority','frameIndex','frameDelay','frameEnded','affineEnded','affineIndex','affineDelay','scaleX','scaleY','rotation']
STATE_COLUMNS=['tick','visualCount','activeTask','liveSprites','matrices']+['taskData'+str(i) for i in range(13)]
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m

def build(root,reference):
 pg=module('orb_defense_power_base',root/'tests/Game.Presentation.Tests/Reference/generate_power_guard_vectors.py')
 base=module('orb_defense_native_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py')
 h,metadata=pg.build(root,reference);h=h.split('static const long long config0')[0]
 src={n:(reference/('src/'+n+'.c')).read_text() for n in ['battle_anim_mons','battle_anim_effects_1','battle_anim_effects_3']}
 funcs=[base.function(src[file],name) for file,names in [('battle_anim_mons',['InitAnimLinearTranslation','AnimTranslateLinear','StartAnimLinearTranslation','AnimTranslateLinear_WithFollowup']),('battle_anim_effects_1',['AnimPowerAbsorptionOrb']),('battle_anim_effects_3',['AnimSwallowBlueOrb','AnimTask_StockpileDeformMon','AnimTask_SwallowDeformMon'])] for name in names]
 declarations=[base.declaration(src[file],name) for file,name in [('battle_anim_effects_1','sStockpileAbsorptionOrbAffineCmds'),('battle_anim_effects_3','sStockpileDeformMonAffineAnimCmds'),('battle_anim_effects_3','sSwallowDeformMonAffineAnimCmds')]]
 declarations.append(re.search(r'static const union AffineAnimCmd \*const sStockpileAbsorptionOrbAffineAnimTable\[\]\s*=\s*\{.*?\};',src['battle_anim_effects_1'],re.S)[0])
 templates=[re.search(r'const struct SpriteTemplate '+name+r'\s*=\s*\{.*?\};',src[file],re.S)[0] for file,name in [('battle_anim_effects_1','gStockpileAbsorptionOrbSpriteTemplate'),('battle_anim_effects_3','gSwallowBlueOrbSpriteTemplate')]]
 h+='\n#define ANIM_TAG_GRAY_ORB 7\nstatic const struct OamData gOamData_AffineDouble_ObjNormal_8x8={.affineMode=3,.priority=2};\n'
 h+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(declarations+templates+funcs)+'\n'
 for k,configs in enumerate(CONFIGS):h+='static const int config'+str(k)+'[][4]={'+','.join('{'+','.join(map(str,a))+'}' for a in configs)+'};\n'
 h+='static const int (*configs[])[4]={config0,config1,config2,config3};\nstatic const int counts[]={'+','.join(str(len(c)) for c in CONFIGS)+'};\n'+RUNNER
 spans=[]
 for snippet in funcs+declarations+templates:
  file=next(n for n,s in src.items() if snippet in s);line=src[file][:src[file].index(snippet)].count('\n')+1;spans.append(dict(file='src/'+file+'.c',firstLine=line,lastLine=line+snippet.count('\n'),sha256=hashlib.sha256(snippet.encode()).hexdigest(),declaration=snippet.split('\n')[0]));assert snippet in h
 return h,dict(sourceRevision=metadata['sourceRevision'],sourceSha256=metadata['sourceSha256'],sourceSpans=spans,kinds=['StockpileOrb','SwallowOrb','StockpileDeform','SwallowDeform'],configs=CONFIGS,spriteColumns=SPRITE_COLUMNS,stateColumns=STATE_COLUMNS,conventions=dict(tickMinusOne='After source synchronous callback and initial AnimateSprite',sprites='After AnimateSprites before RunTasks',afterTaskSprites='After RunTasks',geometry=metadata['geometry'],hidden='Task config[3] sets hidden-present actor; no hidden battler is omitted',host='Original native frame/affine/linear/task functions; host-safe pointer handles, fixed geometry and BIOS matrix adapter only'))
RUNNER=r'''
static int currentKind,currentConfig,currentTick;
static int live(void){int n=0;for(int i=slots;i<64;i++)n+=gSprites[i].inUse;return n;}
static void prefix(char type){printf("%d:%d:%d:%d:%d,%c,%d",currentKind,mode,gBattleAnimAttacker,gBattleAnimTarget,currentConfig,type,currentTick);}
static void emitSprites(char type){for(int i=currentKind>=2?gBattleAnimAttacker:slots;i<(currentKind>=2?gBattleAnimAttacker+1:64);i++){struct Sprite*s=&gSprites[i];if(!s->inUse)continue;struct OamMatrix m=(s->oam.affineMode&1)?gOamMatrices[s->oam.matrixNum]:(struct OamMatrix){256,0,0,256};struct AffineAnimState*a=&sAffineAnimStates[s->oam.matrixNum];int row[]={i,s->x,s->y,s->x2,s->y2,s->invisible,s->oam.tileNum,(s->oam.matrixNum>>3)&1,(s->oam.matrixNum>>4)&1,s->subpriority,m.a,m.b,m.c,m.d,s->oam.affineMode,s->oam.objMode,s->oam.priority,s->animCmdIndex,s->animDelayCounter,s->animEnded,s->affineAnimEnded,a->animCmdIndex,a->delayCounter,a->xScale,a->yScale,a->rotation};if(s->oam.affineMode&1)row[7]=row[8]=0;prefix(type);for(unsigned j=0;j<sizeof(row)/sizeof(row[0]);j++)printf(",%d",row[j]);puts("");}}
static void emitState(void){prefix('T');printf(",%d,%d,%d,%u",gAnimVisualTaskCount,gTasks[0].isActive,live(),gOamMatrixAllocBitmap);for(int i=0;i<13;i++)printf(",%d",gTasks[0].data[i]);puts("");}
static void runCase(int kind,int actor,int target,int config){
 currentKind=kind;currentConfig=config;currentTick=-1;slots=mode?4:2;gBattleAnimAttacker=actor;gBattleAnimTarget=target;const int*c=configs[kind][config];
 memset(gBattleAnimArgs,0,sizeof(gBattleAnimArgs));memset(gSprites,0,sizeof(gSprites));memset(gTasks,0,sizeof(gTasks));memset(sAffineAnimStates,0,sizeof(sAffineAnimStates));memset(&battleSpritesData,0,sizeof(battleSpritesData));gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=0;for(int i=0;i<32;i++)SetOamMatrix(i,256,0,0,256);
 for(int i=0;i<slots;i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->usingSheet=TRUE;s->animBeginning=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->oam.affineMode=1;s->oam.matrixNum=i;s->oam.paletteNum=i;s->affineAnimPaused=TRUE;s->data[0]=i;s->x=GetBattlerSpriteCoord(i,2);s->y=GetBattlerSpriteCoord(i,3);s->subpriority=GetBattlerSpriteSubpriority(i);battleSpritesData.healthBoxesData[i].matrixNum=i;AffineAnimStateReset(i);}
 if(kind>=2&&c[3]){gSprites[actor].invisible=TRUE;battleSpritesData.battlerData[actor].invisible=TRUE;}
 for(int i=0;i<3;i++)gBattleAnimArgs[i]=c[i];
 if(kind<2){CreateSpriteAndAnimate((const struct SpriteTemplate*[]){&gStockpileAbsorptionOrbSpriteTemplate,&gSwallowBlueOrbSpriteTemplate}[kind],GetBattlerSpriteCoord(actor,2),GetBattlerSpriteCoord(actor,3),GetBattlerSpriteSubpriority(actor)-2);gAnimVisualTaskCount++;}
 else{gTasks[0].isActive=TRUE;gTasks[0].func=kind==2?AnimTask_StockpileDeformMon:AnimTask_SwallowDeformMon;gAnimVisualTaskCount++;gTasks[0].func(0);}
 emitSprites('P');if(kind>=2)emitSprites('Q');emitState();
 for(currentTick=0;currentTick<1000;currentTick++){AnimateSprites();emitSprites('P');if(gTasks[0].isActive)gTasks[0].func(0);if(kind>=2)emitSprites('Q');emitState();if(!gAnimVisualTaskCount){assert(live()==0);assert(gOamMatrixAllocBitmap==((1u<<slots)-1));return;}}
 fprintf(stderr,"timeout kind=%d mode=%d actor=%d target=%d config=%d\n",kind,mode,actor,target,config);abort();}
int main(void){for(int k=0;k<4;k++)for(mode=0;mode<2;mode++)for(int a=0;a<(mode?4:2);a++)for(int t=0;t<(mode?4:2);t++)for(int c=0;c<counts[k];c++)runCase(k,a,t,c);}
'''
def main():
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--reference',type=Path);p.add_argument('--output-dir',type=Path,required=True);a=p.parse_args();a.output_dir.mkdir(parents=True,exist_ok=True)
 target=a.output_dir/'orb_defense_vectors.json'
 if target.exists():p.error('Refusing to overwrite existing candidate')
 h,result=build(a.root,a.reference or a.root/'content/raw/pokefirered/037335f4');c=a.output_dir/'orb_defense_source.c';exe=a.output_dir/'orb_defense_source';c.write_text(h);subprocess.run(['cc','-std=c11','-O0','-fwrapv',str(c),'-o',str(exe)],check=True)
 output=subprocess.check_output([str(exe)],text=True);(a.output_dir/'orb_defense_source.rows').write_text(output);cases={}
 for line in output.splitlines():
  key,typ,tick,*values=line.split(',');tick=int(tick);v=list(map(int,values));row=cases.setdefault(key,{}).setdefault(tick,dict(sprites=[],afterTaskSprites=[]))
  if typ=='T':assert len(v)+1==len(STATE_COLUMNS);row['state']=[tick]+v
  else:assert len(v)==len(SPRITE_COLUMNS);row['sprites' if typ=='P' else 'afterTaskSprites'].append(v)
 result['cases']={k:list(v.values()) for k,v in cases.items()};assert len(cases)==20*sum(map(len,CONFIGS))
 for key,rows in result['cases'].items():assert rows[0]['state'][0]==-1 and rows[-1]['state'][1:4]==[0,0,0],key
 result['coverage']=dict(caseCount=len(cases),rowCount=sum(map(len,result['cases'].values())))
 # Targets are deliberately varied in the native harness. They do not affect
 # these actor-owned callbacks, so share identical numeric trajectories.
 unique={};trajectories=[];mapping={}
 for key,rows in result.pop('cases').items():
  compact=[[row['state'],row['sprites'],row['afterTaskSprites']] for row in rows];signature=json.dumps(compact,separators=(',',':'))
  if signature not in unique:unique[signature]=len(trajectories);trajectories.append(compact)
  mapping[key]=unique[signature]
 result['caseTrajectories']=mapping;result['trajectories']=trajectories
 target.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
if __name__=='__main__':main()
