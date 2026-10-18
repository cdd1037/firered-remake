#!/usr/bin/env python3
"""Pinned whole-life frozen-cube task, raw sprite engine and source palette writes.
All cases retain every frame, both live and pre-task OAM lifetimes, and all colors.
"""
import argparse,hashlib,importlib.util,json,re,tempfile
from pathlib import Path
PINS={'src/data/battle_anim.h': 'ec8dc3de772056de958bba1227e7172b500a0380','include/constants/battle_anim.h': '4fc4e4407fb91c086c0b9a0b52188a5e20ce4d6e','src/battle_anim_status_effects.c': '6d3a0e1b37e8f36ae123f3547f29f9be55a623b3', 'src/sprite.c': 'd0198d53004775c8664dcccf57833e178833776e', 'include/sprite.h': '6a1b272119ce6e3a8dcf45adf2eef8923a6d5175', 'include/gba/types.h': '35d02e26391d872b486ca051d51f8485f3a7a0e5', 'include/gba/io_reg.h': 'a8a36f7e295855dbc4281f5f9a9f223ddda34a16'}
CONFIGS=[[4,-1],[15,-1],[4,1]] # palette slot, free raw sprite cells

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def source(root):
 raw=root/'content/raw/pokefirered/037335f4'
 for path,blob in PINS.items():
  data=(raw/path).read_bytes()
  if hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()!=blob:raise ValueError('Pinned source differs: '+path)
 base=module('frozen_base',root/'tests/Game.Presentation.Tests/Reference/generate_orbit_spray_vectors.py');x=module('frozen_extract',root/'tests/Reference/Recovery/source_extract.py')
 with tempfile.TemporaryDirectory() as d:
  p=Path(d)/'source.c';base.build(root,'tasks',p);h=p.read_text().split('static const long long config0')[0]
 h=h.replace('u8 affineMode,matrixNum,shape,size,paletteNum,objMode;', 'u8 affineMode,matrixNum,shape,size,paletteNum,objMode;u8 priority:2;u8 bpp;u16 x,y;')
 h=h.replace('struct Sprite;struct SpriteTemplate;', 'struct Sprite;struct SpriteTemplate;struct SubspriteTable;')
 h=h.replace('int sheetTileStart;s16', 'const struct SubspriteTable*subspriteTables;u8 subspriteTableNum,subspriteMode;int sheetTileStart;s16')
 h=h.replace('static int paletteSlots[2];','static int cubePaletteSlot;static int paletteSlots[2];').replace('tag==ANIM_TAG_RAZOR_LEAF?paletteSlots[1]:0;', 'tag==ANIM_TAG_RAZOR_LEAF?paletteSlots[1]:cubePaletteSlot;')
 src=(raw/'src/battle_anim_status_effects.c').read_text();sprite=(raw/'src/sprite.c').read_text();types=(raw/'include/gba/types.h').read_text();header=(raw/'include/sprite.h').read_text()
 h+='\n#include "gba/io_reg.h"\n#define NELEMS(a) (sizeof(a)/sizeof((a)[0]))\n#define gDummySpriteAnimTable dummyFrames\n#define gDummySpriteAffineAnimTable dummyAffines\n#define SUBSPRITES_ON 1\n'
 h+='\n'.join(line for line in types.splitlines() if line.startswith('#define SPRITE_') or line.startswith(('#define ST_OAM_SIZE_', '#define ST_OAM_SQUARE', '#define ST_OAM_H_RECTANGLE', '#define ST_OAM_V_RECTANGLE', '#define ST_OAM_OBJ_BLEND', '#define ST_OAM_4BPP')))+'\n'
 h+='\n'.join(line for line in (raw/'include/constants/battle_anim.h').read_text().splitlines() if line.startswith(('#define ANIM_SPRITES_START ', '#define ANIM_TAG_ICE_CUBE ')))+'\n'
 h+=re.search(r'struct Subsprite\s*\{.*?\};',header,re.S)[0]+re.search(r'struct SubspriteTable\s*\{.*?\};',header,re.S)[0]+'\n'
 h+='static u16 gpu[128];static void SetGpuReg(int offset,u16 value){gpu[offset/2]=value;}\n'
 h+=re.search(r'const struct OamData gOamData_AffineOff_ObjBlend_64x64\s*=\s*\{.*?\};',(raw/'src/data/battle_anim.h').read_text(),re.S)[0]+'\n'
 h+=x.function(sprite,'SetSubspriteTables')+'\n'
 for name in ['sFrozenIceCubeSubsprites','sFrozenIceCubeSubspriteTable','sFrozenIceCubeSpriteTemplate']:
  h+=re.search(r'static const struct \w+ '+name+r'(?:\[\])?\s*=\s*\{.*?\};',src,re.S)[0]+'\n'
 names=['AnimTask_FrozenIceCube','AnimTask_FrozenIceCube_Step1','AnimTask_FrozenIceCube_Step2','AnimTask_FrozenIceCube_Step3','AnimTask_FrozenIceCube_Step4'];fs=[x.function(src,n) for n in names];h+='\n'.join(f.split('{',1)[0]+';' for f in fs)+'\n'+'\n'.join(fs)
 h+='\nstatic const int configs[][2]={'+','.join('{'+','.join(map(str,c))+'}' for c in CONFIGS)+'};\n'
 h+=r'''
static struct Sprite saved[65];static int config;
static int owned(struct Sprite*s){return s->inUse&&s->template==&sFrozenIceCubeSpriteTemplate;}
static void emit(int tick){
 int live=0,matrices=0;for(int i=0;i<64;i++)live+=owned(&gSprites[i]);for(int i=0;i<32;i++)matrices+=(gOamMatrixAllocBitmap>>i)&1;
 printf("%d:%d:%d:%d,%d,%d,%d,%d,%d,%d",mode,gBattleAnimAttacker,gBattleAnimTarget,config,tick,gTasks[0].isActive,live,gpu[REG_OFFSET_BLDCNT/2],gpu[REG_OFFSET_BLDALPHA/2],matrices);
 for(int i=0;i<5;i++)printf(",%d",gTasks[0].data[i]);putchar('|');int first=1;
 for(int i=0;i<64;i++){struct Sprite*s=&saved[i];if(!owned(s))continue;printf("%s%d,%d,%d,%d,%d,%d,%d,%d,%d,%d",first?"":";",i,s->x,s->y,s->x2,s->y2,!s->invisible,s->oam.tileNum,s->oam.priority,s->subpriority,s->oam.objMode);first=0;}putchar('|');
 for(int i=0;i<16;i++)printf("%s%u,%u",i?",":"",gPlttBufferUnfaded[OBJ_PLTT_ID(cubePaletteSlot)+i],gPlttBufferFaded[OBJ_PLTT_ID(cubePaletteSlot)+i]);putchar('\n');
}
int main(void){for(mode=0;mode<2;mode++)for(gBattleAnimAttacker=0;gBattleAnimAttacker<(mode?4:2);gBattleAnimAttacker++)for(gBattleAnimTarget=0;gBattleAnimTarget<(mode?4:2);gBattleAnimTarget++)for(config=0;config<3;config++){
 slots=mode?4:2;cubePaletteSlot=configs[config][0];memset(gSprites,0,sizeof gSprites);memset(gTasks,0,sizeof gTasks);memset(gpu,0,sizeof gpu);memset(sAffineAnimStates,0,sizeof sAffineAnimStates);gOamMatrixAllocBitmap=(1u<<slots)-1;gAnimVisualTaskCount=1;
 for(int i=0;i<(configs[config][1]<0?slots:64-configs[config][1]);i++){struct Sprite*s=&gSprites[i];s->inUse=TRUE;s->callback=SpriteCallbackDummy;s->anims=dummyFrames;s->affineAnims=dummyAffines;s->animPaused=s->affineAnimPaused=TRUE;}
 for(int i=0;i<512;i++)gPlttBufferUnfaded[i]=gPlttBufferFaded[i]=(i*997)&32767;
 gTasks[0].isActive=TRUE;AnimTask_FrozenIceCube(0);memcpy(saved,gSprites,sizeof saved);emit(-1);int tick;
 for(tick=0;tick<200&&gTasks[0].isActive;tick++){AnimateSprites();memcpy(saved,gSprites,sizeof saved);gTasks[0].func(0);emit(tick);}assert(tick<200&&!gTasks[0].isActive&&gAnimVisualTaskCount==0);
 }return 0;}
'''
 return h

def build(root,out,full=False):
 out.mkdir(parents=True,exist_ok=True);compiler=module('frozen_compile',root/'tests/Game.Presentation.Tests/Reference/generate_cure_vectors.py');text=compiler.compile_rows(root,source(root),'frozen-cube-native',out);cases={}
 for line in text.splitlines():
  state,sprites,palette=line.split('|');key,*state=state.split(',');cases.setdefault(key,[]).append(dict(state=list(map(int,state)),sprites=[list(map(int,row.split(','))) for row in sprites.split(';') if row],palette=list(map(int,palette.split(',')))))
 assert len(cases)==60 and all(v[-1]['state'][1:3]==[0,0] for v in cases.values())
 if not full:
  keys=[f'0:0:1:{i}' for i in range(3)]+['0:1:0:0']+[f'1:{a}:{t}:0' for a in range(4) for t in range(4)]+['1:2:3:1','1:3:2:2'];cases={k:cases[k] for k in keys}
 return dict(configs=CONFIGS,cases=cases,coverage=dict(caseCount=len(cases),fullCaseCount=60,frames=sum(map(len,cases.values()))),sourceBlobs=PINS)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output-dir',type=Path,required=True);p.add_argument('--full',action='store_true');a=p.parse_args();dest=a.output_dir/'frozen-cube-source-vectors.json'
 if dest.exists():p.error('Refusing to overwrite source vectors')
 result=build(a.root,a.output_dir,a.full);dest.write_text(json.dumps(result,separators=(',',':'))+'\n');print(result['coverage'])
