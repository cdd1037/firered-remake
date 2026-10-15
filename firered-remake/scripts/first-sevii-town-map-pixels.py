"""Pillow image I/O and First-Sevii atlas composition."""
import re

from image_io import png, indexed_png

def colors(rgb):return [tuple(((c//8)<<3)|((c//8)>>2) for c in row) for row in rgb]
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


def palette(data, count):
    # Shared decoder's RGB555 expansion, extended here for the original 80-color map palette.
    lines=data.decode('ascii').splitlines()
    if lines[:3] != ['JASC-PAL','0100',str(count)] or len(lines)!=count+3:
        raise ValueError('Unexpected Town Map palette profile')
    rows=[]
    for line in lines[3:]:
        if not re.fullmatch(r'\d+ \d+ \d+',line): raise ValueError('Malformed palette')
        row=tuple(map(int,line.split()))
        if any(v<0 or v>255 for v in row): raise ValueError('Palette range differs')
        rows.append(row)
    return colors(rows)


compressed_png = png


def render_tilemap(tiles, entries, palettes):
    if len(entries)!=600: raise ValueError('Unexpected Town Map tilemap dimensions')
    result=canvas(240,160)
    for offset,entry in enumerate(entries):
        number,bank=entry&1023,entry>>12
        if number>=len(tiles) or bank not in palettes: raise ValueError('Map tile/palette outside source')
        blit(result,240,tile_rgba(tiles[number],palettes[bank],bool(entry&1024),bool(entry&2048)),
                    8,8,(offset%30)*8,(offset//30)*8)
    return bytes(result)


def overlay(destination,source):
    if len(destination)!=len(source): raise ValueError('Map overlay size differs')
    for i in range(0,len(source),4):
        if source[i+3]: destination[i:i+4]=source[i:i+4]
