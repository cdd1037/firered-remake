#!/usr/bin/env python3
"""Rebuild bounded Tower presentation pixels from authenticated local pret blobs."""
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
 if not p or pathlib.PurePosixPath(p).is_absolute() or '\\' in p or any(x in ('','.','..') for x in p.split('/')):raise ValueError('Unsafe Tower presentation path')
 q=root/p
 if any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Tower presentation symlink rejected')
 return q
def resolve(roots,p):
 for root in roots:
  q=safe(root,p)
  if q.exists():return q
 raise FileNotFoundError(p)
def authenticated(roots):
 raw=safe(roots[0],'scripts/recipes/presentation/pokemon-tower-presentation.lock.json').read_bytes()
 lock=json.loads(raw)
 if (lock['schemaVersion'],lock['repository'],lock['commit'])!=(1,'pret/pokefirered',COMMIT):raise ValueError('Tower presentation inventory differs')
 source={};inputs={}
 for entries,dest in [(lock['files'],source),(lock['inputs'],inputs)]:
  for row in entries:
   b=resolve(roots,row.get('localPath',row['path'])).read_bytes()
   dest[row['path']]=b
 return lock,source,inputs

def build(roots):
 lock,s,inputs=authenticated(roots);text=lambda p:s[p].decode();need=lambda value,message:None if value else (_ for _ in ()).throw(ValueError(message))
 import ui_tiles as ui
 bg=text('src/battle_bg.c');block=re.search(r'\[BATTLE_TERRAIN_INDOOR_1\] =\s*\{([^}]+)\}',bg)[1]
 for value in ['.tileset = sBattleTerrainTiles_Indoor,','.tilemap = sBattleTerrainTilemap_Indoor,','.palette = sBattleTerrainPalette_Indoor1']:need(value in block,'Tower terrain selection differs')
 need('{MAP_BATTLE_SCENE_INDOOR_1, BATTLE_TERRAIN_INDOOR_1}' in bg,'Tower terrain mapping differs')
 for floor in range(1,8):
  m=json.loads(s[f'data/maps/PokemonTower_{floor}F/map.json']);need(m['battle_scene']=='MAP_BATTLE_SCENE_INDOOR_1','Tower map battle scene differs');need(m['weather']==('WEATHER_NONE' if floor<3 else 'WEATHER_FOG_HORIZONTAL'),'Tower floor fog differs')
 prefix='graphics/battle_terrain/indoor/';tiles=ui.tile_list(s[prefix+'terrain.png'])[:77];pal=ui.palette(s[prefix+'1.pal']);tilemap=s[prefix+'terrain.bin']
 need(len(pal)==48 and len(tilemap)==4096,'Tower terrain shape differs')
 for entry in struct.unpack('<2048H',tilemap):
  tile,bank=entry&1023,entry>>12;need(tile<77 and bank in (0,2,3,4) and (bank!=0 or set(tiles[tile])=={0}),'Tower tile reference differs')
 full=ui.render_map(tiles,tilemap,{i+2:pal[i*16:(i+1)*16] for i in range(3)},240,160);terrain=full[:240*112*4]
 need(set(terrain[3::4])=={255} and set(full[240*112*4+3::4])=={0},'Tower terrain coverage differs')
 w,h,indices,_=ui.indexed_png(s['graphics/weather/fog_horizontal.png']);need((w,h)==(64,64),'Tower fog texture differs')
 fog=ui.rgba(indices,ui.palette(s['graphics/weather/default.pal']));need(set(fog[3::4])=={255},'Tower fog opacity source differs')
 weather=text('src/field_weather_effects.c')
 for binding in ['gWeatherPtr->fogHScrollPosX = (gSpriteCoordOffsetX - gWeatherPtr->fogHScrollOffset) & 0xFF;','if (++gWeatherPtr->fogHScrollCounter > 3)','Weather_SetTargetBlendCoeffs(12, 8, 3);','sprite->x = gWeatherPtr->fogHScrollPosX + 32 + sprite->tSpriteColumn * 64;']:need(binding in weather,'Tower fog semantics differ')
 need(re.search(r'#define NUM_FOG_HORIZONTAL_SPRITES\s+20',text('include/constants/field_weather.h')) is not None,'Tower fog sprite grid differs')
 healing=text('data/maps/PokemonTower_5F/scripts.inc').split('PokemonTower_5F_EventScript_PurifiedZone::')[1].split('PokemonTower_5F_EventScript_ExitPurifiedZone::')[0]
 need(re.findall(r'^\s*(fadescreen[^\n]*|special[^\n]*|play[^\n]*)',healing,re.M)==['fadescreen FADE_TO_WHITE','special HealPlayerParty','fadescreen FADE_FROM_WHITE'],'Tower healing visual sequence differs')
 need('if (transformType == 255) // Ghost unveiled with Silph Scope' in text('src/battle_gfx_sfx_util.c'),'Ghost reveal binding differs')
 need('if (CheckBattleTypeGhost(mon, gSprites[healthboxSpriteId].sBattlerId))\n        gender = 100;' in text('src/battle_interface.c'),'Ghost gender suppression differs')
 need('{ 176, 40 }' in text('src/battle_anim_mons.c'),'Ghost single-battle origin differs')
 existing=json.loads(inputs['content/generated/sprites/pokemon/rocktunnel/battle-art.json']);marowak=[x for x in existing['sprites'] if x['sourceSpeciesId']==105 and x['side']=='front'];need(len(marowak)==2 and all(x['singleBattleTopLeft']==dict(x=144,y=21) for x in marowak),'Inherited Marowak pose differs')
 output={'indoor1-scene.png':ui.png(240,112,terrain),'fog-horizontal.png':ui.png(64,64,fog)}
 meta=dict(schemaVersion=1,
  terrain=dict(id='BATTLE_TERRAIN_INDOOR_1',texture='indoor1-scene.png',width=240,height=112,floors=list(range(1,8))),
  fog=dict(texture='fog-horizontal.png',width=64,height=64,floors=list(range(3,8)),dependsOnSilphScope=False,spriteCount=20,columns=5,rows=4,pixelsPerScroll=1,ticksPerScroll=4,xScrollMask=255,sourcePriority=2,blendStart=[0,16],blendTarget=[12,8],blendDelay=3,alternateCoefficientUpdates=True,blendFormula='min(31, ((fog5 * EVA) + (back5 * EVB)) >> 4)',liveGbaCompositeVerified=False),
  ghost=dict(texture='content/generated/maps/regions/pokemon-tower/presentation/ghost-front.png',name='GHOST',topLeft=[144,8],width=64,height=64,showLevel=True,showHp=True,showGender=False,showCaughtMarker=False,shinyEntryAnimation=False),
  marowak=dict(sourceSpeciesId=105,textureNormal='content/generated/sprites/pokemon/rocktunnel/marowak-front-normal.png',textureShiny='content/generated/sprites/pokemon/rocktunnel/marowak-front-shiny.png',topLeft=[144,21],revealTransformType=255,initialGhostAppearance=True,postRevealSpeciesAppearance=True),
  healing=dict(map='map.pokemon-tower-5f',steps=['FADE_TO_WHITE','HealPlayerParty','FADE_FROM_WHITE'],whiteRgb555=32767,sourceFadeOutStart=0,sourceFadeOutTarget=16,sourceFadeOutDelta=2,sourceFadeOutAlternatesBgObjPalettes=True,sourceFogFadeInSteps=16,machineAnimation=False,musicCue=None),
  outputs=[dict(path=p) for p,b in sorted(output.items())])
 output['manifest.json']=encode(meta)
 return output

def run(root=ROOT,dependencies=(),check=False):
 roots=[root,*dependencies];output=build(roots)
 for rel,b in output.items():
  p=safe(root,'content/generated/ui/pokemon-tower-presentation/'+rel)
  if check:
   if p.read_bytes()!=b:raise ValueError('Tower output differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return dict(passed=True,sourceBlobs=33,newTextures=2,manifestBytes=len(output['manifest.json']))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',action='append',type=pathlib.Path,default=[]);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.project_root,a.dependency_root,a.check),indent=2))
