#!/usr/bin/env python3
"""Rebuild the bounded first-Sevii presentation from immutable local Git blobs.
No ROM download, source execution, audio rendering, or map rerendering.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse, io, json, pathlib, re, struct, subprocess
from PIL import Image

COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE = '7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE = 'firered-first-sevii-presentation-v1'
ROOT = _PROJECT_ROOT
TRAINERS = ('AROMA_LADY_NIKKI','AROMA_LADY_VIOLET','BIKER_GOON','BIKER_GOON_2','BIKER_GOON_3','CUE_BALL_PAXTON','SWIMMER_FEMALE_TISHA','TUBER_ALEXIS','TUBER_AMIRA','TWINS_JOY_MEG')
PORTRAITS = (('aroma_lady','AromaLady'),('biker','Biker'),('cue_ball','CueBall'),('swimmer_f','SwimmerF'),('tuber_f','TuberF'),('twins','Twins'))
ITEMS = ('METEORITE','TRI_PASS','IAPAPA_BERRY','MAX_ELIXIR','RAZZ_BERRY','PINAP_BERRY','FRESH_WATER','SODA_POP')

from image_io import png as encode_rgba_png

def need(ok, message):
    if not ok: raise ValueError(message)
def canonical(d): return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root, rel):
    need(rel and not pathlib.PurePosixPath(rel).is_absolute() and '\\' not in rel and ':' not in rel and all(x not in ('','.','..') for x in rel.split('/')), 'Unsafe relative path')
    p=pathlib.Path(root).absolute()/rel
    need(not any(x.is_symlink() for x in (p,*p.parents)), 'Symlink rejected')
    return p
png = encode_rgba_png

class Source:
    def __init__(self, root, reference):
        self.root,self.reference,self.files,self.raw=root,reference,{},{}
        if reference: pass
        self.lock=None
        lock=safe(root,'scripts/recipes/presentation/first-sevii-presentation.lock.json')
        if lock.exists():
            b=lock.read_bytes();self.lock={r['path']:r for r in json.loads(b)['files']}
    def read(self,p):
        if p in self.raw:return self.raw[p]
        b=subprocess.check_output(['git','-C',str(self.reference),'show',COMMIT+':'+p]) if self.reference else safe(self.root,'content/raw/pokefirered/037335f4/'+p).read_bytes()
        row=dict(path=p)
        if self.lock is not None:need(self.lock.get(p)==row,'Original source differs: '+p)
        self.files[p]=row;self.raw[p]=b;return b
    def text(self,p):return self.read(p).decode()

def build(root,reference,dependency):
    s=Source(root,reference);out={};assets=[]
    def add(identity,w,h,rgba,inputs):
        need(len(rgba)==w*h*4,'RGBA dimensions differ');out[identity+'.png']=png(w,h,rgba)
        assets.append(dict(id=identity, width=w, height=h, texturePath=identity + '.png'))
    def palette(data):
        lines=data.decode().splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Source palette differs')
        return [tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
    def indexed(image_path,palette_path=None,pad=None):
        image=Image.open(io.BytesIO(s.read(image_path)));need(image.mode=='P' and max(image.tobytes())<16,'Source image differs')
        pal=palette(s.read(palette_path)) if palette_path else [tuple((c>>3<<3)|(c>>3>>2) for c in image.getpalette()[i:i+3]) for i in range(0,48,3)]
        w,h=image.size;rgba=bytes(c for index in image.tobytes() for c in (*pal[index],255 if index else 0))
        if pad:need(w==pad[0] and h<=pad[1],'Source padding differs');rgba+=bytes(w*(pad[1]-h)*4);h=pad[1]
        return w,h,rgba
    ferry=s.text('src/seagallop.c')
    for marker in ['++task->data[1] == 140','ChangeBgX(3, 0x600, 1)','gSprites[spriteId].data[0] = 48','sprite->data[2] % 5 == 0','0x1888','ANIMCMD_FRAME(0x00, 0x14)','ANIMCMD_FRAME(0x10, 0x14)','ANIMCMD_FRAME(0x20, 0x0f)','BGMusicStopped() && !gPaletteFade.active']:
        need(marker in ferry,'Ferry source binding differs: '+marker)
    for p in ['include/constants/seagallop.h','data/scripts/seagallop.inc','src/palette.c','src/field_weather.c','src/field_fadetransition.c','src/overworld.c','src/sprite.c','graphics_file_rules.mk']:s.read(p)
    need('graphics/seagallop/water.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles 41' in s.text('graphics_file_rules.mk'),'Ferry water tile truncation differs')
    for n,pad in [('ferry_sprite',(64,64)),('wake',None)]:
        p='graphics/seagallop/'+n+'.png';pal='graphics/seagallop/ferry_and_wake.pal';w,h,rgba=indexed(p,pal,pad);add('seagallop-'+n,w,h,rgba,[p,pal])
    image=Image.open(io.BytesIO(s.read('graphics/seagallop/water.png')));need(image.size==(64,48) and image.mode=='P','Water source image differs');ix=image.tobytes();pal=[tuple((c>>3<<3)|(c>>3>>2) for c in image.getpalette()[i:i+3]) for i in range(0,48,3)]
    tiles=[bytes(ix[(ty*8+y)*64+tx*8+x] for y in range(8) for x in range(8)) for ty in range(6) for tx in range(8)][:41]
    for direction in ['eb','wb']:
        p='graphics/seagallop/'+direction+'_tilemap.bin';b=s.read(p);need(len(b)==2048,'Water tilemap size differs');rgba=bytearray(256*256*4)
        for pos,(tile,) in enumerate(struct.iter_unpack('<H',b)):
            n=tile&1023;need(n<41 and (tile>>12==4 or tile==0),'Water tile/palette index differs')
            for y in range(8):
                for x in range(8):
                    index=tiles[n][(7-y if tile&2048 else y)*8+(7-x if tile&1024 else x)];target=((pos//32*8+y)*256+pos%32*8+x)*4
                    rgba[target:target+4]=bytes((*(pal[index] if tile>>12==4 else (0,0,0)),255))
        add('seagallop-water-'+direction,256,256,bytes(rgba),['graphics/seagallop/water.png',p])
    item_table=s.text('src/data/item_icon_table.h');item_graphics=s.text('src/data/graphics/items.h');item_data=json.loads(s.read('src/data/items.json'))['items'];items=[]
    for name in ITEMS:
        symbol='ITEM_'+name;binding=re.search(r'\['+symbol+r'\]\s*=\s*\{(\w+),\s*(\w+)\}',item_table);need(binding is not None,'Item binding missing')
        paths=[]
        for g in binding.groups():
            m=re.search(r'const u32 '+g+r'\[\] = INCBIN_U32\("([^"]+)"\)',item_graphics);need(m is not None,'Item resource missing');paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
        w,h,rgba=indexed(*paths);need((w,h)==(24,24),'Item dimensions differ');identity='item-'+name.lower().replace('_','-');add(identity,w,h,rgba,paths)
        row=next(d for d in item_data if d['itemId']==symbol);items.append(dict(id='pret:'+symbol,symbol=symbol,name=row['english'],description=row['description_english'].replace(chr(92)+'n','\n'),fieldUseFunction=row['fieldUseFunc'],pocket=row['pocket'],assetId=identity))
    graphics=s.text('src/data/graphics/trainers.h');pic_table=s.text('src/data/trainer_graphics/front_pic_tables.h');constants=s.text('include/constants/trainers.h');classes=s.text('src/data/text/trainer_class_names.h');trainer_source=s.text('src/data/trainers.h');portraits=[];trainers=[]
    for stem,symbol in PORTRAITS:
        p='graphics/trainers/front_pics/'+stem+'_front_pic.png';pal='graphics/trainers/palettes/'+stem+'.pal'
        need(f'gTrainerFrontPic_{symbol}[] = INCBIN_U32("{p.replace(".png",".4bpp.lz")}")' in graphics and f'TRAINER_SPRITE({stem.upper()}, gTrainerFrontPic_{symbol}, 0x800)' in pic_table,'Trainer sprite binding differs')
        w,h,rgba=indexed(p,pal);need((w,h)==(64,64),'Trainer dimensions differ');identity='first-sevii-'+stem.replace('_','-')+'-front';add(identity,w,h,rgba,[p,pal]);picture='TRAINER_PIC_'+stem.upper();numeric=int(re.search(r'^#define '+picture+r'\s+(\d+)',constants,re.M)[1]);portraits.append(dict(assetId=identity,trainerPic=picture,sourceTrainerPicId=numeric))
    for name in TRAINERS:
        symbol='TRAINER_'+name;m=re.search(r'\['+symbol+r'\]\s*=\s*\{(.*?)\n    \}',trainer_source,re.S);need(m is not None,'Trainer binding missing: '+symbol);body=m[1]
        cls=re.search(r'\.trainerClass = (\w+)',body)[1];pic=re.search(r'\.trainerPic = (\w+)',body)[1];name=re.search(r'\.trainerName = _\("([^"]+)"\)',body)[1];display=re.search(r'\['+cls+r'\]\s*= _\("([^"]+)"\)',classes)[1]
        trainers.append(dict(id='pret:'+symbol,name=name,trainerPic=pic,trainerClass=cls,className=display))
    out['presentation.json']=canonical(dict(schemaVersion=1,assets=assets,items=items,portraits=portraits,trainers=trainers))
    lock=canonical(dict(schemaVersion=1,profile=PROFILE,repository='pret/pokefirered',commit=COMMIT,files=[s.files[p] for p in sorted(s.files)]))
    manifest=canonical(dict(schemaVersion=1,outputs=[dict(path=p) for p,b in sorted(out.items())]))
    result={'content/generated/ui/first-sevii-presentation/'+p:b for p,b in out.items()};result['content/generated/ui/first-sevii-presentation/manifest.json']=manifest;result['scripts/recipes/presentation/first-sevii-presentation.lock.json']=lock
    result.update({'content/raw/pokefirered/037335f4/'+p:b for p,b in s.raw.items()})

    return result,dict(passed=True,assets=len(assets),itemIcons=len(items),trainerPortraits=len(portraits),trainers=len(trainers),originalSources=len(s.files))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--dependency-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();out,result=build(a.project_root,a.reference_root,a.dependency_root or a.project_root)
    for rel,b in out.items():
        target=safe(a.project_root,rel)
        if a.check:need(target.read_bytes()==b,'Generated output differs: '+rel)
        else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
    print(json.dumps(result))
