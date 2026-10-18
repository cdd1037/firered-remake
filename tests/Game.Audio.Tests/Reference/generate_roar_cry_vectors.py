#!/usr/bin/env python3
"""Extract source PlayCryInternal parameter selection; no copied preset switch."""
import argparse,importlib.util,json,re,subprocess,tempfile
from pathlib import Path

def build(root):
 raw=root/'content/raw/pokefirered/037335f4'
 def module(n,p):
  s=importlib.util.spec_from_file_location(n,p);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
 ref=module('reference_c',root/'tests/Reference/reference_c.py');ref.verify_reference(raw);ex=module('extract',root/'tests/Game.Presentation.Tests/Reference/generate_fang_vectors.py')
 source=(raw/'src/sound.c').read_text();header=(raw/'include/constants/sound.h').read_text();constants='\n'.join(line for line in header.splitlines() if re.match(r'#define CRY_MODE_\w+\s+\d+',line))
 h=r'''
#include <stdint.h>
#include <stdio.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int8_t s8;typedef int bool32;
#define TRUE 1
#define FALSE 0
static int volume,pan,pitch,length,progress,release,chorus,priority,reverse;
static int gCryTable[512],gCryTable_Reverse[512],gMPlay_PokemonCry;
#define SETTER(N,n) static void SetPokemonCry##N(int v){n=v;}
SETTER(Volume,volume) SETTER(Panpot,pan) SETTER(Pitch,pitch) SETTER(Length,length) SETTER(Progress,progress) SETTER(Release,release) SETTER(Chorus,chorus) SETTER(Priority,priority)
static int SpeciesToCryId(int s){return s;}
static int SetPokemonCryTone(int *tone){reverse=tone==&gCryTable_Reverse[3];return 1;}
'''+constants+'\n'+ex.function(source,'PlayCryInternal')+r'''
int main(void){int pans[]={-128,-64,0,63,127};for(int mode=0;mode<=12;mode++)for(int p=0;p<5;p++){PlayCryInternal(4,pans[p],120,10,mode);printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",mode,pan,volume,pitch,length,progress,release,chorus,priority,reverse);}return 0;}
'''
 with tempfile.TemporaryDirectory() as d:
  c=Path(d)/'cry-presets.c';exe=Path(d)/'cry-presets';c.write_text(h);ref.compile_c(['cc','-std=gnu11','-O0',str(c),'-o',str(exe)]);out=subprocess.check_output([str(exe)],text=True)
 return [list(map(int,line.split(','))) for line in out.splitlines()]
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--root',type=Path,required=True);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(build(a.root),separators=(',',':'))+'\n')
