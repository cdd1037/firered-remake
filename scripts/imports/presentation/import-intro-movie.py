#!/usr/bin/env python3
"""Convert pinned pret intro.c assets to compact independent layers/sprite strips.
Source revision 037335f4c725d7c9aecdac87066f2002b4bd7e14. No source code is executed.
"""
import argparse, io, json, struct, sys
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'scripts/lib'))
from presentation_pixels import gba_4bpp_indices
RAW=ROOT/'content/raw/pokefirered/037335f4'
OUT=ROOT/'content/generated/ui/intro-movie'

def generate(raw=RAW):
    root=raw/'graphics/intro'; files={}; textures=[]
    def palette(name):
        p=root/(name+'.pal')
        return [int(v) for line in p.read_text().splitlines()[3:] for v in line.split()] if p.exists() else Image.open(root/(name+'.png')).getpalette()
    def convert(im,pal,transparent=True):
        im=gba_4bpp_indices(im)
        out=Image.new('RGBA',im.size)
        out.putdata([tuple(((pal[i*3+c]>>3)<<3)|(pal[i*3+c]>>5) for c in range(3))+(0 if transparent and i%16==0 else 255,) for i in im.getdata()]);return out
    def png(name,im):
        b=io.BytesIO();im.save(b,format='PNG',optimize=True);files[name+'.png']=b.getvalue();textures.append(dict(name=name,width=im.width,height=im.height))
    def layer(name,source,pals):
        tiles=Image.open(root/(source+'.png')); data=(root/(source+'.bin')).read_bytes()
        out=Image.new('RGBA',(256,len(data)//64*8))
        for i,(entry,) in enumerate(struct.iter_unpack('<H',data)):
            tile=entry&1023;bank=entry>>12;x=tile%(tiles.width//8)*8;y=tile//(tiles.width//8)*8
            if y>=tiles.height:raise ValueError('Tile outside source '+source)
            pal=pals.get(bank)
            if pal is None:raise ValueError('Missing palette '+source+' bank '+str(bank))
            part=convert(tiles.crop((x,y,x+8,y+8)),pal)
            if entry&1024:part=part.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            if entry&2048:part=part.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            out.paste(part,(i%32*8,i//32*8))
        png(name,out)
    black=[0]*48
    layer('copyright','copyright',{0:palette('copyright')})
    layer('gf-bg','game_freak/bg',{0:palette('game_freak/bg')})
    layer('grass-bg','scene_1/bg',{2:palette('scene_1/bg')})
    layer('grass-front','scene_1/grass',{0:black,1:palette('scene_1/grass')})
    p=palette('scene_2/bg');banks={0:black,**{i+1:p[i*48:(i+1)*48] for i in range(3)}}
    layer('wide-bg','scene_2/bg',banks);layer('wide-plants','scene_2/plants',banks)
    layer('close-gengar','scene_2/gengar_close',{5:palette('gengar')})
    layer('close-nidorino','scene_2/nidorino_close',{6:palette('scene_2/nidorino_close')})
    p=palette('scene_3/bg');layer('fight-bg','scene_3/bg',{0:black,1:p[:48],2:p[48:]})
    layer('fight-gengar','scene_3/gengar_anim',{5:palette('gengar')})
    for name,source,pal in [('gf-name','game_freak/game_freak','game_freak/logo'),('gf-logo','game_freak/logo','game_freak/logo'),('gf-presents','game_freak/presents','game_freak/logo'),('star','game_freak/star','game_freak/star'),('sparkles-small','game_freak/sparkles_small','game_freak/sparkles'),('sparkles-big','game_freak/sparkles_big','game_freak/sparkles'),('wide-gengar','scene_2/gengar','gengar'),('wide-nidorino','scene_2/nidorino','nidorino'),('nidorino','scene_3/nidorino','nidorino'),('grass','scene_3/grass','scene_3/grass'),('swipe','scene_3/swipe','scene_3/swipe'),('dust','scene_3/recoil_dust','scene_3/recoil_dust')]:
        png(name,convert(Image.open(root/(source+'.png')),palette(pal)))
    # Four source OBJ pieces: 64x64,32x64,64x64,32x64 with tile offsets 0,64,96,160.
    tiles=Image.open(root/'scene_3/gengar_static.png');out=Image.new('RGBA',(96,128))
    for start,w,h,dx,dy in [(0,64,64,0,0),(64,32,64,64,0),(96,64,64,0,64),(160,32,64,64,64)]:
        for i in range(w*h//64):
            t=start+i;x=t%8*8;y=t//8*8;out.paste(convert(tiles.crop((x,y,x+8,y+8)),palette('gengar')),(dx+i%(w//8)*8,dy+i//(w//8)*8))
    png('gengar-static',out)
    files['intro-movie.json']=(json.dumps(dict(schemaVersion=1,textures=textures),indent=2)+'\n').encode()
    return files

def main():
    p=argparse.ArgumentParser();p.add_argument('--raw',type=Path,default=RAW);p.add_argument('--out',type=Path,default=OUT);p.add_argument('--check',action='store_true');a=p.parse_args()
    for name,data in generate(a.raw).items():
        path=a.out/name
        if a.check:
            if not path.exists() or path.read_bytes()!=data:raise ValueError('Intro resource differs: '+name)
        else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(data)
    print('Intro movie compact layers verified' if a.check else 'Intro movie compact layers generated')
if __name__=='__main__':main()
