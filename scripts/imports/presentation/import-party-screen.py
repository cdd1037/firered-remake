#!/usr/bin/env python3
"""Decode pret 037335f4c725d7c9aecdac87066f2002b4bd7e14 party panels.
Source recipes stay here; runtime consumes only one compact RGBA atlas.
"""
import argparse, sys, re, struct
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[3]/'scripts/lib'))
from image_io import indexed_png,png
from ui_tiles import colors,tile_list,tile_rgba,canvas,blit
from project_paths import PROJECT_ROOT

def convert(raw):
    root=raw/'graphics/party_menu'; source=(raw/'src/data/party_menu.h').read_text()
    data=(root/'bg.png').read_bytes(); _,_,_,rgb=indexed_png(data)
    pals=colors(rgb); tiles=tile_list(data); out=canvas(240,928)
    def ids(name):
        return [int(n) for n in re.search(r'\b'+name+r'\[\]\s*=\s*\{([^}]+)',source)[1].split(',')]
    def stamp(entries,w,h,x,y,pal=None):
        for i,entry in enumerate(entries):
            t=entry if pal is not None else entry&1023
            p=pal if pal is not None else pals[(entry>>12)*16:(entry>>12)*16+16]
            blit(out,240,tile_rgba(tiles[t],p,bool(entry&1024) if pal is None else False,bool(entry&2048) if pal is None else False,False),8,8,x+i%w*8,y+i//w*8)
    bg=struct.unpack('<1024H',(root/'bg.bin').read_bytes())
    for y in range(20):stamp(bg[y*32:y*32+30],30,1,0,y*8)
    states=[('EmptySlot','EmptySlot'),('CurrSelection','CurrSelection'),('Fainted','Fainted'),('CurrSelectionFainted','CurrSelection'),('SelectedForAction','SelectedForAction'),('SelectedForAction','CurrSelection')]
    for egg in range(2):
        for state,(inside,border) in enumerate(states):
            pal=pals[48:64].copy()
            for group,name in [(1,inside),(2,border)]:
                key='sPartyBox'+name+'PalIds'+str(group)
                if name=='CurrSelectionFainted':key='sPartyBoxCurrSelectionFaintedPalIds'
                for offset,index in zip(ids('sPartyBoxPalOffsets'+str(group)),ids(key)):pal[offset]=pals[index]
            y=160+(egg*6+state)*56
            for name,w,x in [('main',10,0),('wide',18,80)]:
                entries=(root/('slot_'+name+('_no_hp' if egg else '')+'.bin')).read_bytes()
                stamp(entries,w,len(entries)//w,x,y,pal)
    stamp((root/'slot_wide_empty.bin').read_bytes(),18,3,80,832,pals[48:64])
    for selected in range(2):
        entries=struct.unpack('<14H',(root/'cancel_button.bin').read_bytes())
        stamp([(v&4095)|((2 if selected else 1)<<12) for v in entries],7,2,selected*56,864)
    for name,x in [('hold_icons',112),('pokeball',144)]:
        w,h,indices,rgb=indexed_png((root/(name+'.png')).read_bytes());pal=colors(rgb)
        # First source icon frame; held item/mail are adjacent 8px sprites.
        height=h;width=w
        rgba=bytes(c for yy in range(height) for xx in range(width) for c in (*pal[indices[yy*w+xx]],0 if indices[yy*w+xx]==0 else 255))
        blit(out,240,rgba,width,height,x,864)
    for state,name in enumerate(('Green','Yellow','Red')):
        light,dark=ids('sHPBar'+name+'PalIds')
        for row,index in enumerate((dark,light,light)):
            blit(out,240,bytes((*pals[index],255))*48,48,1,0,896+state*4+row)
    for row,index in enumerate((61,50,50)):
        blit(out,240,bytes((*pals[index],255))*48,48,1,0,908+row)
    return png(240,928,out)

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--raw',type=Path,default=PROJECT_ROOT/'content/raw/pokefirered/037335f4')
    p.add_argument('--out',type=Path,default=PROJECT_ROOT/'content/generated/ui/party-screen')
    p.add_argument('--check',action='store_true')
    a=p.parse_args();data=convert(a.raw);target=a.out/'atlas.png'
    if a.check:
        if target.read_bytes()!=data:raise ValueError('Generated party atlas differs from source conversion')
        print('Party atlas matches source conversion')
    else:
        a.out.mkdir(parents=True,exist_ok=True);target.write_bytes(data);print(target)
if __name__=='__main__':main()
