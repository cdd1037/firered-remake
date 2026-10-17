"""Extract original sprite palette allocation and music recoloring functions."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def function(source,name):
 m=re.search(r'^(?:static )?(?:void|u8|u16|bool8) '+name+r'\([^;]+?\)\n\{',source,re.M)
 if not m:raise ValueError(name)
 i=m.end();depth=1
 while depth:depth+=(source[i]=='{')-(source[i]=='}');i+=1
 return source[m.start():i]

def build(reference,helper):
 spec=importlib.util.spec_from_file_location("reference_c",helper);ref=importlib.util.module_from_spec(spec);spec.loader.exec_module(ref);ref.verify_reference(reference)
 sprite=(reference/'src/sprite.c').read_text();effect=(reference/'src/battle_anim_effects_1.c').read_text()
 header=(reference/'include/constants/battle_anim.h').read_text()
 tags={name:10000+int(value) for name,value in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',header,re.M)}
 table=re.search(r'static const u16 sParticlesColorBlendTable\[\]\[6\] =\s*\{.*?\};',effect,re.S)[0]
 funcs=[function(sprite,n) for n in ['IndexOfSpritePaletteTag','AllocSpritePalette','FreeSpritePaletteByTag']]+[function(effect,n) for n in ['AnimTask_MusicNotesRainbowBlend','AnimTask_MusicNotesClearRainbowBlend']]
 c='''#include <stdint.h>\n#include <stdio.h>\ntypedef uint8_t u8;typedef uint16_t u16;
#define TAG_NONE 65535
#define NELEMS(a) (sizeof(a)/sizeof((a)[0]))
#define RGB(r,g,b) ((r)|((g)<<5)|((b)<<10))
#define RGB_WHITE RGB(31,31,31)
#define OBJ_PLTT_ID(i) (256+(i)*16)
static u16 sSpritePaletteTags[16],gPlttBufferFaded[512];static u8 gReservedSpritePaletteCount=4;
static void DestroyAnimVisualTask(u8 taskId){}
'''
 c+='\n'.join('#define '+name+' '+str(tags[name]) for name in re.findall(r'ANIM_TAG_\w+',table))+'\n'+table+'\n'+'\n'.join(funcs)
 c+='''
static void emit(int scenario,int step,int result){printf("%d,%d,%d",scenario,step,result);for(int i=0;i<16;i++)printf(",%u",sSpritePaletteTags[i]);for(int i=256;i<512;i++)printf(",%u",gPlttBufferFaded[i]);puts("");}
int main(){for(int test=0;test<5;test++){for(int i=0;i<16;i++)sSpritePaletteTags[i]=TAG_NONE;for(int i=0;i<512;i++)gPlttBufferFaded[i]=(i*37)&32767;
// Base palette plus optional pre-existing alias or full allocation pool.
sSpritePaletteTags[4]=ANIM_TAG_MUSIC_NOTES;
if(test==1)sSpritePaletteTags[5]=ANIM_TAG_BENT_SPOON;
if(test==2)for(int i=5;i<16;i++)sSpritePaletteTags[i]=30000+i;
if(test==3)for(int i=5;i<14;i++)sSpritePaletteTags[i]=30000+i;
if(test==4)sSpritePaletteTags[4]=TAG_NONE;
emit(test,0,-1);AnimTask_MusicNotesRainbowBlend(0);emit(test,1,IndexOfSpritePaletteTag(ANIM_TAG_BENT_SPOON));AnimTask_MusicNotesClearRainbowBlend(0);emit(test,2,IndexOfSpritePaletteTag(ANIM_TAG_BENT_SPOON));int result=AllocSpritePalette(12345);emit(test,3,result);
}return 0;}
'''
 with tempfile.TemporaryDirectory() as directory:
  root=Path(directory);(root/'oracle.c').write_text(c);ref.compile_c(['cc','-std=c99','-O0',str(root/'oracle.c'),'-o',str(root/'oracle')])
  rows=[[int(v) for v in line.split(',')] for line in subprocess.check_output([str(root/'oracle')],text=True).splitlines()]
 return dict(columns=['scenario','step','lookupOrAllocationResult','tags[16]','fadedObjColors[256]'],rows=rows)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--helper',type=Path,required=True);a=p.parse_args();data=build(a.reference,a.helper);a.output.write_text(json.dumps(data,separators=(',',':'))+'\n');print(len(data['rows']))
