"""Pinned Pokédex tile/window, menu badge and keypad conversion. No runtime source reads."""
import io
import re
import struct
from PIL import Image

CATEGORIES = dict(GrasslandPkmn='grassland', ForestPkmn='forest', WatersEdgePkmn='waters_edge',
                 SeaPkmn='sea', CavePkmn='cave', MountainPkmn='mountain', RoughTerrainPkmn='rough_terrain',
                 UrbanPkmn='urban', RarePkmn='rare', unknown='qmark', numerical='numerical')
VISUAL_PATHS = frozenset(['graphics/pokedex/'+n for n in ('page_icon_tilemap.bin','mini_page.png',
    'national_dex_bgtiles.png','national_dex_bgpals.pal','caught_marker.png')] +
    ['graphics/interface/pokemon_types.pal', 'graphics/fonts/keypad_icons.png', 'src/text.c', 'src/palette.c', 'src/blit.c', 'src/window.c'] +
    ['graphics/pokedex/cat_icon_'+n+'.png' for n in CATEGORIES.values()])


def build_visuals(sources):
    def raw(path):
        im=Image.open(io.BytesIO(sources[path])); im.load()
        if im.mode not in ('P','L'): raise ValueError('Expected indexed Pokédex image: '+path)
        return im
    def indices(im):
        return [v & 15 if im.mode=='P' else 15-v//17 for v in im.tobytes()]
    def palette(path):
        rows=sources[path].decode().splitlines()
        if rows[:2]!=['JASC-PAL','0100']: raise ValueError('Invalid Pokédex palette')
        return [tuple(((int(v)>>3)<<3)|((int(v)>>3)>>2) for v in row.split()) for row in rows[3:]]
    def colors(im,pal,transparent=True):
        return Image.frombytes('RGBA',im.size,bytes(c for i in indices(im) for c in (*pal[i],0 if i==0 and transparent else 255)))
    images={}
    screen=sources['src/pokedex_screen.c'].decode()
    cursor=[tuple((int(v)<<3)|(int(v)>>2) for v in row) for row in
            re.findall(r'RGB\((\d+), (\d+), (\d+)\)',screen.split('sDexScreen_CategoryCursorPals[] = {')[1].split('};')[0])]
    tilemap=struct.unpack('<64H',sources['graphics/pokedex/page_icon_tilemap.bin'])
    for mode in ('kanto','national'):
        pal=palette('graphics/pokedex/'+mode+'_dex_bgpals.pal')
        images[f'{mode}/fade']=Image.new('RGBA',(1,1),(*pal[7],255))
        sheet=raw('graphics/pokedex/'+mode+'_dex_bgtiles.png')
        def tile(number,pal=pal):
            n=number&1023; x=n%(sheet.width//8)*8; y=n//(sheet.width//8)*8
            if y+8>sheet.height: raise ValueError('Pokédex tile out of bounds')
            img=colors(sheet.crop((x,y,x+8,y+8)),pal,False)
            if number&1024: img=img.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
            if number&2048: img=img.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            return img
        for n in range(32): images[f'{mode}/tile/{n}']=tile(n)
        images[f'{mode}/mini']=colors(raw('graphics/pokedex/mini_page.png'),pal)
        images[f'{mode}/caught']=colors(raw('graphics/pokedex/caught_marker.png'),pal)
        for phase in range(5):
            cp=list(pal[80:96]); cp[2],cp[8]=cursor[phase*2:phase*2+2]
            circle=Image.new('RGBA',(64,64))
            for i,n in enumerate(tilemap): circle.paste(tile(n,cp),(i%8*8,i//8*8))
            images[f'{mode}/circle/{phase}']=circle
        for name,t in [('habitat',2),('contents',14),('blank',12),('area',1),('entry',2)]:
            bg=Image.new('RGBA',(240,160))
            for y in range(20):
                for x in range(30): bg.paste(tile(t),(x*8,y*8))
            # Header/footer source windows are filled with palette color 15.
            bg.paste((*pal[15],255),(0,0,240,16)); bg.paste((*pal[15],255),(0,144,240,160))
            if name=='entry':
                for x in range(30):
                    bg.paste(tile(4 if x==0 else 4|1024 if x==29 else 5),(x*8,16))
                    bg.paste(tile(10 if x==0 else 10|1024 if x==29 else 11),(x*8,136))
                for y in range(3,17):
                    for x in range(30):
                        edge=x in (0,29)
                        n=(6 if y<11 else 7 if y==11 else 9) if edge else (1 if y<11 else 8 if y==11 else 2)
                        bg.paste(tile(n|(1024 if x==29 else 0)),(x*8,y*8))
            if name=='area':
                for x in range(30):
                    bg.paste(tile(4 if x==0 else 4|1024 if x==29 else 5),(x*8,16))
                    bg.paste(tile((4 if x==0 else 4|1024 if x==29 else 5)|2048),(x*8,136))
                for y in range(3,17):
                    bg.paste(tile(6),(0,y*8)); bg.paste(tile(6|1024),(232,y*8))
                for x in range(1,13):
                    bg.paste(tile(29 if x==1 else 29|1024 if x==12 else 30),(x*8,72))
                    bg.paste(tile((29 if x==1 else 29|1024 if x==12 else 30)|2048),(x*8,128))
                for y in range(10,16):
                    bg.paste(tile(31),(8,y*8)); bg.paste(tile(31|1024),(96,y*8))
            images[f'{mode}/{name}']=bg
        keys=raw('graphics/fonts/keypad_icons.png')
        if keys.width!=128:raise ValueError('Keypad source bitmap stride differs')
        keyrows=re.findall(r'\[CHAR_(\w+)\]\s*=\s*\{\s*(0x[0-9A-Fa-f]+),\s*(\d+),\s*(\d+)\s*\}',sources['src/text.c'].decode())
        for key,off,w,h in keyrows:
            w,h=int(w),int(h); tile_offset=int(off,16)
            sx=(tile_offset%16)*8; sy=(tile_offset//16)*8
            pixels=indices(keys.crop((sx,sy,sx+w,sy+h)))
            images[f'{mode}/key/{key}']=Image.frombytes('RGBA',(w,h),bytes(c for i in pixels for c in (*pal[i],0 if i==0 else 255)))
    menu=raw('graphics/interface/menu_info.png'); pal=palette('graphics/interface/pokemon_types.pal')
    if menu.width!=128:raise ValueError('Type source bitmap stride differs')
    for key,w,h,off in re.findall(r'\[(TYPE_\w+) \+ 1\]\s*=\s*\{\s*(\d+),\s*(\d+),\s*(0x[0-9A-Fa-f]+)\s*\}',sources['src/list_menu.c'].decode()):
        w,h=int(w),int(h); tile_offset=int(off,16)
        sx=(tile_offset%16)*8; sy=(tile_offset//16)*8
        pixels=indices(menu.crop((sx,sy,sx+w,sy+h)))
        images[key]=Image.frombytes('RGBA',(w,h),bytes(c for i in pixels for c in (*pal[i],0 if i==0 else 255)))
    if len([k for k in images if k.startswith('TYPE_')])!=18: raise ValueError('Incomplete type badges')
    for category,name in CATEGORIES.items():
        im=raw('graphics/pokedex/cat_icon_'+name+'.png'); p=im.getpalette()
        if not p: raise ValueError('Category icon palette missing')
        pal=[tuple(((c>>3)<<3)|((c>>3)>>2) for c in p[i:i+3]) for i in range(0,48,3)]
        images['category/'+category]=colors(im,pal)
    # Stable shelf atlas. Every runtime sprite explicitly names its crop.
    width=512;x=y=row=0;positions={}
    for name,im in sorted(images.items()):
        if x+im.width>width:x=0;y+=row;row=0
        positions[name]=(x,y); x+=im.width;row=max(row,im.height)
    atlas=Image.new('RGBA',(width,y+row));sprites={}
    texture=dict(texturePath='content/generated/sprites/pokemon/pokedex-menu/visual-atlas.png',width=width,height=atlas.height)
    for name,im in sorted(images.items()):
        x,y=positions[name];atlas.paste(im,(x,y));sprites[name]=dict(texture=texture,sourceX=x,sourceY=y,width=im.width,height=im.height)
    stream=io.BytesIO();atlas.save(stream,format='PNG',optimize=True)
    columns=[[int(n) for n in re.findall(r'\d+',row)] for row in re.findall(r'\{([^{}]+)\}',screen.split('sDexScreenPageTurnColumns[][30] = {')[1].split('};')[0])]
    if len(columns)!=10 or any(len(r)!=30 or any(n>30 for n in r) for r in columns): raise ValueError('Invalid source page-turn table')
    palette_source=sources['src/palette.c'].decode()
    delta=int(re.search(r'gPaletteFade.deltaY = (\d+);',palette_source)[1])
    finishing=int(re.search(r'softwareFadeFinishingCounter == (\d+)',palette_source)[1])
    return dict(sprites=sprites,pageTurnColumns=columns,fadeStep=delta,fadeFrames=(16//delta+1)*2+finishing+1),{'visual-atlas.png':stream.getvalue()},[texture]
