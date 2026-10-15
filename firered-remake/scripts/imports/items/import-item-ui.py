#!/usr/bin/env python3
"""Bounded source-tile reconstruction of opening Potion PC/bag pages. No ROM or upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse,importlib.util,json,pathlib,re,struct,sys
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result
inputs=module('item_ui_inputs',ROOT/'scripts/imports/source/fetch-item-ui.py')
# Share the reviewed local PNG/tile codec, never an upstream tool or script.
codec=module('item_ui_codec',ROOT/'scripts/imports/presentation/import-lab-battle-ui.py')
TEXT_SYMBOLS = '''gOtherText_Use gOtherText_Give gOtherText_Toss gFameCheckerText_Cancel
gText_TimesStrVar1 gText_Items2 gText_DepositItem gText_CloseBag gText_Var1IsSelected
gText_DepositedStrVar2StrVar1s gText_NoRoomToStoreItems gText_NoMoreRoomInBag
gText_WithdrawHowMany gText_DepositHowManyStrVars1 gText_WithdrewQuantItem
gText_WithdrawItem gText_Withdraw gText_ReturnToPC gText_ThereIsNoPokemon
gText_ItemStorage gText_Mailbox gText_TurnOff gText_WithdrawItem2 gText_DepositItem2
gText_TakeOutItemsFromThePC gText_StoreItemsInThePC gText_GoBackToThePreviousMenu
gText_WhatWouldYouLikeToDo gText_ThereAreNoItems gText_TheresNoMailHere
gText_SelectorArrow2'''.split()

def import_text(sources):
    source=sources['src/strings.c'].decode('utf-8');texts={};refs={}
    for symbol in TEXT_SYMBOLS:
        matches=list(re.finditer(r'^const u8 '+re.escape(symbol)+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);',source,re.M))
        if len(matches)!=1:raise ValueError('Expected one source string: '+symbol)
        m=matches[0];value=json.loads(m[1]);controls=[]
        if value.endswith('{PAUSE_UNTIL_PRESS}'):
            value=value.removesuffix('{PAUSE_UNTIL_PRESS}');controls=['PAUSE_UNTIL_PRESS']
        if any(t not in ('STR_VAR_1','STR_VAR_2') for t in re.findall(r'\{([^}]+)\}',value)):raise ValueError('Unsupported display text control')
        texts[symbol]=value;refs[symbol]=dict(path='src/strings.c',symbol=symbol,line=source[:m.start()].count('\n')+1,terminalControls=controls)
    items=json.loads(sources['src/data/items.json']);potions=[i for i in items['items'] if i.get('itemId')=='ITEM_POTION']
    if len(potions)!=1:raise ValueError('Expected one Potion text record')
    p=potions[0]
    for key,field in [('ITEM_POTION_NAME','english'),('ITEM_POTION_DESCRIPTION','description_english')]:
        value=p[field].replace(r'\n','\n')
        if '\\' in value or '{' in value:raise ValueError('Unsupported Potion display text control')
        texts[key]=value;refs[key]=dict(path='src/data/items.json',symbol='ITEM_POTION',field=field)
    return texts,refs

def composite(dest,dw,source,sw,sh,x,y):
    if x<0 or y<0 or x+sw>dw or y+sh>len(dest)//dw//4:raise ValueError('Composite bounds differ')
    for row in range(sh):
        for col in range(sw):
            si=(row*sw+col)*4;di=((row+y)*dw+col+x)*4
            if source[si+3]==255:dest[di:di+4]=source[si:si+4]
            elif source[si+3]!=0:raise ValueError('Only binary GBA transparency is supported')

def render_map(source_png,tilemap):
    tiles=codec.tile_list(source_png);pal=codec.colors(codec.indexed_png(source_png)[3])
    if len(tilemap)!=2048 or len(pal)!=48:raise ValueError('Expected 32x32 tilemap and three palette banks')
    entries=struct.unpack('<1024H',tilemap);out=codec.canvas(240,160,(*pal[0],255))
    for y in range(20):
        for x in range(30):
            e=entries[y*32+x];tid=e&1023;bank=e>>12
            if tid>=len(tiles) or bank>=3:raise ValueError('Tile reference outside locked source')
            composite(out,240,codec.tile_rgba(tiles[tid],pal[bank*16:(bank+1)*16],bool(e&1024),bool(e&2048)),8,8,x*8,y*8)
    return bytes(out)

def frame(pixels,w,h):
    if w<24 or h<24 or w%8 or h%8:raise ValueError('Frame must use whole 8px tiles')
    out=codec.canvas(w,h)
    for y in range(0,h,8):
        for x in range(0,w,8):
            sx=0 if x==0 else 16 if x==w-8 else 8;sy=0 if y==0 else 16 if y==h-8 else 8
            tile=b''.join(pixels[((sy+i)*24+sx)*4:((sy+i)*24+sx+8)*4] for i in range(8))
            codec.blit(out,w,tile,8,8,x,y)
    return bytes(out)

def convert(lock,sources):
    for path,needle in [
        ('src/item_pc.c','gMultiuseListMenuTemplate.lettersSpacing = 1;'),
        ('src/item_pc.c','ItemPc_AddTextPrinterParameterized(windowId, FONT_SMALL, gStringVar4, 110, y, 0, 0, 0xFF, 1);'),
        ('src/item_menu.c','gMultiuseListMenuTemplate.lettersSpacing = 0;'),
        ('src/item_menu.c','LZDecompressWram(gBagBg_ItemPC_Tilemap, sBagBgTilemapBuffer);'),
        ('src/bag.c','u32 x = 0x48 - GetStringWidth(FONT_NORMAL_COPY_1, str, 0);'),
        ('src/bag.c','x = 0x40 - GetStringWidth(FONT_SMALL, gText_DepositItem, 0);'),
        ('src/item_menu_icons.c','ANIMCMD_FRAME(0x80, 0)'),
        ('src/item_menu_icons.c','CreateSprite(&sSpriteTemplate_Bag, 40, 68, 0)'),
        ('src/item_menu_icons.c','gSprites[spriteId].x2 = 24;'),
        ('src/item_menu_icons.c','gSprites[spriteId].y2 = 140;'),
        ('src/text.c','if (textPrinter->japanese)\n                textPrinter->printerTemplate.currentX += (gGlyphInfo.width + textPrinter->printerTemplate.letterSpacing);\n            else\n                textPrinter->printerTemplate.currentX += gGlyphInfo.width;'),
        ('src/data/item_icon_table.h','[ITEM_POTION]         = {gItemIcon_Potion, gItemIconPalette_Potion}'),
        ('tools/gbagfx/convert_png.c','% (1 << destBitDepth)')]:codec.require(sources[path].decode(),needle)
    assets=[];outputs={}
    def add(id,w,h,pixels,x=0,y=0):
        data=codec.png(w,h,pixels);outputs[id+'.png']=data
        assets.append(dict(id=id,texturePath=id+'.png',width=w,height=h,x=x,y=y))
    pc=render_map(sources['graphics/item_pc/bg.png'],sources['graphics/item_pc/bg.bin'])
    add('pc-withdraw-background',240,160,pc)
    stdpal=codec.palette(sources['graphics/text_window/stdpal_3.pal'])
    std=codec.rgba(codec.indexed_png(sources['graphics/text_window/std.png'])[2],stdpal)
    add('std-frame',24,24,std)
    # OBJ tile offset 0x80 = settled male ordinary-items frame, centered at (40,68).
    bw,bh,bag_indices,_=codec.indexed_png(sources['graphics/interface/bag_male.png'])
    if (bw,bh)!=(64,256):raise ValueError('Bag sprite layout differs')
    bag=codec.rgba(bag_indices[128*64:192*64],codec.palette(sources['graphics/interface/bag.pal']))
    for id,mapname in [('bag-items-background','bg.bin'),('pc-deposit-background','bg_item_pc.bin')]:
        out=bytearray(render_map(sources['graphics/item_menu/bg.png'],sources['graphics/item_menu/'+mapname]))
        composite(out,240,bag,64,64,8,36)
        # Deposit mode replaces the pocket label with the source standard window.
        if id=='pc-deposit-background':composite(out,240,frame(std,80,32),80,32,0,0)
        add(id,240,160,bytes(out))
    pw,ph,pi,_=codec.indexed_png(sources['graphics/items/icons/potion.png'])
    if (pw,ph)!=(24,24):raise ValueError('Potion icon layout differs')
    # Source pads 3x3 tiles to 4x4, then draws the 32px object centered at (24,140).
    potion=codec.rgba(pi,codec.palette(sources['graphics/items/icon_palettes/potion.pal']))
    add('potion',24,24,potion,8,124)
    aw,ah,ai,_=codec.indexed_png(sources['graphics/interface/red_arrow_other.png'])
    if (aw,ah)!=(16,32):raise ValueError('Pocket arrow layout differs')
    right=bytes(ai[y*16+(15-x)] for y in range(16) for x in range(16))
    add('pocket-right-arrow',16,16,codec.rgba(right,codec.palette(sources['graphics/interface/red_arrow.pal'])),64,64)
    menu=codec.palette(sources['graphics/interface/std_menu.pal']);desc=codec.palette(sources['graphics/text_window/stdpal_2.pal']);bagpal=codec.palette(sources['graphics/item_menu/bag_window_pal.pal'])
    display = dict(logicalWidth=240, logicalHeight=160, textureFilter='nearest', paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)', pcListWindow=[56, 8, 152, 96], bagListWindow=[88, 8, 144, 96], pcListText=[65, 10], bagListText=[97, 10], pcListCursor=[57, 10], bagListCursor=[89, 10], pcQuantityText=[166, 10], bagQuantityText=[198, 10], rowStep=16, pcListLetterSpacing=0, bagListLetterSpacing=0, quantityFont='small', quantityLetterSpacing=0, quantityFormat='source multiplication glyph + right-aligned three-character quantity', descriptionText=[40, 115], descriptionLetterSpacing=0, descriptionLineStep=14, descriptionFont='normal', pcTitleText=[8, 9], pcTitleFont='small', pcTitleLineStep=14, pcTitle='WITHDRAW\nITEM', bagTitleCenter=[44, 9], bagTitleFont='normal', bagTitle='ITEMS', depositTitleCenter=[40, 9], depositTitleFont='small', depositTitle='DEPOSIT ITEM', pcActionFrame=[168, 96, 72, 64], pcActionText=[184, 106], pcActionCursor=[176, 106], pcSelectedFrame=[40, 112, 128, 48], pcSelectedText=[48, 122], pcResultFrame=[40, 112, 200, 48], pcResultText=[48, 122], popupLetterSpacing=0, popupLineStep=14, battleBagActionFrame=[168, 120, 72, 40], battleBagActionText=[184, 130], battleBagActionCursor=[176, 130], bagSelectedFrame=[40, 112, 128, 48], bagSelectedText=[48, 122], potionRect=[8, 124, 24, 24], pocketRightArrowRect=[64, 64, 16, 16], menuTextRgb=list(menu[2]), menuShadowRgb=list(menu[3]), titleTextRgb=list(menu[1]), titleShadowRgb=list(menu[2]), descriptionTextRgb=list(desc[10]), descriptionShadowRgb=list(desc[2]), bagDescriptionTextRgb=list(bagpal[1]), bagDescriptionShadowRgb=list(bagpal[2]), sourceDeclaredLetterSpacing=dict(pcList=1, description=2, popup=1), englishLetterSpacingRule='src/text.c ignores letterSpacing outside Japanese glyph rendering')
    texts,text_refs=import_text(sources)
    catalog=dict(schemaVersion=1,repository=inputs.REPOSITORY,display=display,texts=texts,textSourceRefs=text_refs,assets=assets)
    outputs['item-ui.json']=inputs.canonical(catalog)
    outputs['manifest.json']=runtime_json_bytes(dict(schemaVersion=1,profile=inputs.PROFILE,commit=inputs.COMMIT,outputs=[dict(path=p) for p,d in sorted(outputs.items())],), sort_keys=True)
    return outputs

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--output',type=pathlib.Path,default=ROOT/'content/generated/ui/item-ui');p.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/items/item-ui.lock.json');a=p.parse_args()
    if a.output.resolve().is_relative_to(a.raw.resolve()) or a.raw.resolve().is_relative_to(a.output.resolve()):raise ValueError('Raw/output roots overlap')
    lock=inputs.load_lock(a.lock);outputs=convert(lock,inputs.read_verified(lock,a.raw));targets=[(inputs.safe_path(a.output,n),d) for n,d in outputs.items()];a.output.mkdir(parents=True,exist_ok=True)
    for target,data in targets:
        stage=inputs.safe_path(a.output,target.name+'.tmp');stage.write_bytes(data);stage.replace(target)
    print(json.dumps({'status':'imported','assets':len(outputs)-2}))
if __name__=='__main__':main()
