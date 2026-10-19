#!/usr/bin/env python3
"""Convert pinned pret Summary tilemaps to generated-only native page textures.

Inputs: pret/pokefirered 037335f4c725d7c9aecdac87066f2002b4bd7e14.
No reference scripts are executed; pixel decoding uses the shared tile library.
"""
import argparse
from pathlib import Path
import struct
import re
import json
import ast
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / 'scripts/lib'))
from image_io import indexed_png, png
from ui_tiles import tile_list, colors, palette, canvas, blit, tile_rgba
from project_paths import PROJECT_ROOT

PAGES = ('info', 'skills', 'moves', 'moves_info', 'egg')

def source_name_table(text, table, prefix):
    """Read one named source table; never derive labels from identifiers."""
    tables = re.findall(r'\b' + table + r'\[[^;=]+\]\s*=\s*\{(.*?)^\};', text, re.M | re.S)
    if len(tables) != 1:
        raise ValueError('Missing or ambiguous display-name table: ' + table)
    row = re.compile(r'\[(' + prefix + r'\w+)\]\s*=\s*_\("((?:[^"\\]|\\.)*)"\),')
    pairs = [(symbol, ast.literal_eval('"' + value + '"')) for symbol, value in row.findall(tables[0])]
    names = dict(pairs)
    if not names or len(names) != len(pairs) or row.sub('', tables[0]).strip() or any(not name.strip() for name in names.values()):
        raise ValueError('Invalid display-name rows: ' + table)
    return names


def display_names(raw):
    abilities = source_name_table((raw / 'src/data/text/abilities.h').read_text(), 'gAbilityNames', 'ABILITY_')
    types = source_name_table((raw / 'src/battle_main.c').read_text(), 'gTypeNames', 'TYPE_')
    none = re.findall(r'\bgText_PokeSum_Item_None\[\] = _\("((?:[^"\\]|\\.)*)"\);',
                      (raw / 'src/strings.c').read_text())
    if len(none) != 1 or not none[0].strip():
        raise ValueError('Missing or ambiguous Summary no-item text')
    return dict(schemaVersion=1, types=types, abilities=abilities, noHeldItem=ast.literal_eval('"' + none[0] + '"'))


def convert(raw):
    root = raw / 'graphics/summary_screen'
    data = (root / 'bg.png').read_bytes()
    w, h, _, rgb = indexed_png(data)
    if (w,h,len(rgb)) != (128,256,112):
        raise ValueError('Summary tile/palette geometry differs')
    tiles, pals = tile_list(data), colors(rgb)
    def render(name, shiny, opaque=False):
        entries = list(struct.iter_unpack('<H', (root / name).read_bytes()))
        if len(entries) not in (640,1024):
            raise ValueError('Summary screen block geometry differs')
        out = canvas(240,160)
        for y in range(20):
            for x in range(30):
                entry = entries[y*32+x][0]
                tile, bank = entry & 1023, entry >> 12
                if tile >= len(tiles) or bank > 4: raise ValueError('Unsupported Summary tile reference')
                if shiny and bank < 2: bank = 6-bank
                blit(out,240,tile_rgba(tiles[tile],pals[bank*16:bank*16+16],bool(entry&1024),bool(entry&2048),not opaque),8,8,x*8,y*8)
        return out
    outputs = {}
    progress_source=(raw/'src/pokemon_summary_screen.c').read_text().split('static void PokeSum_DrawPageProgressTiles(void)\n{',1)[1].split('static void PokeSum_PrintMonTypeIcons',1)[0]
    progress_base=345
    def progress(out,page,shiny):
        case='INFO' if page=='egg' else page.upper()
        block=progress_source.split('case PSS_PAGE_'+case+':',1)[1].split('break;',1)[0]
        if case=='INFO':
            parts=block.split('else',1)
            block=parts[1] if page=='egg' else parts[0]
        elif case=='MOVES_INFO':
            block=block.split('else',1)[1]
        for tile,x,y,w,h in re.findall(r'FillBgTilemapBufferRect\(3,\s*(\d+) \+ PAGE_PROGRESS_BASE_TILE_NUM, (\d+), (\d+), (\d+), (\d+), 0\)',block):
            for yy in range(int(h)):
                for xx in range(int(w)):
                    pal=pals[96:112] if shiny else pals[:16]
                    blit(out,240,tile_rgba(tiles[int(tile)+progress_base],pal,transparent=False),8,8,(int(x)+xx)*8,(int(y)+yy)*8)
    for shiny in (False,True):
        for page in PAGES:
            back = render('moves_info_page.bin',shiny,True)
            front = render('page_'+page+'.bin',shiny)
            for i in range(0,len(front),4):
                if front[i+3]:back[i:i+4]=front[i:i+4]
            if page == 'moves_info':
                right = render('page_moves.bin',shiny)
                for y in range(16,160):
                    for x in range(120,240):
                        i=(y*240+x)*4
                        if right[i+3]:back[i:i+4]=right[i:i+4]
            progress(back,page,shiny)
            outputs[page+('-shiny' if shiny else '')+'.png']=png(240,160,back)
    cursor_pal=palette((root/'move_selection_cursor.pal').read_bytes())
    for side in ('left','right'):
        data=(root/f'move_selection_cursor_{side}.png').read_bytes()
        w,h,indices,_=indexed_png(data)
        if (w,h)!=(64,64):raise ValueError('Summary cursor geometry differs')
        outputs['cursor-'+side+'.png']=png(w,h,bytes(c for i in indices for c in (*cursor_pal[i],0 if i==0 else 255)))
    _,_,_,bar_rgb=indexed_png((root/'exp_bar.png').read_bytes())
    for name,source,pal in [('hp-green','hp_bar',colors(bar_rgb)),('hp-yellow','hp_bar',palette((root/'hp_bar_yellow.pal').read_bytes())),('hp-red','hp_bar',palette((root/'hp_bar_red.pal').read_bytes())),('exp','exp_bar',colors(bar_rgb))]:
        frames=tile_list((root/(source+'.png')).read_bytes())
        if len(frames)!=12:raise ValueError('Summary bar geometry differs')
        bar=canvas(96,8)
        for i,tile in enumerate(frames):blit(bar,96,tile_rgba(tile,pal),8,8,i*8,0)
        outputs[name+'.png']=png(96,8,bar)
    w,h,indices,rgb=indexed_png((root/'shiny_star.png').read_bytes())
    pal=colors(rgb)
    outputs['shiny-star.png']=png(w,h,bytes(c for i in indices for c in (*pal[i],0 if i==0 else 255)))
    marking_tiles=tile_list((raw/'graphics/misc/mon_markings.png').read_bytes())
    marking_pal=palette((root/'marking.pal').read_bytes())
    markings=canvas(32,128)
    for combo in range(16):
        for col in range(4):blit(markings,32,tile_rgba(marking_tiles[combo*4+col],marking_pal),8,8,col*8,combo*8)
    outputs['markings.png']=png(32,128,markings)
    def strings(path, prefix=""):
        text=(raw/path).read_text()
        values={name:ast.literal_eval('"'+value+'"') for name,value in re.findall(r'(\w+)\[\] = _\("((?:[^"\\]|\\.)*)"\);',text) if name.startswith(prefix)}
        return text,values
    move_text,move_values=strings('src/move_descriptions.c')
    moves={symbol:move_values[name] for symbol,name in re.findall(r'\[(MOVE_\w+)\s*- 1\] = (\w+)',move_text)}
    ability_text,ability_values=strings('src/data/text/abilities.h')
    abilities={symbol:ability_values[name] for symbol,name in re.findall(r'\[(ABILITY_\w+)\] = (\w+)',ability_text) if name in ability_values}
    if not moves or not abilities:raise ValueError('Missing Summary descriptions')
    _,all_strings=strings('src/strings.c','gText_PokeSum_')
    memo_keys=('MetInATrade','FatefulEncounterMet','Met','ApparentlyMet','Hatched','FatefulEncounterHatched','ApparentlyFatefulEncounterHatched')
    memos={key:all_strings['gText_PokeSum_'+key] for key in memo_keys}
    sections=json.loads((raw/'src/data/region_map/region_map_sections.json').read_text())['map_sections']
    first=next(i for i,x in enumerate(sections) if x['id']=='MAPSEC_PALLET_TOWN')
    locations={x['id']:x['name'] for x in sections[first:]}
    source=(raw/'src/pokemon_summary_screen.c').read_text()
    motions={name:[int(v) for v in body.replace('\n','').split(',') if v.strip()] for name,body in re.findall(r'static const s8 (s(?:MonPicBounceYDelta|EggPicShakeXDelta)_\w+)\[\] =\s*\{(.*?)\};',source,re.S)}
    species=(raw/'src/data/pokemon/species_info.h').read_text()
    facing={symbol:flag=='FALSE' for symbol,body in re.findall(r'\[(SPECIES_\w+)\]\s*=\s*\{[ \t]*\n(.*?)\n    \}',species,re.S) for flag in re.findall(r'\.noFlip\s*=\s*(TRUE|FALSE)',body)}
    if len(facing)<386 or 'SPECIES_NONE' in facing or 'SPECIES_BULBASAUR' not in facing:raise ValueError('Missing Summary species facing data')
    outputs['text.json']=(json.dumps(dict(moves=moves,abilities=abilities,memos=memos,locations=locations,motions=motions,facing=facing),ensure_ascii=False,sort_keys=True)+'\n').encode()
    outputs['display-names.json']=(json.dumps(display_names(raw),ensure_ascii=False,sort_keys=True)+'\n').encode()
    return outputs

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--raw',type=Path,default=PROJECT_ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--target',type=Path,default=PROJECT_ROOT/'content/generated/ui/summary-screen')
    parser.add_argument('--check',action='store_true')
    args=parser.parse_args()
    result=convert(args.raw)
    for name,data in result.items():
        target=args.target/name
        if args.check:
            if not target.is_file() or target.read_bytes()!=data:raise ValueError('Summary output differs: '+name)
        else:
            target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(f'Summary: {len(result)} generated artifacts verified' if args.check else f'Summary: {len(result)} artifacts generated')
