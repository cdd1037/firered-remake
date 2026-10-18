#!/usr/bin/env python3
"""Basic hit/slice scripts and current-battler geometry import. No ROM input."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse,io,json,re,struct,subprocess,tempfile
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
OPS={'loadspritegfx','monbg','setalpha','playsewithpan','loopsewithpan','createsprite','createvisualtask','delay','waitforvisualfinish','clearmonbg','blendoff','end'}


def read_sources(reference):
    lock = json.loads((_PROJECT_ROOT / 'scripts/recipes/battle/source-hit-animation.lock.json').read_bytes())
    if lock['sourceCommit'] != COMMIT:
        raise ValueError('Wrong source revision')
    result = {}
    for row in lock['files']:
        path = row['path']
        result[path] = (subprocess.check_output(['git', '-C', str(reference), 'show', COMMIT + ':' + path])
                        if (reference / '.git').exists() else (reference / path).read_bytes())
    return result

def rgb555(data,size,palette=None):
 with Image.open(io.BytesIO(data)) as im:
  if im.size!=size or im.mode!='P':raise ValueError('Original indexed sprite shape differs')
  im.load();pal=palette if palette is not None else im.getpalette()
  indices=list(im.tobytes());values=[]
  for index in indices:
   if index>15:raise ValueError('Original 4bpp index overflow')
   red,green,blue=pal[index*3:index*3+3]
   values.append((red>>3)|((green>>3)<<5)|((blue>>3)<<10)|(0x8000 if index else 0))
  return struct.pack('<HH',*size)+struct.pack('<'+'H'*len(values),*values)

def indexed_impact(raw):
 # Preserve native indices even when palette entries repeat. Palette animation
 # uses the source tag/bank, never a reverse lookup from the rendered colors.
 registrations=raw['src/data/battle_anim.h'].decode();graphics=raw['src/graphics.c'].decode()
 if not re.search(r'\{gBattleAnimSpriteGfx_Impact,\s*0x0200,\s*ANIM_TAG_IMPACT\}',registrations,re.I) or not re.search(r'\{gBattleAnimSpritePal_Impact,\s*ANIM_TAG_IMPACT\}',registrations):raise ValueError('Impact source registration differs')
 for kind,suffix in [('Gfx','4bpp'),('Pal','gbapal')]:
  if not re.search(r'gBattleAnimSprite'+kind+r'_Impact\[\] = INCBIN_U32\("graphics/battle_anims/sprites/impact\.'+suffix+r'\.lz"\)',graphics):raise ValueError('Impact source material differs')
 with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/impact.png'])) as im:
  if im.mode!='P' or im.size!=(32,32):raise ValueError('Impact source indexed shape differs')
  im.load();palette=im.getpalette()[:48]
  indices=[v for y in range(0,32,8) for x in range(0,32,8) for v in im.crop((x,y,x+8,y+8)).tobytes()]
 if len(palette)!=48 or any(v>15 for v in indices):raise ValueError('Impact source 4bpp palette differs')
 packed=bytes(indices[i]|(indices[i+1]<<4) for i in range(0,len(indices),2))
 colors=[(palette[i]>>3)|((palette[i+1]>>3)<<5)|((palette[i+2]>>3)<<10) for i in range(0,48,3)]
 return dict(id='impact',width=32,height=32,indicesPath='impact.4bpp',palettePath='impact.pal16',paletteTag='ANIM_TAG_IMPACT'),{'impact.4bpp':packed,'impact.pal16':struct.pack('<16H',*colors)}

def castform_assets(raw, generic_geometry, rgb555):
    import re
    read=raw.__getitem__
    source=read('src/battle_anim_mons.c').decode()
    constants=read('include/constants/battle.h').decode()
    species=int(re.search(r'^#define SPECIES_CASTFORM\s+(\d+)',read('include/constants/species.h').decode(),re.M)[1])
    def table(name): return re.search(r'\b'+name+r'\[[^\]]+\]\s*=\s*\{(.*?)\};',source,re.S)[1]
    front={k:tuple(map(int,(w,h,y))) for k,w,h,y in re.findall(r'\[(CASTFORM_\w+)\]\s*=\s*\{\s*\.size\s*=\s*MON_COORDS_SIZE\((\d+),\s*(\d+)\),\s*\.y_offset\s*=\s*(\d+)',table('gCastformFrontSpriteCoords'))}
    elev={k:int(v) for k,v in re.findall(r'\[(CASTFORM_\w+)\]\s*=\s*(\d+)',table('sCastformElevations'))}
    backy={k:int(v) for k,v in re.findall(r'\[(CASTFORM_\w+)\]\s*=\s*(\d+)',table('sCastformBackSpriteYCoords'))}
    rules=read('graphics_file_rules.mk').decode()
    folders=re.findall(r'\$\(CASTFORMGFXDIR\)/(\w+)/front\.4bpp',rules)
    symbols=sorted(front,key=lambda k:int(re.search(r'^#define '+k+r'\s+(\d+)',constants,re.M)[1]))
    if len(symbols)!=4 or len(folders)!=4 or set(front)!=set(elev) or set(front)!=set(backy):raise ValueError('Castform source form tables differ')
    for path in ['back.4bpp','normal.gbapal','shiny.gbapal']:
        order=re.findall(r'\$\(CASTFORMGFXDIR\)/(\w+)/'+re.escape(path),rules)
        if order!=folders:raise ValueError('Castform frame/palette concatenation differs')
    base=next(r for r in generic_geometry if r['species']==species and r['side']=='back')
    geometry=[];assets=[];blobs={}
    for symbol,folder in zip(symbols,folders):
        form=int(re.search(r'^#define '+symbol+r'\s+(\d+)',constants,re.M)[1])
        for side in ['front','back']:
            w,h,y=front[symbol] if side=='front' else (base['sourceWidth'],base['sourceHeight'],backy[symbol])
            geometry.append(dict(species=species,form=form,side=side,sourceWidth=w,sourceHeight=h,yOffset=y,elevation=elev[symbol] if side=='front' else 0))
            for variant in ['normal','shiny']:
                text=read(f'graphics/pokemon/castform/{folder}/{variant}.pal').decode().splitlines()
                if text[:3]!=['JASC-PAL','0100','16']:raise ValueError('Castform palette shape differs')
                palette=[int(v) for line in text[3:] for v in line.split()]
                if len(palette)!=48 or any(v<0 or v>255 for v in palette):raise ValueError('Castform palette values differ')
                identity=f'castform-{form}-{side}-{variant}'
                data=rgb555(read(f'graphics/pokemon/castform/{folder}/{side}.png'),(64,64),palette)
                blobs[identity+'.rgb555']=data
                assets.append(dict(id=identity,path=identity+'.rgb555',species=species,form=form,side=side,paletteVariant=variant))
    return geometry,assets,blobs

def additional_mon_assets(raw):
    # CreateAdditionalMonSpriteForMoveAnim(ignoreDeoxys=TRUE) DMA-copies the
    # first 0x800-byte frame, unlike ordinary FireRed Deoxys display (frame1).
    species=int(re.search(r'^#define SPECIES_DEOXYS\s+(\d+)',raw['include/constants/species.h'].decode(),re.M)[1])
    graphics=raw['src/data/graphics/pokemon.h'].decode()
    block=re.search(r'#ifdef FIRERED\n(.*?gMonFootprint_Deoxys.*?\n)#endif',graphics,re.S)
    if block is None:raise ValueError('Deoxys FireRed graphics block is missing')
    assets=[];blobs={}
    for side in ['front','back']:
        image=f'graphics/pokemon/deoxys/{side}.png'
        if f'gMon{side.title()}Pic_Deoxys[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' not in block[1]:raise ValueError('Deoxys base sheet binding differs')
        with Image.open(io.BytesIO(raw[image])) as im:
            if im.mode!='P' or im.size!=(64,128):raise ValueError('Deoxys concatenated frames differ')
            indices=list(im.crop((0,0,64,64)).tobytes())
        if len(indices)!=4096 or any(i>15 for i in indices):raise ValueError('Deoxys base 4bpp frame differs')
        for variant in ['normal','shiny']:
            text=raw[f'graphics/pokemon/deoxys/{variant}.pal'].decode().splitlines()
            if text[:3]!=['JASC-PAL','0100','16']:raise ValueError('Deoxys palette header differs')
            palette=[tuple(map(int,line.split())) for line in text[3:]]
            if len(palette)!=16 or any(len(c)!=3 or any(v<0 or v>255 for v in c) for c in palette):raise ValueError('Deoxys palette values differ')
            values=[(palette[i][0]>>3)|((palette[i][1]>>3)<<5)|((palette[i][2]>>3)<<10)|(0x8000 if i else 0) for i in indices]
            identity=f'deoxys-base-{side}-{variant}'
            blobs[identity+'.rgb555']=struct.pack('<HH',64,64)+struct.pack('<4096H',*values)
            assets.append(dict(id=identity,path=identity+'.rgb555',species=species,side=side,paletteVariant=variant,frame=0))
    return assets,blobs

def build(reference):
 raw=read_sources(reference);script=raw['data/battle_anim_scripts.s'].decode();scripts=[]
 macros=set(re.findall(r'^\s*\.macro (\w+)',raw['asm/macros/battle_anim_script.inc'].decode(),re.M))
 for move,name in [(1,'POUND'),(10,'SCRATCH'),(15,'CUT'),(33,'TACKLE'),(163,'SLASH'),(206,'FALSE_SWIPE'),(68,'COUNTER'),(233,'VITAL_THROW'),(39,'TAIL_WHIP'),(148,'FLASH')]:
  start=re.search(r'^Move_'+name+r':\n',script,re.M)
  if start is None:raise ValueError('Missing original move script')
  rows=[]
  for offset,line in enumerate(script[start.end():].splitlines()):
   text=line.split('@',1)[0].strip()
   if not text:continue
   if text.endswith(':'):raise ValueError('Unexpected branch before end')
   op,*tail=text.split(None,1);args=[x.strip() for x in tail[0].split(',')] if tail else []
   if op not in OPS or op not in macros:raise ValueError('Unsupported slice command: '+op)
   rows.append({'op':op,'args':args,'line':script[:start.end()].count('\n')+offset+1})
   if op=='end':break
  if not rows or rows[-1]['op']!='end':raise ValueError('Unterminated original script')
  scripts.append({'moveId':move,'label':'Move_'+name,'commands':rows,'dependencies':{
   'commands':sorted({r['op'] for r in rows}),
   'spriteTemplates':sorted({r['args'][0] for r in rows if r['op']=='createsprite'}),
   'taskCallbacks':sorted({r['args'][0] for r in rows if r['op']=='createvisualtask'}),
   'graphicsTags':sorted({r['args'][0] for r in rows if r['op']=='loadspritegfx'}),
   'sounds':sorted({r['args'][0] for r in rows if r['op'] in ['playsewithpan','loopsewithpan']})}})
 constants=raw['include/constants/battle_anim.h'].decode()
 tag_base=int(re.search(r'^#define ANIM_SPRITES_START\s+(\d+)',constants,re.M)[1])
 tags={name:tag_base+int(value) for name,value in re.findall(r'^#define (ANIM_TAG_\w+)\s+\(ANIM_SPRITES_START\s*\+\s*(\d+)\)',constants,re.M)}
 for row in scripts:row['spriteTags']={tag:tags[tag] for tag in row['dependencies']['graphicsTags']}
 indexed,indexed_blobs=indexed_impact(raw)
 # Original graphics table shares Impact's palette for Scratch and Cut; SLASH_2
 # shares Slash's sheet but its own source JASC palette. Do not use PNG palette guesses.
 with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/impact.png'])) as image:impact_pal=image.getpalette()
 pal_text=raw['graphics/battle_anims/sprites/slash_2.pal'].decode().splitlines()
 if pal_text[:3]!=['JASC-PAL','0100','16']:raise ValueError('Slash2 source palette header differs')
 slash2_pal=[int(c) for line in pal_text[3:] for c in line.split()]
 if len(slash2_pal)!=48 or any(c<0 or c>255 for c in slash2_pal):raise ValueError('Slash2 palette shape differs')
 images={'hands-and-feet':rgb555(raw['graphics/battle_anims/sprites/hands_and_feet.png'],(32,128)),
  'impact':rgb555(raw['graphics/battle_anims/sprites/impact.png'],(32,32)),
  'scratch':rgb555(raw['graphics/battle_anims/sprites/scratch.png'],(32,160),impact_pal),
  'cut':rgb555(raw['graphics/battle_anims/sprites/cut.png'],(32,128),impact_pal),
  'slash':rgb555(raw['graphics/battle_anims/sprites/slash.png'],(32,128)),
  'slash-2':rgb555(raw['graphics/battle_anims/sprites/slash.png'],(32,128),slash2_pal)}
 recipes=[]
 for path,template,asset,table in [
  ('src/battle_anim_fight.c','gFistFootSpriteTemplate','hands-and-feet','sAnims_HandsAndFeet'),
  ('src/battle_anim_effects_3.c','gScratchSpriteTemplate','scratch','sScratchAnimTable'),
  ('src/battle_anim_effects_1.c','gCuttingSliceSpriteTemplate','cut','sCuttingSliceAnimTable'),
  ('src/battle_anim_effects_1.c','gSlashSliceSpriteTemplate','slash','sSlashSliceAnimTable'),
  ('src/battle_anim_effects_1.c','gFalseSwipeSliceSpriteTemplate','slash-2','sSlashSliceAnimTable'),
  ('src/battle_anim_effects_1.c','gFalseSwipePositionedSliceSpriteTemplate','slash-2','sSlashSliceAnimTable')]:
  text=raw[path].decode()
  match=re.search(r'const struct SpriteTemplate '+template+r' =\s*\{(.*?)\};',text,re.S)
  if match is None:raise ValueError('Missing source sprite template')
  body=match[1];callback=re.search(r'\.callback = (\w+)',body)[1];oam=re.search(r'\.oam = &(\w+)',body)[1]
  if oam not in ['gOamData_AffineOff_ObjBlend_32x32','gOamData_AffineOff_ObjNormal_32x32'] or '.anims = '+table not in body:raise ValueError('Original template recipe differs')
  table_body=re.search(r'static const union AnimCmd \*const '+table+r'\[\] =\s*\{(.*?)\};',text,re.S)[1]
  frames=[]
  for array in re.findall(r'\b(s\w+)\s*,',table_body):
   animation=re.search(r'static const union AnimCmd '+array+r'\[\] =\s*\{(.*?)\};',text,re.S)[1]
   values=[]
   for line in animation.splitlines():
    line=line.strip().rstrip(',')
    if not line:continue
    frame=re.fullmatch(r'ANIMCMD_FRAME\((\d+), (\d+)(, \.hFlip = TRUE)?\)',line)
    if frame:values.append(dict(tile=int(frame[1]),duration=int(frame[2]),hFlip=bool(frame[3]),vFlip=False))
    elif line!='ANIMCMD_END':raise ValueError('Unsupported source sprite frame command: '+line)
   if not values or not animation.strip().endswith('ANIMCMD_END,'):raise ValueError('Source frame table has no end')
   frames.append(values)
  recipes.append(dict(template=template,asset=asset,callback=callback,blend='ObjBlend' in oam,animations=frames,
   sourcePath=path,sourceLine=text.count('\n',0,match.start())+1))
 with Image.open(io.BytesIO(raw['graphics/battle_anims/sprites/substitute.png'])) as image:pal=image.getpalette()
 front=rgb555(raw['graphics/battle_anims/sprites/substitute.png'],(64,64),pal)
 back=rgb555(raw['graphics/battle_anims/sprites/substitute_back.png'],(64,64),pal)
 species={s:int(n) for s,n in re.findall(r'^#define\s+(SPECIES_\w+)\s+(\d+)\s*$',raw['include/constants/species.h'].decode(),re.M)}
 species.update({s:species['SPECIES_EGG']+int(n) for s,n in re.findall(r'^#define\s+(SPECIES_UNOWN_\w+)\s+\(NUM_SPECIES \+ (\d+)\)',raw['include/constants/species.h'].decode(),re.M)})
 elevation={s:int(n) for s,n in re.findall(r'\[(SPECIES_\w+)\]\s*=\s*(\d+)',raw['src/data/pokemon_graphics/enemy_mon_elevation.h'].decode())}
 geometry=[]
 for side in ['back','front']:
  text=raw['src/data/pokemon_graphics/'+side+'_pic_coordinates.h'].decode()
  rows=re.findall(r'\[(SPECIES_\w+)\]\s*=\s*\{\s*\.size\s*=\s*MON_COORDS_SIZE\((\d+),\s*(\d+)\),\s*\.y_offset\s*=\s*(\d+)',text)
  for name,w,h,y in rows:
   if name not in species:raise ValueError('Unknown coordinate species: '+name)
   geometry.append({'species':species[name],'side':side,'sourceWidth':int(w),'sourceHeight':int(h),'yOffset':int(y),'elevation':elevation.get(name,0) if side=='front' else 0})
 if len(geometry)!=880:raise ValueError('Expected exact pinned 880 geometry records')
 if len({(x['species'],x['side']) for x in geometry})!=880:raise ValueError('Duplicate geometry')
 def asset(id,data):return {'id':id,'path':id+'.rgb555'}
 sines=[int(float(x)*256) for x in re.findall(r'Q_8_8\((-?[\d.]+)\)',raw['src/trig.c'].decode().split('};',1)[0])]
 if len(sines)!=320:raise ValueError('Original sine table differs')
 anim={'sineTable':sines,'schemaVersion':1,'scripts':scripts,'assets':[asset(id,data) for id,data in images.items()],'spriteRecipes':recipes,'indexedAssets':[indexed]}
 forms,form_assets,form_blobs=castform_assets(raw,geometry,rgb555)
 additional_assets,additional_blobs=additional_mon_assets(raw)
 art={'schemaVersion':1,'formGeometry':forms,'geometry':geometry,'assets':[asset('substitute-front',front),asset('substitute-back',back),*form_assets,*additional_assets]}
 def encode(x):return (json.dumps(x,separators=(',',':'))+'\n').encode()
 return {'source-hit-animations':{'manifest.json':encode(anim),**{id+'.rgb555':data for id,data in images.items()},**indexed_blobs},'campaign-hit-art':{'manifest.json':encode(art),'substitute-front.rgb555':front,'substitute-back.rgb555':back,**form_blobs,**additional_blobs}}

def main():
 p=argparse.ArgumentParser();p.add_argument('legacy_reference',type=Path,nargs='?');p.add_argument('legacy_output',type=Path,nargs='?');p.add_argument('--reference',type=Path);p.add_argument('--output',type=Path);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference is not None or a.output is not None:
  if a.reference is None or a.output is None or a.legacy_reference is not None or a.legacy_output is not None:p.error('Use --reference and --output together without positional arguments')
  data=build(a.reference)
  folders={'source-hit-animations':'battle/animations/hit','campaign-hit-art':'battle/art/campaign-hit-art'}
  for folder,files in data.items():
   for name,blob in files.items():
    path=a.output/folders[folder]/name
    if a.check:
     if path.read_bytes()!=blob:raise SystemExit('Hit runtime output differs: '+str(path))
    else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(blob)
 else:
  if a.legacy_reference is None or a.legacy_output is None or a.check:p.error('Use --reference and --output; legacy positional generation also remains supported')
  out=a.legacy_output
  if any((out/name).exists() for name in ['source-hit-animations','campaign-hit-art']):raise SystemExit('Refuse existing generated output')
  data=build(a.legacy_reference)
  out.mkdir(parents=True,exist_ok=True)
  with tempfile.TemporaryDirectory(prefix='.hit-import-',dir=out) as temp:
   temp=Path(temp)
   for name,files in data.items():
    (temp/name).mkdir()
    for filename,blob in files.items():(temp/name/filename).write_bytes(blob)
   for name in data:(temp/name).rename(out/name)
 print(json.dumps({'passed':True,'resources':sum(len(files) for files in data.values())}))
if __name__=='__main__':main()
