#!/usr/bin/env python3
"""Render twelve Mail papers and Easy Chat presentation directly from pinned raw source."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_source import ItemSource
from item_catalog import inventory_item, item_json
import argparse
import json
from pathlib import Path
import re
import struct
import sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
PROFILE = 'firered-mail-presentation-v1'
BASE = 'content/generated/ui/mail-presentation/'
NAMES = ['orange','harbor','glitter','mech','wood','wave','bead','shadow','tropic','dream','fab','retro']

from image_io import png as encode_rgba_png

def need(ok, message):
    if not ok: raise ValueError(message)
def encoded(value): return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, default=item_json)+'\n').encode()
def generate(raw):
    from world_source import WorldSource
    import ui_tiles as codec
    src = WorldSource(raw); outputs={}; assets=[]
    shared_item = ItemSource(src.read).definition
    def capture(path, pattern, symbol):
        text=src.text(path); match=re.search(pattern,text,re.M|re.S); need(match is not None,'Missing source symbol: '+symbol)
        return match, dict(path=path,symbol=symbol,startLine=text[:match.start()].count('\n')+1,
                           endLine=text[:match.end()].count('\n')+1)
    def block(path, name): return capture(path,r'^static const [^\n]*\b'+name+r'\[\][^=]*= \{(.*?)^\};',name)
    png = encode_rgba_png
    def asset(identifier,w,h,pixels,source_paths, reuse=None):
        data=png(w,h,pixels); path=reuse or BASE+identifier+'.png'; shared=reuse is not None
        outputs[path.removeprefix('content/generated/')]=data
        result=dict(id=identifier,width=w,height=h,pngPath=path,shared=shared)
        assets.append(result);return result
    mail=src.text('src/mail.c')
    need('sMailViewResources->mailArrangementType = 1;' in mail and 'FillBgTilemapBufferRect_Palette0(2, 0x001, 0, 0, 30, 20);' in mail,
         'Original mail arrangement/background differs')
    gfx, gfxref=block('src/mail.c','sGfxHeaders'); layout, layoutref=block('src/mail.c','sMessageLayouts_5x2')
    rows,rowref=block('src/mail.c','sLayout_5x2')
    need(re.findall('numWordsInLine = (\\d+)', rows[1]) == ['2', '2', '2', '2', '1'], 'Original 9-word read layout differs')
    need('messageLinesBuffer[i][0] != EOS && sMailViewResources->messageLinesBuffer[i][0] != CHAR_SPACE' in mail and
         'y += sMailViewResources->messageLayout->linesLayout[i].lineHeight;' in mail,'Original empty-line suppression differs')
    gend,gendref=block('src/mail.c','sGenderPals')
    rgb5=[tuple(map(int,c)) for c in re.findall(r'RGB\((\d+),\s*(\d+),\s*(\d+)\)',gend[1])]
    need(rgb5==[(13,22,26),(5,13,20),(28,15,17),(20,6,14)],'Original gender palette differs')
    expand=lambda rgb:[(c<<3)|(c>>2) for c in rgb]
    items=[];stationery=[]
    itemrows=src.json('src/data/items.json')['items']; constants=src.text('include/constants/items.h')
    graphics=src.text('src/data/graphics/mail.h');itemgraphics=src.text('src/data/graphics/items.h')
    for index,name in enumerate(NAMES):
        symbol='ITEM_'+name.upper()+'_MAIL';item_id=121+index
        need(re.search(r'^#define\s+'+symbol+r'\s+'+str(item_id)+r'\s*$',constants,re.M),'Original mail item identity differs')
        header=re.search(r'\[ITEM_TO_MAIL\('+symbol+r'\)\]\s*= \{(.*?)\n    \}',gfx[1],re.S)[1]
        attrs=re.search(r'\[ITEM_TO_MAIL\('+symbol+r'\)\]\s*= \{(.*?)\n    \}',layout[1],re.S)[1]
        attr={k:int(v) for k,v in re.findall(r'\.(\w+) = (\d+),',attrs)}
        need(attr['numRows']==5 and attr['messageTop']==3 and attr['messageLeft']==8,'Mail 5x2 geometry differs')
        colors=[expand(tuple(map(int,c))) for c in re.findall(r'RGB\((\d+),\s*(\d+),\s*(\d+)\)',header)]
        paths=['graphics/mail/'+name+'/'+n for n in ('tiles.png','palette.pal','map.bin')]
        for suffix,decl in [('palette','palette.gbapal'),('tiles','tiles.4bpp.lz'),('map','map.bin.lz')]:
            need('graphics/mail/'+name+'/'+decl in graphics,'Missing original mail graphics binding')
        tiles=codec.tile_list(src.read(paths[0]));pal=codec.palette(src.read(paths[1]));data=src.read(paths[2])
        size=int(re.search(r'\.size = (0x\w+)',header)[1],16)
        need(size > 0 and len(data)==1280,'Mail tilemap dimensions differ')
        tilemap=struct.unpack('<640H',data)
        need(all(v>>12==0 and (v&1023)<len(tiles) for v in tilemap),'Mail source tile references differ')
        ids=[]
        for gender in (0,1):
            palette=list(pal);palette[10:12]=[expand(v) for v in rgb5[gender*2:gender*2+2]];out=codec.canvas(240,160)
            for y in range(20):
                for x in range(30):
                    entry=tilemap[y*32+x];tile=tiles[entry&1023]
                    indices=[tile[(7-py if entry&2048 else py)*8+(7-px if entry&1024 else px)] or tiles[1][py*8+px] for py in range(8) for px in range(8)]
                    codec.blit(out,240,codec.rgba(indices,palette,False),8,8,x*8,y*8)
            identifier=name+'-mail-'+('male' if gender==0 else 'female');ids.append(identifier)
            asset(identifier,240,160,bytes(out),paths,'content/generated/ui/npc-trade-mail/'+identifier+'.png' if name=='fab' else None)
        item=next(i for i in itemrows if i['itemId']==symbol)
        need(item['type']=='ITEM_TYPE_MAIL' and item['price']==50 and item['secondaryId']==index and item['holdEffect']=='HOLD_EFFECT_NONE', 'Mail inventory metadata differs')
        _,itemref=capture('src/data/items.json',r'^    \{\n      "english": "'+item['english']+r'",.*?^    \}',symbol)
        meta=shared_item(symbol)
        definition=dict(item=meta)
        definition.update(description=item['description_english'].replace('\\n','\n'), healHp=0, cureStatus=0, effectBytes=[], useSupport=10)
        items.append(definition)
        binding,_=capture('src/data/item_icon_table.h',r'^    \['+symbol+r'\]\s*= \{(\w+), (\w+)\},',symbol)
        ipaths=[]
        for b in binding.groups():
            p=re.search(r'^const u32 '+b+r'\[\]\s*= INCBIN_U32\("([^"]+)"\);',itemgraphics,re.M)[1]
            ipaths.append(p.replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
        w,h,indices,_=codec.indexed_png(src.read(ipaths[0]));need((w,h)==(24,24),'Mail item icon dimensions differ')
        pixels=codec.rgba(indices,codec.palette(src.read(ipaths[1])))
        reuse={'fab':'content/generated/ui/npc-trade-mail/item-fab-mail.png','retro':'content/generated/data/services/department-fourth-floor/retro-mail.png','dream':'content/generated/sprites/pokemon/six-island/item-dream-mail.png'}.get(name)
        icon=asset('item-'+name+'-mail',w,h,pixels,ipaths,reuse)
        stationery.append(dict(itemId=item_id, itemSymbol=symbol, name=item['english'], backgroundIds=ids, itemIconId=icon['id'], messageX=32, messageY=35, lineHeight=16, wordsPerLine=[2, 2, 2, 2, 1], authorX=120 + attr['nameX'], authorY=120 + attr['nameY'], authorPrefix='From ', textRgb=colors[0], shadowRgb=colors[1], iconKind=name if name in ('bead', 'dream') else 'none', iconCenterX=96 if name == 'bead' else 40 if name == 'dream' else 0, iconCenterY=128 if name in ('bead', 'dream') else 0))
    # Preserve species indirection as data. The active composition supplies existing canonical and 28 Unown textures.
    iconcode=src.text('src/pokemon_icon.c');maildata=src.text('src/mail_data.c')
    need('value += (SPECIES_UNOWN_B - 1);' in iconcode and '#define UNOWN_OFFSET 30000' in maildata and
         'species == SPECIES_DEOXYS && extra == TRUE' in iconcode,'Original mail species/icon mapping differs')
    src.read('include/constants/species.h')
    # Settled Easy Chat surfaces. Keep source sheets; only three compact background PNGs are materialized.
    ec=src.text('src/easy_chat_3.c');ec2=src.text('src/easy_chat_2.c');sourcegfx=src.text('src/graphics.c')
    need('.type = EASY_CHAT_TYPE_MAIL,\n        .numColumns = 2,\n        .numRows = 5,\n        .frameId = 2,' in ec2,'Original Mail editor template differs')
    need('RedrawFrameByRect(1, 10, 23, 10);' in ec and 'RedrawFrameByRect(0, 10, 30, 10);' in ec and
         'ChangeBgY(2, 0x800, 0);' in ec,'Original picker frame/scroll differs')
    wpath='graphics/easy_chat/window.png';ww,wh,wi,wp=codec.indexed_png(src.read(wpath));wt=codec.tile_list(src.read(wpath));wpal=codec.colors(wp)
    wdata=src.read('graphics/easy_chat/window.bin');need(len(wdata)==1280,'Easy Chat map dimensions differ');wmap=struct.unpack('<640H',wdata)
    fpath='graphics/easy_chat/text_input_frame.png';ft=codec.tile_list(src.read(fpath))
    orange=codec.palette(src.read('graphics/easy_chat/text_input_frame_orange.pal'));green=codec.palette(src.read('graphics/easy_chat/text_input_frame_green.pal'))
    textrows=src.text('graphics/easy_chat/text.pal').splitlines();need(textrows[:3]==['JASC-PAL','0100','5'] and len(textrows)==8,'Easy Chat text palette differs');textpal=[expand([int(c)>>3 for c in row.split()]) for row in textrows[3:]]
    def tile(out,ts,pal,n,x,y):
        rgba=codec.rgba(ts[n&1023],pal)
        for py in range(8):
            for px in range(8):
                source=(py*8+px)*4;target=((y*8+py)*240+x*8+px)*4
                if rgba[source+3]:out[target:target+4]=rgba[source:source+4]
    for mode in ('fields','groups','words'):
        out=codec.canvas(240,160)
        for y in range(20):
            for x in range(30):
                e=wmap[y*32+x];need(e>>12==0,'Easy Chat base palette differs')
                codec.blit(out,240,codec.tile_rgba(wt[e&1023],wpal,bool(e&1024),bool(e&2048),False),8,8,x*8,y*8)
        # Source phrase window overwrites BG3 pixels and the orange frame has transparent center tile 0.
        codec.blit(out,240,bytes([*textpal[1],255])*192*80,192,80,24,0)
        codec.blit(out,240,bytes([*textpal[1],255])*192*16,192,16,32,88)
        for y in range(10):
            for x in range(2,28): tile(out,ft,orange,5 if x==2 else 7 if x==27 else 0,x,y)
        if mode!='fields':
            left,width=(1,23) if mode=='groups' else (0,30);right=left+width-1
            # Window0 exposes BG2 only inside the green frame; BG2 source origin is y=-8.
            codec.blit(out,240,bytes([*textpal[1],255])*(width-2)*8*64,(width-2)*8,64,(left+1)*8,88)
            for y in range(10,20):
                for x in range(left,right+1):
                    n=(1 if x==left else 3 if x==right else 2) if y==10 else (9 if x==left else 11 if x==right else 10) if y==19 else (5 if x==left else 7 if x==right else 0)
                    tile(out,ft,green,n,x,y)
        asset('easy-chat-'+mode,240,160,bytes(out),[wpath,'graphics/easy_chat/window.bin',fpath,'graphics/easy_chat/text_input_frame_orange.pal','graphics/easy_chat/text_input_frame_green.pal','graphics/easy_chat/text.pal'])
    for name in ('triangle_cursor','rectangle_cursor','button_window','mode','scroll_indicator','start_select_buttons'):
        path='graphics/easy_chat/'+name+'.png';w,h,indices,pal=codec.indexed_png(src.read(path))
        if name in ('mode','scroll_indicator','start_select_buttons'):
            _,_,_,pal=codec.indexed_png(src.read('graphics/easy_chat/button_window.png'))
        asset('easy-chat-'+name.replace('_','-'),w,h,codec.rgba([v%16 for v in indices],codec.colors(pal)),[path])
    # UNDERSCORE is source extended glyph F9 09, absent from the shared English character atlas.
    from PIL import Image
    import io
    fontpath='graphics/fonts/latin_normal.png';font=Image.open(io.BytesIO(src.read(fontpath)))
    need(font.mode=='P' and font.size==(256,512),'Source normal font shape differs')
    glyph=0x109;widths=re.search(r'static const u8 sFontNormalCopy1LatinGlyphWidths\[\]\s*=\s*\{([\s\d,]+)\};',src.text('src/text.c'))
    advance=int(re.findall(r'\d+',widths[1])[glyph])
    need('UNDERSCORE    = F9 09' in src.text('charmap.txt') and advance==10,'Source underscore differs')
    roles=list(font.crop(((glyph%16)*16,(glyph//16)*16,(glyph%16)*16+16,(glyph//16)*16+14)).tobytes())
    pixels = bytes((c for role in roles for c in ([*textpal[4], 255] if role == 1 else [*textpal[3], 255] if role == 2 else [0, 0, 0, 0])))
    asset('easy-chat-underscore',16,14,pixels,[fontpath,'src/text.c','charmap.txt'])
    specials=[];special_pixels=codec.canvas(96,14)
    for i,glyph in enumerate([0x53,0x55,0x56,0x57,0x58,0x59]):
        roles=list(font.crop(((glyph%16)*16,(glyph//16)*16,(glyph%16)*16+16,(glyph//16)*16+14)).tobytes())
        pixels = bytes((c for role in roles for c in ([*textpal[2], 255] if role == 1 else [*textpal[3], 255] if role == 2 else [0, 0, 0, 0])))
        codec.blit(special_pixels,96,pixels,16,14,i*16,0)
        specials.append(dict(sourceGlyph=glyph,codepoint=0xE100+i,x=i*16,width=16,height=14,advance=int(re.findall(r'\d+',widths[1])[glyph])))
    need('PKMN        = 53 54' in src.text('charmap.txt') and 'POKEBLOCK   = 55 56 57 58 59' in src.text('charmap.txt'),'Easy Chat glyph token binding differs')
    asset('easy-chat-special-glyphs',96,14,bytes(special_pixels),[fontpath,'src/text.c','charmap.txt'])
    textsyms=['gText_From','gText_CombineNineWordsOrPhrases','gText_AndMakeAMessage','gText_TheMailMessage','gText_IsAsShownOkay',
        'gText_QuitEditing','gText_StopGivingPkmnMail','gText_DelAllCancelOk','gText_AllTextBeingEditedWill','gText_BeDeletedThatOkay']
    texts=[]
    for symbol in textsyms:
        m,ref=capture('src/strings.c',r'^const u8 '+symbol+r'\[\] = _\("((?:[^"\\]|\\.)*)"\);',symbol)
        texts.append(dict(symbol=symbol,text=m[1].replace('\\n','\n'),source=ref))
    document=dict(schemaVersion=1,assets=assets,stationery=stationery,items=items,
        texts=texts,
        iconRules=dict(unownOffset=30000,unownForms=28,unownSpecies=201,unownB=413,maxSpecies=411,frame=0),
        easyChat=dict(fieldColumns=2,fieldRows=5,lastRowColumns=1,fieldX=41,fieldY=2,fieldAdvance=17,blankAdvance=advance,lineHeight=16,
            groupTextX=[18,102],wordTextX=[32,136],pickerTextY=90,pickerRows=4,footerTextX=[32,119,196],footerTextY=90,
            alphabetColumnOffsets=[0,12,24,56,68,80,92],textRgb=textpal[2],shadowRgb=textpal[3],blankRgb=textpal[4],specialGlyphs=specials))
    outputs['ui/mail-presentation/catalog.json']=encoded(document)
    return outputs


def main():
    from generated_output_compare import check_item_outputs
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=Path,default=ROOT/'content/generated')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args(); outputs=generate(args.raw)
    if args.check: check_item_outputs(args.out,outputs)
    else:
        for name,data in outputs.items():
            target=args.out/name;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(check=args.check,outputs=len(outputs))))

if __name__=='__main__':main()
