#!/usr/bin/env python3
"""Build a bounded C oracle by extracting exact functions from authenticated local source.
This does not run any reference-repository script. It emits an independent compiled C executable.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

FUNCTIONS = ['Task_SpinReels', 'GetNextReelPosition', 'StopReel1', 'StopReel2', 'StopReel3',
             'TwoReelBiasCheck', 'OneReelBiasCheck', 'TestReelIconAttribute', 'ReelIconToPayoutRank',
             'CalcSlotBias', 'CalcPayout']
TABLES = ['sSecondReelBiasCheckIndices', 'sThirdReelBiasCheckIndices', 'sRowAttributes',
          'sReelBiasChances', 'sReelIconAnimByReelAndPos', 'sPayoutTable']

PRELUDE = r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int16_t s16; typedef int32_t s32; typedef uint32_t bool32;
typedef void (*MainCallback)(void);
#define TRUE 1
#define FALSE 0
#define NUM_REELS 3
#define REEL_LENGTH 21
#define NUM_MATCH_LINES 5
static u32 seed; static unsigned draws;
static u16 Random(void) { seed = seed * 0x41c64e6du + 0x6073; draws++; return seed >> 16; }
static void UpdateReelIconSprites(const s16 *a, const s16 *b) { (void)a; (void)b; }
'''
HARNESS = r'''
static void reset(void) { memset(&state, 0, sizeof(state)); draws=0; }
int main(int argc, char **argv) {
  if (argc != 2) return 2;
  if (!strcmp(argv[1],"payout")) {
    for(int bet=1;bet<=3;bet++) for(int a=0;a<21;a++) for(int b=0;b<21;b++) for(int c=0;c<21;c++) {
      reset(); state.bet=bet; state.reelPositions[0]=a;state.reelPositions[1]=b;state.reelPositions[2]=c;
      unsigned rank=CalcPayout(), mask=0;for(int i=0;i<5;i++)if(state.winFlags[i])mask|=1u<<i;
      printf("%d %d %d %d %u %u %u\n",bet,a,b,c,rank,state.payout,mask);
    }
  } else if (!strcmp(argv[1],"bias")) {
    int cooldowns[]={0,1,5,60};
    for(int machine=0;machine<6;machine++)for(int bias=0;bias<7;bias++)for(int cd=0;cd<4;cd++)for(unsigned s=0;s<256;s++) {
      reset(); seed=s * 0x10001u;state.machineIdx=machine;state.machineBias=bias;state.biasCooldown=cooldowns[cd];
      CalcSlotBias();
      printf("%d %d %d %u %u %u %u %u\n",machine,bias,cooldowns[cd],s*0x10001u,state.machineBias,state.biasCooldown,seed,draws);
    }
  } else if (!strcmp(argv[1],"first")) {
    for(int bias=0;bias<7;bias++)for(int pos=0;pos<21;pos++)for(int sub=0;sub<3;sub++)for(unsigned s=0;s<32;s++) {
      int next=(pos-(sub!=0)+21)%21;
      if(bias==0 && next<3) continue; /* Original out-of-bounds read is outside admitted oracle. */
      reset();seed=s;state.machineBias=bias;state.reelPositions[0]=pos;state.reelSubpixel[0]=sub;
      StopReel1(0);
      printf("%d %d %d %u %d %u %u\n",bias,pos,sub,s,state.destReelPos[0],seed,draws);
    }
  } else if (!strcmp(argv[1],"second")) {
    for(int bias=0;bias<7;bias++)for(int a=0;a<21;a++)for(int b=0;b<21;b++)for(int sub=0;sub<3;sub++) {
      reset();state.machineBias=bias;state.reelPositions[0]=a;state.reelPositions[1]=b;state.reelSubpixel[1]=sub;
      StopReel2(1);
      printf("%d %d %d %d %d %u\n",bias,a,b,sub,state.destReelPos[1],state.reel2BiasInPlay);
    }
  } else if (!strcmp(argv[1],"third")) {
    for(int bias=0;bias<7;bias++)for(int a=0;a<21;a++)for(int b=0;b<21;b++)for(int c=0;c<21;c++)for(int sub=0;sub<3;sub++) {
      reset();state.machineBias=bias;state.reelPositions[0]=a;state.reelPositions[1]=b;state.reelPositions[2]=c;state.reelSubpixel[2]=sub;
      state.reelStopOrder[1]=1;StopReel3(2);
      printf("%d %d %d %d %d %d\n",bias,a,b,c,sub,state.destReelPos[2]);
    }
  } else if (!strcmp(argv[1],"tick")) {
    for(int pos=0;pos<21;pos++)for(int sub=0;sub<3;sub++)for(int spin=0;spin<2;spin++)for(int dest=0;dest<22;dest++) {
      reset();state.reelPositions[0]=pos;state.reelSubpixel[0]=sub;state.reelIsSpinning[0]=spin;state.destReelPos[0]=dest;
      Task_SpinReels(0);
      printf("%d %d %d %d %d %d %u %d\n",pos,sub,spin,dest,state.reelPositions[0],state.reelSubpixel[0],state.reelIsSpinning[0],state.destReelPos[0]);
    }
  } else return 3;
  return 0;
}
'''

if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source',type=Path)
    parser.add_argument('output',type=Path)
    args=parser.parse_args()
    data=args.source.read_bytes()
    if hashlib.sha256(data).hexdigest()!='bc80b3334a1aae51db36cfccd7df33cf08fc19b18d9c871e6aecf0e58056087d':
        raise ValueError('Oracle source hash differs')
    source=data.decode()
    enum_blocks=[]
    for prefix in ['ICON_', 'PAYOUT_', 'ROWATTR_']:
        enum_blocks.append(re.search(r'enum \{\n\s+'+prefix+r'.*?\n\};',source,re.S)[0])
    state=re.search(r'struct SlotMachineState\n\{.*?\n\};',source,re.S)[0]
    tables=[re.search(r'^static const [^\n]+\b'+name+r'[^=]*=\s*\{.*?^\};',source,re.M|re.S)[0] for name in TABLES]
    functions=[re.search(r'^static [^\n;]+\b'+name+r'\([^;\n]*\)\n\{.*?^\}',source,re.M|re.S)[0] for name in FUNCTIONS]
    declarations=[body.split('\n{',1)[0]+';' for body in functions]
    # The source has an undefined, dead read of this uninitialized variable before assigning it.
    # Remove only that no-op pair; the actual value assignment and all algorithm branches stay exact.
    functions=[body.replace('    firstStoppedPos++; firstStoppedPos--;\n','') for body in functions]
    output='\n\n'.join([PRELUDE,*enum_blocks,state,
        'static struct SlotMachineState state;\nstatic struct SlotMachineState *sSlotMachineState = &state;',
        *tables,*declarations,*functions,HARNESS])
    args.output.mkdir(parents=True,exist_ok=True)
    path=args.output/'oracle.c';path.write_text(output)
    command=['cc','-std=c11','-O0','-Wall','-Wextra','-Wno-unused-parameter','-fsanitize=undefined,address',str(path),'-o',str(args.output/'oracle')]
    subprocess.run(command,check=True)
    (args.output/'oracle-build.json').write_text(json.dumps(dict(sourceSha256=hashlib.sha256(data).hexdigest(),
        generatedSha256=hashlib.sha256(output.encode()).hexdigest(),command=command,
        adaptations=['fixed-width host integer aliases','stubbed sprite update','explicit zeroed test state',
                     'removed dead uninitialized firstStoppedPos increment/decrement',
                     'excluded negative first-reel table reads']),indent=2)+'\n')
