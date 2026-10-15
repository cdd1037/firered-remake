#!/usr/bin/env python3
"""Read pinned local source bytes and render the original early-rival settled front frame."""
import argparse,  io, json, pathlib, re, subprocess
from PIL import Image

ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def canonical(value): return (json.dumps(value,sort_keys=True,indent=2)+'\n').encode()
def safe(root,relative):
    if pathlib.PurePosixPath(relative).is_absolute() or '\\' in relative or any(p in ('','.','..') for p in relative.split('/')):
        raise ValueError('Unsafe source path')
    result=root/relative
    if any(p.is_symlink() for p in [result,*result.parents]): raise ValueError('Symlink source path')
    return result

def run(repository,raw,output):
    lock_bytes=(ROOT/'content/route22-trainer-art-source-lock.json').read_bytes()
    lock=json.loads(lock_bytes)
    if (lock['repository'],lock['commit'],len(lock['files']))!=('pret/pokefirered',COMMIT,8): raise ValueError('Source identity differs')
    project=json.loads((ROOT/'content/source-lock.json').read_bytes())
    if (project['source']['sourceCommit'],project['referenceGame']['gameEdition'],project['referenceGame']['romRevision'])!=(COMMIT,'FIRERED',0): raise ValueError('Source profile differs')
    sources={}
    for entry in lock['files']:
        path=entry['path']
        data=subprocess.check_output(['git','-C',str(repository),'show',COMMIT+':'+path]) if repository else safe(raw,path).read_bytes()
        sources[path]=data
        if repository:
            target=safe(raw,path);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    def require(path,text):
        if text not in sources[path].decode(): raise ValueError('Source declaration differs: '+text)
    require('include/constants/trainers.h','#define TRAINER_PIC_RIVAL_EARLY           106')
    require('include/constants/trainers.h','#define TRAINER_CLASS_RIVAL_EARLY         81')
    require('src/data/graphics/trainers.h','gTrainerFrontPic_RivalEarly[] = INCBIN_U32("graphics/trainers/front_pics/rival_early_front_pic.4bpp.lz")')
    require('src/data/graphics/trainers.h','gTrainerPalette_RivalEarly[] = INCBIN_U32("graphics/trainers/palettes/rival_early.gbapal.lz")')
    require('src/data/trainer_graphics/front_pic_tables.h','TRAINER_SPRITE(RIVAL_EARLY, gTrainerFrontPic_RivalEarly, 0x800)')
    require('src/data/trainer_graphics/front_pic_tables.h','TRAINER_PAL(RIVAL_EARLY, gTrainerPalette_RivalEarly)')
    coords=re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}',sources['src/data/trainer_graphics/front_pic_tables.h'].decode())
    if tuple(map(int,coords[106]))!=(8,1): raise ValueError('Rival source sprite geometry differs')
    require('src/battle_controller_opponent.c','(8 - gTrainerFrontPicCoords[trainerPicId].size) * 4 + 40')
    require('tools/gbagfx/gfx.c','#define DOWNCONVERT_BIT_DEPTH(x) ((x) / 8)')
    for starter in ['SQUIRTLE','BULBASAUR','CHARMANDER']:
        block=sources['src/data/trainers.h'].decode().split('[TRAINER_RIVAL_ROUTE22_EARLY_'+starter+'] =')[1].split('},',1)[0]
        if '.trainerPic = TRAINER_PIC_RIVAL_EARLY' not in block or '.trainerClass = TRAINER_CLASS_RIVAL_EARLY' not in block: raise ValueError('Rival trainer mapping differs')
    path='graphics/trainers/front_pics/rival_early_front_pic.png';palette='graphics/trainers/palettes/rival_early.pal'
    with Image.open(io.BytesIO(sources[path])) as image:
        if image.mode!='P' or image.size!=(64,64): raise ValueError('Rival indexed geometry differs')
        indices=image.tobytes()
    lines=sources[palette].decode().splitlines()
    if lines[:3]!=['JASC-PAL','0100','16'] or len(lines)!=19 or max(indices)>15: raise ValueError('Rival palette differs')
    colors=[tuple((v//8<<3)|(v//8>>2) for v in map(int,line.split())) for line in lines[3:]]
    rgba=bytes(v for index in indices for v in (*colors[index],255 if index else 0))
    output.mkdir(parents=True,exist_ok=True)
    name='rival-early-front'
    Image.frombytes('RGBA',(64,64),rgba).save(output/(name+'.png'))
    asset = dict(id=name, texturePath=name + '.png', width=64, height=64, x=144, y=8, sourceImage=path, sourcePalette=palette, sourceFrame=0, sourceTrainerPicId=106)
    document=dict(profile='firered-route22-trainer-art-v1',sourceCommit=COMMIT,inputs=lock['files'],assets=[asset],paletteExpansion='(rgb5 << 3) | (rgb5 >> 2)',limitations=['Static settled front frame. Slide, throw, loss portrait timing, native framebuffer and original-ROM visual equivalence remain separate checks.'])
    encoded=canonical(document);(output/'route22-trainer-art.json').write_bytes(encoded)
    print(json.dumps(dict(asset=asset),indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--source-repo',type=pathlib.Path);parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--output',type=pathlib.Path,default=ROOT/'content/generated/route22-trainer-art')
    args=parser.parse_args();run(args.source_repo,args.raw,args.output)
