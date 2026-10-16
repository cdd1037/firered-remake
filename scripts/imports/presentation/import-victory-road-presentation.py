#!/usr/bin/env python3
"""Rebuild bounded Victory Road presentation from locked pret bytes and predecessor resources."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT
from audio_manifest import runtime_manifest_bytes

from shared_trainers import resolve_trainers
from runtime_json import runtime_json_bytes
import argparse, importlib.util, io, json, pathlib, re, struct, subprocess
from PIL import Image
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-victory-road-presentation-v1'
BASE='content/generated/sprites/pokemon/victory-road/'
RAW='content/raw/pokefirered/037335f4/'
PORTRAITS = [('black-belt-front', 'black_belt', 'BlackBelt', 'BLACK_BELT', 105, 'content/generated/ui/viridian-gym-presentation/black-belt-front.png'), ('cooltrainer-m-front', 'cool_trainer_m', 'CooltrainerM', 'COOLTRAINER_M', 110, 'content/generated/ui/viridian-gym-presentation/cooltrainer-m-front.png'), ('cooltrainer-f-front', 'cool_trainer_f', 'CooltrainerF', 'COOLTRAINER_F', 111, 'content/generated/sprites/trainers/celadon-gym/cooltrainer-f-front.png'), ('cool-couple-front', 'cool_couple', 'CoolCouple', 'COOL_COUPLE', 128, None), ('juggler-front', 'juggler', 'Juggler', 'JUGGLER', 102, 'content/generated/ui/fuchsia-gym-presentation/juggler-front.png'), ('tamer-front', 'tamer', 'Tamer', 'TAMER', 103, 'content/generated/ui/fuchsia-gym-presentation/tamer-front.png'), ('pokemaniac-front', 'pokemaniac', 'PokeManiac', 'POKEMANIAC', 88, 'content/generated/sprites/trainers/rocktunnel/pokemaniac-front.png')]
ITEMS=[('TM02','TMHM','DragonTMHM','tm_hm','dragon_tm_hm'),('TM07','TMHM','IceTMHM','tm_hm','ice_tm_hm'),
 ('TM37','TMHM','RockTMHM','tm_hm','rock_tm_hm'),('TM50','TMHM','FireTMHM','tm_hm','fire_tm_hm'),
 ('GUARD_SPEC','BattleStatItem','GuardSpec','battle_stat_item','guard_spec'),('SITRUS_BERRY','SitrusBerry','SitrusBerry','sitrus_berry','sitrus_berry')]
MAPS=['Route23','VictoryRoad_1F','VictoryRoad_2F','VictoryRoad_3F','IndigoPlateau_Exterior']
CUES=['MUS_VICTORY_ROAD','MUS_MT_MOON','MUS_ENCOUNTER_BOY','MUS_ENCOUNTER_GIRL','MUS_VS_TRAINER','MUS_VICTORY_TRAINER','MUS_VS_WILD','MUS_VICTORY_WILD','MUS_SURF','SE_M_STRENGTH','SE_FALL','SE_ICE_BREAK','MUS_OBTAIN_ITEM','MUS_OBTAIN_TMHM']
from image_io import png as encode_rgba_png

def need(v,m):
 if not v:raise ValueError(m)
def encode(v):return (json.dumps(v,indent=2,ensure_ascii=False)+'\n').encode()
def safe(root,path):
 need(path and not pathlib.PurePosixPath(path).is_absolute() and '\\' not in path and ':' not in path and all(p not in ('','.','..') for p in path.split('/')),'Unsafe resource path')
 p=pathlib.Path(root).absolute()/path;need(not any(x.is_symlink() for x in (p,*p.parents)),'Resource symlink rejected');return p
png = encode_rgba_png
def palette(b):
 lines=b.decode().splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Source palette shape');return [tuple(((int(v)>>3)<<3)|((int(v)>>3)>>2) for v in row.split()) for row in lines[3:]]
def pixels(image,pal):
 im=Image.open(io.BytesIO(image));need(im.mode=='P' and max(im.tobytes())<16,'Source indexed pixels');colors=palette(pal);return im.size,bytes(v for i in im.tobytes() for v in (*colors[i],255 if i else 0))
def opponent_art(raw,out):
 # These two species occur only in trainer parties. Preserve the predecessor battle-art
 # RGB5 expansion and source coordinates; this package grants no ownership admission.
 sprites=[];icons=[];text=lambda p:raw[p].decode();graphics=text('src/data/graphics/pokemon.h')
 def decode(imagepath,palpath,shape,first_frame=False):
  im=Image.open(io.BytesIO(raw[imagepath]));ix=im.tobytes();need(im.mode=='P' and im.size==shape and max(ix)<16,'Pokemon indexed pixels: '+imagepath)
  if first_frame:ix=ix[:1024]
  lines=text(palpath).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Pokemon palette framing')
  colors=[tuple((int(v)>>3)*255//31 for v in row.split()) for row in lines[3:]]
  return ix,bytes(v for i in ix for v in (*colors[i],255 if i else 0))
 def emit(stem,w,h,rgba):
  out[stem+'.png']=png(w,h,rgba)
  return dict(texturePath=BASE + stem + '.png', width=w, height=h)
 for name,stem,sid in [('Dewgong','dewgong',87),('Exeggutor','exeggutor',103)]:
  symbol='SPECIES_'+stem.upper();need(re.search(r'^#define '+symbol+r'\s+'+str(sid)+r'\s*$',text('include/constants/species.h'),re.M),'Pokemon source identity')
  em=re.search(r'\['+symbol+r'\]\s*=\s*(\d+)',text('src/data/pokemon_graphics/enemy_mon_elevation.h'));elevation=int(em[1]) if em else 0
  for side in ('front','back'):
   cp=f'src/data/pokemon_graphics/{side}_pic_coordinates.h';cm=re.search(r'\['+symbol+r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',text(cp));need(cm,'Pokemon coordinates missing');width,height,yoff=map(int,cm.groups())
   image=f'graphics/pokemon/{stem}/{side}.png';need(f'gMon{side.title()}Pic_{name}[] = INCBIN_U32("{image[:-4]}.4bpp.lz")' in graphics,'Pokemon sprite binding')
   for variant in ('normal','shiny'):
    pal=f'graphics/pokemon/{stem}/{variant}.pal';ps='gMonPalette_' if variant=='normal' else 'gMonShinyPalette_';need(f'{ps}{name}[] = INCBIN_U32("{pal[:-4]}.gbapal.lz")' in graphics,'Pokemon palette binding')
    ix,rgba=decode(image,pal,(64,64));x,y=(144,8+yoff-elevation) if side=='front' else (40,48+yoff)
    sprites.append(dict(speciesId='pret:'+symbol,sourceSpeciesId=sid,side=side,paletteVariant=variant,sourceImagePath=image,sourcePalettePath=pal,sourceCoordinatePath=cp,sourceWidth=width,sourceHeight=height,sourceYOffset=yoff,sourceEnemyElevation=elevation,singleBattleTopLeft=dict(x=x,y=y),paletteExpansion='floor(rgb5 * 255 / 31)',transparentIndex=0,**emit(f'{stem}-{side}-{variant}',64,64,rgba)))
  table=text('src/pokemon_icon.c');pm=re.search(r'\['+symbol+r'\]\s*=\s*(\d+),',table);need(pm and re.search(r'\['+symbol+r'\]\s*=\s*gMonIcon_'+name+r',',table),'Pokemon icon table binding');pi=int(pm[1]);image=f'graphics/pokemon/{stem}/icon.png';pal=f'graphics/pokemon/icon_palettes/icon_palette_{pi}.pal'
  need(f'gMonIcon_{name}[] = INCBIN_U8("{image[:-4]}.4bpp")' in graphics,'Pokemon icon graphics binding');ix,rgba=decode(image,pal,(32,64),True)
  icons.append(dict(speciesId='pret:'+symbol,sourceSpeciesId=sid,sourceImagePath=image,sourcePalettePath=pal,paletteIndex=pi,sourceFrameIndex=0,sourceFrameCount=2,paletteExpansion='floor(rgb5 * 255 / 31)',**emit(f'icons/{stem}',32,32,rgba)))
 return sprites,icons
def authenticate(root,inherited,rules,reference):
 rawlock=safe(root,'scripts/recipes/presentation/victory-road-presentation.lock.json').read_bytes();lock=json.loads(rawlock)
 need((lock['schemaVersion'], lock['profile'], lock['commit']) == (1, PROFILE, COMMIT),'Source identity differs');raw={};reused={}
 for row in lock['files']:
  p=row['path'];b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+p]) if reference else safe(root,RAW+p).read_bytes()
  raw[p]=b
 for row in lock['inheritedInputs']:
  p=row['path'];b=safe(rules if p.startswith('content/generated/data/regions/victory-road/') else inherited,p).read_bytes();reused[p]=b
 return lock,raw,reused

def build(lock,raw,reused,generated_directory=ROOT/'content/generated'):
 text=lambda p:raw[p].decode();out={};portraits=[];icons=[];layers=[];tiles=[]
 sprites,pokemon_icons=opponent_art(raw,out)
 table=text('src/data/trainer_graphics/front_pic_tables.h');graphics=text('src/data/graphics/trainers.h')
 coords=re.findall(r'\{\.size = (\d+), \.y_offset = (\d+)\}',table.split('const struct CompressedSpriteSheet')[0])
 for identity,stem,symbol,picture,number,inherited in PORTRAITS:
  image=f'graphics/trainers/front_pics/{stem}_front_pic.png';pal=f'graphics/trainers/palettes/{stem}.pal'
  need(f'gTrainerFrontPic_{symbol}[] = INCBIN_U32("graphics/trainers/front_pics/{stem}_front_pic.4bpp.lz")' in graphics and f'gTrainerPalette_{symbol}[] = INCBIN_U32("graphics/trainers/palettes/{stem}.gbapal.lz")' in graphics and f'TRAINER_SPRITE({picture}, gTrainerFrontPic_{symbol}, 0x800)' in table and f'TRAINER_PAL({picture}, gTrainerPalette_{symbol})' in table,'Portrait source binding')
  need(re.search(r'^#define TRAINER_PIC_'+picture+r'\s+'+str(number)+r'\s*$',text('include/constants/trainers.h'),re.M) and coords[number][0]=='8','Portrait identity')
  size,rgba=pixels(raw[image],raw[pal]);need(size==(64,64),'Portrait dimensions')
  if inherited:need(Image.open(io.BytesIO(reused[inherited])).convert('RGBA').tobytes()==rgba,'Inherited portrait pixels: '+identity);path=inherited
  else:pass;out[identity+'.png']=png(*size,rgba);path = BASE + identity + '.png'
  portraits.append(dict(id=identity, trainerPic='TRAINER_PIC_' + picture, sourceTrainerPicId=number, texturePath=path, inherited=bool(inherited), width=64, height=64, sourceYOffset=int(coords[number][1])))
 trainers=[];catalog=json.loads(reused['content/generated/data/regions/victory-road/trainers.json']);need(len(catalog['trainers'])==12,'Trainer catalog scope')
 for t in resolve_trainers(generated_directory, catalog):
  cls=t['trainerClass'];picture=t['trainerPic'];name=t['sourceTrainerName'];symbol=t['id']['value'][5:];block=re.search(r'\['+symbol+r'\] = \{(.*?)\n    \}',text('src/data/trainers.h'),re.S)
  need(block and '.trainerName = _("'+name+'")' in block[1] and re.search(r'\.trainerClass\s*=\s*'+cls+r'\s*,',block[1]) and re.search(r'\.trainerPic\s*=\s*'+picture+r'\s*,',block[1]),'Trainer metadata binding')
  need(re.search(r'^#define '+symbol+r'\s+'+str(t['sourceTrainerId'])+r'\s*$',text('include/constants/opponents.h'),re.M) and re.search(r'\.doubleBattle\s*=\s*'+('TRUE' if t['doubleBattle'] else 'FALSE')+r'\s*,',block[1]),'Trainer source ID/mode')
  clsname=re.search(r'\['+cls+r'\]\s*= _\("([^\"]+)"\)',text('src/data/text/trainer_class_names.h'));need(clsname,'Trainer class name missing')
  p=next(p for p in portraits if p['trainerPic']==picture);need(p['sourceTrainerPicId']==t['sourceTrainerPic'],'Trainer portrait ID')
  trainers.append(dict(trainer=t['id']['value'],sourceTrainerId=t['sourceTrainerId'],name=name,trainerClass=cls,className=clsname[1],trainerPic=picture,portrait=p['id'],encounterMusicGender=t['encounterMusicGender'],doubleBattle=t['doubleBattle']))
 for item,image_symbol,pal_symbol,stem,palstem in ITEMS:
  need(re.search(r'\[ITEM_'+item+r'\]\s*= \{gItemIcon_'+image_symbol+r', gItemIconPalette_'+pal_symbol+r'\}',text('src/data/item_icon_table.h')),'Item source binding')
  image='graphics/items/icons/'+stem+'.png';pal='graphics/items/icon_palettes/'+palstem+'.pal'
  graphics=text('src/data/graphics/items.h')
  need(re.search(r'gItemIcon_'+image_symbol+r'\[\]\s*=\s*INCBIN_U32\("'+re.escape(image[:-4]+'.4bpp.lz')+r'"\)',graphics) and re.search(r'gItemIconPalette_'+pal_symbol+r'\[\]\s*=\s*INCBIN_U32\("'+re.escape(pal[:-4]+'.gbapal.lz')+r'"\)',graphics),'Item graphics declarations')
  size,rgba=pixels(raw[image],raw[pal]);need(size==(24,24),'Item icon size');name='item_'+item.lower();out[name+'.png']=png(*size,rgba)
  icons.append(dict(item='pret:ITEM_' + item, texturePath=BASE + name + '.png', width=24, height=24))
 # Source metatile IDs are shared by all four barrier scripts. Rebuild all three layers, including transparent replacement pixels.
 bases=['data/tilesets/primary/general','data/tilesets/secondary/cave'];images=[Image.open(io.BytesIO(raw[b+'/tiles.png'])) for b in bases];palettes=[palette(raw[bases[0 if n<7 else 1]+f'/palettes/{n:02d}.pal']) for n in range(13)];palettes[0][0]=(0,0,0)
 attrs=[struct.unpack('<'+'I'*(len(raw[b+'/metatile_attributes.bin'])//4),raw[b+'/metatile_attributes.bin']) for b in bases];metatiles=[struct.unpack('<'+'H'*(len(raw[b+'/metatiles.bin'])//2),raw[b+'/metatiles.bin']) for b in bases];emitted=[]
 for base,img in zip(bases,images):
  need(img.mode=='P' and img.width%8==0 and img.height%8==0 and max(img.tobytes())<16,'Field tile format');rule=re.search(re.escape('$(TILESETGFXDIR)/'+base.removeprefix('data/tilesets/')+'/tiles.4bpp:')+r'.*?\n\t[^\n]*?-num_tiles (\d+)',text('tileset_rules.mk'));emitted.append(int(rule[1]) if rule else img.width*img.height//64)
 labels={k:int(v,16) for k,v in re.findall(r'^#define\s+(METATILE_\w+)\s+(0x[\da-fA-F]+)',text('include/constants/metatile_labels.h'),re.M)};scripts='\n'.join(text('data/maps/'+m+'/scripts.inc') for m in MAPS[1:4]);symbols=sorted(set(re.findall(r'^\s*setmetatile [^,\n]+, [^,\n]+, (METATILE_\w+), [01]',scripts,re.M)))
 need(symbols==['METATILE_Cave_Floor_Ledge_Bottom','METATILE_Cave_Floor_Ledge_Top','METATILE_Cave_RockBarrier_Bottom','METATILE_Cave_RockBarrier_Top'],'Barrier metatile scope');atlases=[Image.new('RGBA',(64,16)) for _ in range(3)]
 def tile(value):
  tid,pal=value&1023,value>>12;bi,local=(0,tid) if tid<640 else (1,tid-640);img=images[bi];need(pal<13 and local<emitted[bi],'Uninitialized tile/palette');pixels=[]
  for y in range(8):
   for x in range(8):
    ix,iy=(7-x if value&0x400 else x),(7-y if value&0x800 else y);c=img.getpixel((local%(img.width//8)*8+ix,local//(img.width//8)*8+iy));pixels.append((*palettes[pal][c],0 if c==0 else 255))
  im=Image.new('RGBA',(8,8));im.putdata(pixels);return im
 for col,symbol in enumerate(symbols):
  mid=labels[symbol];bi,local=(0,mid) if mid<640 else (1,mid-640);attribute=attrs[bi][local];v=metatiles[bi][local*8:local*8+8];groups={0:[[0x3014]*4,v[:4],v[4:]],1:[v[:4],v[4:],[0]*4],2:[v[:4],[0]*4,v[4:]]}.get((attribute>>29)&3);need(groups and len(v)==8,'Metatile layers')
  for atlas,group in zip(atlases,groups):
   for n,value in enumerate(group):atlas.paste(tile(value),(col*16+n%2*8,n//2*8))
  tiles.append(dict(symbol=symbol,metatileId=mid,rawAttributes=attribute,x=col*16,y=0))
 for n,atlas in enumerate(atlases):
  name='barrier-'+['bottom','middle','top'][n];rgba=atlas.tobytes();out[name+'.png']=png(64,16,rgba);layers.append(dict(layer=n, texturePath=BASE + name + '.png', width=64, height=16))
 # Register the original SE_ICE_BREAK song already inside the authenticated source resource pack.
 pack=reused['content/generated/audio/synth/firered.pack']
 songs={k:int(v) for k,v in re.findall(r'^#define ((?:MUS|SE)_\w+)\s+(\d+)',text('include/constants/songs.h'),re.M)};table=re.findall(r'^\s*song (\w+),\s*(\d+),\s*(\d+)',text('sound/song_table.inc'),re.M);sid=songs['SE_ICE_BREAK'];address,player,second=struct.unpack_from('<IHH',pack,0x084A32CC+sid*8-0x08489968);need(table[sid]==('se_ice_break',str(player),str(second)) and player in (1,2,3),'Source cue table')
 extra=dict(id='SE_ICE_BREAK', sourceSymbol='SE_ICE_BREAK', sourcePlayer=player, priority=pack[address - 138975592 + 2], sourceCounter=None, songAddress=address, species=0, looping=False, cryMode=0)
 # The reviewed MIDI reader checks the original finite sequence, never executes source repository code.
 helper=reused['scripts/lib/audit-early-audio.py'];scope={'__name__':'victory_road_midi_reader','__file__':str(ROOT/'scripts/lib/audit-early-audio.py')};exec(compile(helper,'audit-early-audio.py','exec'),scope);midi=scope['midi'](raw[extra['sourcePath']]);need(not any(m['text'] in ('[',']') for m in midi['markers']),'Barrier cue unexpectedly loops')
 cues=[c for p,b in reused.items() if p.endswith('audio/synth/manifest.json') for c in json.loads(b)['cues']];need(not any(c['id']=='SE_ICE_BREAK' for c in cues),'Barrier cue already registered');cues.append(extra);need(all(sum(c['id']==name for c in cues)==1 for name in CUES),'Inherited cue closure')
 out['synth-manifest.json']=runtime_manifest_bytes(dict(schemaVersion=1, cues=[extra]))
 maps=[]
 for name in MAPS:
  m=json.loads(raw['data/maps/'+name+'/map.json']);need(m['music']==('MUS_MT_MOON' if name.startswith('VictoryRoad') else 'MUS_VICTORY_ROAD') and m['battle_scene']=='MAP_BATTLE_SCENE_NORMAL','Map presentation source');maps.append(dict(sourceMap=name,music=m['music'],battleScene=m['battle_scene']))
 manifest=dict(schemaVersion=1,trainers=trainers,portraits=portraits,items=icons,sprites=sprites,pokemonIcons=pokemon_icons,trainerOnlySpecies=[87,103],tiles=tiles,layers=layers,maps=maps,audio=dict(requiredCues=CUES,additionalCueIds=['SE_ICE_BREAK'],),outputs=[dict(path=p) for p,b in sorted(out.items())]);out['manifest.json']=runtime_json_bytes(manifest);return out

def run(root=ROOT,inherited=None,rules=None,reference=None,check=False):
 lock,raw,reused=authenticate(root,inherited or root,rules or root,reference);out=build(lock,raw,reused,(rules or root)/'content/generated')
 if check:
  for p,b in out.items():need(safe(root,BASE+p).read_bytes()==b,'Generated bytes differ: '+p)
 else:
  for p,b in [(RAW+p,b) for p,b in raw.items()]+[(BASE+p,b) for p,b in out.items()]:
   dest=safe(root,p);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 return dict(passed=True,sourceFiles=len(raw),trainers=12,portraits=7,newPortraits=1,inheritedPortraits=6,items=6,barrierMetatiles=4,barrierLayers=3,newSpecies=2,trainerOnlySpecies=[87,103],newBattleSprites=8,newPokemonIcons=2,newOwnedSpecies=0,additionalCueIds=['SE_ICE_BREAK'],manifestBytes=len(out['manifest.json']),sourceAndStaticChecksOnly=True)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--inherited-root',type=pathlib.Path);p.add_argument('--rules-root',type=pathlib.Path);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.inherited_root,a.rules_root,a.reference_root,a.check)))
