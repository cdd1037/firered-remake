#!/usr/bin/env python3
"""Exact Cut field-move player frames and outdoor streaks; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from generated_output_compare import generated_output_matches
import argparse
import ui_tiles as codec
import json
import pathlib
import re
import struct
import subprocess
import sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-vermilion-gym-field-move-art-v1'

def safe(root, path=None):
    if '..' in pathlib.Path(root).parts: raise ValueError('Parent traversal root rejected')
    p = pathlib.Path(root).absolute()
    if path is not None:
        if not path or pathlib.PurePosixPath(path).is_absolute() or '\\' in path or any(x in ('','.','..') for x in path.split('/')):
            raise ValueError('Unsafe field-move path')
        p /= path
    if any(x.is_symlink() for x in (p,*p.parents)): raise ValueError('Field-move symlink rejected')
    return p

def read_lock():
    b = safe(ROOT, 'scripts/recipes/story/vermilion-gym-assets-field-move.lock.json').read_bytes()
    d = json.loads(b)
    if (d['schemaVersion'], d['repository'], d['commit'], d['profile'], d['gameVersion'], d['revision']) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0): raise ValueError('Field-move source identity differs')
    return d

def read_inputs(lock, raw, reference=None):
    sources = {}
    if reference:
        reference = safe(reference)
    for row in lock['files']:
        path = row['path']; target = safe(raw,path)
        if reference:
            b = subprocess.check_output(['git','-C',str(reference),'cat-file','blob',COMMIT + ':' + row['path']])
        else:
            b = target.read_bytes()
        sources[path] = b
    return sources

def build(lock, data):
    text = {p:b.decode() for p,b in data.items() if not p.endswith(('.png','.bin'))}
    def require(path, fragment):
        if fragment not in text[path]: raise ValueError('Missing source field-move contract: '+fragment)
    def match(path, pattern):
        values=list(re.finditer(pattern,text[path],re.M|re.S))
        if len(values)!=1: raise ValueError('Nonunique field-move source match')
        return values[0]
    require('tools/gbagfx/gfx.c','#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)')
    require('Makefile','%.gbapal: %.png  ; $(GFX) $< $@')
    require('src/data/object_events/object_event_graphics.h','gObjectEventPal_Player[] = INCBIN_U16("graphics/object_events/palettes/player.gbapal")')
    require('src/field_player_avatar.c','[PLAYER_AVATAR_GFX_FIELD_MOVE] = {OBJ_EVENT_GFX_RED_FIELD_MOVE, OBJ_EVENT_GFX_GREEN_FIELD_MOVE}')
    require('src/field_player_avatar.c','StartSpriteAnim(&gSprites[gPlayerAvatar.spriteId], ANIM_FIELD_MOVE);')
    m=match('src/data/object_events/object_event_anims.h',r'^static const union AnimCmd sAnim_FieldMove\[\] = \{(.*?)^\};')
    frames=[(int(a),int(b)) for a,b in re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)',m[1])]
    if frames != [(0,4),(1,4),(2,4),(3,4),(4,8)] or 'ANIMCMD_END' not in m[1]: raise ValueError('Field-move animation differs')
    require('src/sprite.c','if (duration)\n            duration--;')
    require('src/sprite.c','if (duration)\n        duration--;')
    outputs,assets={},[]
    def save(identity, width,height,indices,colors,image,palette,tilemap=None,framewidth=None,frameheight=None,durations=None):
        rgba=codec.rgba(indices,colors);png=codec.png(width,height,rgba)
        outputs[identity+'.png']=png
        assets.append(dict(id=identity,width=width,height=height,frameWidth=framewidth or width,frameHeight=frameheight or height,
            frameDurations=durations or [],sourceImage=image,sourcePalette=palette,sourceTilemap=tilemap))
    for gender in ('Red','Green'):
        lower=gender.lower();image='graphics/object_events/pics/people/'+lower+'_item.png';pal='graphics/object_events/palettes/player.pal'
        require('src/data/object_events/object_event_graphics.h','gObjectEventPic_'+gender+'Item[] = INCBIN_U16("'+image.replace('.png','.4bpp')+'")')
        require('src/event_object_movement.c','{gObjectEventPal_Player,                  OBJ_EVENT_PAL_TAG_PLAYER_'+gender.upper()+'}')
        info=match('src/data/object_events/object_event_graphics_info.h',r'^const struct ObjectEventGraphicsInfo gObjectEventGraphicsInfo_'+gender+r'FieldMove = \{(.*?)^\};')[1]
        for fragment in ['.width = 16,','.height = 32,','.size = 256,','.paletteSlot = PALSLOT_PLAYER,',
                         '.paletteTag = OBJ_EVENT_PAL_TAG_PLAYER_'+gender.upper()+',','.images = sPicTable_'+gender+'Item,','.anims = sAnimTable_RedGreenFieldMove,']:
            if fragment not in info: raise ValueError('Player field-move source geometry/binding differs')
        require('src/data/object_events/object_event_graphics_info_pointers.h','[OBJ_EVENT_GFX_'+gender.upper()+'_FIELD_MOVE]')
        table=match('src/data/object_events/object_event_pic_tables.h',r'^static const struct SpriteFrameImage sPicTable_'+gender+r'Item\[\] = \{(.*?)^\};')[1]
        if re.findall(r'overworld_frame\(gObjectEventPic_'+gender+r'Item, (\d+), (\d+), (\d+)\)',table) != [('2','4',str(n)) for n in range(9)]:
            raise ValueError('Player field-move original frame table differs')
        width,height,idx,_=codec.indexed_png(data[image])
        if (width,height)!=(144,32): raise ValueError('Source nine-frame player sheet differs')
        selected=bytes(value for y in range(32) for value in idx[y*144:y*144+80])
        save(lower+'-field-move',80,32,selected,codec.palette(data[pal]),image,pal,framewidth=16,frameheight=32,durations=[v for _,v in frames])
    image='graphics/field_effects/pics/field_move_streaks_outdoors.png';tilemap=image.replace('.png','.bin')
    for suffix,name,kind in [('4bpp','Gfx','U16'),('gbapal','Pal','U16'),('bin','Tilemap','U16')]:
        require('src/field_effect.c','sFieldMoveStreaksOutdoors_'+name+'[] = INCBIN_'+kind+'("'+image.replace('.png','.'+suffix)+'")')
    require('src/field_effect.c','CpuCopy16(sFieldMoveStreaksOutdoors_Gfx, (void *)(VRAM + charbase), 0x200);')
    require('src/field_effect.c','LoadPalette(sFieldMoveStreaksOutdoors_Pal, BG_PLTT_ID(15), sizeof(sFieldMoveStreaksOutdoors_Pal));')
    require('src/field_effect.c','dest = (u16 *)(VRAM + (10 * 32) + screenbase);')
    require('src/field_effect.c','for (i = 0; i < (10 * 32); i++, dest++)\n        *dest = sFieldMoveStreaksOutdoors_Tilemap[i] | 0xF000;')
    w,h,indices,pal=codec.indexed_png(data[image]);tiles=data[tilemap]
    if (w,h,len(tiles)) != (32,32,640): raise ValueError('Outdoor source graphics/tilemap shape differs')
    entries=struct.unpack('<320H',tiles);banner=bytearray(256*80)
    for n,entry in enumerate(entries):
        tile=entry&1023
        if entry>>12 or tile>=16: raise ValueError('Outdoor source tile/palette index differs')
        for y in range(8):
            for x in range(8):
                sx=7-x if entry&1024 else x;sy=7-y if entry&2048 else y
                banner[(n//32*8+y)*256+n%32*8+x]=indices[(tile//4*8+sy)*32+tile%4*8+sx]
    save('outdoor-streaks',256,80,banner,codec.colors(pal[:16]),image,image,tilemap=tilemap)
    for fragment in ['task->data[1] = WIN_RANGE(0xF0, 0xF1);','task->data[2] = WIN_RANGE(0x50, 0x51);',
        'task->data[5] -= 16;','win0h_lo -= 16;','win0v_lo -= 2;','win0v_hi += 2;',
        'win0v_lo = 0x28;','win0v_hi = 0x78;','win0v_lo += 6;','win0v_hi -= 6;',
        'win0v_lo = 0x50;','win0v_hi = 0x51;','personality, 0x140, 0x50, 0);',
        'if ((sprite->x -= 20) <= 0x78)','sprite->x = 0x78;','sprite->data[1] = 30;',
        'if (sprite->x < -0x40)','sprite->x -= 20;']:
        require('src/field_effect.c',fragment)
    cadence=dict(bannerY=40,bannerHeight=80,bannerPaletteBank=15,backgroundScrollPerTick=-16,
        openingLeft=240,openingRight=241,openingTop=80,openingBottom=81,openHorizontalStep=-16,openVerticalStep=2,
        openTop=40,openBottom=120,closeVerticalStep=6,closeTop=80,closeBottom=81,
        monStartCenterX=320,monCenterY=80,monStopCenterX=120,monStep=-20,monHoldTicks=30,monExitXExclusive=-64)
    outputs['manifest.json']=runtime_json_bytes(dict(schemaVersion=1,
        assets=assets,cadence=cadence,
        transparentIndex=0,paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',
        outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
    return outputs

def run(raw,out,reference=None,check=False):
    raw,out=safe(raw),safe(out)
    if raw==out or raw in out.parents or out in raw.parents: raise ValueError('Field-move raw/output overlap')
    if reference:
        ref=safe(reference)
        if any(p==ref or p in ref.parents or ref in p.parents for p in (raw,out)): raise ValueError('Reference/output overlap')
    source_root = ROOT / 'content/raw'
    if out == source_root or source_root in out.parents or out in source_root.parents:
        raise ValueError('Generated output overlaps project raw sources')
    if reference:
        generated_root = ROOT / 'content/generated'
        if raw == generated_root or generated_root in raw.parents or raw in generated_root.parents:
            raise ValueError('Raw materialization overlaps generated resources')
    lock=read_lock();sources=read_inputs(lock,raw,reference);outputs=build(lock,sources)
    materials={(out,p):b for p,b in outputs.items()}
    if reference: materials.update({(raw,p):b for p,b in sources.items()})
    targets=[]
    for (parent,p),b in materials.items():
        target,stage=safe(parent,p),safe(parent,p+'.tmp')
        if any(q.exists() and not q.is_dir() for q in (target.parent,*target.parent.parents)):raise ValueError('Invalid output parent')
        if any(q.exists() and not q.is_file() for q in (target,stage)):raise ValueError('Invalid output file')
        targets.append((target,stage,b))
    for target,stage,b in targets:
        if check:
            matches = generated_output_matches(target, b) if target.is_relative_to(out) else target.read_bytes() == b
            if not matches: raise ValueError('Field-move output differs: ' + str(target))
        else:
            target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(b);stage.replace(target)
    return dict(passed=True,sourceInputs=len(sources),assets=len(json.loads(outputs['manifest.json'])['assets']),outputs=len(outputs))
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path)
    p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4')
    p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/ui/vermilion-gym-field-move-art')
    p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.raw,a.out,a.reference_repo,a.check)))
