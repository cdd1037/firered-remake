"""Extract the pinned cave EXIT tasks into a bounded local C palette oracle.

Only the five original task bodies and their task creator execute. The authored
host stubs GPU/decompression/main callbacks, records every palette write and
applies documented RGB555 alpha arithmetic to all sixteen pixel indices. The
pixel values are a compositing oracle, not evidence of native rendered output.
No upstream script runs. Ordinary managed tests only read the ignored output.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import tempfile

from reference_c import COMMIT, compile_c

SOURCE_HASH = 'e9d52450071d1a7b54c1774c98b1317910fbdbc6916e4839c42d9ae5dc8307a9'
ART_HASHES = {
    'tiles.png': '5258c8e16ac2b67f869b42255a700341f962783d00fe5d148ca0188326f73c80',
    'white.pal': '76a0069bca585ae0b06f4cf2e66a380cceebc8c3e56f47b49a14c7edb0f183be',
}


def extract_function(source, name):
    match = re.search(r'^static void ' + re.escape(name) + r'\([^;]*?\)\n\{', source, re.M)
    if match is None:
        raise ValueError('Missing cave EXIT function: ' + name)
    end, depth = match.end(), 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[match.start():end] + '\n'


HEADER = r'''
#include <stdio.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int16_t s16;
#define BG_PLTT_ID(bank) ((bank)*16)
#define PLTT_SIZEOF(count) ((count)*2)
#define BG_CHAR_ADDR(bank) 0
#define BG_SCREEN_ADDR(bank) 0
#define BGCNT_PRIORITY(x) (x)
#define BGCNT_CHARBASE(x) ((x)<<2)
#define BGCNT_SCREENBASE(x) ((x)<<8)
#define DISPCNT_MODE_0 0
#define DISPCNT_OBJ_1D_MAP 0x40
#define DISPCNT_BG0_ON 0x100
#define DISPCNT_OBJ_ON 0x1000
#define BLDCNT_TGT1_BG0 1
#define BLDCNT_EFFECT_BLEND 0x40
#define BLDCNT_TGT2_BG1 0x200
#define BLDCNT_TGT2_BG2 0x400
#define BLDCNT_TGT2_BG3 0x800
#define BLDCNT_TGT2_OBJ 0x1000
#define BLDCNT_TGT2_BD 0x2000
enum { REG_OFFSET_DISPCNT,REG_OFFSET_BLDCNT,REG_OFFSET_BLDALPHA,REG_OFFSET_BLDY,REG_OFFSET_BG0CNT };
struct Task { void(*func)(u8); s16 data[16]; } gTasks[1];
struct Main { void(*savedCallback)(void); } gMain;
struct PaletteWrite { int offset,count; u16 colors[16]; } writes[2];
u16 registers[5],palette[256],sCaveTransitionPalette[16],sCaveTransitionPalette_White[16];
const u32 sCaveTransitionTiles[1]={0},sCaveTransitionTilemap[1]={0};
int writeCount,complete,frame;
void ReturnedToMap(void){}
void SetMainCallback2(void(*callback)(void))
{if(callback!=gMain.savedCallback)exit(3);complete=1;}
u8 CreateTask(void(*callback)(u8),u8 priority)
{if(priority!=0)exit(4);gTasks[0].func=callback;return 0;}
void SetGpuReg(int reg,u16 value){registers[reg]=value;}
void LZ77UnCompVram(const void* source,void* destination){(void)source;(void)destination;}
void LoadPalette(const void* source,int offset,int bytes)
{
    if(writeCount>=2||offset<0||offset+bytes/2>256||bytes<0||bytes>32||bytes%2)exit(5);
    struct PaletteWrite* write=&writes[writeCount++];write->offset=offset;write->count=bytes/2;
    memcpy(palette+offset,source,bytes);memcpy(write->colors,source,bytes);
}
'''

MAIN = r'''
void colors(const u16* values,int count)
{printf("[");for(int i=0;i<count;i++){if(i)printf(",");printf("%u",values[i]);}printf("]");}
int phase(void)
{
    if(gTasks[0].func==Task_FlashTransition_Exit_0)return 0;
    if(gTasks[0].func==Task_FlashTransition_Exit_1)return 1;
    if(gTasks[0].func==Task_FlashTransition_Exit_2)return 2;
    if(gTasks[0].func==Task_FlashTransition_Exit_3)return 3;
    if(gTasks[0].func==Task_FlashTransition_Exit_4)return 4;
    exit(6);
}
void row(void)
{
    int bg=(registers[REG_OFFSET_DISPCNT]&DISPCNT_BG0_ON)!=0;
    int blend=bg&&(registers[REG_OFFSET_BLDCNT]&0xc0)==BLDCNT_EFFECT_BLEND;
    int a=registers[REG_OFFSET_BLDALPHA]&31,b=(registers[REG_OFFSET_BLDALPHA]>>8)&31;
    printf("{\"state\":[%d,%d,%d,%d,%d,%d,%u,%d],\"palette\":",frame,phase(),bg,blend,a,b,palette[0],complete);
    colors(palette+224,16);printf(",\"palette0\":");colors(palette,16);
    printf(",\"writes\":[");
    for(int i=0;i<writeCount;i++)
    {if(i)printf(",");printf("{\"offset\":%d,\"colors\":",writes[i].offset);colors(writes[i].colors,writes[i].count);printf("}");}
    printf("],\"rgba\":[");
    for(int index=0;index<16;index++)
    {
        if(index)printf(",");
        for(int channel=0;channel<3;channel++)
        {
            int background=(palette[0]>>(channel*5))&31;
            int value=background;
            if(bg&&index)
            {
                value=(palette[224+index]>>(channel*5))&31;
                if(blend)
                {value=(value*(a>16?16:a)+background*(b>16?16:b))/16;if(value>31)value=31;}
            }
            printf("%d,",(value<<3)|(value>>2));
        }
        printf("255");
    }
    printf("]}");
}
int main(void)
{
    printf("{\"traces\":[");
    for(int kind=0;kind<2;kind++)
    {
        memset(gTasks,0,sizeof(gTasks));memset(registers,0,sizeof(registers));memset(palette,0,sizeof(palette));
        writeCount=complete=frame=0;gMain.savedCallback=ReturnedToMap;
        memcpy(sCaveTransitionPalette,originalPalette,sizeof(originalPalette));
        memcpy(sCaveTransitionPalette_White,originalWhite,sizeof(originalWhite));
        if(kind)
        {
            for(int i=0;i<16;i++)
            {
                sCaveTransitionPalette[i]=(u16)((i*2)|((31-i)<<5)|((i+7)<<10));
                sCaveTransitionPalette_White[i]=(u16)((31-i)|(i<<5)|((31-i/2)<<10));
            }
            palette[0]=3|(9<<5)|(17<<10);
        }
        if(kind)printf(",");
        printf("{\"name\":\"%s\",\"initialBackdrop\":%u,\"transitionPalette\":",kind?"distinctPalette":"source",palette[0]);
        colors(sCaveTransitionPalette,16);printf(",\"whitePalette\":");colors(sCaveTransitionPalette_White,16);
        FlashTransition_Exit();printf(",\"frames\":[");row();
        for(frame=1;frame<=64;frame++)
        {
            writeCount=0;gTasks[0].func(0);printf(",");row();if(complete)break;
        }
        if(!complete)return 2;printf("]}");
    }
    printf("]}\n");return 0;
}
'''


def read_palettes(graphics):
    from PIL import Image
    for name, digest in ART_HASHES.items():
        if hashlib.sha256((graphics/name).read_bytes()).hexdigest()!=digest:
            raise ValueError('Cave palette input revision mismatch: '+name)
    with Image.open(graphics/'tiles.png') as image:
        if image.mode!='P':
            raise ValueError('Cave tiles must retain their indexed source palette')
        rgb=image.getpalette()[:48]
    palette=[(rgb[i]>>3)|((rgb[i+1]>>3)<<5)|((rgb[i+2]>>3)<<10) for i in range(0,48,3)]
    lines=(graphics/'white.pal').read_text().splitlines()
    if lines[:3]!=['JASC-PAL','0100','16'] or len(lines)!=19:
        raise ValueError('Unexpected white palette shape')
    white=[]
    for line in lines[3:]:
        red,green,blue=map(int,line.split())
        white.append((red>>3)|((green>>3)<<5)|((blue>>3)<<10))
    return palette,white


def generate(reference,graphics=None):
    raw=(reference/'src/fldeff_flash.c').read_bytes()
    if hashlib.sha256(raw).hexdigest()!=SOURCE_HASH:
        raise ValueError('Cave EXIT reference revision mismatch: src/fldeff_flash.c')
    palette,white=read_palettes(graphics or reference/'graphics/cave_transition')
    source=raw.decode()
    names=['FlashTransition_Exit']+['Task_FlashTransition_Exit_'+str(i) for i in range(5)]
    bodies=[extract_function(source,name) for name in names]
    code=HEADER
    for name,values in [('originalPalette',palette),('originalWhite',white)]:
        code+='static const u16 '+name+'[16]={'+','.join(map(str,values))+'};\n'
    code+=''.join(body.split('\n{',1)[0]+';\n' for body in bodies)+''.join(bodies)+MAIN
    with tempfile.TemporaryDirectory() as directory:
        path=Path(directory)/'field_cave_exit.c'
        binary=Path(directory)/('field_cave_exit.exe' if os.name=='nt' else 'field_cave_exit')
        path.write_text(code)
        compile_c(['cc','-std=c11','-O0',str(path),'-o',str(binary)])
        result=json.loads(subprocess.check_output([str(binary)],text=True))
    for trace in result['traces']:
        frames=trace['frames']
        if len(frames)!=39 or frames[-1]['state'][0]!=38 or frames[-1]['state'][-1]!=1:
            raise ValueError('Unexpected EXIT completion boundary')
        actual=[(row['state'][0],write['offset'],len(write['colors'])) for row in frames for write in row['writes']]
        expected=[(2,224,16),(2,224,8)]+[(21+i,224,8-i) for i in range(8)]+[(29,0,16)]
        if actual!=expected:
            raise ValueError('Unexpected EXIT palette-write boundaries: '+str(actual))
    return {'sourceCommit':COMMIT,'columns':['frame','taskPhase','backgroundEnabled','blendEnabled','alphaA','alphaB','backdropColor','complete'],**result}


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--graphics',type=Path,help='Optional independent pinned graphics directory while staging imports')
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args();output=generate(args.reference,args.graphics)
    args.output.parent.mkdir(parents=True,exist_ok=True)
    args.output.write_text(json.dumps(output,separators=(',',':'))+'\n')
    print(json.dumps({'traces':len(output['traces']),'rows':sum(len(trace['frames']) for trace in output['traces']),'completeFrames':38}))


if __name__=='__main__':
    main()
