#!/usr/bin/env python3
"""Import bounded first-clear League pixels and credits from pinned local git objects."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
import argparse,  io, json, pathlib, re, struct, subprocess
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
BASE='content/generated/sprites/pokemon/pokemon-league/'
RAW='content/raw/pokefirered/037335f4/'
ROOT=_PROJECT_ROOT

def enc(o):return (json.dumps(o,ensure_ascii=False,indent=2)+'\n').encode()
def need(ok,msg):
 if not ok:raise ValueError(msg)
def run(root,reference,inherited,check=False):
 raw={};out={};assets=[]
 def get(p):
  if p not in raw:raw[p]=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+p]) if reference else (root/RAW/p).read_bytes()
  return raw[p]
 def text(p):return get(p).decode()
 def emit(name,im):
  rgba=im.convert('RGBA').tobytes();b=io.BytesIO();im.convert('RGBA').save(b,format='PNG');out[name+'.png']=b.getvalue()
  row = dict(id=name, width=im.width, height=im.height, texturePath=name + '.png');assets.append(row);return row
 def indexed(p,pal=None,transparent=True):
  im=Image.open(io.BytesIO(get(p)));need(im.mode=='P','Indexed source expected: '+p);ix=list(im.getdata())
  if pal:
   rows=text(pal).splitlines();need(rows[:3]==['JASC-PAL','0100','16'],'Palette framing');colors=[tuple(map(int,x.split())) for x in rows[3:]]
  else:
   pl=im.getpalette();colors=[tuple(pl[i:i+3]) for i in range(0,len(pl),3)]
  colors=[tuple(((v>>3)<<3)|((v>>3)>>2) for v in c) for c in colors]
  dst=Image.new('RGBA',im.size);dst.putdata([(*colors[i],0 if transparent and i==0 else 255) for i in ix]);return dst
 # Explicit source provenance for FSM, source text, trainer selection.
 for p in ['src/palette.c','src/field_weather.c','src/field_effect.c','src/hall_of_fame.c','src/credits.c','src/strings.c','src/pokemon.c','src/battle_main.c','src/data/trainers.h','src/data/graphics/trainers.h','src/data/trainer_graphics/front_pic_tables.h','src/data/text/trainer_class_names.h','include/constants/trainers.h','include/constants/opponents.h','include/constants/metatile_labels.h','tileset_rules.mk']:get(p)
 table=text('src/data/trainers.h');constants=text('include/constants/trainers.h');opponents=text('include/constants/opponents.h');classes=text('src/data/text/trainer_class_names.h');portraits=[];trainers=[]
 for stem in ['elite_four_lorelei','elite_four_bruno','elite_four_agatha','elite_four_lance','champion_rival','red','leaf']:
  row=emit(stem+'-front',indexed('graphics/trainers/front_pics/'+stem+'_front_pic.png','graphics/trainers/palettes/'+stem+'.pal'))
  row['trainerPic']='TRAINER_PIC_'+stem.upper();row['sourceTrainerPicId']=int(re.search(r'^#define '+row['trainerPic']+r'\s+(\d+)',constants,re.M)[1]);portraits.append(row)
 for symbol in ['TRAINER_ELITE_FOUR_'+x for x in ['LORELEI','BRUNO','AGATHA','LANCE']]+['TRAINER_CHAMPION_FIRST_'+x for x in ['SQUIRTLE','BULBASAUR','CHARMANDER']]:
  b=re.search(r'\['+symbol+r'\] = \{(.*?)\n    \}',table,re.S)[1];cls=re.search(r'\.trainerClass = (\w+)',b)[1];pic=re.search(r'\.trainerPic = (\w+)',b)[1]
  trainers.append(dict(trainer='pret:'+symbol,sourceTrainerId=int(re.search(r'^#define '+symbol+r'\s+(\d+)',opponents,re.M)[1]),name=re.search(r'\.trainerName = _\("([^"]+)"\)',b)[1],trainerClass=cls,className=re.search(r'\['+cls+r'\]\s*= _\("([^"]+)"\)',classes)[1],trainerPic=pic,portrait=next(r['id'] for r in portraits if r['trainerPic']==pic)))
 emit('hof-monitor',indexed('graphics/field_effects/pics/hof_monitor.png'))
 emit('hof-tiles',indexed('graphics/hall_of_fame/hall_of_fame.png',transparent=False))
 emit('confetti',indexed('graphics/misc/confetti.png'))
 for stem in ['player_male','player_female','ground_grass','ground_dirt','ground_city']:
  emit('credits-'+stem,indexed('graphics/credits/'+stem+'.png'))
 for stem in ['copyright','the_end']:
  img=indexed('graphics/credits/'+stem+'.png',transparent=False);tm=struct.unpack('<'+'H'*(len(get('graphics/credits/'+stem+'.bin'))//2),get('graphics/credits/'+stem+'.bin'));canvas=Image.new('RGBA',(256,256))
  for n,v in enumerate(tm):
   tid=v&1023;x=(tid%(img.width//8))*8;y=(tid//(img.width//8))*8;t=img.crop((x,y,x+8,y+8))
   if v&0x400:t=t.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
   if v&0x800:t=t.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
   canvas.paste(t,(n%32*8,n//32*8))
  emit('credits-'+stem,canvas.crop((0,0,240,160)))
 # Source tile replacement pixels, including alpha clearing on upper layers.
 bases=['data/tilesets/primary/building','data/tilesets/secondary/pokemon_league'];images=[Image.open(io.BytesIO(get(b+'/tiles.png'))) for b in bases]
 pals=[]
 for n in range(13):
  rows=text(bases[0 if n<7 else 1]+f'/palettes/{n:02d}.pal').splitlines()[3:];pals.append([tuple(((int(v)>>3)<<3)|((int(v)>>3)>>2) for v in r.split()) for r in rows])
 pals[0][0]=(0,0,0)
 attrs=[struct.unpack('<'+'I'*(len(get(b+'/metatile_attributes.bin'))//4),get(b+'/metatile_attributes.bin')) for b in bases];mets=[struct.unpack('<'+'H'*(len(get(b+'/metatiles.bin'))//2),get(b+'/metatiles.bin')) for b in bases]
 symbols=['Door_Top_Open','Door_Mid_Open','Entry_TopLeft_Closed','Entry_TopMid_Closed','Entry_TopRight_Closed','Entry_BottomLeft_Closed','Entry_BottomMid_Closed','Entry_BottomRight_Closed','Floor_ShadeFull_Lance'];tiles=[];atlases=[Image.new('RGBA',(len(symbols)*16,16)) for _ in range(3)]
 def tile(v):
  tid,pal=v&1023,v>>12;bi,local=(0,tid) if tid<640 else (1,tid-640);im=images[bi];pix=[]
  for y in range(8):
   for x in range(8):
    ix=7-x if v&0x400 else x;iy=7-y if v&0x800 else y;i=im.getpixel((local%(im.width//8)*8+ix,local//(im.width//8)*8+iy));pix.append((*pals[pal][i],0 if i==0 else 255))
  dst=Image.new('RGBA',(8,8));dst.putdata(pix);return dst
 for col,suffix in enumerate(symbols):
  symbol='METATILE_PokemonLeague_'+suffix;mid=int(re.search(r'^#define '+symbol+r'\s+(0x\w+)',text('include/constants/metatile_labels.h'),re.M)[1],16);bi,local=(0,mid) if mid<640 else (1,mid-640);v=mets[bi][local*8:local*8+8];a=attrs[bi][local];groups={0:[[0x3014]*4,v[:4],v[4:]],1:[v[:4],v[4:],[0]*4],2:[v[:4],[0]*4,v[4:]]}[(a>>29)&3]
  for atlas,group in zip(atlases,groups):
   for n,value in enumerate(group):atlas.paste(tile(value),(col*16+n%2*8,n//2*8))
  tiles.append(dict(metatileId=mid,x=col*16,y=0))
 for i,atlas in enumerate(atlases):emit('doors-'+str(i),atlas)
 strings={n:s.replace('\\n','\n').replace('\\\"','\"') for n,s in re.findall(r'const u8 (\w+)\[\] = _\("((?:[^"\\]|\\.)*)"\);',text('src/strings.c'))}
 credits=text('src/credits.c');enums=re.search(r'enum CreditsString\s*\{(.*?)\};',credits,re.S)[1];names=re.findall(r'CREDITS_STRING_(\w+)',enums);pairs=re.findall(r'\{ (\w+), (\w+), (?:TRUE|FALSE)\s*\}',re.search(r'sCreditsTexts\[\] = \{(.*?)\n\};',credits,re.S)[1]);need(len(names)==len(pairs),'Credits row count');texts={n:dict(title=strings.get(a,''),names=strings.get(b,'')) for n,(a,b) in zip(names,pairs)}
 maps=[]
 for symbol,body in re.findall(r'sOverworldCmd_(\w+)\[\] = \{(.*?)\n\};',credits,re.S):
  m=re.search(r'CREDITSOVWLDLOADMAP\((\w+), (\d+), (\d+), (\d+)\)',body);v=re.search(r'CREDITSOVWLDSCROLL\((-?\d+), (-?\d+), (0x\w+)\)',body)
  maps.append(dict(symbol=symbol,map=m[1],x=int(m[2]),y=int(m[3]),dx=int(v[1]),dy=int(v[2]),duration=int(v[3],16)))
 commands=[]
 for op,param,duration in re.findall(r'CREDITS_(MAPNEXT|PRINT|MAP|MON|THEENDGFX|WAITBUTTON)\((\w+)(?:,\s*(\d+))?\)',re.search(r'sCreditsScript\[\] = \{(.*?)\n\};',credits,re.S)[1]):
  commands.append(dict(op=op,param=param,ticks=int(duration or (param if op=='WAITBUTTON' else 0))))
 need(len(commands)==66,'Credits command count')
 # New trainer-only species; this does not extend player ownership admission.
 sprites=[];icons=[]
 species=[('machamp',68),('gengar',94),('hitmonlee',106),('hitmonchan',107),('rhydon',112),('jynx',124),('aerodactyl',142),('dragonair',148),('dragonite',149)]
 for stem,sid in species:
  sym='SPECIES_'+stem.upper();elevation=re.search(r'\['+sym+r'\]\s*=\s*(\d+)',text('src/data/pokemon_graphics/enemy_mon_elevation.h'));elevation=int(elevation[1]) if elevation else 0
  for side in ['front','back']:
   cp='src/data/pokemon_graphics/'+side+'_pic_coordinates.h';coord=re.search(r'\['+sym+r'\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',text(cp));need(coord,'Pokemon coordinates');yoff=int(coord[3])
   for variant in ['normal','shiny']:
    im=indexed('graphics/pokemon/'+stem+'/'+side+'.png','graphics/pokemon/'+stem+'/'+variant+'.pal')
    # Battle rendering already uses floor(rgb5 * 255 / 31) for all predecessor sprites.
    pix=[]
    for r,g,b,a in im.getdata():pix.append(((r>>3)*255//31,(g>>3)*255//31,(b>>3)*255//31,a))
    im.putdata(pix);row=emit(stem+'-'+side+'-'+variant,im);row.update(speciesId='pret:'+sym,sourceSpeciesId=sid,side=side,paletteVariant=variant,x=144 if side=='front' else 40,y=8+yoff-elevation if side=='front' else 48+yoff);sprites.append(row)
  pi=int(re.search(r'\['+sym+r'\]\s*=\s*(\d+),',text('src/pokemon_icon.c'))[1]);im=indexed('graphics/pokemon/'+stem+'/icon.png',f'graphics/pokemon/icon_palettes/icon_palette_{pi}.pal').crop((0,0,32,32));row=emit(stem+'-icon',im);row.update(speciesId='pret:'+sym,sourceSpeciesId=sid);icons.append(row)
 inputs=[dict(path=p) for p,b in sorted(raw.items())]
 lock=enc(dict(schemaVersion=1,sourceCommit=COMMIT,files=inputs));lockpath=root/'scripts/recipes/presentation/pokemon-league-presentation.lock.json'
 manifest=runtime_json_bytes(dict(schemaVersion=1,assets=assets,trainers=trainers,portraits=portraits,sprites=sprites,pokemonIcons=icons,tiles=tiles,credits=dict(title=strings['gString_PokemonFireRed_Staff'],texts=texts,commands=commands,maps=maps),texts={k:strings[k] for k in ['gText_WelcomeToHOF','gText_LeagueChamp','gText_SavingDontTurnOffThePower2','gText_Level','gText_IDNumber']},outputs=[dict(path=p) for p,b in sorted(out.items())]));out['manifest.json']=manifest
 if check:
  need(lockpath.read_bytes()==lock,'Lock differs')
  for p,b in out.items():need((root/BASE/p).read_bytes()==b,'Generated resource differs: '+p)
 else:
  lockpath.write_bytes(lock)
  for p,b in [(RAW+p,b) for p,b in raw.items()]+[(BASE+p,b) for p,b in out.items()]:
   dest=root/p;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(b)
 return dict(passed=True,rawFiles=len(raw),outputs=len(out),manifestBytes=len(manifest),lockBytes=len(lock),creditsCommands=len(commands),originalVisualFidelityVerified=False)
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--inherited-root',type=pathlib.Path,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.reference_root,a.inherited_root,a.check),indent=2))
