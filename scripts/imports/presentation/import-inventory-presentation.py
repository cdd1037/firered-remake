#!/usr/bin/env python3
"""Convert pinned TM Case layers/discs and storage hand sprites; never run upstream code."""
import argparse
import io
import json
import re
import struct
import sys
from pathlib import Path
from PIL import Image
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from image_io import png
from presentation_pixels import gba_4bpp_indices, palette, rgba
from presentation_source import canonical, need, safe
from project_paths import PROJECT_ROOT
from ui_tiles import require
from world_source import WorldSource


def indexed(source, path, size):
    with Image.open(io.BytesIO(source.read(path))) as image:
        need(image.size == size and image.mode in ('P', 'L'), 'Unexpected indexed geometry: ' + path)
        result = gba_4bpp_indices(image).copy()
        result.putdata([index & 15 for index in result.tobytes()])
        return result


def background(tiles, data, colors, *, foreground=False, shaded=False):
    need(len(data) == 2048 and len(colors) == 64, 'TM Case screenblock/palette geometry differs')
    entries = struct.unpack('<1024H', data)
    out = Image.new('RGBA', (240, 160), (*colors[0], 255) if not foreground else (0, 0, 0, 0))
    for y in range(160):
        for x in range(240):
            entry = entries[y // 8 * 32 + x // 8]
            tile, bank = entry & 1023, entry >> 12
            if shaded and y >= 96: bank = 3
            need(tile < tiles.width * tiles.height // 64 and bank < 4, 'TM Case tile reference outside input')
            px, py = x % 8, y % 8
            index = tiles.getpixel((tile % (tiles.width // 8) * 8 + (7-px if entry & 1024 else px),
                                   tile // (tiles.width // 8) * 8 + (7-py if entry & 2048 else py)))
            # Color zero is transparent on every BG palette bank, revealing the backdrop.
            if index: out.putpixel((x,y), (*colors[bank*16+index], 255))
    return out


def generate(source):
    tm = source.text('src/tm_case.c')
    cursor = source.text('src/pokemon_storage_system_data.c')
    graphics = source.text('src/pokemon_storage_system_graphics.c')
    for text in ['#define DISC_BASE_X 41', '#define DISC_BASE_Y 46', '#define DISC_CASE_DISTANCE 20',
                 '#define DISC_Y_MOVE 10', 'min(sTMCaseDynamicResources->numTMs + 1, 5)',
                 'SetBgTilemapPalette(2, 0, 12, 30, 8, 2 * shade + 1)',
                 'Q_24_8(14 * tmIdx)', 'Q_24_8(8 * tmIdx)']:
        require(tm, text)
    for text in ['ANIMCMD_FRAME(0, 30)', 'ANIMCMD_FRAME(16, 30)', 'ANIMCMD_FRAME(32, 5)',
                 'ANIMCMD_FRAME(48, 5)', 'case 8: // Cursor has reached bottom',
                 'gStorage->cursorSprite->y2++', 'gStorage->cursorSprite->y2--']:
        require(cursor, text)
    for text in ['gStorage->shiftTimer == 16', 'gSineTable[gStorage->shiftTimer * 8] / 16',
                 'gStorage->shiftTimer & 1', 'gStorage->cursorSprite->y + gStorage->cursorSprite->y2 + 4']:
        require(graphics, text)
    outputs, assets = {}, []
    def emit(name, image):
        outputs[name+'.png'] = png(image.width, image.height, image.tobytes())
        assets.append(dict(id=name, texturePath=name+'.png', width=image.width, height=image.height))
    tiles = indexed(source, 'graphics/tm_case/tm_case.png', (64,96))
    for gender in ('male','female'):
        pal = palette(source.read('graphics/tm_case/menu_'+gender+'.pal'))
        for shade in (False,True):
            emit('case-'+gender+('-shaded' if shade else ''), background(tiles, source.read('graphics/tm_case/menu.bin'), pal, shaded=shade))
        emit('case-'+gender+'-front', background(tiles, source.read('graphics/tm_case/tm_case.bin'), pal, foreground=True))
    disc = indexed(source, 'graphics/tm_case/disc.png', (32,64))
    colors = palette(source.read('graphics/tm_case/disc_types_1.pal')) + palette(source.read('graphics/tm_case/disc_types_2.pal'))
    need(len(colors) == 272, 'Disc type palette geometry differs')
    types = re.findall(r'\[(TYPE_\w+)\]\s*=\s*(0x[0-9a-f]+)', tm.split('sTMSpritePaletteOffsetByType',1)[1].split('};',1)[0])
    need(len(types) == 17 and len(set(t[0] for t in types)) == 17, 'Disc type palette bindings differ')
    atlas = Image.new('RGBA',(32*len(types),64))
    for i, (_, off) in enumerate(types):
        atlas.paste(Image.frombytes('RGBA',disc.size,rgba(disc.tobytes(),colors[int(off,16):int(off,16)+16])),(i*32,0))
    emit('discs',atlas)
    tutorial=Image.new('RGBA',atlas.size)
    tutorial.putdata([tuple((v*12//16)*8+(v*12//16)//4 for v in (r>>3,g>>3,b>>3))+(a,)
                      for r,g,b,a in atlas.get_flattened_data()])
    emit('discs-tutorial',tutorial)
    hand = indexed(source, 'graphics/pokemon_storage/cursor.png', (32,128))
    shadow = indexed(source,'graphics/pokemon_storage/cursor_shadow.png',(16,16))
    for mode, palname in [('single','misc2'),('multi','misc1')]:
        pal = palette(source.read('graphics/pokemon_storage/'+palname+'.pal'))
        emit('hand-'+mode,Image.frombytes('RGBA', hand.size, rgba(hand.tobytes(),pal)))
    pal = palette(source.read('graphics/pokemon_storage/misc2.pal'))
    emit('hand-shadow',Image.frombytes('RGBA',shadow.size,rgba(shadow.tobytes(),pal)))
    arrows=indexed(source,'graphics/interface/red_arrow_other.png',(16,32)).crop((0,16,16,32))
    arrow=Image.frombytes('RGBA',arrows.size,rgba(arrows.tobytes(),palette(source.read('graphics/interface/red_arrow.pal'))))
    emit('scroll-up',arrow);emit('scroll-down',arrow.transpose(Image.Transpose.FLIP_TOP_BOTTOM))
    info = indexed(source,'graphics/interface/menu_info.png',(128,128))
    pal = palette(source.read('graphics/interface/pokemon_types.pal'))
    list_code = source.text('src/list_menu.c')
    labels = Image.new('RGBA',(40,48))
    for row, name in enumerate(['TYPE','POWER','ACCURACY','PP']):
        m = re.search(r'\[MENU_INFO_ICON_'+name+r'\]\s*=\s*\{\s*40,\s*12,\s*(0x\w+)\s*\}',list_code)
        need(m is not None, 'Move info label geometry differs')
        off = int(m[1],16); x,y = off%16*8,off//16*8
        labels.paste(Image.frombytes('RGBA',(40,12),rgba(info.crop((x,y,x+40,y+12)).tobytes(),pal)),(0,row*12))
    emit('move-labels',labels)
    hm = indexed(source,'graphics/tm_case/hm.png',(16,16)).crop((0,0,16,12))
    menu_palette=palette(source.read('graphics/interface/std_menu.pal'))
    # InitWindowTemplatesAndPals replaces menu colors 6/7 for the orange HM badge.
    require(tm,'static ALIGNED(4) const u16 sPal3Override[] = {RGB(8, 8, 8), RGB(30, 16, 6)};')
    menu_palette[6]=(66,66,66);menu_palette[7]=(247,132,49)
    emit('hm',Image.frombytes('RGBA',hm.size,rgba(hm.tobytes(),menu_palette)))
    require(source.text('charmap.txt'),'NO            = F9 08')
    with Image.open(io.BytesIO(source.read('graphics/fonts/latin_small.png'))) as font:
        need(font.mode=='P' and font.size==(256,256),'Number symbol font geometry differs')
        glyph=font.crop((64,128,72,141))
        emit('number',Image.frombytes('RGBA',glyph.size,bytes(c for role in glyph.tobytes() for c in
            ((*menu_palette[2 if role==1 else 3],255) if role in (1,2) else (0,0,0,0)))))
    sine = [int(float(v)*256) for v in re.findall(r'Q_8_8\((-?[0-9.]+)\)',source.text('src/trig.c').split('};',1)[0])]
    need(len(sine) >= 249, 'Inventory sine samples missing')
    doc = dict(schemaVersion=1, assets=assets, discTypes=[key for key,_ in types],
               shiftOffsets=[int(sine[t*8]/16) for t in range(17)],
               arrowOffsets=[int(sine[t*8]*2/256) for t in range(32)],
               statForeground=list(pal[14]),statShadow=list(pal[10]))
    outputs['inventory.json'] = canonical(doc)
    return outputs


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=Path,default=PROJECT_ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=Path,default=PROJECT_ROOT/'content/generated/ui/inventory')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    need(not args.out.resolve().is_relative_to(args.raw.resolve()) and not args.raw.resolve().is_relative_to(args.out.resolve()), 'Source/output overlap')
    outputs=generate(WorldSource(args.raw))
    for name,data in outputs.items():
        path=safe(args.out,name)
        if args.check: need(path.read_bytes()==data,'Inventory presentation regeneration differs: '+name)
        else:
            path.parent.mkdir(parents=True,exist_ok=True); path.write_bytes(data)
    print(json.dumps(dict(check=args.check,outputs=sorted(outputs))))


if __name__=='__main__':main()
