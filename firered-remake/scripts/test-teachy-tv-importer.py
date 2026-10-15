#!/usr/bin/env python3
"""Small source/asset boundary checks; not a historical campaign replay."""
import argparse
import sys
sys.dont_write_bytecode = True
import importlib.util
import json
from pathlib import Path
import struct
import zlib

ROOT=Path(__file__).resolve().parent.parent
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value
catalog=module('ttv_importer_test',ROOT/'scripts/import-teachy-tv.py')
art=module('ttv_art_test',ROOT/'scripts/import-teachy-tv-art.py')
parser=argparse.ArgumentParser();parser.add_argument('--git-dir',required=True,type=Path);args=parser.parse_args()
checks=[]
def check(name,ok):
    if not ok:raise AssertionError(name)
    checks.append(name)
lock,sources=catalog.read_sources(args.git_dir,ROOT/'scripts/teachy-tv.lock.json')
doc=catalog.convert(lock,sources)
check('six_original_lessons',len(doc['lessons'])==6)
check('six_text_intros_and_outros',all(any(t['symbol']==lesson['introductionText'] for t in doc['texts']) and any(t['symbol']==lesson['conclusionText'] for t in doc['texts']) for lesson in doc['lessons']))
check('original_four_then_two_menu_gate',[x['sourceIndex'] for x in doc['menu'][1]['entries']]==[0,1,2,3,-2])
check('tm_demo_original_four_items',[x['item'] for x in doc['demonstrationInventory']['tmCase']]==['ITEM_TM01','ITEM_TM03','ITEM_TM09','ITEM_TM35'])
check('bag_register_is_temporary','sBackupPlayerBag->registeredItem' in sources['src/item_menu.c'].decode())
check('no_invented_action_sentinels',[len(x['battle']['chooseAction']) for x in doc['lessons'][:4]]==[2,3,3,3])
check('all_original_voiceover_hooks',[len(x['battle']['voiceovers']) for x in doc['lessons'][:4]]==[4,5,7,6])
check('source_uses_follow_me_music',doc['presentationContract']['sourceShowMidi']=='sound/songs/midi/mus_follow_me.mid')
assets,contract=art.convert(sources)
check('ten_dedicated_artifacts',len(assets)==10)
def pixel(png,x,y):
    at=8;data=b'';width=struct.unpack_from('>I',png,16)[0]
    while at<len(png):
        size=struct.unpack_from('>I',png,at)[0]
        if png[at+4:at+8]==b'IDAT':data+=png[at+8:at+8+size]
        at+=size+12
    raw=zlib.decompress(data);offset=y*(width*4+1)+1+x*4
    return list(raw[offset:offset+4])
check('screen_opening_transparent',pixel(assets['screen-frame.png']['bytes'],50,50)[3]==0)
check('screen_border_opaque',pixel(assets['screen-frame.png']['bytes'],0,0)[3]==255)
check('route_crop_original_dimensions',assets['route1-background.png']['width']==256 and assets['route1-background.png']['height']==256)
check('source_palette_backdrop_black',contract['palettes'][0]==(0,0,0))
check('host_nine_original_frames',assets['host-sheet.png']['width']==16*9 and assets['host-sheet.png']['height']==32)
if not checks:raise AssertionError('empty_suite')
print(json.dumps(dict(status='pass',checks=len(checks),tests=checks),indent=2))
