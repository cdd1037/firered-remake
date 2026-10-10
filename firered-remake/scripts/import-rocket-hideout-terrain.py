#!/usr/bin/env python3
"""Authenticate the separate original Indoor2 settled battle background."""
import argparse,hashlib,importlib.util,json,pathlib,re,struct,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
LOCK_SHA='033fb31fc6d0a3f6d9bb8dc82d261687e929c0e4017d882a78d36ad1dc5d72ec'
MANIFEST_SHA='88bb8890282965fb1f9560f93db7863c26ce52dead768044df4b103b215e77af'
def sha(b):return hashlib.sha256(b).hexdigest()
def encode(d):return (json.dumps(d,indent=2)+'\n').encode()
def safe(root,p):
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe terrain path')
 q=root/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Terrain symlink rejected')
 return q
def authenticated(root=ROOT):
 b=safe(root,'scripts/rocket-hideout-terrain.lock.json').read_bytes()
 if len(b)!=3419 or sha(b)!=LOCK_SHA:raise ValueError('Terrain lock differs')
 lock=json.loads(b)
 if (lock['schemaVersion'],lock['commit'],len(lock['files']),len(lock['inputs']))!=(1,COMMIT,12,2):raise ValueError('Terrain source inventory differs')
 source={}
 for row in lock['files']:
  b=safe(root,'content/raw/rocket-hideout-terrain/'+row['path']).read_bytes()
  if (len(b),sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())!=(row['byteLength'],row['sha256'],row['gitBlobSha1']):raise ValueError('Terrain source differs: '+row['path'])
  source[row['path']]=b
 for row in lock['inputs']:
  b=safe(root,row['path']).read_bytes()
  if (len(b),sha(b))!=(row['byteLength'],row['sha256']):raise ValueError('Terrain decoder dependency differs')
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
 p=root/'scripts/import-lab-battle-ui.py';spec=importlib.util.spec_from_file_location('rocket_terrain_codec',p);ui=importlib.util.module_from_spec(spec);spec.loader.exec_module(ui)
 prefix='graphics/battle_terrain/indoor/';png=source[prefix+'terrain.png'];tilemap=source[prefix+'terrain.bin']
 if ui.indexed_png(png)[:2]!=(64,80) or len(tilemap)!=4096:raise ValueError('Indoor2 source sheet/map size differs')
 tiles=ui.tile_list(png)[:77];palette=ui.palette(source[prefix+'2.pal'])
 if len(palette)!=48:raise ValueError('Indoor2 source palette length differs')
 for entry in struct.unpack('<2048H',tilemap):
  tile,bank=entry&1023,entry>>12
  if tile>=77 or bank not in (0,2,3,4) or bank==0 and set(tiles[tile])!={0}:raise ValueError('Indoor2 tile/palette reference differs')
 full=ui.render_map(tiles,tilemap,{i+2:palette[i*16:(i+1)*16] for i in range(3)},240,160);scene=full[:240*112*4]
 if set(scene[3::4])!={255} or set(full[240*112*4+3::4])!={0}:raise ValueError('Indoor2 scene coverage differs')
 return {'indoor2-scene.png':ui.png(240,112,scene)},sha(scene)
def run(root=ROOT,check=False):
 source=authenticated(root);output,rgba=build(source,root)
 manifest=encode(dict(schemaVersion=1,profile='firered-rocket-hideout-terrain-v1',sourceCommit=COMMIT,lockSha256=LOCK_SHA,terrain='BATTLE_TERRAIN_INDOOR_2',width=240,height=112,rgbaSha256=rgba,outputs=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(output.items())]))
 if sha(manifest)!=MANIFEST_SHA:raise ValueError('Reviewed terrain output differs')
 output['manifest.json']=manifest
 for rel,b in output.items():
  p=safe(root,'content/generated/rocket-hideout-terrain/'+rel)
  if check:
   if p.read_bytes()!=b:raise ValueError('Generated terrain differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,sourceBlobs=12,manifestSha256=sha(manifest),manifestBytes=len(manifest),rgbaSha256=rgba)
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.check),indent=2))
