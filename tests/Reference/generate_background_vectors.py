#!/usr/bin/env python3
"""Explicit raw-source reference vectors; does not import or call product/conversion code."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
from PIL import Image

# These independent recipe rows are the source BattleBackground declarations and
# graphics_file_rules.mk num_tiles values, not inferred from generated resources.
RECIPES = [('grass','terrain',98),('longgrass','terrain',98),('sand','terrain',83),('underwater','terrain',85),
           ('water','terrain',81),('pond','terrain',75),('mountain','terrain',80),('cave','terrain',84),
           ('building','terrain',77),('building','../indoor/plain',77),('building','../indoor/link',77),
           ('building','../indoor/gym',77),('building','../indoor/leader',77),('indoor','2',77),('indoor','1',77),
           ('indoor','lorelei',77),('indoor','bruno',77),('indoor','agatha',77),('indoor','lance',77),('indoor','champion',77)]


def rgb(r,g,b): return (r//8)|((g//8)<<5)|((b//8)<<10)

def pal(path): return [rgb(*map(int,line.split())) for line in path.read_text().splitlines()[3:]]

def image_tiles(path, count):
    image=Image.open(path)
    # Independent coordinate lookup (the importer crops complete tiles then packs nibbles).
    return [[image.getpixel((tile%(image.width//8)*8+x,tile//(image.width//8)*8+y)) for y in range(8) for x in range(8)] for tile in range(count)]


class Hardware:
    def __init__(self, raw, terrain):
        self.raw=raw; self.tiles=[[0]*64 for _ in range(1024)];self.map=[0]*2048;self.colors=pal(raw/'graphics/battle_interface/textbox1.pal')+pal(raw/'graphics/battle_interface/textbox2.pal')+[0]*48
        self.terrain=terrain;self.restore()
    def restore(self):
        folder,palette,count=RECIPES[self.terrain];base=self.raw/'graphics/battle_terrain'/folder
        self.tiles[:count]=image_tiles(base/'terrain.png',count)
        self.map=list(struct.unpack('<2048H',(base/'terrain.bin').read_bytes()))
        self.colors[32:80]=pal(base/(palette+'.pal'))
    def move(self, name):
        base=self.raw/'graphics/battle_anims/backgrounds'/name
        image=Image.open(base.with_suffix('.png'));count=image.width*image.height//64
        self.tiles[:count]=image_tiles(base.with_suffix('.png'),count)
        data=base.with_suffix('.bin').read_bytes();self.map[:len(data)//2]=struct.unpack('<%dH'%(len(data)//2),data)
        colors=image.getpalette();self.colors[32:48]=[rgb(*colors[i:i+3]) for i in range(0,48,3)]
    def sample(self,x,y,screen=1):
        x%=512 if screen else 256;y%=256
        entry=self.map[(x//256)*1024+(y//8)*32+(x//8)%32]
        sx=x%8;sy=y%8
        if entry&1024:sx=7-sx
        if entry&2048:sy=7-sy
        color=self.tiles[entry&1023][sy*8+sx]
        return (entry>>12,color) if color else (0,0)
    def render(self,w,h,sx=0,sy=0,screen=1):
        data=bytearray()
        for y in range(h):
            for x in range(w):
                bank,index=self.sample(x+sx,y+sy,screen);data.extend(struct.pack('<H',self.colors[bank*16+index]|32768))
        return hashlib.sha256(data).hexdigest()


def build(raw):
    terrains=[dict(id=i,sha256=Hardware(raw,i).render(240,112)) for i in range(len(RECIPES))]
    hardware=Hardware(raw,0);states=[]
    for name,action in [('terrain',None),('dark','dark'),('psychic-after-dark','psychic'),('restore','restore'),('psychic-after-restore','psychic')]:
        if action=='restore':hardware.restore()
        elif action:hardware.move(action)
        points=[(0,0),(239,0),(240,0),(255,7),(256,0),(511,111),(0,159),(0,160),(0,255),(-1,-1),(255,255),(256,255)]
        states.append(dict(name=name,sha256=hardware.render(512,256),scroll256=hardware.render(240,112,249,-13,0),scroll512=hardware.render(240,112,249,-13,1),
                           samples=[dict(x=x,y=y,bank=hardware.sample(x,y)[0],index=hardware.sample(x,y)[1]) for x,y in points]))
    return dict(terrains=terrains,states=states)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('reference',type=Path);p.add_argument('output',type=Path);args=p.parse_args()
    if args.output.exists():raise SystemExit('Refuse existing reference vectors; review an explicitly new candidate')
    args.output.write_text(json.dumps(build(args.reference),indent=2)+'\n')
