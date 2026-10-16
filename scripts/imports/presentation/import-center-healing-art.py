#!/usr/bin/env python3
"""Import fixed local healing sprites and all shared RGB555 palette phases; no upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse, json, pathlib, re, subprocess, sys
sys.dont_write_bytecode = True
ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
PROFILE = 'firered-center-healing-art-v1'
PATHS = frozenset(['src/field_effect.c', 'src/sprite.c', 'src/sound.c', 'data/field_effect_scripts.s',
    'graphics/field_effects/pics/pokeball_glow.png', 'graphics/field_effects/pics/pokemoncenter_monitor.png'])
import ui_tiles as codec

def canonical(value): return (json.dumps(value, sort_keys=True, indent=2)+'\n').encode()
def pairs(values):
    result = {}
    for key, value in values:
        if key in result: raise ValueError('Duplicate JSON member: '+key)
        result[key] = value
    return result
def safe(root, relative):
    root = pathlib.Path(root)
    if root.is_symlink(): raise ValueError('Healing root symlink rejected')
    root = root.resolve(); candidate = root/relative
    if pathlib.PurePosixPath(relative).is_absolute() or '..' in pathlib.PurePosixPath(relative).parts or '\\' in relative or not candidate.resolve().is_relative_to(root):
        raise ValueError('Unsafe healing path: '+relative)
    if any(p.is_symlink() for p in [candidate, *candidate.parents] if p != root and p.is_relative_to(root)):
        raise ValueError('Healing symlink rejected: '+relative)
    return candidate
def load_lock(path=ROOT/'scripts/recipes/presentation/center-healing-art.lock.json'):
    data=pathlib.Path(path).read_bytes()
    lock=json.loads(data,object_pairs_hook=pairs)
    if (lock['schemaVersion'],lock['repository'],lock['commit'],lock['profile'])!=(1,'pret/pokefirered',COMMIT,PROFILE): raise ValueError('Healing source identity differs')
    if len(lock['files'])!=len(PATHS) or {e['path'] for e in lock['files']}!=PATHS: raise ValueError('Healing source set differs')
    return lock
def read_sources(lock, raw, repository=None):
    pending=[]; sources={}
    for e in lock['files']:
        target=safe(raw,e['path'])
        data=target.read_bytes() if target.exists() else subprocess.check_output(['git','-C',str(repository),'show',COMMIT+':'+e['path']]) if repository else target.read_bytes()
        sources[e['path']]=data; pending.append((target,data))
    for target,data in pending:
        target.parent.mkdir(parents=True,exist_ok=True); target.write_bytes(data)
    return sources
def palette_variants(rgb):
    base=[tuple(c>>3 for c in row) for row in rgb[:16]]
    if len(base)!=16: raise ValueError('Healing palette requires sixteen colors')
    variants=[base]
    for mode in ['staggered','uniform']:
        for counter in range(4):
            colors=list(base)
            for index,offset in [(8,3),(6,2),(2,1),(5,0),(3,0)]:
                phase=((counter+offset)&3) if mode=='staggered' else counter
                factor=[16,12,8,0][phase]
                colors[index]=tuple(c+(((31-c)*factor)>>4) if channel<2 else c for channel,c in enumerate(base[index]))
            variants.append(colors)
    return [[tuple((c<<3)|(c>>2) for c in row) for row in colors] for colors in variants]
def convert(lock,sources):
    src=sources['src/field_effect.c'].decode()
    for needle in ['.oam = &sOamData_8x8,','.oam = &sOamData_32x16,','.paletteTag = 4103,','sprite->sTimer = 25;','sprite->sTimer = 32;','sprite->sTimer = 30;','ANIMCMD_LOOP(3)','task->tFirstBallX = 93;','task->tFirstBallY = 36;','task->tMonitorX = 128;','task->tMonitorY = 24;']:
        if needle not in src: raise ValueError('Healing source contract differs: '+needle)
    match=re.search(r'sAnim_Flicker\[\] = \{(.*?)\n\};',src,re.S)
    frames=[dict(frame=int(f),ticks=int(t)) for f,t in re.findall(r'ANIMCMD_FRAME\((\d+), (\d+)\)',match[1])]
    if frames!=[dict(frame=f,ticks=t) for f,t in [(1,5),(2,5),(3,7),(2,5),(1,5),(0,5)]]: raise ValueError('Healing monitor animation differs')
    if not re.search(r'FANFARE_HEAL\].*?MUS_HEAL,\s*160',sources['src/sound.c'].decode()): raise ValueError('Healing fanfare counter differs')
    bw,bh,balls,rgb=codec.indexed_png(sources['graphics/field_effects/pics/pokeball_glow.png'])
    mw,mh,monitor,_=codec.indexed_png(sources['graphics/field_effects/pics/pokemoncenter_monitor.png'])
    if (bw,bh,mw,mh)!=(8,8,32,64): raise ValueError('Healing source PNG dimensions differ')
    palettes=palette_variants(rgb); outputs={}; assets=[]
    for id,fw,fh,count,indices in [('pokeball-glow',8,8,1,balls),('pokecenter-monitor',32,16,4,monitor)]:
        w=fw*count;h=fh*len(palettes);out=bytearray(w*h*4)
        for variant,pal in enumerate(palettes):
            for frame in range(count):
                rgba=codec.rgba(indices[frame*fw*fh:(frame+1)*fw*fh],pal)
                codec.blit(out,w,rgba,fw,fh,frame*fw,variant*fh)
        data=codec.png(w,h,out);outputs[id+'.png']=data
        assets.append(dict(id=id,texturePath=id+'.png',width=w,height=h,frameWidth=fw,frameHeight=fh,frameCount=count,paletteCount=len(palettes)))
    document=dict(schemaVersion=1,assets=assets,
        firstBall=dict(x=93,y=36),ballOffsets=[dict(x=x,y=y) for x,y in [(0,0),(6,0),(0,4),(6,4),(0,8),(6,8)]],monitor=dict(x=128,y=24),
        monitorFrames=frames,monitorLoops=4,paletteVariants=['base']+['staggered-'+str(i) for i in range(4)]+['uniform-'+str(i) for i in range(4)],
        timing=dict(ballSpacingTicks=25,lastBallPauseTicks=32,flashPhaseTicks=8,firstFlashCycles=3,lastFlashTicks=24,afterFlashTicks=30,fanfareCounterTicks=160,activeTicksBase=195))
    outputs['center-healing-art.json']=runtime_json_bytes(document, sort_keys=True)
    outputs['manifest.json']=canonical(dict(outputs=[dict(path=p) for p,b in sorted(outputs.items())],))
    return outputs
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--repository',type=pathlib.Path);parser.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');parser.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/ui/center-healing-art');parser.add_argument('--lock',type=pathlib.Path,default=ROOT/'scripts/recipes/presentation/center-healing-art.lock.json');args=parser.parse_args()
    lock=load_lock(args.lock);outputs=convert(lock,read_sources(lock,args.raw,args.repository))
    for name,data in outputs.items():
        target=safe(args.out,name);target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    print(json.dumps(dict(status='verified',sourceFiles=len(lock['files']),outputs=len(outputs))))
if __name__=='__main__': main()
