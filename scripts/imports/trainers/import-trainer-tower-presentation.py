#!/usr/bin/env python3
"""Import pinned Trainer Tower portraits, new opponent pixels, shiny variants and prize icons; no audio."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,  importlib.util, io, json, re, sys, tempfile
from pathlib import Path
from PIL import Image
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
PROFILE='firered-trainer-tower-presentation-v1'
PACKAGE='sprites/pokemon/trainer-tower'
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
NEW_SPECIES=['AMPHAROS','BLISSEY','CORSOLA','DUNSPARCE','ESPEON','FERALIGATR','FLAREON','GRANBULL','LEDIAN','MAGBY','MANTINE','POLITOED','POLIWRATH','PORYGON2','QWILFISH','SMOOCHUM','TOGEPI','VAPOREON']
# Meowth and Seaking are already admitted species; Tower needs their source shiny forms.
SPECIES=sorted(NEW_SPECIES+['MEOWTH','SEAKING'])
# Earlier packages own these identical pixels. Tower retains its own geometry and
# source annotations, and checks the shared image against its source conversion.
SHARED_ART = {'aroma-lady-front': 'content/generated/ui/first-sevii-presentation/first-sevii-aroma-lady-front.png',
 'beauty-front': 'content/generated/sprites/trainers/celadon-gym/beauty-front.png',
 'biker-front': 'content/generated/sprites/trainers/celadon-approach/biker-front.png',
 'black-belt-front': 'content/generated/ui/viridian-gym-presentation/black-belt-front.png',
 'bug-catcher-front': 'content/generated/sprites/trainers/forest/bug-catcher-front.png',
 'burglar-front': 'content/generated/sprites/trainers/cinnabar/burglar-front.png',
 'camper-front': 'content/generated/sprites/trainers/pewter/camper-front.png',
 'cool-couple-front': 'content/generated/sprites/pokemon/seven-island/cool-couple-front.png',
 'cooltrainer-f-front': 'content/generated/sprites/trainers/celadon-gym/cooltrainer-f-front.png',
 'cooltrainer-m-front': 'content/generated/ui/viridian-gym-presentation/cooltrainer-m-front.png',
 'crush-girl-front': 'content/generated/sprites/pokemon/sevii-ruby/crush-girl-front.png',
 'crush-kin-front': 'content/generated/sprites/pokemon/sevii-ruby/crush-kin-front.png',
 'cue-ball-front': 'content/generated/ui/first-sevii-presentation/first-sevii-cue-ball-front.png',
 'engineer-front': 'content/generated/ui/vermilion-gym-assets/engineer-front.png',
 'fisherman-front': 'content/generated/battle/art/ssanne-battle-art/fisherman-front.png',
 'gentleman-front': 'content/generated/battle/art/ssanne-battle-art/gentleman-front.png',
 'hiker-front': 'content/generated/sprites/trainers/mtmoon/hiker-front.png',
 'juggler-front': 'content/generated/ui/fuchsia-gym-presentation/juggler-front.png',
 'lass-front': 'content/generated/sprites/trainers/route3/lass-front.png',
 'meowth-back-normal': 'content/generated/sprites/pokemon/vermilion/meowth-back-normal.png',
 'meowth-back-shiny': 'content/generated/sprites/pokemon/vermilion/meowth-back-shiny.png',
 'meowth-front-normal': 'content/generated/sprites/pokemon/vermilion/meowth-front-normal.png',
 'meowth-front-shiny': 'content/generated/sprites/pokemon/vermilion/meowth-front-shiny.png',
 'picnicker-front': 'content/generated/data/trainer-battles/cerulean/picnicker-front.png',
 'pokemon-ranger-f-front': 'content/generated/sprites/pokemon/sevii-ruby/pokemon-ranger-f-front.png',
 'pokemon-ranger-m-front': 'content/generated/sprites/pokemon/sevii-ruby/pokemon-ranger-m-front.png',
 'psychic-f-front': 'content/generated/sprites/pokemon/seven-island/psychic-f-front.png',
 'psychic-m-front': 'content/generated/sprites/trainers/saffron-gym/psychic-m-front.png',
 'sailor-front': 'content/generated/battle/art/ssanne-battle-art/sailor-front.png',
 'scientist-front': 'content/generated/sprites/trainers/silph/scientist-front.png',
 'seaking-back-normal': 'content/generated/sprites/pokemon/rocktunnel/seaking-back-normal.png',
 'seaking-back-shiny': 'content/generated/sprites/pokemon/rocktunnel/seaking-back-shiny.png',
 'seaking-front-normal': 'content/generated/sprites/pokemon/rocktunnel/seaking-front-normal.png',
 'seaking-front-shiny': 'content/generated/sprites/pokemon/rocktunnel/seaking-front-shiny.png',
 'sis-and-bro-front': 'content/generated/sprites/trainers/cinnabar/sis-and-bro-front.png',
 'super-nerd-front': 'content/generated/sprites/trainers/mtmoon/super-nerd-front.png',
 'swimmer-f-front': 'content/generated/ui/first-sevii-presentation/first-sevii-swimmer-f-front.png',
 'swimmer-m-front': 'content/generated/data/trainer-battles/cerulean/swimmer-m-front.png',
 'tuber-f-front': 'content/generated/ui/first-sevii-presentation/first-sevii-tuber-f-front.png',
 'twins-front': 'content/generated/ui/double-battle-ui/twins-front.png',
 'young-couple-front': 'content/generated/sprites/pokemon/seven-island/young-couple-front.png',
 'youngster-front': 'content/generated/sprites/trainers/route3/youngster-front.png'}
def canonical(v):return (json.dumps(v,sort_keys=True,indent=2)+'\n').encode()
def need(v,m):
    if not v:raise ValueError(m)
def safe(p):need(not any(x.is_symlink() for x in [p,*p.parents]),'Symlink rejected: '+str(p))
def build(target,dep,ref,check):
    path=dep/'scripts/imports/source/import-cinnabar-source-preparation.py'
    spec=importlib.util.spec_from_file_location('tower_presentation_reader',path);reader=importlib.util.module_from_spec(spec);spec.loader.exec_module(reader);reader.PROFILE=PROFILE
    src=reader.Source(ref);out={};assets=[]
    def indexed(image,pal,shape=None,floor=False):
        im=Image.open(io.BytesIO(src.read(image)));need(im.mode=='P' and (shape is None or im.size==shape) and max(im.tobytes())<16,'Invalid indexed source '+image)
        lines=src.text(pal).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Invalid palette '+pal)
        colors=[tuple((int(c)>>3)*255//31 if floor else ((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
        rgba=bytes(v for i in im.tobytes() for v in (*colors[i],255 if i else 0));return im.size,rgba
    def emit(identity,image,pal,shape=None,floor=False):
        (width,height),rgba=indexed(image,pal,shape,floor);im=Image.frombytes('RGBA',(width,height),rgba);buf=io.BytesIO();im.save(buf,format='PNG',optimize=False,compress_level=9)
        texture_path = SHARED_ART.get(identity, f'content/generated/{PACKAGE}/{identity}.png')
        if identity in SHARED_ART:
            shared = dep / texture_path
            safe(shared)
            with Image.open(shared) as existing:
                need(existing.size == (width, height) and existing.convert('RGBA').tobytes() == rgba,
                     'Shared Tower artwork differs: ' + texture_path)
        else:
            out[identity+'.png']=buf.getvalue()
        row = dict(id=identity, width=width, height=height, texturePath=texture_path,  paletteExpansion='floor(rgb5 * 255 / 31)' if floor else 'replicate rgb5 bits', transparentIndex=0)
        assets.append(row);return row
    graphics=src.text('src/data/graphics/pokemon.h');constants=src.text('include/constants/species.h');sprites=[]
    for name in SPECIES:
        symbol='SPECIES_'+name;number=int(re.search(r'^#define '+symbol+r'\s+(\d+)\s*$',constants,re.M)[1]);stem=name.lower()
        for side in ['front','back']:
            coords='src/data/pokemon_graphics/'+side+'_pic_coordinates.h';m=re.search(r'\['+symbol+r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',src.text(coords));need(m,'Missing source coordinates '+symbol)
            width,height,y=map(int,m.groups());e=re.search(r'\['+symbol+r'\]\s*=\s*(\d+)',src.text('src/data/pokemon_graphics/enemy_mon_elevation.h'));elevation=int(e[1]) if e else 0
            image=f'graphics/pokemon/{stem}/{side}.png';need(image[:-4]+'.4bpp.lz' in graphics,'Source image declaration differs')
            for variant in ['normal','shiny']:
                pal=f'graphics/pokemon/{stem}/{variant}.pal';need(pal[:-4]+'.gbapal.lz' in graphics,'Source palette declaration differs')
                asset=emit(stem+'-'+side+'-'+variant,image,pal,(64,64),True)
                sprites.append(dict(speciesId='pret:'+symbol,sourceSpeciesId=number,side=side,paletteVariant=variant,x=144 if side=='front' else 40,y=8+y-elevation if side=='front' else 48+y,sourceWidth=width,sourceHeight=height,sourceYOffset=y,sourceEnemyElevation=elevation,newOpponentSpecies=name in NEW_SPECIES,**asset))
    sets=src.text('src/trainer_tower_sets.c');facilities=sorted(set(re.findall(r'\.facilityClass = (FACILITY_CLASS_\w+)',sets)))
    lookups=src.text('src/data/pokemon/trainer_class_lookups.h');pics_table=lookups.split('const u8 gFacilityClassToTrainerClass')[0]
    trainer_graphics=src.text('src/data/graphics/trainers.h');picture_table=src.text('src/data/trainer_graphics/front_pic_tables.h');trainer_constants=src.text('include/constants/trainers.h');class_names=src.text('src/data/text/trainer_class_names.h')
    coords=re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}',picture_table.split('const struct CompressedSpriteSheet')[0]);portraits=[];bindings=[];done={}
    for facility in facilities:
        pic=re.search(r'\['+facility+r'\]\s*=\s*(TRAINER_PIC_\w+)',pics_table)[1];cls=re.search(r'\['+facility+r'\]\s*=\s*(TRAINER_CLASS_\w+)',lookups)[1]
        class_name=re.search(r'\['+cls+r'\]\s*=\s*_\("([^\"]+)"\)',class_names)[1]
        number=int(re.search(r'^#define '+pic+r'\s+(\d+)\s*$',trainer_constants,re.M)[1]);key=pic.removeprefix('TRAINER_PIC_')
        bindings.append(dict(facilityClass=facility,trainerPic=pic,sourceTrainerPicId=number,trainerClass=cls,className=class_name,portrait=key.lower().replace('_','-')+'-front'))
        if pic in done:continue
        done[pic]=True
        ps=re.search(r'TRAINER_SPRITE\('+key+r', (\w+), 0x800\)',picture_table)[1];cs=re.search(r'TRAINER_PAL\('+key+r', (\w+)\)',picture_table)[1];paths=[]
        for sym in [ps,cs]:
            declared=re.search(r'const u32 '+sym+r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)',trainer_graphics)[1];paths.append(declared.replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
        portraits.append(dict(trainerPic=pic,sourceTrainerPicId=number,x=144,y=8+int(coords[number][1]),sourceYOffset=int(coords[number][1]),**emit(key.lower().replace('_','-')+'-front',*paths,(64,64))))
    tower=src.text('src/trainer_tower.c');prizes=re.findall(r'ITEM_\w+',re.search(r'sPrizeList\[\] = \{(.*?)\n\};',tower,re.S)[1]);need(len(prizes)==15,'Prize scope differs')
    item_graphics=src.text('src/data/graphics/items.h');icon_table=src.text('src/data/item_icon_table.h');icons=[]
    for item in prizes:
        symbols=re.search(r'\['+item+r'\]\s*=\s*\{(\w+), (\w+)\}',icon_table).groups();paths=[]
        for sym in symbols:
            declared=re.search(r'const u32 '+sym+r'\[\]\s*= INCBIN_U32\("([^\"]+)"\)',item_graphics)[1];paths.append(declared.replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
        icons.append(dict(itemId='pret:'+item,prizeIndex=prizes.index(item),**emit('items/'+item.removeprefix('ITEM_').lower().replace('_','-'),*paths)))
    # Retain original UI declaration text for labels/results and original elevator animation bindings.
    for p in ['include/constants/trainer_tower.h','src/script_menu.c','src/data/text/trainer_class_names.h','src/field_door.c']:
        src.read(p,'presentation-metadata')
    door_animations=[]
    door_source=src.text('src/field_door.c')
    for name,palette_slot in [('Lobby',8),('Roof',11)]:
        symbol='TrainerTower'+name+'Elevator';image='graphics/door_anims/trainer_tower_'+name.lower()+'_elevator.png'
        im=Image.open(io.BytesIO(src.read(image)));need(im.mode=='P' and im.size==(16,96),'Original elevator animation sheet differs')
        palette_slots=list(map(int,re.search(r'sDoorAnimPalettes_'+symbol+r'\[\] = \{([^}]+)\}',door_source)[1].split(',')))
        need(palette_slots==[palette_slot,palette_slot,2,2,2,2,2,2],'Elevator tile palette mapping differs')
        palette_paths=['data/tilesets/primary/building/palettes/02.pal',f'data/tilesets/secondary/trainer_tower/palettes/{palette_slot:02d}.pal']
        for palette in palette_paths:src.read(palette,'elevator-animation-palette')
        door_animations.append(dict(id=name.lower()+'-elevator',sourceRef=src.ref('src/field_door.c','sDoorGraphics'),indexedSourcePath=image,width=16,height=96,frameWidth=16,frameHeight=32,frameCount=3,paletteSlots=palette_slots,palettePaths=palette_paths,openFrames=[dict(duration=4,tileOffset=i) for i in [65535,0,256,512]],closeFrames=[dict(duration=4,tileOffset=i) for i in [512,256,0,65535]],closedFrameUsesMapMetatiles=True))
    catalog=dict(schemaVersion=1,newOpponentSpecies=NEW_SPECIES,newOwnedSpecies=[],sprites=sprites,trainerBindings=bindings,trainerPortraits=portraits,itemIcons=icons,assets=assets,doorAnimations=door_animations,audioIncluded=False,
      fixedShinyCases=[dict(course='Single',floor=7,species='SPECIES_MEOWTH'),dict(course='Double',floor=1,species='SPECIES_ESPEON'),dict(course='Knockout',floor=4,species='SPECIES_SEAKING')])
    out['catalog.json']=canonical(catalog)
    lock=src.lock();lock.update(gameVersion='FIRERED',revision=0,completeGame=False);lb=canonical(lock)
    manifest=dict(schemaVersion=1,inputCount=len(src.entries),outputCount=len(out),spriteCount=len(sprites),portraitCount=len(portraits),itemIconCount=len(icons),assetCount=len(assets),authorDependencies=[dict(path='scripts/imports/source/import-cinnabar-source-preparation.py')],outputs=[dict(path=p) for p,b in sorted(out.items())])
    mb=canonical(manifest)
    writes=[(target / 'content/generated' / PACKAGE / 'manifest.json', mb)]+[(target/'content/raw/pokefirered/037335f4'/p,b) for p,b in sorted(src.data.items())]+[(target/'content/generated'/PACKAGE/p,b) for p,b in sorted(out.items())]
    for p,b in writes:
        safe(p)
        if check:need(p.is_file() and p.read_bytes()==b,'Regeneration differs '+str(p))
        else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
    print(json.dumps({k:v for k,v in manifest.items() if k not in ['outputs','authorDependencies']}|dict(check=check),indent=2))
def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',required=True,type=Path);ap.add_argument('--project-root',type=Path,default=ROOT);ap.add_argument('--dependency-root',type=Path,default=ROOT);ap.add_argument('--check',action='store_true');a=ap.parse_args()
    for p in [a.reference_root,a.project_root,a.dependency_root]:safe(p.absolute())
    ref,target,dep=[p.resolve() for p in [a.reference_root,a.project_root,a.dependency_root]];need(not(target==ref or target.is_relative_to(ref) or ref.is_relative_to(target)),'Source/output overlap');build(target,dep,ref,a.check)
if __name__=='__main__':main()
