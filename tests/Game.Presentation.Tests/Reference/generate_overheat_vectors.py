#!/usr/bin/env python3
"""Explicit source-C candidates for palette-buffer ownership and Overheat callbacks.

The bounded harness extracts pinned C; it never executes upstream scripts or the
managed renderer. Ordinary tests consume the reviewed JSON rather than regenerate it.
"""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def module(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m

def build(root):
 raw=root/'content/raw/pokefirered/037335f4';ref=module('ref',root/'tests/Reference/reference_c.py');ref.verify_reference(raw)
 extract=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py');fn=extract.function
 def run(c,name):
  with tempfile.TemporaryDirectory() as temp:
   source=Path(temp)/(name+'.c');exe=Path(temp)/name;source.write_text(c);ref.compile_c(['cc','-std=gnu11','-O0','-fno-strict-aliasing',str(source),'-o',str(exe)]);return [list(map(int,l.split(','))) for l in subprocess.check_output([str(exe)],text=True).splitlines()]
 common='''#include <stdint.h>\n#include <stdio.h>\n#include <stdlib.h>\n#include <string.h>\ntypedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int16_t s16;typedef int32_t s32;typedef u8 bool8;\n#define TRUE 1\n#define FALSE 0\n'''
 utility=(raw/'src/battle_anim_utility_funcs.c').read_text();blend=(raw/'src/blend_palette.c').read_text();gba=(raw/'include/gba/types.h').read_text()
 assert re.search(r'u16 \*multiUseBuffer;', (raw/'include/battle.h').read_text())
 palette=common+re.search(r'struct PlttData\s*\{.*?\};',gba,re.S)[0]+'''
#define PLTT_ID(i) ((i)*16)
#define PLTT_SIZE_4BPP 32
#define FREE_AND_SET_NULL(p) do{free(p);p=NULL;}while(0)
static s16 gBattleAnimArgs[8];static u8 gBattleAnimAttacker,gBattleAnimTarget;
static u16 gPlttBufferUnfaded[512],gPlttBufferFaded[512];
static struct {u16 *multiUseBuffer;} gfx,*gMonSpritesGfxPtr=&gfx;
static void *AllocZeroed(int n){return calloc(1,n);}
static int destroys;static void DestroyAnimVisualTask(u8 id){destroys++;}
static u32 GetBattlePalettesMask(bool8 bg,bool8 a,bool8 t,bool8 ap,bool8 tp,bool8 x,bool8 y){if(!bg||a||t||ap||tp||x||y)abort();return 0xe;}
'''
 names=['AnimTask_AllocBackupPalBuffer','AnimTask_FreeBackupPalBuffer','AnimTask_CopyPalUnfadedToBackup','AnimTask_CopyPalUnfadedFromBackup','AnimTask_CopyPalFadedToUnfaded']
 palette+='\n'.join(fn(utility,n) for n in names)+'\n'+fn(blend,'BlendPalette')+r'''
static void emit(int actor,int sel,int pass,int phase,int bank){printf("%d,%d,%d,%d,%d,%d,%d",actor,sel,pass,phase,bank,destroys,gfx.multiUseBuffer!=NULL);for(int i=0;i<16;i++)printf(",%u",gPlttBufferUnfaded[bank*16+i]);for(int i=0;i<16;i++)printf(",%u",gPlttBufferFaded[bank*16+i]);puts("");}
int main(void){for(int actor=0;actor<4;actor++)for(int sel=0;sel<3;sel++)for(int pass=0;pass<2;pass++){
 gBattleAnimAttacker=actor;gBattleAnimTarget=actor^1;int bank=sel==0?1:sel==1?16+actor:16+(actor^1);destroys=0;
 for(int i=0;i<512;i++){int n=(i+pass*16)&31;gPlttBufferUnfaded[i]=n|((31-n)<<5)|(((n*7)&31)<<10);gPlttBufferFaded[i]=32767-gPlttBufferUnfaded[i];}
 gBattleAnimArgs[0]=sel;gBattleAnimArgs[1]=pass?255:0;emit(actor,sel,pass,0,bank);
 AnimTask_AllocBackupPalBuffer(0);emit(actor,sel,pass,1,bank);
 AnimTask_CopyPalUnfadedToBackup(0);emit(actor,sel,pass,2,bank);
 BlendPalette(bank*16,16,13,28);emit(actor,sel,pass,3,bank);
 AnimTask_CopyPalFadedToUnfaded(0);emit(actor,sel,pass,4,bank);
 BlendPalette(bank*16,16,13,18|(18<<5)|(18<<10));emit(actor,sel,pass,5,bank);
 AnimTask_CopyPalUnfadedFromBackup(0);emit(actor,sel,pass,6,bank);
 BlendPalette(bank*16,16,0,18|(18<<5)|(18<<10));emit(actor,sel,pass,7,bank);
 AnimTask_FreeBackupPalBuffer(0);emit(actor,sel,pass,8,bank);
}return 0;}
'''
 result={'palette':run(palette,'palette-backup')}
 exhaustive=palette[:palette.index('int main(void)')]+r'''int main(void){for(int color=0;color<32768;color++){gPlttBufferUnfaded[0]=color;BlendPalette(0,1,13,28);int once=gPlttBufferFaded[0];gPlttBufferUnfaded[0]=once;BlendPalette(0,1,13,18|(18<<5)|(18<<10));printf("%d,%d,%u\n",color,once,gPlttBufferFaded[0]);}return 0;}'''
 result['exhaustive']=run(exhaustive,'palette-exhaustive')
 sprite=(raw/'src/sprite.c').read_text();dragon=(raw/'src/battle_anim_dragon.c').read_text();trig=(raw/'src/trig.c').read_text();header=(raw/'include/sprite.h').read_text()
 names=['BeginAnim','ContinueAnim','AnimCmd_frame','AnimCmd_end','AnimCmd_jump','AnimCmd_loop','BeginAnimLoop','ContinueAnimLoop','JumpToTopOfAnimLoop','DecrementAnimDelayCounter']
 funcs=[fn(sprite,n) for n in names]+[fn(dragon,n) for n in ['AnimOverheatFlame','AnimOverheatFlame_Step']]+[fn(trig,n) for n in ['Sin','Cos']]
 flame=common+'''
#define BATTLER_COORD_X_2 2
#define BATTLER_COORD_Y_PIC_OFFSET 3
#define ST_OAM_AFFINE_ON_MASK 1
#define Q_8_8(n) ((s16)((n)*256))
'''+header[header.index('struct AnimFrameCmd\n'):header.index('\nenum\n')]+r'''
struct Sprite {u8 animCmdIndex,animLoopCounter,animNum;u8 animDelayCounter:6;bool8 inUse,animEnded,animBeginning,animPaused,usingSheet,hFlip,vFlip;int sheetTileStart,images; s16 x,y,x2,y2,data[8];struct {u8 affineMode;int tileNum;} oam;const union AnimCmd *const *anims;void(*callback)(struct Sprite*);};
static s16 gBattleAnimArgs[8];static u8 gBattleAnimAttacker;static int doubles;static u16 sUnusedOverheatData[7];
static void SetSpriteOamFlipBits(struct Sprite*s,u8 h,u8 v){s->hFlip=h;s->vFlip=v;}
static void RequestSpriteFrameImageCopy(int a,int b,int c){abort();}
static void DestroyAnimSprite(struct Sprite*s){s->inUse=0;}
static u8 GetBattlerSpriteCoord(u8 id,int type){int coords[2][4][2]={{{72,80},{176,40},{48,40},{112,80}},{{32,80},{200,40},{90,88},{152,32}}};return coords[doubles][id][type&1]+(type&1?7:0);}
'''+re.search(r'const s16 gSineTable\[\]\s*=\s*\{.*?\};',trig,re.S)[0]+'\n'
 flame+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\nstatic void (*sAnimCmdFuncs[])(struct Sprite*)={AnimCmd_loop,AnimCmd_jump,AnimCmd_end,AnimCmd_frame};\n'+'\n'.join(funcs)+'\n'+extract.declaration(dragon,'sAnim_OutrageOverheatFire_0')+r'''
static const union AnimCmd *const frames[]={sAnim_OutrageOverheatFire_0};
static void animate(struct Sprite*s){if(s->animBeginning)BeginAnim(s);else ContinueAnim(s);}
int main(void){for(int d=0;d<2;d++)for(int actor=0;actor<(d?4:2);actor++)for(int variant=0;variant<5;variant++)for(int angle=0;angle<256;angle+=32)for(int layer=0;layer<(variant==0?3:1);layer++){
 doubles=d;gBattleAnimAttacker=actor;int offsets[3]={-20,0,10};gBattleAnimArgs[0]=variant==4?-7:1;gBattleAnimArgs[1]=angle;gBattleAnimArgs[2]=variant>=3?32767:variant==2?-31:30;gBattleAnimArgs[3]=variant==1?0:variant>=3?60:25;gBattleAnimArgs[4]=offsets[layer];struct Sprite s={0};s.inUse=s.usingSheet=s.animBeginning=1;s.anims=frames;AnimOverheatFlame(&s);animate(&s);
 for(int tick=-1;tick<70;tick++){if(tick>=0){s.callback(&s);if(s.inUse)animate(&s);}printf("%d,%d,%d,%d,%d,%d",d,actor,variant,angle,layer,tick);for(int i=0;i<5;i++)printf(",%d",gBattleAnimArgs[i]);printf(",%d,%d,%d,%d,%d,%d,%d",s.x,s.y,s.x2,s.y2,s.inUse,s.oam.tileNum,s.animDelayCounter);for(int i=0;i<8;i++)printf(",%d",s.data[i]);puts("");if(!s.inUse)break;}
}return 0;}
'''
 result['flame']=run(flame,'overheat-flame');return result
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():raise SystemExit('Refusing to overwrite reviewed vectors; use a new candidate path.')
 data=build(a.root);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print({k:len(v) for k,v in data.items()})
