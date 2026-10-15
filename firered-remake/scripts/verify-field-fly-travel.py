#!/usr/bin/env python3
"""Compare production presentation numerics with four unmodified, pinned C callbacks.
The small authored C harness stubs affine allocation only. It does not emulate rendering/audio.
"""
import argparse,json,pathlib,re,subprocess,tempfile
p=argparse.ArgumentParser();p.add_argument('--reference-root',type=pathlib.Path,required=True);p.add_argument('--timeline',type=pathlib.Path,required=True);a=p.parse_args()
r=pathlib.Path(__file__).resolve().parents[1];lock=json.loads((r/'scripts/field-fly-travel.lock.json').read_text());commit=lock['sourceCommit']
def source(path):
 b=subprocess.check_output(['git','show',commit+':'+path],cwd=a.reference_root);f=next(f for f in lock['files'] if f['path']==path)
 return b.decode()
s=source('src/field_effect.c');trig=source('src/trig.c');data=json.loads(a.timeline.read_text())
values=[int(float(v)*256) for v in re.findall(r'Q_8_8\(([-0-9.]+)\)',trig.split('const s16 gSineTable[] =')[1].split('};')[0])]
for v in data['sine']:
 assert v['sin72']==values[v['angle']]*72>>8
 assert v['cos180']==values[v['angle']+64]*180>>8
names=['SpriteCB_FlyBirdLeaveBall','SpriteCB_FlyBirdSwoopDown','SpriteCB_FlyBirdWithPlayer','SpriteCB_FlyBirdReturnToBall']
bodies=[]
for n in names:
 start=s.index('static void '+n+'(struct Sprite *sprite)\n{');end=s.index('\n}',start)+2;bodies.append(s[start:end])
# Assert original task gates independently of the C# test output.
for literal in ['task->tTimer = 16;','(++task->tTimer) >= 8','(++task->tTimer) >= 10','task->tTimer = 33;','task->data[1] = 16;','s16 yOffsets[18] = {-2, -4, -5, -6, -7, -8, -8, -8, -7, -7, -6, -5, -3, -2, 0, 2, 4, 8};']:
 assert literal in s,literal
harness=r'''
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#define FALSE 0
#define TRUE 1
#define MALE 0
#define MAX_SPRITES 64
#define ST_OAM_AFFINE_DOUBLE 3
#define ST_OAM_AFFINE_OFF 0
#define sAnimCompleted data[7]
#define sInitData data[0]
#define sPlayerSpriteId data[6]
struct Sprite { struct {int affineMode,matrixNum,shape,size;} oam; void *affineAnims; int x,y,x2,y2,coordOffsetEnabled,invisible; int16_t data[8]; };
struct Sprite gSprites[65];
struct Save {int playerGender;} save,*gSaveBlock2Ptr=&save;
void *sAffineAnims_FlyBirdBall;
void InitSpriteAffineAnim(struct Sprite*s){}
void StartSpriteAffineAnim(struct Sprite*s,int a){}
void FreeOamMatrix(int n){}
void CalcCenterToCornerVec(struct Sprite*s,int a,int b,int c){}
'''
harness+='static const int sine[] = {'+','.join(map(str,values))+'};\nint Sin(int a,int b){return sine[a]*b>>8;}\nint Cos(int a,int b){return sine[a+64]*b>>8;}\n'+'\n'.join(bodies)+r'''
int main(void){int gender,op;struct Sprite s={0};while(scanf("%d %d",&gender,&op)==2){save.playerGender=gender;
if(op==0){memset(&s,0,sizeof(s));s.x=255;s.y=180;s.data[6]=MAX_SPRITES;}
if(op==1){memset(&s,0,sizeof(s));s.x=120;s.data[6]=MAX_SPRITES;}
if(op==2){s.data[6]=0;}
if(op==3){memset(&s,0,sizeof(s));s.x=120;s.data[6]=MAX_SPRITES;}
if(op>=10){switch(op){case 10:SpriteCB_FlyBirdLeaveBall(&s);break;case 11:SpriteCB_FlyBirdSwoopDown(&s);break;case 12:SpriteCB_FlyBirdWithPlayer(&s);break;case 13:SpriteCB_FlyBirdReturnToBall(&s);break;}
printf("%d %d %d\n",s.x+s.x2,s.y+s.y2,s.data[7]);}}
}
'''
checks=0;summary=[]
with tempfile.TemporaryDirectory(prefix='firered-fly-oracle-') as temp:
 temp=pathlib.Path(temp);(temp/'oracle.c').write_text(harness);subprocess.run(['cc','-std=c99','-O2',str(temp/'oracle.c'),'-o',str(temp/'oracle')],check=True)
 for f in data['flights']:
  gender=int(f['female']);commands=[];expected=[];previous=None
  stages={q['stage']:q['tick'] for q in f['changes']}
  if f['outgoing']:
   assert stages['swoop']-stages['face-left']==16
   assert stages['jump-on']-stages['swoop']==8
   assert stages['fly-off']-stages['jump-on']==10
   assert stages['done']-stages['fade-out']==16
  else:
   assert stages['jump-off']-stages['ride-in']==33
   assert stages['wait-bird']-stages['jump-off']==18
   assert stages['done']-stages['hold']==16
  for v in f['samples']:
   stage=v['stage']
   if stage!=previous:
    if stage=='bird-leave':commands.append((gender,0))
    if stage in ('swoop','ride-in'):commands.append((gender,1))
    if stage in ('fly-off','ride-in'):commands.append((gender,2))
    if stage=='bird-return':commands.append((gender,3))
   previous=stage
   callback=10 if stage in ('bird-leave','face-left') else 11 if stage in ('swoop','jump-on','jump-off','wait-bird','return-pose') else 12 if stage in ('fly-off','fade-out','ride-in') else 13
   commands.append((gender,callback));expected.append((v['x'],v['y']))
  output=subprocess.check_output([str(temp/'oracle')],input=''.join(f'{g} {op}\n' for g,op in commands),text=True)
  rows=[tuple(map(int,l.split())) for l in output.splitlines()]
  assert len(rows)==len(expected)
  for i,(row,xy) in enumerate(zip(rows,expected)):
   assert row[:2]==xy,(gender,f['outgoing'],i,row,xy,f['samples'][i]);checks+=1
  summary.append({'female':f['female'],'outgoing':f['outgoing'],'callbackPositions':len(rows),'stages':f['changes']})
result={'passed':True,'sourceCommit':commit,'sourceCallbackBodies':4,'sineComparisons':512,'callbackPositionComparisons':checks,'flights':summary,'scope':'CPU numeric callback/task gates only; no native-screen, GBA affine-pixel or physical-audio acceptance'}
print(json.dumps(result,indent=2))
