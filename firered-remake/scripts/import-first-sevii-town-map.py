#!/usr/bin/env python3
"""Import the bounded first-Sevii atlas from pinned Git blobs or its authenticated cache."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse,importlib.util,json,pathlib,re,struct,subprocess,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parent.parent
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-first-sevii-town-map-v1'
MAPS={
 'OneIsland':'map.one-island','OneIsland_Harbor':'map.one-island-harbor',
 'OneIsland_PokemonCenter_1F':'map.one-island-pokemon-center-1f',
 'TwoIsland':'map.two-island','TwoIsland_Harbor':'map.two-island-harbor',
 'TwoIsland_JoyfulGameCorner':'map.two-island-joyful-game-corner',
 'TwoIsland_PokemonCenter_1F':'map.two-island-pokemon-center-1f',
 'ThreeIsland':'map.three-island','ThreeIsland_Harbor':'map.three-island-harbor',
 'ThreeIsland_Port':'map.three-island-port','ThreeIsland_BondBridge':'map.three-island-bond-bridge',
 'ThreeIsland_BerryForest':'map.three-island-berry-forest',
 'ThreeIsland_PokemonCenter_1F':'map.three-island-pokemon-center-1f','ThreeIsland_Mart':'map.three-island-mart',
 'CinnabarIsland':'map.cinnabar-island','CinnabarIsland_PokemonCenter_1F':'map.cinnabar-pokemon-center-1f'
}
BASE='graphics/region_map/'
PATHS=frozenset(['src/region_map.c','src/text.c','src/strings.c','include/characters.h','include/constants/flags.h',
 'include/region_map.h','data/layouts/layouts.json','src/data/region_map/region_map_sections.json',
 'src/data/region_map/region_map_layout_sevii_123.h','graphics/fonts/keypad_icons.png',
 *[BASE+p for p in ['kanto.bin','sevii_123.bin','region_map.png','region_map.pal','map_edge.bin','map_edge.png','top_bar.pal',
 'switch_map_menu.png','switch_map_kanto_sevii_123.bin','switch_map_cursor_left.png','switch_map_cursor_right.png','switch_map_cursor.pal']],
 *['data/maps/'+m+'/map.json' for m in MAPS]])
spec=importlib.util.spec_from_file_location('first_sevii_pixels',ROOT/'scripts/first-sevii-town-map-pixels.py')
pixels=importlib.util.module_from_spec(spec);spec.loader.exec_module(pixels)
def canonical(d):return (json.dumps(d,ensure_ascii=False,sort_keys=True,indent=2)+'\n').encode()
def pairs(rows):
 d={}
 for k,v in rows:
  if k in d:raise ValueError('Duplicate JSON member: '+k)
  d[k]=v
 return d
def safe(root,relative=None):
 root=pathlib.Path(root).absolute();target=root
 if relative is not None:
  p=pathlib.PurePosixPath(relative)
  if not relative or p.is_absolute() or '..' in p.parts or '\\' in relative or str(p)!=relative:raise ValueError('Unsafe path')
  target=root/relative
 if any(p.is_symlink() for p in [target,*target.parents]):raise ValueError('Symlink path')
 return target

def load_sources(lock,raw,repo):
 out={}
 for e in lock['files']:
  b=safe(safe(raw, e['path'])).read_bytes() if repo is None else subprocess.check_output(['git','-C',str(repo),'show',COMMIT+':'+e['path']],timeout=30)
  out[e['path']]=b
 return out
def convert(s):
 code=s['src/region_map.c'].decode();outputs={};assets=[]
 for needle in ['#define SWITCH_BUTTON_X 21','#define SWITCH_BUTTON_Y 11',
  'sSwitchMapMenu->maxSelection = 1;','sSwitchMapMenu->yOffset = 6;',
  'sSwitchMapMenu->chosenRegion = GetRegionMapPlayerIsOn();',
  'sSwitchMapMenu->currentSelection = sSwitchMapMenu->chosenRegion;',
  'sMapCursor->snapId %= 3;', 'sMapCursor->snapId == 0 && GetSelectedRegionMap() != GetRegionMapPlayerIsOn()',
  'WriteSequenceToBgTilemapBuffer(0, 0x0F0, 0x18, 14, 3, 1, 0x3, 0x001);',
  'WriteSequenceToBgTilemapBuffer(0, 0x100, 0x18, 15, 3, 1, 0x3, 0x001);',
  'WriteSequenceToBgTilemapBuffer(0, 0x110, 0x18, 16, 3, 1, 0x3, 0x001);',
  'case MAPSEC_BERRY_FOREST:\n        sMapCursor->x = 14;\n        sMapCursor->y = 12;',
  'case MAP_TYPE_INDOOR:', 'warp = &gSaveBlock1Ptr->escapeWarp;',
  'divisor = width / sMapSectionDimensions[sMapCursor->selectedMapsec][0];',
  'divisor = height / sMapSectionDimensions[sMapCursor->selectedMapsec][1];']:
  if needle not in code:raise ValueError('Source semantics differ: '+needle)
 def add(id,w,h,rgba,paths):
  if len(rgba)!=w*h*4 or not 0<w*h<=38400:raise ValueError('Asset dimensions')
  outputs[id+'.png']=pixels.compressed_png(w,h,rgba)
  assets.append(dict(id=id, width=w, height=h, texturePath=id + '.png', sourcePaths=paths))
 mp=pixels.palette(s[BASE+'region_map.pal'],80);tp=pixels.palette(s[BASE+'top_bar.pal'],16)
 pals={i:mp[i*16:(i+1)*16].copy() for i in range(5)}
 pals[2]=[tuple((((((c>>3)<<8)//100)*95)>>8) for c in rgb) for rgb in pals[2]]
 pals[2]=[tuple((c<<3)|(c>>2) for c in rgb) for rgb in pals[2]];pals[2][15]=mp[47]
 for p in pals.values():p[0]=tp[15]
 tiles=pixels.tile_list(s[BASE+'region_map.png']);edges=pixels.tile_list(s[BASE+'map_edge.png'])
 ee=list(struct.unpack('<600H',s[BASE+'map_edge.bin']))
 for x,n in [(0,2),(1,3),(28,62),(29,63),*[(x,61) for x in range(2,28)]]:ee[30+x]=0x2000+n
 for id,path in [('kanto-switch','kanto.bin'),('sevii123','sevii_123.bin'),('sevii123-locked','sevii_123.bin')]:
  entries=list(struct.unpack('<600H',s[BASE+path]))
  if id!='sevii123-locked':
   for y,base in [(14,0xf0),(15,0x100),(16,0x110)]:
    for x in range(3):entries[y*30+24+x]=0x3000+base+x
  frame=pixels.canvas(240,160,(*tp[15],255));pixels.overlay(frame,pixels.render_tilemap(edges,ee,pals));pixels.overlay(frame,pixels.render_tilemap(tiles,entries,pals))
  add(id,240,160,frame,[BASE+p for p in ['region_map.png','region_map.pal',path,'map_edge.png','map_edge.bin','top_bar.pal']])
 menu=pixels.render_tilemap(pixels.tile_list(s[BASE+'switch_map_menu.png']),struct.unpack('<600H',s[BASE+'switch_map_kanto_sevii_123.bin']),pals)
 add('switch-menu',240,160,menu,[BASE+'switch_map_menu.png',BASE+'switch_map_kanto_sevii_123.bin',BASE+'region_map.pal'])
 pal=pixels.palette(s[BASE+'switch_map_cursor.pal'],16)
 for side in ['left','right']:
  path=BASE+'switch_map_cursor_'+side+'.png';w,h,inds,_=pixels.indexed_png(s[path])
  if (w,h)!=(32,64):raise ValueError('Arrow sheet profile differs')
  for n in range(2):add('switch-'+side+'-'+str(n),32,32,pixels.rgba(inds[n*1024:(n+1)*1024],pal),[path,BASE+'switch_map_cursor.pal'])
 keypad='graphics/fonts/keypad_icons.png';w,h,inds,_=pixels.indexed_png(s[keypad])
 match=re.search(r'\[CHAR_DPAD_UPDOWN\]\s*=\s*\{\s*(0x[0-9A-Fa-f]+),\s*(\d+),\s*(\d+)\s*\}',s['src/text.c'].decode())
 if match is None or tuple(map(int,match.groups()[1:]))!=(8,12):raise ValueError('Keypad up/down metadata differs')
 tile=int(match[1],16);x=tile%16*8;y=tile//16*8
 add('keypad-updown',8,12,pixels.rgba([inds[(y+r)*w+x+c] for r in range(12) for c in range(8)],tp),[keypad,'src/text.c',BASE+'top_bar.pal'])
 sections=json.loads(s['src/data/region_map/region_map_sections.json'])['map_sections'];by={v['id']:v for v in sections}
 rows=re.findall(r'\{(MAPSEC_[A-Z0-9_, ]+)\}',s['src/data/region_map/region_map_layout_sevii_123.h'].decode());grid=[re.findall(r'MAPSEC_[A-Z0-9_]+',r) for r in rows]
 if len(grid)!=30 or any(len(r)!=22 for r in grid):raise ValueError('Section grid differs')
 flags=dict(re.findall(r'case (MAPSEC_\w+):\n        return FlagGet\((FLAG_\w+)\)',code));cells=[];icons=[]
 for y in range(15):
  for x in range(22):
   a=grid[y][x];b=grid[y+15][x];cells.append(dict(x=x,y=y,sectionId=a,name=by.get(a,{}).get('name',''),dungeonSectionId=b,dungeonName=by.get(b,{}).get('name','')))
   if b!='MAPSEC_NONE':icons.append(dict(x=x,y=y,centerX=36+x*8,centerY=36+y*8,sectionId=b,visitedFlag=flags[b],visibleFlag=None))
 layouts={l['id']:l for l in json.loads(s['data/layouts/layouts.json'])['layouts'] if 'id' in l};positions=[]
 for name,id in MAPS.items():
  path='data/maps/'+name+'/map.json';m=json.loads(s[path]);l=layouts[m['layout']];sec=by[m['region_map_section']]
  override=dict(x=14,y=12) if sec['id']=='MAPSEC_BERRY_FOREST' else None
  requires_escape=m['map_type']=='MAP_TYPE_INDOOR' and (sec['width'],sec['height'])!=(1,1)
  positions.append(dict(stableMapId=id,sourceName=name,sourceMap=m['id'],sectionId=sec['id'],width=l['width'],height=l['height'],sectionX=sec['x'],sectionY=sec['y'],sectionWidth=sec['width'],sectionHeight=sec['height'],mapType=m['map_type'],override=override,region=0 if name.startswith('Cinnabar') else 1,requiresEscapeWarp=requires_escape,sourcePaths=[path,'data/layouts/layouts.json','src/data/region_map/region_map_sections.json','src/region_map.c']))
 for symbol in ['InitRegionMapType','BufferRegionMapBg','InitSwitchMapMenu','HandleSwitchMapInput','SnapToIconOrButton','GetPlayerPositionOnRegionMap','GetPlayerPositionOnRegionMap_HandleOverrides']:
  m=re.search(r'^static [^;\n]+ '+symbol+r'\([^;\n]*\)\n\{',code,re.M)
  if m is None:raise ValueError('Source function missing: '+symbol)
 texts={k:re.search(r'const u8 '+sym+r'\[\] = _\("(.*)"\);',s['src/strings.c'].decode())[1] for k,sym in [('pick','gText_RegionMap_UpDownPick'),('ok','gText_RegionMap_AButtonOK'),('switch','gText_RegionMap_AButtonSwitch')]}
 outputs['town-map.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,assets=assets,cells=cells,icons=icons,positions=positions,uiTexts=texts,unlockFlag='FLAG_SYS_SEVII_MAP_123',switchMenu=dict(maxSelection=1,yOffset=6,highlightLeft=72,highlightRight=168,highlightHeight=32,leftArrowX=88,rightArrowX=152,arrowSize=32,arrowFrameTicks=20,cancelRestoresPlayerRegion=True),limitations=['Static completed menu and atlas states; GPU blend and open/close transitions are not native-verified.','Guide pages, Fly and Sevii4567 are not implemented.','Three Island Harbor requires source escapeWarp; do not guess its player marker.','New projection covers the 14 first-Sevii maps and Cinnabar Island/Center only; older authenticated early-Kanto projection remains separate.']))
 outputs['manifest.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=len(s),assetCount=len(assets),upstreamScriptsExecuted=False,romDownloaded=False,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
 return outputs
def import_content(raw,out,repo=None,lock_path=None):
 raw=safe(raw);out=safe(out)
 if raw.is_relative_to(out) or out.is_relative_to(raw):raise ValueError('Overlapping directories')
 if repo is not None and any(p.resolve().is_relative_to(pathlib.Path(repo).resolve()) or pathlib.Path(repo).resolve().is_relative_to(p.resolve()) for p in (raw,out)):raise ValueError('Output overlaps source repository')
 lb=safe(lock_path or ROOT / 'scripts/import-config/first-sevii-town-map.json').read_bytes()
 lock=json.loads(lb,object_pairs_hook=pairs)
 if (lock['commit'], lock['profile'], lock['repository'], lock['gameEdition'], lock['romRevision']) != (COMMIT, PROFILE, 'pret/pokefirered', 'FIRERED', 0) or len(lock['files']) != len(PATHS) or {e['path'] for e in lock['files']} != PATHS:raise ValueError('Source profile differs')
 s=load_sources(lock,raw,repo);outfiles=convert(s)
 material=[(raw,p,b) for p,b in s.items()]+[] if repo is not None else []
 material += [(out,p,b) for p,b in outfiles.items()]
 for root,p,b in material:
  target=safe(root,p);stage=safe(root,p+'.tmp');target.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(b);stage.replace(target)
 return dict(profile=PROFILE,inputCount=len(s),assetCount=9,outputCount=len(outfiles))
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--raw',type=pathlib.Path,default=ROOT/'content/raw/pokefirered/037335f4');p.add_argument('--out',type=pathlib.Path,default=ROOT/'content/generated/first-sevii-town-map');p.add_argument('--lock',type=pathlib.Path);a=p.parse_args();print(json.dumps(import_content(a.raw,a.out,a.reference_repo,a.lock),sort_keys=True))
if __name__=='__main__':main()
