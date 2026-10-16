#!/usr/bin/env python3
"""Import pinned native ball frames, including LoadBallGfx's palette-index overlay."""
import sys, json, re, argparse, tempfile, os
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'lib'))
from project_paths import PROJECT_ROOT
import ui_tiles as codec
ROOT = PROJECT_ROOT
RAW = ROOT/'content/raw/pokefirered/037335f4'
OUT = ROOT/'content/generated/battle/capture-balls'
BALLS = ['master','ultra','great','poke','safari','net','dive','nest','repeat','timer','luxury','premier']

def convert(raw=RAW):
    header=(raw/'src/data/graphics/interface_pokeballs.h').read_text()
    pw,ph,opened,_=codec.indexed_png((raw/'graphics/interface/ball_open.png').read_bytes())
    if (pw,ph)!=(16,16): raise ValueError('Invalid native open-ball frame')
    atlas=bytearray(192*48*4)
    for i,ball in enumerate(BALLS):
        if f'graphics/interface/ball/{ball}.4bpp.lz' not in header: raise ValueError('Missing source ball binding: '+ball)
        w,h,pixels,palette=codec.indexed_png((raw/f'graphics/interface/ball/{ball}.png').read_bytes())
        if (w,h)!=(16,48): raise ValueError('Unexpected ball dimensions: '+ball)
        if ball not in ('dive','luxury','premier'): pixels=pixels[:512]+opened
        rgba=codec.rgba(pixels,codec.colors(palette))
        for y in range(48): atlas[(y*192+i*16)*4:(y*192+i*16+16)*4]=rgba[y*64:(y+1)*64]
    trig=(raw/'src/trig.c').read_text().split('const s16 gSineTable[] =',1)[1].split('};',1)[0]
    sine=[round(float(v)*256) for v in re.findall(r'Q_8_8\(([-\d.]+)\)',trig)]
    if len(sine)<320 or sine[0]!=0 or sine[64]!=256: raise ValueError('Native sine table invalid')
    pw,ph,particles,_=codec.indexed_png((raw/'graphics/battle_anims/sprites/particles.png').read_bytes())
    _,_,_,palette=codec.indexed_png((raw/'graphics/battle_anims/sprites/circle_impact.png').read_bytes())
    if (pw,ph)!=(8,64): raise ValueError('Particle tiles changed')
    particle_png=codec.png(pw,ph,codec.rgba(particles,codec.colors(palette)))
    special=(raw/'src/battle_anim_special.c').read_text()
    colors={name.lower():int(r)|(int(g)<<5)|(int(b)<<10) for name,r,g,b in re.findall(r'\[BALL_(\w+)\]\s*= RGB\((\d+), (\d+), (\d+)\)',special)}
    trainers=bytearray(128*320*4)
    for gender,name in enumerate(('red','leaf')):
        tw,th,indices,_=codec.indexed_png((raw/f'graphics/trainers/back_pics/{name}_back_pic.png').read_bytes())
        if (tw,th)!=(64,320): raise ValueError('Native trainer throw frames changed')
        rgba=codec.rgba(indices,codec.palette((raw/f'graphics/trainers/palettes/{name}_back_pic.pal').read_bytes()))
        for y in range(320):trainers[(y*128+gender*64)*4:(y*128+gender*64+64)*4]=rgba[y*256:(y+1)*256]
    anims=(raw/'src/data/trainer_graphics/back_pic_anims.h').read_text()
    trainer_frames=[]
    for name in ('Red','Leaf'):
        block=anims.split(f'sAnimCmd_{name}_1[] =',1)[1].split('};',1)[0]
        frames=[tuple(map(int,pair)) for pair in re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)',block)]
        if trainer_frames and trainer_frames!=frames: raise ValueError('Trainer throws require distinct timelines')
        trainer_frames=frames
    main=(raw/'src/battle_main.c').read_text()
    offsets=[int(v) for v in main.split('sPlayerThrowXTranslation[] = {',1)[1].split('}',1)[0].split(',')]
    data={'trainerThrowFrames':[{'frame':f,'ticks':t,'xOffset':offsets[i]} for i,(f,t) in enumerate(trainer_frames)],'fadeColors':[colors[name] for name in BALLS],'schemaVersion':1,'texturePath':'balls.png','width':192,'height':48,'balls':[
        {'ball':i+1,'x':i*16} for i in range(12)],'sine':sine[:320]}
    return codec.png(128,320,bytes(trainers)), particle_png, codec.png(192,48,bytes(atlas)), (json.dumps(data,separators=(',',':'))+'\n').encode()

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw',type=Path,default=RAW)
    parser.add_argument('--out',type=Path,default=OUT)
    parser.add_argument('--check',action='store_true',help='Compare conversion with generated files without writing')
    args=parser.parse_args()
    trainers,particles,png,manifest=convert(args.raw)
    outputs={'trainers.png':trainers,'particles.png':particles,'balls.png':png,'balls.json':manifest}
    if args.check:
        for name,data in outputs.items():
            if (args.out/name).read_bytes()!=data: raise ValueError('Capture ball conversion differs: '+name)
    else:
        args.out.mkdir(parents=True,exist_ok=True)
        for name,data in outputs.items():
            with tempfile.NamedTemporaryFile(dir=args.out,delete=False) as handle:
                temporary=Path(handle.name)
                try:
                    handle.write(data);handle.flush();os.fsync(handle.fileno());os.replace(temporary,args.out/name)
                finally:temporary.unlink(missing_ok=True)
    print(json.dumps({'status':'verified','balls':12,'frames':36,'particleTiles':8,'outputs':len(outputs),'check':args.check}))

if __name__=='__main__': main()
