#!/usr/bin/env python3
"""Authenticate the separate original Indoor2 settled battle background."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

import argparse, json, pathlib, re, struct, sys
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe terrain path')
 q=root/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Terrain symlink rejected')
 return q
def authenticated(root=ROOT):
 b=safe(root,'scripts/recipes/presentation/rocket-hideout-terrain.lock.json').read_bytes()
 lock=json.loads(b)
 if (lock['schemaVersion'],lock['commit'])!=(1,COMMIT):raise ValueError('Terrain source inventory differs')
 source={}
 for row in lock['files']:
  b=safe(root,'content/raw/pokefirered/037335f4/'+row['path']).read_bytes()
  source[row['path']]=b
 return source
def build(source,root=ROOT):
 text=lambda p:source[p].decode()
 bg=text('src/battle_bg.c')
 for binding in ['static const u32 sBattleTerrainTiles_Indoor[] = INCBIN_U32("graphics/battle_terrain/indoor/terrain.4bpp.lz");','static const u32 sBattleTerrainTilemap_Indoor[] = INCBIN_U32("graphics/battle_terrain/indoor/terrain.bin.lz");','static const u32 sBattleTerrainPalette_Indoor2[] = INCBIN_U32("graphics/battle_terrain/indoor/2.gbapal.lz");','{MAP_BATTLE_SCENE_INDOOR_2, BATTLE_TERRAIN_INDOOR_2}']:
  if binding not in bg:raise ValueError('Indoor2 source binding differs')
 block=re.search(r'\[BATTLE_TERRAIN_INDOOR_2\] =\s*\{([^}]+)\}',bg)[1]
 if '.tileset = sBattleTerrainTiles_Indoor,' not in block or '.tilemap = sBattleTerrainTilemap_Indoor,' not in block or '.palette = sBattleTerrainPalette_Indoor2' not in block:raise ValueError('Indoor2 source selection differs')
 if '$(BATTLETERRAINGFXDIR)/indoor/terrain.4bpp: %.4bpp: %.png\n\t$(GFX) $< $@ -num_tiles 77 -Wnum_tiles' not in text('graphics_file_rules.mk'):raise ValueError('Indoor2 original tile count differs')
 for name in ['CeladonCity_GameCorner','RocketHideout_B1F','RocketHideout_B2F','RocketHideout_B3F','RocketHideout_B4F','RocketHideout_Elevator']:
  m=json.loads(source['data/maps/'+name+'/map.json'])
  if m['battle_scene']!=('MAP_BATTLE_SCENE_NORMAL' if name=='CeladonCity_GameCorner' else 'MAP_BATTLE_SCENE_INDOOR_2'):raise ValueError('Rocket map source battle scene differs')
 import ui_tiles as ui
 prefix='graphics/battle_terrain/indoor/';png=source[prefix+'terrain.png'];tilemap=source[prefix+'terrain.bin']
 if ui.indexed_png(png)[:2]!=(64,80) or len(tilemap)!=4096:raise ValueError('Indoor2 source sheet/map size differs')
 tiles=ui.tile_list(png)[:77];palette=ui.palette(source[prefix+'2.pal'])
 if len(palette)!=48:raise ValueError('Indoor2 source palette length differs')
 for entry in struct.unpack('<2048H',tilemap):
  tile,bank=entry&1023,entry>>12
  if tile>=77 or bank not in (0,2,3,4) or bank==0 and set(tiles[tile])!={0}:raise ValueError('Indoor2 tile/palette reference differs')
 full=ui.render_map(tiles,tilemap,{i+2:palette[i*16:(i+1)*16] for i in range(3)},240,160);scene=full[:240*112*4]
 if set(scene[3::4])!={255} or set(full[240*112*4+3::4])!={0}:raise ValueError('Indoor2 scene coverage differs')
 return {'indoor2-scene.png':ui.png(240,112,scene)}
def run(root=ROOT,check=False):
 source=authenticated(root);output=build(source,root)
 manifest=encode(dict(schemaVersion=1,terrain='BATTLE_TERRAIN_INDOOR_2',width=240,height=112,outputs=[dict(path=p) for p,b in sorted(output.items())]))
 output['manifest.json']=manifest
 for rel,b in output.items():
  p=safe(root,'content/generated/battle/terrain/rocket-hideout/'+rel)
  if check:
   if p.read_bytes()!=b:raise ValueError('Generated terrain differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,sourceBlobs=12,manifestBytes=len(manifest))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.check),indent=2))
