#!/usr/bin/env python3
"""Rebuild authenticated Eevee normal/shiny battle art, party icon and footprint."""
import argparse,binascii,importlib.util,io,json,pathlib,re,struct,sys,zlib
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('eevee_metadata',ROOT/'scripts/import-eevee-pokemon.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
COMMIT=meta.COMMIT
def png(w,h,pixels):
 def chunk(tag,b):return struct.pack('>I',len(b))+tag+b+struct.pack('>I',binascii.crc32(tag+b)&0xffffffff)
 assert len(pixels)==w*h*4
 scan=b''.join(b'\0'+pixels[y*w*4:(y+1)*w*4] for y in range(h));blocks=[]
 for start in range(0,len(scan),65535):
  block=scan[start:start+65535];n=len(block);blocks.append(bytes([int(start+n==len(scan))])+struct.pack('<HH',n,n^65535)+block)
 stream=b'\x78\x01'+b''.join(blocks)+struct.pack('>I',zlib.adler32(scan)&0xffffffff)
 return b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',stream)+chunk(b'IEND',b'')
def build(root):
 lock,s=meta.authenticated(root);inventory={r['path']:r for r in lock['files']};out={};sprites=[];icons=[];footprints=[]
 def need(v,m):
  if not v:raise ValueError(m)
 def palette(path):
  lines=s[path].decode().splitlines();need(lines[:2]==['JASC-PAL','0100'] and len(lines)==int(lines[2])+3,'Palette framing differs');rgb=[tuple(map(int,x.split())) for x in lines[3:]];need(all(len(x)==3 and all(0<=c<=255 for c in x) for x in rgb),'Palette values differ');return [tuple((c>>3)*255//31 for c in x) for x in rgb],[(r>>3)|((g>>3)<<5)|((b>>3)<<10) for r,g,b in rgb]
 def image(path,shape):
  im=Image.open(io.BytesIO(s[path]));need(im.mode=='P' and im.size==shape,'Indexed art shape differs: '+path);indices=bytes(im.get_flattened_data());need(all(x<16 for x in indices),'Source art exceeds4bpp palette: '+path);return indices
 def rgba(indices,pal):return bytes(c for i in indices for c in (*pal[i],0 if i==0 else 255))
 def source(path,symbol,match=None,kind='pret'):
  r=dict(kind=kind,path=path,symbol=symbol,repository='pret/pokefirered',commit=COMMIT,url=f'https://github.com/pret/pokefirered/blob/{COMMIT}/{path}')
  if match:
   text=s[path].decode();r.update(startLine=text.count('\n',0,match.start())+1,endLine=text.count('\n',0,match.end())+1);r['url']+=f"#L{r['startLine']}"
  return r
 def small(path,symbol,match=None):return {k:v for k,v in source(path,symbol,match).items() if k in ('path','symbol','startLine','endLine')}
 def add(stem,w,h,pixels):
  out[stem+'.png']=png(w,h,pixels);return dict(texturePath=stem + '.png', width=w, height=h)
 elevations=s['src/data/pokemon_graphics/enemy_mon_elevation.h'].decode();em=re.search(r'\[SPECIES_EEVEE\]\s*=\s*(\d+)',elevations);elevation=int(em[1]) if em else 0;need(elevation==0,'Eevee ground elevation differs')
 graphics=s['src/data/graphics/pokemon.h'].decode()
 for side in ('front','back'):
  path=f'src/data/pokemon_graphics/{side}_pic_coordinates.h';cm=re.search(r'\[SPECIES_EEVEE\]\s*=\s*\{\s*\.size = MON_COORDS_SIZE\((\d+), (\d+)\),\s*\.y_offset = (\d+)',s[path].decode());need(cm is not None,'Sprite coordinates missing');w,h,yoffset=map(int,cm.groups());need((w,h,yoffset)==((40,48,13) if side=='front' else (48,48,10)),'Sprite coordinates differ')
  image_path=f'graphics/pokemon/eevee/{side}.png';indices=image(image_path,(64,64));need(f'graphics/pokemon/eevee/{side}.4bpp.lz' in graphics,'Sprite binding differs')
  alpha=[(i%64,i//64) for i,v in enumerate(indices) if v];xs,ys=zip(*alpha);bounds=dict(x=min(xs),y=min(ys),width=max(xs)-min(xs)+1,height=max(ys)-min(ys)+1)
  x,y=(144,8+yoffset-elevation) if side=='front' else (40,48+yoffset)
  for variant in ('normal','shiny'):
   palpath=f'graphics/pokemon/eevee/{variant}.pal';pal,pal555=palette(palpath);asset=add(f'eevee-{side}-{variant}',64,64,rgba(indices,pal))
   sprites.append(dict(speciesId='pret:SPECIES_EEVEE',sourceSpeciesId=133,side=side,paletteVariant=variant,sourceImagePath=image_path,sourcePalettePath=palpath,frameCount=1,sourceFrameIndex=0,frameWidth=64,frameHeight=64,sourceWidth=w,sourceHeight=h,sourceYOffset=yoffset,sourceEnemyElevation=elevation,singleBattleBaseCenter=dict(x=176 if side=='front' else 72,y=40 if side=='front' else 80),singleBattleCenter=dict(x=x+32,y=y+32),singleBattleTopLeft=dict(x=x,y=y),alphaBounds=bounds,transparentIndex=0,paletteRgb555=pal555,paletteExpansion='floor(rgb5 * 255 / 31)',flipH=False,sourceRefs=[source(image_path,'gMon'+side.title()+'Pic_Eevee'),source(path,'gMon'+side.title()+'PicCoords[SPECIES_EEVEE]',cm),source('src/data/pokemon_graphics/enemy_mon_elevation.h','gEnemyMonElevation default zero'),source(palpath,('gMonPalette_' if variant=='normal' else 'gMonShinyPalette_')+'Eevee')],**asset))
 icontext=s['src/pokemon_icon.c'].decode();pm=re.search(r'\[SPECIES_EEVEE\]\s*=\s*(\d+),',icontext);need(pm and pm[1]=='2','Icon palette differs');im=re.search(r'\[SPECIES_EEVEE\]\s*=\s*gMonIcon_Eevee,',icontext);need(im is not None,'Icon table differs')
 path='graphics/pokemon/eevee/icon.png';indices=image(path,(32,64))[:1024];palpath='graphics/pokemon/icon_palettes/icon_palette_2.pal';pal,pal555=palette(palpath);asset=add('icons/eevee',32,32,rgba(indices,pal));icons.append(dict(speciesId='pret:SPECIES_EEVEE',sourceSpeciesId=133,sourceFrameIndex=0,sourceFrameCount=2,paletteIndex=2,paletteVariant='shared-normal-shiny',transparentIndex=0,paletteRgb555=pal555,sourceRefs=[small(path,'gMonIcon_Eevee'),small(palpath,'gMonIconPalettes'),small('src/pokemon_icon.c','gMonIconPaletteIndices[SPECIES_EEVEE]',pm),small('src/pokemon_icon.c','gMonIconTable[SPECIES_EEVEE]',im)],**asset))
 path='graphics/pokemon/eevee/footprint.png';need('[SPECIES_EEVEE] = gMonFootprint_Eevee,' in s['src/data/pokemon_graphics/footprint_table.h'].decode(),'Footprint binding differs');im=Image.open(io.BytesIO(s[path]));need(im.mode=='1' and im.size==(16,16),'Footprint shape differs');indices=bytes(1 if x==0 else 0 for x in im.get_flattened_data());pal,_=palette('graphics/pokedex/kanto_dex_bgpals.pal');asset=add('eevee-footprint',16,16,rgba(indices,pal[:16]));footprints.append(dict(sourceSpeciesId=133,source=small(path,'gMonFootprint_Eevee'),**asset))
 path='src/data/pokemon/pokedex_entries.h';dm=re.search(r'\[NATIONAL_DEX_EEVEE\]\s*=\s*\{(.*?)\n    \}',s[path].decode(),re.S);need(dm is not None,'Dex entry missing');f=dict(re.findall(r'\.(\w+) = (.*?),\n',dm[1]));need(f['categoryName']=='_("EVOLUTION")' and f['description']=='gEeveePokedexText','Dex identities differ')
 dp='src/data/pokemon/pokedex_text_fr.h';dt=re.search(r'const u8 gEeveePokedexText\[\] = _\(\n(.*?)\);',s[dp].decode(),re.S);description=''.join(json.loads(x) for x in re.findall(r'"(?:[^"\\]|\\.)*"',dt[1]));dex=dict(sourceSpeciesId=133,name='EEVEE',category='EVOLUTION',heightDecimeters=int(f['height']),weightHectograms=int(f['weight']),description=description,source=source(path,'NATIONAL_DEX_EEVEE',dm,'source'),descriptionSource=source(dp,'gEeveePokedexText',dt,'source'))
 manifest=dict(schemaVersion=1,profile='firered-eevee-art-v1',sourceCommit=COMMIT,sprites=sprites,icons=icons,footprints=footprints,dex=dex,limits=['Party icon is source frame0; normal and shiny share the icon palette.','No move animations, overworld sprites or audio are in this art package.','Pixels use the existing catalog floor(rgb5 * 255 / 31) display convention.'],outputs=[dict(path=p) for p,b in sorted(out.items())]);out['manifest.json']=meta.canonical(manifest);return out

def run(root=ROOT,check=False):
 out=build(root)
 for rel,b in out.items():
  path=root/'content/generated/eevee-art'/rel
  if check:
   if meta.read(root,'content/generated/eevee-art/'+rel)!=b:raise ValueError('Eevee art bytes differ: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
 print(json.dumps(dict(passed=True,sourceBlobs=len(meta.authenticated(root)[0]['files']),battleSprites=4,icons=1,footprints=1,outputCount=len(out),manifestBytes=len(out['manifest.json'])),indent=2))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();run(a.project_root,a.check)
