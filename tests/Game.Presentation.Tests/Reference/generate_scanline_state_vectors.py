#!/usr/bin/env python3
"""Pinned scanline lifecycle and genuine GPU queue branches; no CPU cycle claim.

The authored host adapter substitutes register storage and DMA transport only.
Native callbacks, buffer indexing, queue insertion, and flush are extracted intact.
The explicit scene phase profile is main writes queued / VBlank base writes immediate.
"""
import argparse
import hashlib
import importlib.util
import json
import re
import subprocess
import tempfile
from pathlib import Path

PINS = {
    'src/scanline_effect.c': '7b50649a25d2e9762a5cf47be9815148a751e44b',
    'include/scanline_effect.h': '5bd8b554686ebd85eaf21ea551ff722a5d8c8984',
    'include/gba/io_reg.h': 'a8a36f7e295855dbc4281f5f9a9f223ddda34a16',
    'src/gpu_regs.c': '2f52c5be334f90ce41eb52f8d4828e50e6da8e6d',
}

def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result

def native_source(root, gpu_source):
    raw = root / 'content/raw/pokefirered/037335f4'
    source = {}
    for name, expected in PINS.items():
        path = gpu_source if name == 'src/gpu_regs.c' else raw / name
        data = path.read_bytes()
        digest = hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()
        if digest != expected:
            raise ValueError('Pinned git blob differs: ' + name)
        source[name] = data.decode()
    x = module('scanline_extract', root / 'tests/Reference/Recovery/source_extract.py')
    macros = x.macros([source['include/gba/io_reg.h']], [
        'DMA_ENABLE', 'DMA_START_HBLANK', 'DMA_REPEAT', 'DMA_SRC_INC',
        'DMA_DEST_INC', 'DMA_16BIT', 'DMA_32BIT', 'DMA_DEST_RELOAD',
        'REG_OFFSET_BG1HOFS', 'REG_OFFSET_BG2HOFS', 'REG_OFFSET_BLDALPHA',
        'REG_OFFSET_DISPSTAT', 'REG_OFFSET_VCOUNT', 'DISPCNT_FORCED_BLANK',
        'DISPSTAT_HBLANK_INTR', 'DISPSTAT_VBLANK_INTR'])
    h = r'''
#include <assert.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int16_t s16; typedef int32_t s32; typedef uint8_t bool8;
typedef volatile uint16_t vu16; typedef volatile uint32_t vu32;
#define TRUE 1
#define FALSE 0
#define EWRAM_DATA
static union {u32 align; u16 half[128];} registers;
#define REG_BASE ((uintptr_t)&registers.half[0])
#define REG_VCOUNT registers.half[REG_OFFSET_VCOUNT/2]
#define REG_DISPCNT registers.half[0]
#define REG_DISPSTAT registers.half[REG_OFFSET_DISPSTAT/2]
static const void *dmaSource; static volatile void *dmaDestination; static u32 dmaControl;
static void DmaStop(int id){assert(id==0);dmaSource=NULL;dmaDestination=NULL;dmaControl=0;}
static void DmaSet(int id,const void*src,volatile void*dest,u32 control){assert(id==0);dmaSource=src;dmaDestination=dest;dmaControl=control;}
static void DestroyTask(u8 id){assert(0 && "No generic wave task admitted");}
static void CpuFill16(u16 value,void*dest,size_t bytes){assert(value==0);memset(dest,0,bytes);}
'''
    h += macros + source['include/scanline_effect.h'] + '\n'
    h += re.sub(r'^#include[^\n]*\n', '', source['src/scanline_effect.c'].split('#define tStartLine')[0], flags=re.M)
    h += re.sub(r'^#include[^\n]*\n', '', source['src/gpu_regs.c'].split('u16 GetGpuReg')[0], flags=re.M)
    h += r'''
static int offset(int dest){assert(dest>=0&&dest<3);return (int[]){REG_OFFSET_BG1HOFS,REG_OFFSET_BG2HOFS,REG_OFFSET_BLDALPHA}[dest];}
static int destination(void){for(int i=0;i<3;i++)if(gScanlineEffect.dmaDest==&registers.half[offset(i)/2])return i;return 0;}
static int pending(int off){for(int i=0;i<GPU_REG_BUF_SIZE&&sGpuRegWaitingList[i]!=EMPTY_SLOT;i++)if(sGpuRegWaitingList[i]==off)return 1;return 0;}
static void emit(const u16 *frame,int count,int low,int high){
 printf("{\"state\":[%u,%u,%d,%d],\"buffers\":[",gScanlineEffect.srcBuffer,gScanlineEffect.state,destination(),gScanlineEffect.dmaControl==SCANLINE_EFFECT_DMACNT_32BIT);
 for(int bank=0;bank<2;bank++)for(int i=0;i<960;i++)printf("%s%u",bank||i?",":"",gScanlineEffectRegBuffers[bank][i]);
 printf("],\"overrides\":[%d,%d],\"frame\":",low,high);
 if(count){printf("[");for(int i=0;i<count;i++)printf("%s%u",i?",":"",frame[i]);printf("]");}else printf("null");
 printf("}\n");
}
int main(void){
 char op[32];int a,b,c,d;u16 frame[322];
 while(scanf("%31s%d%d%d%d",op,&a,&b,&c,&d)==5){int count=0,low=-1,high=-1;
  if(!strcmp(op,"reset")){memset(&registers,0,sizeof(registers));InitGpuRegManager();ScanlineEffect_Clear();DmaStop(0);}
  else if(!strcmp(op,"clear"))ScanlineEffect_Clear();
  else if(!strcmp(op,"fill")){for(int bank=0;bank<2;bank++)for(int i=0;i<960;i++)gScanlineEffectRegBuffers[bank][i]=(u16)(a+bank*17011+i*977);}
  else if(!strcmp(op,"write")){assert(a>=0&&a<2&&b>=0&&b<960);gScanlineEffectRegBuffers[a][b]=(u16)c;}
  else if(!strcmp(op,"set")){assert(b==0||a!=2);ScanlineEffect_SetParams((struct ScanlineEffectParams){&registers.half[offset(a)/2],b?SCANLINE_EFFECT_DMACNT_32BIT:SCANLINE_EFFECT_DMACNT_16BIT,1,0});}
  else if(!strcmp(op,"stop"))ScanlineEffect_Stop();
  else if(!strcmp(op,"request"))gScanlineEffect.state=3;
  else if(!strcmp(op,"gpu")){REG_VCOUNT=(u16)c;REG_DISPCNT=d?DISPCNT_FORCED_BLANK:0;SetGpuReg((u8)a,(u16)b);}
  else if(!strcmp(op,"flush"))CopyBufferedValuesToGpuRegs();
  else if(!strcmp(op,"capture")){
   int active=gScanlineEffect.state!=0&&gScanlineEffect.state!=3;
   int off=offset(destination()),stride=gScanlineEffect.dmaControl==SCANLINE_EFFECT_DMACNT_32BIT?2:1;
   ScanlineEffect_InitHBlankDmaTransfer();
   if(active){low=pending(off)?GPU_REG_BUF(off):-1;high=stride==2&&pending(off+2)?GPU_REG_BUF(off+2):-1;}
   CopyBufferedValuesToGpuRegs();
   if(active){assert(dmaSource&&dmaDestination);count=161*stride;
    for(int j=0;j<stride;j++)frame[j]=registers.half[off/2+j];
    memcpy(frame+stride,dmaSource,160*stride*sizeof(u16));
   }
  }
  else abort();
  emit(frame,count,low,high);
 }
 return 0;
}
'''
    return h

def operation(op, *args):
    return {'op': op, 'args': list(args)}

def cases():
    result = []
    for dest, word in [(0,0),(1,0),(2,0),(0,1),(1,1)]:
        ops = [operation('reset'),operation('fill',65500),operation('capture'),operation('set',dest,word),
               operation('capture'),operation('write',0,959,41),operation('write',1,321,65535),
               operation('set',dest,word),operation('capture'),operation('request'),operation('capture'),
               operation('capture'),operation('set',dest,word),operation('capture'),operation('stop'),
               operation('capture'),operation('set',1 if dest==2 else dest,1-word),operation('capture'),
               operation('set',2,0),operation('capture'),operation('clear'),operation('capture')]
        result.append({'name':f'lifecycle-{dest}-{word}','ops':ops})
    profiles = [
        ('clean',[]),('line160',[(0,111,160,0)]),('line161',[(0,222,161,0)]),
        ('line225',[(0,333,225,0)]),('line226',[(0,444,226,0)]),
        ('forcedblank160',[(0,555,160,1)]),
        ('dirty-then-immediate',[(0,111,160,0),(0,666,161,0)]),
        ('dirty-deduplicated',[(0,111,160,0),(0,777,160,0)]),
        ('main-queued-vblank-immediate',[(0,111,100,0),(0,888,161,0)]),
    ]
    for dest,word in [(0,0),(1,0),(2,0),(0,1),(1,1)]:
        off=[0x14,0x18,0x52][dest]
        selected=profiles+([('only-high-dirty',[(2,999,160,0)]),('low-dirty-high-clean',[(0,123,160,0),(2,321,161,0)]),('both-dirty',[(0,123,160,0),(2,456,160,0)]),('high-dirty-then-immediate',[(2,123,160,0),(2,654,161,0)])] if word else [])
        for name,writes in selected:
            ops=[operation('reset'),operation('fill',12345),operation('set',dest,word)]
            ops += [operation('gpu',off+delta,value,vcount,blank) for delta,value,vcount,blank in writes]
            ops += [operation('capture'),operation('capture')]
            result.append({'name':f'queue-{dest}-{word}-{name}','ops':ops})
    return result

def build(root,gpu_source):
    helper=module('scanline_compile',root/'tests/Reference/reference_c.py')
    entries=cases()
    operations=[op for case in entries for op in case['ops']]
    stdin=''.join(op['op']+' '+' '.join(map(str,(op['args']+[0]*4)[:4]))+'\n' for op in operations)
    with tempfile.TemporaryDirectory(prefix='scanline-state-source-') as directory:
        path=Path(directory);(path/'state.c').write_text(native_source(root,gpu_source))
        helper.compile_c(['cc','-std=c11','-O0','-fno-strict-aliasing',str(path/'state.c'),'-o',str(path/'state')])
        rows=subprocess.check_output([str(path/'state')],input=stdin,text=True).splitlines()
    if len(rows)!=len(operations):raise ValueError('Missing native state rows')
    captures=0
    for operation,row in zip(operations,rows):
        operation['expected']=json.loads(row)
        expected=operation['expected'];frame=expected['frame']
        if len(expected['buffers'])!=1920:raise ValueError('Missing retained words')
        if frame is not None:
            captures+=1
            if len(frame)!=(322 if expected['state'][3] else 161):raise ValueError('Incomplete DMA frame')
    # Explicit native-branch evidence: no timing assumption is encoded in these checks.
    indexed={case['name']:case for case in entries}
    def first(name):return next(op['expected'] for op in indexed[name]['ops'] if op['op']=='capture')
    for dest,word in [(0,0),(1,0),(2,0),(0,1),(1,1)]:
        key=f'queue-{dest}-{word}-'
        for name in ['clean','line161','line225','forcedblank160']:
            assert first(key+name)['overrides']==[-1,-1]
        for name,value in [('line160',111),('line226',444),('dirty-then-immediate',666),('dirty-deduplicated',777),('main-queued-vblank-immediate',888)]:
            assert first(key+name)['overrides'][0]==value
            assert first(key+name)['frame'][0]==value
        if word:
            assert first(key+'only-high-dirty')['overrides']==[-1,999]
            assert first(key+'low-dirty-high-clean')['overrides']==[123,-1]
            assert first(key+'both-dirty')['overrides']==[123,456]
            assert first(key+'high-dirty-then-immediate')['overrides']==[-1,654]
    return {'sourceCommit':'037335f4c725d7c9aecdac87066f2002b4bd7e14','sourceBlobs':PINS,
            'timingContract':'Conditional VCOUNT branches; scene profile main writes queued, VBlank base writes immediate. No sub-scanline cycle equivalence.',
            'coverage':{'cases':len(entries),'operations':len(operations),'captures':captures,'retainedWordsPerOperation':1920},'cases':entries}

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--root',type=Path,required=True);parser.add_argument('--gpu-source',type=Path);parser.add_argument('--output-dir',type=Path,required=True)
    args=parser.parse_args();root=args.root.resolve();gpu=args.gpu_source or root/'content/raw/pokefirered/037335f4/src/gpu_regs.c'
    output=args.output_dir/'scanline-state-source-vectors.json'
    if output.exists():raise SystemExit('Refusing to overwrite existing source vectors')
    result=build(root,gpu);output.parent.mkdir(parents=True,exist_ok=True);output.write_text(json.dumps(result,separators=(',',':'))+'\n')
    print(json.dumps(result['coverage']))
