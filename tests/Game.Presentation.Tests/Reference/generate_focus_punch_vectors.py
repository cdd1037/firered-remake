#!/usr/bin/env python3
"""Pinned Focus Punch callback with the existing extracted native sprite engine.

The shared Fang oracle supplies the native frame/affine interpreters. This adds
only Focus Punch's original callback, frame and affine declarations, and Sin.
Creation tick -1 is callback then AnimateSprite; normal ticks retain that order.
The no-rotation matrix is the source reciprocal scale, with BIOS identity already
covered by the shared affine geometry authority. No product assemblies are run.
"""
import argparse,hashlib,importlib.util,json,re,subprocess,tempfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def module(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def build(reference,helper,harness):
 ref=module('focus_reference',helper);ref.verify_reference(reference);ex=module('focus_sprite_engine',harness)
 fight=(reference/'src/battle_anim_fight.c').read_text();trig=(reference/'src/trig.c').read_text()
 callback=ex.function(fight,'AnimFocusPunchFist');sin=ex.function(trig,'Sin')
 table=re.search(r'const s16 gSineTable\[\] =\s*\{.*?\};',trig,re.S)[0]
 extra='#define Q_8_8(n) ((s16)((n)*256))\n'+table+'\n'
 h=ex.sprite_harness(reference,[sin,callback],extra)
 h+=ex.declaration(fight,'sAnim_Fist')+'\n'+ex.declaration(fight,'sAffineAnim_FocusPunchFist')+r'''
static const union AnimCmd *const frames[]={sAnim_Fist};
static const union AffineAnimCmd *const affine[]={sAffineAnim_FocusPunchFist};
int main(void){
 struct Sprite*s=&gSprites[0];memset(s,0,sizeof(*s));AffineAnimStateReset(0);
 s->inUse=TRUE;s->animBeginning=TRUE;s->usingSheet=TRUE;s->anims=frames;s->callback=AnimFocusPunchFist;
 s->oam.affineMode=3;s->affineAnimBeginning=TRUE;s->affineAnims=affine;s->x=137;s->y=73;
 for(int tick=-1;tick<100;tick++){
  writes=0;AnimateSprites();struct AffineAnimState*a=&sAffineAnimStates[0];
  printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",tick,s->inUse,s->x,s->y,s->x2,s->y2,s->oam.tileNum,s->animEnded,a->xScale,a->yScale,a->rotation,s->affineAnimEnded,s->data[0],s->data[1],writes,65536/a->xScale,65536/a->yScale,a->delayCounter);
  if(!s->inUse)return 0;
 }
 abort();
}
'''
 with tempfile.TemporaryDirectory() as tmp:
  c=Path(tmp)/'focus.c';exe=Path(tmp)/'focus';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);rows=[list(map(int,line.split(','))) for line in subprocess.check_output([str(exe)],text=True).splitlines()]
 assert [r[0] for r in rows]==list(range(-1,50))
 assert rows[8][11]==0 and rows[9][11]==1 and rows[9][12]==0 and rows[10][12]==1
 assert rows[-1][1]==0 and rows[-1][12]==41 and rows[-1][14]==0
 for r in rows:
  assert r[2:4]==[137,73] and r[5:7]==[0,0] and r[10]==0
  assert r[8:10]==[max(256,512-32*(r[0]+1))]*2
 body=lambda source,text:dict(line=source[:source.index(text)].count('\n')+1,sha256=hashlib.sha256(text.encode()).hexdigest())
 return dict(schemaVersion=1,sources={'callback':body(fight,callback),'sin':body(trig,sin)},columns=['tick','active','x','y','x2','y2','tile','frameEnded','xScale','yScale','rotation','affineEnded','shakeCount','angle','matrixWrites','matrixA','matrixD','affineDelay'],rows=rows)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--helper',type=Path,default=ROOT/'tests/Reference/reference_c.py');p.add_argument('--harness',type=Path,default=Path(__file__).with_name('generate_fang_vectors.py'));p.add_argument('--output',type=Path,required=True);a=p.parse_args()
 if a.output.exists():p.error('Refusing to overwrite an existing source vector candidate')
 data=build(a.reference,a.helper,a.harness);a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print('Focus Punch native callback/affine rows:',len(data['rows']))
