#!/usr/bin/env python3
"""Convert local pinned NPC wireless-trade art and callback data; no upstream code is executed."""
import argparse, io, json, re, tempfile, sys
from pathlib import Path
from PIL import Image
ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'scripts/lib'))
import presentation_pixels as pixels

def convert(raw,out):
    src=(raw/'src/trade_scene.c').read_text(); art=raw/'graphics/trade'
    velocities=list(map(int,re.findall(r'-?\d+',src.split('static const s8 sTradeBallVerticalVelocityTable[] = {')[1].split('};')[0])))
    if len(velocities)!=108:raise ValueError('Incomplete ball velocity table')
    colors=pixels.palette((art/'gba_pal2.pal').read_bytes(),floor=True); tiles=Image.open(art/'gba.png')
    images={}
    # GBA text BG 512x256 uses two consecutive 32x32 screen blocks.
    shadow=pixels.tilemap(tiles,(art/'shadow_map.bin').read_bytes(),colors,256,512,1)
    joined=Image.new('RGBA',(512,256));joined.paste(shadow.crop((0,0,256,256)),(0,0));joined.paste(shadow.crop((0,256,256,512)),(256,0))
    images['shadow']=joined
    images['console']=pixels.tilemap(tiles,(art/'gba_map_wireless.bin').read_bytes(),colors,256,512,1)
    # Bank zero is black during the close-up; cable itself uses the retained GBA banks.
    images['crossing']=pixels.tilemap(tiles,(art/'wireless_signal_closeup.bin').read_bytes(),[(0,0,0)]*16+colors,256,256)
    affine=Image.open(art/'gba_affine.png'); data=(art/'gba_affine_map_wireless.bin').read_bytes(); pal=[(0,0,0)]*16+colors+[(0,0,0)]*192
    im=Image.new('RGBA',(128,128))
    for n,index in enumerate(data):
        for y in range(8):
            for x in range(8):
                c=affine.getpixel((index%16*8+x,index//16*8+y));im.putpixel((n%16*8+x,n//16*8+y),(*pal[c],255))
    images['affine']=im
    for name in ['pokeball','link_mon_glow','link_mon_shadow','cable_end','gba_screen']:
        im=Image.open(art/(name+'.png'));p=im.getpalette()
        colors=pixels.palette((art/('link_mon.pal' if name.startswith('link_mon') else 'gba.pal')).read_bytes(),floor=True) if name!='pokeball' else [tuple((p[i+j]>>3)*255//31 for j in range(3)) for i in range(0,len(p),3)]
        rgba=Image.new('RGBA',im.size);rgba.putdata([(*colors[v],255 if v else 0) for v in (im.getpixel((x,y)) for y in range(im.height) for x in range(im.width))]);images[name]=rgba
    # Signal palettes are only sixteen source states per direction, packed in two sheets.
    signal=Image.open(art/'wireless_signal.png')
    signal=pixels.gba_4bpp_indices(signal)
    params=[list(map(int,re.findall(r'-?\d+',row))) for row in re.findall(r'\{([^{}]+)\}',src.split('static const u8 sWirelessSignalAnimParams[][2] = {')[1].split('};')[0])]
    for direction in ['send','receive']:
        pal=pixels.palette((art/f'wireless_signal_{direction}.pal').read_bytes(),floor=True)
        sheet=Image.new('RGBA',(512,512))
        for i in range(16):
            layer=pixels.tilemap(signal,(art/'wireless_signal.bin').read_bytes(),pal[i*16:(i+1)*16],256,256,3,True)
            # Native art occupies the 128x128 tile area centered at x=120.
            sheet.paste(layer.crop((56,0,184,128)),(i%4*128,i//4*128))
        images['signal_'+direction]=sheet
    # One texture, explicit bounded rectangles. No per-tick images.
    atlas=Image.new('RGBA',(1024,2048));assets=[];x=y=row=0
    for name,im in images.items():
        if x+im.width>1024:x=0;y+=row;row=0
        atlas.paste(im,(x,y));assets.append(dict(id=name,x=x,y=y,width=im.width,height=im.height));x+=im.width;row=max(row,im.height)
    atlas=atlas.crop((0,0,1024,y+row));out.mkdir(parents=True,exist_ok=True);atlas.save(out/'atlas.png')
    doc=dict(schemaVersion=1,width=atlas.width,height=atlas.height,ballVelocity=velocities,signalParameters=params,assets=assets)
    (out/'scene.json').write_text(json.dumps(doc,indent=2)+'\n');return doc
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--raw',type=Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=Path,default=ROOT/'content/generated/ui/npc-trade-scene');p.add_argument('--check',action='store_true');a=p.parse_args()
    if a.check:
        with tempfile.TemporaryDirectory() as tmp:
            convert(a.raw,Path(tmp))
            for name in ['scene.json','atlas.png']:
                if (Path(tmp)/name).read_bytes()!=(a.out/name).read_bytes():raise ValueError('Trade scene output differs: '+name)
        print('NPC trade scene regeneration passed')
    else:
        d=convert(a.raw,a.out);print(f"Imported {len(d['assets'])} regions into {d['width']}x{d['height']} atlas")
