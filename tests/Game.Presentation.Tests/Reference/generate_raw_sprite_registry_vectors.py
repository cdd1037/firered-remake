"""Extract source registry traversal/allocation; authored callbacks log scheduling only."""
from pathlib import Path
import argparse,importlib.util,subprocess,tempfile,json
ROOT=Path(__file__).resolve().parents[3]
p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--helper',type=Path,required=True);p.add_argument('--extractor',type=Path,required=True);p.add_argument('--output',type=Path,required=True);args=p.parse_args();RAW=args.reference
def load(name,path):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
extract=load('extract',args.extractor)
ref=load('reference_c',args.helper);ref.verify_reference(RAW)
source=(RAW/'src/sprite.c').read_text()
funcs=[extract.function(source,n) for n in ['AnimateSprites','CreateSprite','CreateSpriteAtEnd','CreateSpriteAndAnimate','DestroySprite','ResetSprite']]
c=r'''
#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef int16_t s16;
#define MAX_SPRITES 64
#define TILE_SIZE_4BPP 32
#define FREE_SPRITE_TILE(i) ((void)(i))
struct Sprite;typedef void(*Callback)(struct Sprite*);
struct Sprite{int inUse,usingSheet,kind;Callback callback;struct{int tileNum;}oam;struct{int size;}*images;};
struct SpriteTemplate{Callback callback;int kind;};
static struct Sprite gSprites[MAX_SPRITES+1],sDummySprite;
static int scenario,tick,once;
static void logevent(char op,int slot){printf("%d,%d,%c,%d\n",scenario,tick,op,slot);}
static void AnimateSprite(struct Sprite*s){if(s->kind)logevent('F',(int)(s-gSprites));}
static void ResetSprite(struct Sprite*);
static u8 CreateSpriteAt(u8 i,const struct SpriteTemplate*t,s16 x,s16 y,u8 priority){(void)x;(void)y;(void)priority;ResetSprite(&gSprites[i]);gSprites[i].inUse=1;gSprites[i].usingSheet=1;gSprites[i].callback=t->callback;gSprites[i].kind=t->kind;logevent('A',i);return i;}
'''
c+='\n'.join(f.split('\n{',1)[0]+';' for f in funcs)+'\n'+'\n'.join(funcs)+'\n'
c+=r'''
static void dummy(struct Sprite*s){(void)s;}
static void child(struct Sprite*s){logevent('C',(int)(s-gSprites));}
static void oldchild(struct Sprite*s){logevent('O',(int)(s-gSprites));}
static const struct SpriteTemplate Child={child,1};
static void removeAt(int i){logevent('D',i);DestroySprite(&gSprites[i]);}
static void parent(struct Sprite*s){
 int i=(int)(s-gSprites);logevent('P',i);if(once++)return;
 if(scenario==4)removeAt(2);
 if(scenario==5)removeAt(i);
 if(scenario==6){removeAt(2);return;}
 u8 id=scenario==2||scenario==3?CreateSpriteAndAnimate(&Child,0,0,0):scenario==8?CreateSpriteAtEnd(&Child,0,0,0):CreateSprite(&Child,0,0,0);
 if(id==MAX_SPRITES)logevent('X',id);
}
static void place(int i,Callback callback,int kind){gSprites[i].inUse=1;gSprites[i].usingSheet=1;gSprites[i].callback=callback;gSprites[i].kind=kind;}
int main(void){
 for(scenario=0;scenario<10;scenario++){
  memset(gSprites,0,sizeof(gSprites));once=0;
  if(scenario==1||scenario==3){place(1,dummy,0);place(2,parent,1);}
  else if(scenario==5){place(0,dummy,0);place(1,dummy,0);place(2,parent,1);}
  else if(scenario==7){for(int i=0;i<64;i++)place(i,dummy,0);place(1,parent,1);}
  else if(scenario!=9){place(0,dummy,0);place(1,parent,1);if(scenario==4||scenario==6)place(2,oldchild,1);}
  for(tick=0;tick<2;tick++){AnimateSprites();if(scenario==9&&tick==0)CreateSprite(&Child,0,0,0);}
 }
}
'''
with tempfile.TemporaryDirectory() as tmp:
 p=Path(tmp)/'registry.c';exe=Path(tmp)/'registry';p.write_text(c);ref.compile_c(['cc','-std=gnu11','-O0',str(p),'-o',str(exe)]);lines=subprocess.check_output([str(exe)],text=True).splitlines()
rows={}
for line in lines:
 n,t,op,slot=line.split(',');rows.setdefault(n,[]).append([int(t),op,int(slot)])
expected={
 '0':[['P',1],['A',2],['F',1],['C',2],['F',2]],
 '1':[['P',2],['A',0],['F',2]],
 '2':[['P',1],['A',2],['C',2],['F',2],['F',1],['C',2],['F',2]],
 '3':[['P',2],['A',0],['C',0],['F',0],['F',2]],
 '4':[['P',1],['D',2],['A',2],['F',1],['C',2],['F',2]],
 '5':[['P',2],['D',2],['A',2],['F',2]],
 '6':[['P',1],['D',2],['F',1]],
 '7':[['P',1],['X',64],['F',1]],
 '8':[['P',1],['A',63],['F',1],['C',63],['F',63]],
 '9':[['A',0]]}
for n,want in expected.items():assert [r[1:] for r in rows[n] if r[0]==0]==want,(n,rows[n])
assert rows['1'][3:5]==[[1,'C',0],[1,'F',0]]
assert rows['5'][4:]==[[1,'C',2],[1,'F',2]]
assert rows['9'][1:]==[[1,'C',0],[1,'F',0]]
out=args.output;out.write_text(json.dumps(rows,separators=(',',':'))+'\n');print('10 source registry scenarios;',sum(map(len,rows.values())),'events;',out.stat().st_size,'bytes')
