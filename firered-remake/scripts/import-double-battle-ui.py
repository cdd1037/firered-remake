#!/usr/bin/env python3
"""Build the pinned original four-position healthboxes and Twins portrait; local inputs only."""
import argparse, hashlib, importlib.util, json, pathlib, subprocess, sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('ui',ROOT/'scripts/import-lab-battle-ui.py');ui=importlib.util.module_from_spec(spec);spec.loader.exec_module(ui)
def digest(data):return hashlib.sha256(data).hexdigest()
def convert(source,lock):
    data={}
    for path,sha in lock['files'].items():
        b=subprocess.run(['git','-C',str(source),'show',lock['sourceCommit']+':'+path],capture_output=True,check=True).stdout if (source/'.git').exists() else (source/path).read_bytes()
        if digest(b)!=sha:raise ValueError('Source differs: '+path)
        data[path]=b
    code=data['src/battle_interface.c'].decode()
    for s in ['x = 159, y = 75;','x = 171, y = 100;','x = 44, y = 19;','x = 32, y = 44;','ptr += spriteTileNum + 0x400;','objVram += spriteTileNum + 0x420;']:
        if s not in code:raise ValueError('Doubles source layout differs')
    out={};assets=[]
    def add(name,w,h,rgba):
        encoded=ui.png(w,h,rgba);out[name+'.png']=encoded
        assets.append(dict(id=name,path=name+'.png',width=w,height=h,sha256=digest(encoded),rgbaSha256=digest(rgba)))
    pal=ui.palette(data['graphics/battle_interface/healthbox.pal'])
    for side in ('player','opponent'):
        tiles=ui.tile_list(data['graphics/battle_interface/healthbox_doubles_'+side+'.png'])
        if len(tiles)!=64:raise ValueError('Healthbox must comprise two 64x32 objects')
        for start,count in ([(2,6),(32,1),(33,3)] if side=='player' else [(1,7),(32,3)]):
            for n in range(start,start+count):tiles[n]=tiles[n][:40]+bytes([2]*24);tiles[n+8]=bytes([2]*64)
        pixels=ui.canvas(128,32)
        for n,tile in enumerate(tiles):ui.blit(pixels,128,ui.tile_rgba(tile,pal),8,8,(n//32)*64+(n%8)*8,(n%32//8)*8)
        add('healthbox-'+side,128,32,bytes(pixels))
    elements=ui.tile_list(data['graphics/battle_interface/healthbox_elements.png']);barpal=ui.palette(data['graphics/battle_interface/healthbar.pal'])
    add('hp-label',16,8,b''.join(ui.rgba(elements[1],barpal)[y*32:(y+1)*32]+ui.rgba(elements[2],barpal)[y*32:(y+1)*32] for y in range(8)))
    for name,start in [('hp-green',3),('hp-yellow',47),('hp-red',56)]:
        pixels=ui.canvas(72,8)
        for f in range(9):ui.blit(pixels,72,ui.tile_rgba(elements[start+f],barpal),8,8,f*8,0)
        add(name,72,8,bytes(pixels))
    w,h,indices,_=ui.indexed_png(data['graphics/trainers/front_pics/twins_front_pic.png'])
    if (w,h)!=(64,64):raise ValueError('Twins portrait dimensions differ')
    add('twins-front',w,h,ui.rgba(indices,ui.palette(data['graphics/trainers/palettes/twins.pal'])))
    catalog=dict(schemaVersion=1,sourceCommit=lock['sourceCommit'],assets=assets,healthboxCenters=[[159,75],[44,19],[171,100],[32,44]],battlerCenters=[[32,80],[200,40],[90,88],[152,32]],limitations=['Settled source geometry; entry, dedicated doubles move animations and numeric HP toggle remain separate presentation work.'])
    out['double-battle-ui.json']=(json.dumps(catalog,indent=2,sort_keys=True)+'\n').encode()
    return out
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('source',type=pathlib.Path);p.add_argument('output',type=pathlib.Path);a=p.parse_args()
    lock=json.loads((ROOT/'scripts/double-battle-ui.lock.json').read_text());outputs=convert(a.source,lock)
    a.output.mkdir(parents=True,exist_ok=True)
    for name,data in outputs.items():(a.output/name).write_bytes(data)
    print(json.dumps(dict(assets=len(outputs)-1,catalogSha256=digest(outputs['double-battle-ui.json']),sourceCommit=lock['sourceCommit'])))
