#!/usr/bin/env python3
"""Import authenticated, bounded Kanto Town Map from immutable local pret Git objects.
No ROM, network, upstream script execution, Fly or invented Sevii unlocks.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from item_source import ItemSource
from item_catalog import inventory_item, item_json

import argparse, json, os, pathlib, re, struct, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-town-map-v1'
MAPS = {
 'PalletTown':'map.pallet-town', 'PalletTown_PlayersHouse_1F':'map.players-house-1f',
 'PalletTown_PlayersHouse_2F':'map.players-house-2f', 'PalletTown_RivalsHouse':'map.rivals-house',
 'PalletTown_ProfessorOaksLab':'map.professor-oaks-lab', 'Route1':'map.route-1',
 'ViridianCity':'map.viridian-city', 'ViridianCity_Mart':'map.viridian-mart',
 'ViridianCity_PokemonCenter_1F':'map.viridian-pokemon-center-1f', 'Route2':'map.route-2',
 'Route2_ViridianForest_SouthEntrance':'map.route-2-forest-south-gate',
 'Route2_ViridianForest_NorthEntrance':'map.route-2-forest-north-gate',
 'ViridianForest':'map.viridian-forest', 'PewterCity':'map.pewter-city',
 'PewterCity_Gym':'map.pewter-gym', 'PewterCity_Mart':'map.pewter-mart',
 'PewterCity_PokemonCenter_1F':'map.pewter-pokemon-center-1f',
 'PewterCity_House1':'map.pewter-house-1', 'PewterCity_House2':'map.pewter-house-2',
 'PewterCity_Museum_1F':'map.pewter-museum-1f','PewterCity_Museum_2F':'map.pewter-museum-2f'
}
PATHS = frozenset("""src/region_map.c
src/text.c
charmap.txt
graphics/fonts/keypad_icons.png
src/item_use.c
src/strings.c
src/data/items.json
src/data/items.json.txt
src/data/item_icon_table.h
src/data/graphics/items.h
src/item_menu.c
graphics/items/icons/town_map.png
graphics/items/icon_palettes/town_map.pal
src/data/region_map/region_map_sections.json
src/data/region_map/region_map_sections.entries.json.txt
src/data/region_map/region_map_sections.constants.json.txt
src/data/region_map/region_map_layout_kanto.h
include/constants/items.h
include/constants/flags.h
include/constants/vars.h
include/region_map.h
include/text.h
include/characters.h
include/gba/io_reg.h
data/layouts/layouts.json
data/maps/map_groups.json
data/maps/PalletTown_RivalsHouse/scripts.inc
data/maps/PalletTown_RivalsHouse/text.inc
data/maps/PalletTown_ProfessorOaksLab/scripts.inc
graphics/region_map/kanto.bin
graphics/region_map/region_map.png
graphics/region_map/region_map.pal
graphics/region_map/map_edge.bin
graphics/region_map/map_edge.png
graphics/region_map/top_bar.pal
graphics/region_map/cursor.png
graphics/region_map/cursor.pal
graphics/region_map/player_icon_red.png
graphics/region_map/player_icon_red.pal
graphics/region_map/player_icon_leaf.png
graphics/region_map/player_icon_leaf.pal
graphics/region_map/dungeon_icon.png
graphics/region_map/misc_icon.pal
tools/gbagfx/convert_png.c
tools/gbagfx/gfx.c""".splitlines()) | frozenset('data/maps/'+name+'/map.json' for name in MAPS)
import ui_tiles as pixels


from image_io import png as encode_rgba_png

def canonical(value):
    return (json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2, default=item_json) + '\n').encode('utf-8')


def unique_pairs(values):
    result = {}
    for key, value in values:
        if key in result:
            raise ValueError('Duplicate JSON member: ' + key)
        result[key] = value
    return result


def safe(root, relative=None):
    root = pathlib.Path(root).absolute()
    if relative is None:
        target = root
    else:
        path = pathlib.PurePosixPath(relative)
        if not relative or relative == '.' or path.is_absolute() or '..' in path.parts or '\\' in relative or str(path) != relative:
            raise ValueError('Unsafe town map path: ' + relative)
        target = root / relative
    if any(p.is_symlink() for p in [target, *target.parents]):
        raise ValueError('Symlink input/output or ancestor rejected: ' + str(target))
    if not target.resolve().is_relative_to(root.resolve()):
        raise ValueError('Trainer card path escapes root')
    return target


def load_lock(path=None):
    path = path or ROOT / 'scripts/recipes/config/town-map.json'
    data = safe(path).read_bytes()
    lock = json.loads(data, object_pairs_hook=unique_pairs)
    if (lock.get('schemaVersion'), lock.get('repository'), lock.get('commit'), lock.get('profile'), lock.get('gameEdition'), lock.get('romRevision')) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0):
        raise ValueError('Trainer card source identity differs')
    if len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:
        raise ValueError('Trainer card source dependency set differs')
    return lock


def read_sources(lock, raw, repository=None):
    safe(raw)
    sources = {}
    if repository is not None:
        tree = subprocess.run(['git', '-C', str(repository), 'rev-parse', COMMIT + '^{tree}'],
                              check=True, capture_output=True, timeout=30).stdout.decode('ascii').strip()
    for entry in lock['files']:
        target = safe(raw, entry['path'])
        if repository is None:
            data = target.read_bytes()
        else:
            data = subprocess.run(['git', '-C', str(repository), 'show', COMMIT + ':' + entry['path']],
                                  check=True, capture_output=True, timeout=30).stdout
        sources[entry['path']] = data
    return sources


palette = pixels.palette
compressed_png = encode_rgba_png


def render_tilemap(tiles, entries, palettes):
    if len(entries)!=600: raise ValueError('Unexpected Town Map tilemap dimensions')
    result=pixels.canvas(240,160)
    for offset,entry in enumerate(entries):
        number,bank=entry&1023,entry>>12
        if number>=len(tiles) or bank not in palettes: raise ValueError('Map tile/palette outside source')
        pixels.blit(result,240,pixels.tile_rgba(tiles[number],palettes[bank],bool(entry&1024),bool(entry&2048)),
                    8,8,(offset%30)*8,(offset//30)*8)
    return bytes(result)


def overlay(destination,source):
    if len(destination)!=len(source): raise ValueError('Map overlay size differs')
    for i in range(0,len(source),4):
        if source[i+3]: destination[i:i+4]=source[i:i+4]


def convert(sources):
    shared_item = ItemSource(sources).definition
    outputs={};assets=[];code=sources['src/region_map.c'].decode();base='graphics/region_map/'
    def require(path,needle):
        if needle not in sources[path].decode(): raise ValueError('Unsupported Town Map semantics: '+needle)
    for needle in [
        '#define MAP_WIDTH 22','#define MAP_HEIGHT 15','#define CANCEL_BUTTON_X 21','#define CANCEL_BUTTON_Y 13',
        'sMapCursor->spriteX = 8 * sMapCursor->x + 36;', 'sMapCursor->spriteY = 8 * sMapCursor->y + 36;',
        'sMapCursor->moveCounter = 4;', 'ANIMCMD_FRAME(0, 20),\n    ANIMCMD_FRAME(4, 20),',
        'RegionMap_DarkenPalette(mapEdgesPal, NELEMS(mapEdgesPal), 95);',
        'LoadPalette(&sRegionMap_Pal[0x2F], BG_PLTT_ID(2) + 15, PLTT_SIZEOF(1));',
        'r = (((r << 8) / 100) * tint) >> 8;',
        'LoadPalette(&sTopBar_Pal[15], BG_PLTT_ID(4), sizeof(sTopBar_Pal[15]));',
        'WriteSequenceToBgTilemapBuffer(0, 0x0F0, 0x18, 14, 3, 1, 0x3, 0x001);',
        'WriteSequenceToBgTilemapBuffer(0, 0x100, 0x18, 15, 3, 1, 0x3, 0x001);',
        'WriteSequenceToBgTilemapBuffer(0, 0x110, 0x18, 16, 3, 1, 0x3, 0x001);',
        'FillBgTilemapBufferRect(1, 0x03D, 2, 1, 26, 1, 2);',
        'divisor = width / sMapSectionDimensions[sMapCursor->selectedMapsec][0];',
        'divisor = height / sMapSectionDimensions[sMapCursor->selectedMapsec][1];',
        'case MAPSEC_VIRIDIAN_FOREST:\n        sMapCursor->x = 4;\n        sMapCursor->y = 6;',
        'if (mapsec == MAPSEC_CERULEAN_CAVE && !FlagGet(FLAG_SYS_CAN_LINK_WITH_RS))',
        'AddTextPrinterParameterized3(WIN_MAP_NAME, FONT_NORMAL, 2, 2, sTextColor_White, 0, sRegionMap->mapName);',
        'AddTextPrinterParameterized3(WIN_DUNGEON_NAME, FONT_NORMAL, 12, 2, sTextColorTable[GetSelectedMapsecType(LAYER_DUNGEON) - 2], 0, sRegionMap->dungeonName);']:
        require('src/region_map.c',needle)
    require('src/item_use.c','InitRegionMapWithExitCB(REGIONMAP_TYPE_NORMAL, CB2_BagMenuFromStartMenu);')
    require('src/data/item_icon_table.h','[ITEM_TOWN_MAP]       = {gItemIcon_TownMap, gItemIconPalette_TownMap}')
    require('src/data/graphics/items.h','gItemIcon_TownMap[] = INCBIN_U32("graphics/items/icons/town_map.4bpp.lz")')
    require('src/data/graphics/items.h','gItemIconPalette_TownMap[] = INCBIN_U32("graphics/items/icon_palettes/town_map.gbapal.lz")')
    require('src/data/items.json.txt','gItemDescription_{{ item.itemId }}[] = _("{{ item.description_english }}")')
    require('src/item_menu.c','case OPEN_BAG_KEYITEMS:\n                sContextMenuItemsPtr = sContextMenuItemsBuffer;\n                sContextMenuNumItems = 3;')
    require('src/item_menu.c','else if (ItemId_GetPrice(gSpecialVar_ItemId) == 0)')
    require('src/item_menu.c','else if (gBagMenuState.pocket != POCKET_KEY_ITEMS - 1 && ItemId_GetImportance(itemId) == 0)')
    require('tools/gbagfx/convert_png.c','% (1 << destBitDepth)')
    require('charmap.txt','A_BUTTON        = F8 00')
    require('charmap.txt','DPAD_ANY        = F8 0C')
    require('include/characters.h','#define CHAR_DPAD_NONE      0x0C')
    require('src/text.c','gKeypadIconTiles + (sKeypadIcons[keypadIconId].tileOffset * 0x20),\n        0,\n        0,\n        0x80,\n        0x80,')
    require('src/region_map.c','if (!FlagGet(FLAG_SYS_SEVII_MAP_123))\n        sRegionMap->permissions[MAPPERM_HAS_SWITCH_BUTTON] = FALSE;')
    require('include/text.h','FONT_SMALL,\n    FONT_NORMAL_COPY_1,\n    FONT_NORMAL,')
    require('include/gba/io_reg.h','#define BLDCNT_TGT1_BG1      (1 << 1)')
    require('include/gba/io_reg.h','#define BLDCNT_TGT1_BG2      (1 << 2)')
    require('src/region_map.c','SetBldY(BLDCNT_TGT1_BG1 | BLDCNT_TGT1_BG2);')
    for needle in ['checkitemspace ITEM_TOWN_MAP','removeobject LOCALID_TOWN_MAP',
       'setvar VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE, 2',
       'giveitem_msg PalletTown_RivalsHouse_Text_ReceivedTownMapFromDaisy, ITEM_TOWN_MAP, 1, MUS_OBTAIN_KEY_ITEM',
       'goto_if_eq VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE, 1, PalletTown_RivalsHouse_EventScript_GiveTownMap',
       'setvar RECEIVED_TOWN_MAP, TRUE']:
        require('data/maps/PalletTown_RivalsHouse/scripts.inc',needle)
    require('data/maps/PalletTown_ProfessorOaksLab/scripts.inc','setvar VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE, 1')

    def add(identifier,w,h,rgba,paths):
        if len(rgba)!=w*h*4 or not 0<w*h<=38400: raise ValueError('Unexpected Town Map asset dimensions')
        texture_path=identifier+'.png'
        outputs[texture_path]=compressed_png(w,h,rgba)
        assets.append(dict(id=identifier, width=w, height=h, texturePath=texture_path))
    map_pal=palette(sources[base+'region_map.pal'],80);top_pal=palette(sources[base+'top_bar.pal'],16)
    palettes={bank:map_pal[bank*16:(bank+1)*16].copy() for bank in range(5)}
    # Exact RGB555 arithmetic in RegionMap_DarkenPalette, not a floating 95% overlay.
    palettes[2]=[tuple((((((c>>3)<<8)//100)*95)>>8) for c in rgb) for rgb in palettes[2]]
    palettes[2]=[tuple((v<<3)|(v>>2) for v in rgb) for rgb in palettes[2]]
    palettes[2][15]=map_pal[47]
    # Completed normal-mode open animation replaces BG banks' zero entry with top-bar color 15.
    for bank in palettes: palettes[bank][0]=top_pal[15]
    tiles=pixels.tile_list(sources[base+'region_map.png'])
    edge_tiles=pixels.tile_list(sources[base+'map_edge.png'])
    if len(tiles)!=320 or len(edge_tiles)!=64: raise ValueError('Unexpected map tile inventories')
    map_entries=list(struct.unpack('<600H',sources[base+'kanto.bin']))
    edge_entries=list(struct.unpack('<600H',sources[base+'map_edge.bin']))
    # Current pre-Sevii profile: InitRegionMapType disables HAS_SWITCH_BUTTON.
    # Keep the original kanto.bin cells; do not write the later-game Switch icon.
    for x,number in [(0,2),(1,3),(28,62),(29,63),*[(x,61) for x in range(2,28)]]:
        edge_entries[30+x]=0x2000+number
    frame=pixels.canvas(240,160,(*top_pal[15],255))
    overlay(frame,render_tilemap(edge_tiles,edge_entries,palettes))
    overlay(frame,render_tilemap(tiles,map_entries,palettes))
    add('kanto',240,160,frame,[base+p for p in ('region_map.png','region_map.pal','kanto.bin','map_edge.png','map_edge.bin','top_bar.pal')])
    for source_name,ids,palette_name,w,h in [
        ('cursor',('cursor-0','cursor-1'),'cursor',16,16),
        ('player_icon_red',('player-red',),'player_icon_red',16,16),
        ('player_icon_leaf',('player-leaf',),'player_icon_leaf',16,16),
        ('dungeon_icon',('dungeon-unvisited','dungeon-visited'),'misc_icon',8,8)]:
        sw,sh,indices,_=pixels.indexed_png(sources[base+source_name+'.png'])
        if (sw,sh)!=(w,h*len(ids)):raise ValueError('Unexpected source sprite dimensions')
        pal=palette(sources[base+palette_name+'.pal'],16)
        for i,identifier in enumerate(ids):
            add(identifier,w,h,pixels.rgba(indices[i*w*h:(i+1)*w*h],pal),[base+source_name+'.png',base+palette_name+'.pal'])

    icon_path='graphics/items/icons/town_map.png';icon_palette='graphics/items/icon_palettes/town_map.pal'
    iw,ih,indices,_=pixels.indexed_png(sources[icon_path])
    if (iw,ih)!=(24,24):raise ValueError('Town Map Bag icon dimensions differ')
    add('item-town-map',iw,ih,pixels.rgba(indices,palette(sources[icon_palette],16)),[icon_path,icon_palette])
    keypad_path='graphics/fonts/keypad_icons.png'
    kw,kh,keypad_indices,_=pixels.indexed_png(sources[keypad_path])
    if (kw,kh)!=(128,32):raise ValueError('Unsupported source keypad sheet')
    for symbol,identifier in [('CHAR_A_BUTTON','keypad-a'),('CHAR_DPAD_NONE','keypad-dpad-any')]:
        match=re.search(r'\['+symbol+r'\]\s*=\s*\{\s*(0x[0-9A-Fa-f]+),\s*(\d+),\s*(\d+)\s*\}',sources['src/text.c'].decode())
        if match is None:raise ValueError('Missing source keypad metadata')
        tile_offset,width,height=int(match[1],16),int(match[2]),int(match[3])
        if (width,height)!=(8,12):raise ValueError('Keypad icon dimension drift')
        x=(tile_offset%16)*8;y=(tile_offset//16)*8
        if x+width>kw or y+height>kh:raise ValueError('Keypad icon crop outside sheet')
        indices=bytes(keypad_indices[(y+row)*kw+x+col] for row in range(height) for col in range(width))
        add(identifier,width,height,pixels.rgba(indices,top_pal),[keypad_path,'src/text.c','charmap.txt',base+'top_bar.pal'])
    sections=json.loads(sources['src/data/region_map/region_map_sections.json'])['map_sections']
    by_id={s['id']:dict(s,sourceId=i) for i,s in enumerate(sections)}
    layout_source=sources['src/data/region_map/region_map_layout_kanto.h'].decode()
    rows=re.findall(r'\{(MAPSEC_[A-Z0-9_, ]+)\}',layout_source)
    grid=[re.findall(r'MAPSEC_[A-Z0-9_]+',row) for row in rows]
    if len(grid)!=30 or any(len(row)!=22 for row in grid): raise ValueError('Kanto 2x15x22 section grid differs')
    section_ids={s for row in grid for s in row}-{'MAPSEC_NONE'}
    dungeon_types=code[code.index('static u8 GetDungeonMapsecType(u8 mapsec)\n{'):code.index('static u8 GetSelectedMapsecType(u8 layer)\n{')]
    flags=dict(re.findall(r'case (MAPSEC_\w+):\n        return FlagGet\((FLAG_\w+)\)',dungeon_types))
    cells=[];icons=[]
    for y in range(15):
        for x in range(22):
            map_id=grid[y][x];dungeon_id=grid[y+15][x]
            cells.append(dict(x=x,y=y,sectionId=map_id,name=by_id.get(map_id,{}).get('name',''),
                 dungeonSectionId=dungeon_id,dungeonName=by_id.get(dungeon_id,{}).get('name','')))
            if dungeon_id!='MAPSEC_NONE':
                offset=2 if map_id in {'MAPSEC_CERULEAN_CITY','MAPSEC_LAVENDER_TOWN','MAPSEC_FUCHSIA_CITY','MAPSEC_CINNABAR_ISLAND'} else 0
                icons.append(dict(x=x,y=y,centerX=36+8*x+offset,centerY=36+8*y+offset,sectionId=dungeon_id,
                   visitedFlag=flags[dungeon_id],visibleFlag='FLAG_SYS_CAN_LINK_WITH_RS' if dungeon_id=='MAPSEC_CERULEAN_CAVE' else None))
    layouts={v['id']:v for v in json.loads(sources['data/layouts/layouts.json'])['layouts'] if 'id' in v}
    groups=json.loads(sources['data/maps/map_groups.json']);positions=[]
    for source_name,stable_id in MAPS.items():
        path='data/maps/'+source_name+'/map.json';m=json.loads(sources[path]);section=by_id[m['region_map_section']]
        layout=layouts[m['layout']];override=None
        group=[g for g in groups['group_order'] if source_name in groups[g]]
        if len(group)!=1: raise ValueError('Ambiguous source map group')
        map_num=groups[group[0]].index(source_name)
        if section['id']=='MAPSEC_VIRIDIAN_FOREST':override=dict(x=4,y=6)
        elif section['id']=='MAPSEC_ROUTE_2' and map_num in (0,3):
            override=dict(x=4,y=7 if map_num==0 else 5)
        if m['map_type']=='MAP_TYPE_INDOOR' and override is None and (section['width'],section['height'])!=(1,1):
            raise ValueError('Indoor projection requires escape warp not present in this bounded profile')
        positions.append(dict(stableMapId=stable_id,sourceMap=m['id'],sourceName=source_name,sectionId=section['id'],
             width=layout['width'],height=layout['height'],sectionX=section['x'],sectionY=section['y'],
             sectionWidth=section['width'],sectionHeight=section['height'],mapType=m['map_type'],
             override=override))
    item=next(i for i in json.loads(sources['src/data/items.json'])['items'] if i['itemId']=='ITEM_TOWN_MAP')
    expected_item=dict(english='TOWN MAP',itemId='ITEM_TOWN_MAP',price=0,holdEffect='HOLD_EFFECT_NONE',holdEffectParam=0,
        importance=1,registrability=1,pocket='POCKET_KEY_ITEMS',type='ITEM_TYPE_BAG_MENU',fieldUseFunc='FieldUseFunc_TownMap',
        battleUsage=0,battleUseFunc='NULL',secondaryId=0)
    if set(item)!=set(expected_item)|{'description_english'} or any(type(item[k]) is not type(v) or item[k]!=v for k,v in expected_item.items()):
        raise ValueError('Town Map item semantics differ')
    texts={};text_source=sources['data/maps/PalletTown_RivalsHouse/text.inc'].decode()
    for short in ('HiBrothersAtLab','HeardYouBattledRival','ErrandForGrandpaThisWillHelp','ReceivedTownMapFromDaisy',
                   'DontHaveSpaceForThis','ExplainTownMap','PleaseGiveMonsRest','ItsBigMapOfKanto'):
        symbol='PalletTown_RivalsHouse_Text_'+short
        block=re.search(re.escape(symbol)+r'::\n(.*?)(?=\n\w+::|\Z)',text_source,re.S).group(1)
        texts[short]=''.join(re.findall(r'\.string "(.*)"',block))
    strings=sources['src/strings.c'].decode()
    ui_texts={}
    for key,symbol in {'move':'gText_RegionMap_DPadMove','cancel':'gText_RegionMap_AButtonCancel',
                       'switch':'gText_RegionMap_AButtonSwitch','space':'gText_RegionMap_Space'}.items():
        ui_texts[key]=re.search(r'const u8 '+symbol+r'\[\] = _\("(.*)"\);',strings).group(1)
    hide_map=next(v for v in json.loads(sources['data/maps/PalletTown_RivalsHouse/map.json'])['object_events'] if v['local_id']=='LOCALID_TOWN_MAP')
    if hide_map['flag']!='FLAG_HIDE_TOWN_MAP':raise ValueError('Town Map hide flag differs')
    source_item_id=int(re.search(r'^#define ITEM_TOWN_MAP (\d+)$',sources['include/constants/items.h'].decode(),re.M).group(1))
    if source_item_id!=361:raise ValueError('Town Map source item ID differs')
    item_text=sources['src/data/items.json'].decode()
    item_match=re.search(r'^    \{\n      "english": "TOWN MAP",.*?^    \}',item_text,re.M|re.S)
    if item_match is None:raise ValueError('Town Map item source block missing')
    item_ref=dict(path='src/data/items.json',symbol='ITEM_TOWN_MAP',startLine=item_text.count('\n',0,item_match.start())+1,
        endLine=item_text.count('\n',0,item_match.end())+1)
    item_definition=shared_item(item['itemId'])
    item_description=item['description_english'].replace(r'\n','\n')
    if '\\' in item_description or '{' in item_description:raise ValueError('Unsupported Town Map description control')
    gift=dict(itemId='ITEM_TOWN_MAP',sourceItemId=source_item_id,pocket=item['pocket'],registrable=True,
              sceneVariable='VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE',availableScene=1,receivedScene=2,
              temporaryReceivedVariable='VAR_TEMP_1',objectLocalId='LOCALID_TOWN_MAP',hideFlag=hide_map['flag'],
              giftScript='PalletTown_RivalsHouse_EventScript_GiveTownMap',fanfare='MUS_OBTAIN_KEY_ITEM',texts=texts)
    character_defs=sources['include/characters.h'].decode()
    def color_id(symbol):
        return int(re.search(r'^#define '+symbol+r'\s+(0x[0-9A-Fa-f]+)',character_defs,re.M).group(1),16)
    layout=dict(logicalWidth=240,logicalHeight=160,columns=22,rows=15,cellPixels=8,centerOffsetX=36,centerOffsetY=36,
         cursorWidth=16,cursorHeight=16,cursorFrameTicks=20,cursorMoveTicks=4,cursorMovePixelsPerTick=2,
         cancel=dict(x=21,y=13),switchButton=dict(x=21,y=11),mapName=dict(x=26,y=18),dungeonName=dict(x=36,y=34),
         mapNameWindow=dict(x=24,y=16,width=120,height=16),dungeonNameWindow=dict(x=24,y=32,width=120,height=16),
         topBarLeft=dict(x=144,y=0),topBarRight=dict(x=192,y=0),fontId=2,topBarFontId=0,
         textPaletteRgb=top_pal,uiTexts=ui_texts,mapTextColor=color_id('TEXT_COLOR_WHITE'),
         textShadowColor=color_id('TEXT_COLOR_DARK_GRAY'),dungeonVisitedTextColor=color_id('TEXT_COLOR_LIGHT_GREEN'),
         dungeonUnvisitedTextColor=color_id('TEXT_DYNAMIC_COLOR_1'),nameWindowDarkeningCoefficient=6)
    for symbol in ('BufferRegionMapBg','TintMapEdgesPalette','GetPlayerPositionOnRegionMap',
                   'GetPlayerPositionOnRegionMap_HandleOverrides','CreateDungeonIcons','DisplayCurrentMapName','DisplayCurrentDungeonName'):
        match=re.search(r'^static [^;\n]+ '+symbol+r'\([^;\n]*\)\n\{',code,re.M)
        if match is None:raise ValueError('Missing source symbol '+symbol)
    outputs['town-map.json']=canonical(dict(schemaVersion=1,
        assets=assets,layout=layout,cells=cells,icons=icons,
        positions=positions,gift=gift,itemDefinition=item_definition,itemDescription=item_description,switchAvailable=False,sections=[by_id[s] for s in sorted(section_ids)]))
    outputs['manifest.json']=canonical(dict(schemaVersion=1,
         inputCount=len(sources),assetCount=len(assets),
         outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
    return outputs


def import_content(raw,out,repository=None,lock_path=None):
    raw,out=safe(raw),safe(out)
    if raw.resolve().is_relative_to(out.resolve()) or out.resolve().is_relative_to(raw.resolve()):
        raise ValueError('Raw and output directories must be disjoint')
    if repository is not None:
        repo=pathlib.Path(repository).resolve()
        if any(p.resolve().is_relative_to(repo) or repo.is_relative_to(p.resolve()) for p in (raw,out)):
            raise ValueError('Materialization must not overlap the reference repository')
    lock=load_lock(lock_path);sources=read_sources(lock,raw,repository);outputs=convert(sources)
    material={(raw,p):b for p,b in sources.items()} if repository is not None else {}
    material.update({(out,p):b for p,b in outputs.items()})
    targets=[(safe(root,p),safe(root,p+'.tmp'),b) for (root,p),b in material.items()]
    if any(p.exists() and not p.is_file() for target,stage,_ in targets for p in (target,stage)):
        raise ValueError('Input/output target is not a regular file')
    if any(parent.exists() and not parent.is_dir() for target,_,_ in targets for parent in target.parents):
        raise ValueError('Input/output ancestor is not a directory')
    for target,stage,data in targets:
        target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(data);stage.replace(target)
    return dict(profile=PROFILE,inputCount=len(sources),outputCount=len(outputs),assetCount=10)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-repo',type=pathlib.Path,default=os.environ.get('FIRERED_ORIGINAL_REFERENCE'))
    parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/ui/town-map')
    parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/config/town-map.json')
    args=parser.parse_args();print(json.dumps(import_content(args.raw,args.out,args.reference_repo,args.lock),sort_keys=True, default=item_json))

if __name__=='__main__':main()
