#!/usr/bin/env python3
"""Compile FireRed CUT mapping and replacement art through the shared source map renderer."""
from pathlib import Path
import sys
sys.path.insert(0,str(Path(__file__).resolve().parents[3]/'scripts/lib'))
import argparse,json,re,io
from PIL import Image
from project_paths import PROJECT_ROOT
from world_source import WorldSource
from map_source import tileset_bindings
from world_map_renderer import render_map


def mapping(raw):
    text=(raw/'src/fldeff_cut.c').read_text()
    body=re.search(r'sCutGrassMetatileMapping\[\]\[2\] = \{(.*?)\n\};',text,re.S)[1]
    labels={k:int(v,16) for k,v in re.findall(r'#define\s+METATILE_(\w+)\s+(0x[0-9A-Fa-f]+)',(raw/'include/constants/metatile_labels.h').read_text())}
    pairs=re.findall(r'\{\s*METATILE_ID\((\w+),\s*(\w+)\),\s*METATILE_ID\((\w+),\s*(\w+)\)\s*\}',body)
    rows=[[labels[a+'_'+b],labels[c+'_'+d]] for a,b,c,d in pairs]
    if not rows or len({r[0] for r in rows})!=len(rows) or not re.search(r'\{\s*0xffff,\s*0xffff\s*\}',body):
        raise ValueError('Invalid CUT grass mapping')
    return rows


def compile_outputs(root,raw,generated=None):
    generated=generated or root/'content/generated'
    rows=mapping(raw);source=WorldSource(raw);bindings=tileset_bindings(source)
    layouts={l['id']:l for l in source.json('data/layouts/layouts.json')['layouts'] if 'id' in l}
    worlds=json.loads((generated/'maps/world.json').read_bytes())['maps']
    groups={};layout_rows={};images=[];pixels={}
    for world in worlds:
        document=json.loads((generated/world['path']).read_bytes());events=document['events'];layout=layouts[world.get('layout',events['layout'])]
        primary,secondary=[bindings[layout[k]] for k in ('primary_tileset','secondary_tileset')]
        attrs=[source.units('data/tilesets/'+p+'/metatile_attributes.bin','I') for p in (primary,secondary)]
        eligible=[v for k,v in rows if (k if k<640 else k-640)<len(attrs[k>=640]) and (attrs[k>=640][k if k<640 else k-640]>>9)&31==1]
        if not eligible:continue
        pair=(primary,secondary)
        if pair not in groups:
            name=document['sourceMapName']
            exported=render_map(source,name,document['stableId'],'unused.json',tilesets=bindings,metatile_exports=eligible)
            group={}
            for metatile,parts in exported.items():
                key=b''.join(p.tobytes() for p in parts)
                if key not in pixels:
                    pixels[key]=len(images);images.append(parts)
                group[str(metatile)]=pixels[key]
            groups[pair]=group
        layout_rows[layout['id']]=list(groups).index(pair)
    if not images:raise ValueError('No mounted CUT replacement art')
    atlas=Image.new('RGBA',(48,len(images)*16))
    for row,parts in enumerate(images):
        for layer,img in enumerate(parts):atlas.paste(img,(layer*16,row*16))
    particle=Image.open(raw/'graphics/field_effects/fldeff_cut.png')
    if particle.mode!='P' or particle.size!=(8,8):raise ValueError('CUT particle shape')
    palette=particle.getpalette();rgba=Image.new('RGBA',(8,8));rgba.putdata([tuple(((palette[i*3+j]>>3)<<3)|((palette[i*3+j]>>3)>>2) for j in range(3))+(0 if i==0 else 255,) for i in particle.tobytes()])
    def png(image):
        out=io.BytesIO();image.save(out,format='PNG',compress_level=9);return out.getvalue()
    def js(data):return (json.dumps(data,separators=(',',':'),sort_keys=True)+'\n').encode()
    return {'data/field/cut-grass.json':js(rows),'maps/cut-grass-art.json':js(dict(layouts=layout_rows,groups=list(groups.values()))),'maps/cut-grass.png':png(atlas),'ui/field-cut/grass.png':png(rgba)}


def main():
    p=argparse.ArgumentParser();p.add_argument('--root',type=Path,default=PROJECT_ROOT);p.add_argument('--raw',type=Path);p.add_argument('--out',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
    generated=a.out or a.root/'content/generated'
    outputs=compile_outputs(a.root,a.raw or a.root/'content/raw/pokefirered/037335f4',generated)
    for path,data in outputs.items():
        target=generated/path
        if a.check:
            if target.read_bytes()!=data:raise ValueError('Regenerate '+path)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps({k:len(v) for k,v in outputs.items()}))
if __name__=='__main__':main()
