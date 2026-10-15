#!/usr/bin/env python3
"""Deterministic, bounded FireRed lab battle UI tiles. No ROM or upstream execution."""
import argparse, importlib.util, json, pathlib, re, struct, sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('lab_ui_inputs',ROOT/'scripts/fetch-lab-battle-ui.py');inputs=importlib.util.module_from_spec(spec);spec.loader.exec_module(inputs)


from image_io import png, indexed_png

def colors(rgb):return [tuple(((c//8)<<3)|((c//8)>>2) for c in row) for row in rgb]
def palette(data):
    lines=data.decode('ascii').splitlines()
    if lines[:2]!=['JASC-PAL','0100'] or len(lines)!=int(lines[2])+3 or int(lines[2]) not in (16,48):raise ValueError('Unsupported palette')
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

def render_map(tiles,tilemap,pals,w,h,top=0):
    # Screenblock maps: source background 64x32 and textbox 32x64 share block offsets.
    if len(tilemap)!=4096:raise ValueError('Unexpected 2-screen tilemap')
    data=struct.unpack('<2048H',tilemap);out=canvas(w,h)
    for y in range(h//8):
        for x in range(w//8):
            sy=y+top;entry=data[(sy//32)*1024+(sy%32)*32+x];tile=entry&1023;bank=entry>>12
            if tile>=len(tiles):raise ValueError('Tile reference outside source')
            pal=pals.get(bank)
            if pal is None:
                if set(tiles[tile])!={0}:raise ValueError('Missing palette bank')
                continue
            blit(out,w,tile_rgba(tiles[tile],pal,bool(entry&1024),bool(entry&2048)),8,8,x*8,y*8)
    return bytes(out)

def require(source,needle):
    if needle not in source:raise ValueError('Unsupported source semantics: '+needle)
def convert(lock,sources):
    for path,needle in [('src/battle_setup.c','case MAP_TYPE_INDOOR:\n    case MAP_TYPE_SECRET_BASE:\n        return BATTLE_TERRAIN_BUILDING;'),('src/battle_interface.c','x = 44, y = 30;'),('src/battle_interface.c','x = 158, y = 88;'),('src/battle_interface.c','gSprites[healthboxOtherSpriteId].oam.tileNum += 2 * TILE_SIZE_4BPP;'),('src/battle_bg.c','LoadUserWindowGfx(2, 0x012, BG_PLTT_ID(1));'),('graphics_file_rules.mk','$(GFX) $< $@ -num_tiles 77 -Wnum_tiles'),('tools/gbagfx/convert_png.c','% (1 << destBitDepth)')]:require(sources[path].decode(),needle)
    require(sources['src/blend_palette.c'].decode(), '((r + (((data2->r - r) * coeff) >> 4)) << 0)')
    require(sources['src/battle_controller_oak_old_man.c'].decode(), 'BeginNormalPaletteFade(0xFFFFFF7E,\n                                   4,\n                                   0,\n                                   8,\n                                   RGB_BLACK);')
    mapinfo=json.loads(sources['data/maps/PalletTown_ProfessorOaksLab/map.json'])
    if (mapinfo['map_type'],mapinfo['battle_scene'])!=('MAP_TYPE_INDOOR','MAP_BATTLE_SCENE_NORMAL'):raise ValueError('Lab terrain selector differs')
    assets=[];outputs={}
    def add(id,w,h,pixels,x=0,y=0):
        data=png(w,h,pixels);name=id+'.png';outputs[name]=data
        assets.append(dict(id=id,texturePath=name,width=w,height=h,x=x,y=y))
    g='graphics/';b=g+'battle_interface/';t=g+'battle_terrain/building/';tw=g+'text_window/'
    terrain=tile_list(sources[t+'terrain.png'])[:77];pal=palette(sources[t+'terrain.pal']);bg=render_map(terrain,sources[t+'terrain.bin'],{i+2:pal[i*16:(i+1)*16] for i in range(3)},240,160)
    add('background',240,160,bg)
    frame_tiles=tile_list(sources[tw+'type1.png']);frame_pal=colors(indexed_png(sources[tw+'type1.png'])[3]);add('frame1',24,24,rgba(indexed_png(sources[tw+'type1.png'])[2],frame_pal))
    textbox=tile_list(sources[b+'textbox.png']);textbox[18:27]=frame_tiles
    textbox_pal=palette(sources[b+'textbox1.pal']);pals={0:textbox_pal,1:frame_pal}
    for id,top in [('battle-message',14),('action-menu',34),('move-menu',54)]:add(id,240,48,render_map(textbox,sources[b+'textbox.bin'],pals,240,48,top),0,112)
    # Original menu-message frame is shared by ordinary map dialogue and Oak voiceover.
    dialogue_tiles=tile_list(sources[tw+'menu_message.png']);dialogue_pal=palette(sources[tw+'stdpal_0.pal']);dialogue=canvas(240,48,(*dialogue_pal[1],255))
    rows=[[0,1]+[2]*26+[3,4],[5,6]+[None]*26+[8,9],[10,11]+[None]*26+[12,13],[10,11]+[None]*26+[12,13],[5,6]+[None]*26+[8,9],[0,1]+[2]*26+[3,4]]
    for y,row in enumerate(rows):
        for x,n in enumerate(row):
            if n is not None:blit(dialogue,240,tile_rgba(dialogue_tiles[n],dialogue_pal,vflip=y>=3),8,8,x*8,y*8)
    add('dialogue',240,48,bytes(dialogue),0,112)
    health_pal=palette(sources[b+'healthbox.pal']);bar_pal=palette(sources[b+'healthbar.pal']);elements=tile_list(sources[b+'healthbox_elements.png'])
    for side,h,x,y in [('player',64,126,72),('opponent',32,12,14)]:
        tiles=tile_list(sources[b+f'healthbox_singles_{side}.png']);out=canvas(128,h)
        # TextIntoHealthboxObject preserves rows 0..4 and replaces rows 5..15.
        windows=[(2,6),(64,1),(65,3),(23,1),(80,2),(82,2)] if side=='player' else [(1,7),(32,3)]
        for start,count in windows:
            for n in range(start,start+count):
                tiles[n]=tiles[n][:40]+bytes([2]*24);tiles[n+8]=bytes([2]*64)
        if len(tiles)!=h*2:raise ValueError('Unexpected healthbox tile count')
        for n,tile in enumerate(tiles):blit(out,128,tile_rgba(tile,health_pal),8,8,(n//h)*64+(n%h%8)*8,(n%h//8)*8)
        # EXP starts at tile36 then tile96. Source initial bar is overwritten with empty EXP tiles.
        if side=='player':
            for i in range(8):blit(out,128,tile_rgba(elements[12],health_pal),8,8,32+i*8,32)
        add('healthbox-'+side,128,h,bytes(out),x,y)
    add('hp-label',16,8,b''.join(rgba(elements[1],bar_pal)[y*32:(y+1)*32]+rgba(elements[2],bar_pal)[y*32:(y+1)*32] for y in range(8)))
    for name,start,pal in [('hp-green',3,bar_pal),('hp-yellow',47,bar_pal),('hp-red',56,bar_pal),('exp',12,health_pal)]:
        out=canvas(72,8)
        for f in range(9):blit(out,72,tile_rgba(elements[start+f],pal),8,8,f*8,0)
        add(name,72,8,bytes(out))
    contract=dict(logicalWidth=240,logicalHeight=160,textureFilter='nearest',backgroundTerrain='BATTLE_TERRAIN_BUILDING',defaultFrame=1,paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',paletteRgb8={'battle':textbox_pal,'dialogue':dialogue_pal,'frame1':frame_pal,'healthbox':health_pal,'healthbar':bar_pal},playerHealthboxCenter=[158,88],opponentHealthboxCenter=[44,30],healthboxOriginalCenterToCorner=[-32,-16],playerHpLabel=[158,88],opponentHpLabel=[36,30],playerHpBar=[174,88,48,8],opponentHpBar=[52,30,48,8],playerExpBar=[158,104,64,8],dialogueText=[16,121],battleMessageText=[10,122],actionPromptText=[10,122],actionMenuText=[[136,122],[192,122],[136,138],[192,138]],moveText=[[16,121],[88,121],[16,137],[88,137]],messageWindow=[8,120,224,32],dialogueWindow=[16,120,208,32],actionPromptWindow=[8,120,112,32],actionMenuWindow=[136,120,96,32],playerName=[142,75],opponentName=[20,17],playerLevelWindow=[198,75],opponentLevelWindow=[76,17],playerCurrentHpText=[186,93],playerMaximumHpText=[206,93],playerCurrentHpWindow=[182,88,24,16],playerMaximumHpWindow=[206,88,16,16],staticDarkening={'coefficient':8,'targetRgb555':0,'sourceMask':'0xFFFFFF7E','formula':'rgb5 + ((-rgb5 * coefficient) >> 4)','stage':'after verified original pixels; derived display textures only','paletteFadeTimingImplemented':False})
    catalog=dict(schemaVersion=1,profile=inputs.PROFILE,repository=inputs.REPOSITORY,sourceCommit=inputs.COMMIT,display=contract,assets=assets,sourceRefs=lock['files'],limitations=['Static settled layout only; no intro, damage, faint, blink, palette fades, text timing or audio.','Only default frame1, normal lab building terrain and ordinary singles.','Healthbox text and game state are rendered separately.','Local source research only; no public asset distribution.'])
    outputs['lab-battle-ui.json']=inputs.canonical(catalog)
    outputs['manifest.json']=inputs.canonical(dict(schemaVersion=1,profile=inputs.PROFILE,commit=inputs.COMMIT,inputs=lock['files'],outputs=[dict(path=p) for p,d in sorted(outputs.items())],upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False))
    return outputs

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--output',type=pathlib.Path,default=ROOT/'content/generated/lab-battle-ui');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/lab-battle-ui.lock.json');a=p.parse_args()
    if a.output.resolve().is_relative_to(a.raw.resolve()) or a.raw.resolve().is_relative_to(a.output.resolve()):raise ValueError('Raw/output roots overlap')
    lock=inputs.load_lock(a.lock);outputs=convert(lock,inputs.read_verified(lock,a.raw));targets=[(inputs.safe_path(a.output,n),d) for n,d in outputs.items()];a.output.mkdir(parents=True,exist_ok=True)
    for target,data in targets:stage=inputs.safe_path(a.output,target.name+'.tmp');stage.write_bytes(data);stage.replace(target)
    print(json.dumps({'status':'imported','assets':len(outputs)-2}))
if __name__=='__main__':main()
