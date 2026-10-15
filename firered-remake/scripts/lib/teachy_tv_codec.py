"""Pillow image I/O and authored GBA palette/tile conversion."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[2] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import re

from image_io import png, indexed_png as _indexed_png

def indexed_png(data):
    return _indexed_png(data, max_pixels=1048576)

def colors(rgb):return [tuple(((c//8)<<3)|((c//8)>>2) for c in row) for row in rgb]
def palette(data):
    lines=data.decode('ascii').splitlines()
    if lines[:2]!=['JASC-PAL','0100'] or len(lines)!=int(lines[2])+3 or int(lines[2]) not in (16,48,64):raise ValueError('Unsupported palette')
    rgb=[]
    for line in lines[3:]:
        if not re.fullmatch(r'\d+ \d+ \d+',line):raise ValueError('Malformed palette')
        row=tuple(map(int,line.split()))
        if any(c>255 for c in row):raise ValueError('Palette range differs')
        rgb.append(row)
    return colors(rgb)
def tile_list(png):
    w,h,indices,_=indexed_png(png)
    return [bytes(indices[(y+dy)*w+x+dx] for dy in range(8) for dx in range(8)) for y in range(0,h,8) for x in range(0,w,8)]
def rgba(indices,pal,transparent=True):return bytes(c for i in indices for c in (*pal[i],0 if i==0 and transparent else 255))
def canvas(w,h,color=(0,0,0,0)):return bytearray(color*(w*h))
def blit(dest,dw,source,sw,sh,x,y):
    dh=len(dest)//dw//4
    if x<0 or y<0 or x+sw>dw or y+sh>dh:raise ValueError('Blit out of bounds')
    for row in range(sh):dest[((y+row)*dw+x)*4:((y+row)*dw+x+sw)*4]=source[row*sw*4:(row+1)*sw*4]
def tile_rgba(tile,pal,hflip=False,vflip=False,transparent=True):return rgba([tile[(7-y if vflip else y)*8+(7-x if hflip else x)] for y in range(8) for x in range(8)],pal,transparent)

