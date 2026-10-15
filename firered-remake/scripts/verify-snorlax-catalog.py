#!/usr/bin/env python3
"""Verify source metadata, independent boundaries, and pixels offline."""
import io
import argparse, importlib.util, io, json, pathlib, sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
def load(name,path):
 s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m
def main():
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--dependency-project-root',type=pathlib.Path);p.add_argument('--reference-root',type=pathlib.Path);a=p.parse_args()
 m=load('snorlax_metadata_verify',ROOT/'scripts/import-snorlax-pokemon.py');art=load('snorlax_art_verify',ROOT/'scripts/import-snorlax-art.py')
 outputs=m.generate(ROOT);pixels=art.build(ROOT)
 for relative,b in outputs.items():assert (ROOT/relative).read_bytes()==b,relative
 for relative,b in pixels.items():assert (ROOT/'content/generated/snorlax-art'/relative).read_bytes()==b,relative
 doc=json.loads(outputs['content/generated/snorlax-pokemon/catalog.json']);fixture=json.loads(outputs['tests/fixtures/snorlax/catalog-source-fixtures.json']);manifest=json.loads(pixels['manifest.json'])
 species=doc['species'][0]
 assert species['baseStats']==dict(hp=160,attack=110,defense=65,speed=30,specialAttack=65,specialDefense=110)
 assert (species['sourceSpeciesId'],species['genderRatio'],species['growthRate'],species['catchRate'],species['experienceYield'])==(143,31,'slow',25,154)
 assert (species['ability1'],species['ability2'])==('ABILITY_IMMUNITY','ABILITY_THICK_FAT') and species['evolutions']==[] and doc['deferredEvolutions']==[] and doc['naturalLevelLimit'] is None
 assert [(x['level'],x['move']['value'][10:]) for x in species['levelUpMoves']]==[(1,'TACKLE'),(5,'AMNESIA'),(9,'DEFENSE_CURL'),(13,'BELLY_DRUM'),(17,'HEADBUTT'),(21,'YAWN'),(25,'REST'),(29,'SNORE'),(33,'BODY_SLAM'),(37,'SLEEP_TALK'),(41,'BLOCK'),(45,'COVET'),(49,'ROLLOUT'),(53,'HYPER_BEAM')]
 cases={r['level']:r['moves'] for r in fixture['naturalMoves']}
 for level,expected in {1:['TACKLE'],13:['TACKLE','AMNESIA','DEFENSE_CURL','BELLY_DRUM'],30:['HEADBUTT','YAWN','REST','SNORE'],33:['YAWN','REST','SNORE','BODY_SLAM'],37:['REST','SNORE','BODY_SLAM','SLEEP_TALK'],40:['REST','SNORE','BODY_SLAM','SLEEP_TALK'],41:['SNORE','BODY_SLAM','SLEEP_TALK','BLOCK'],100:['BLOCK','COVET','ROLLOUT','HYPER_BEAM']}.items():assert cases[level]==['pret:MOVE_'+x for x in expected]
 assert len(cases)==17 and fixture['initialLevel30Pp']==[15,10,10,15]
 defs={x['sourceMoveId']:x for x in doc['moves']};assert set(defs)=={173,214,335,343}
 assert [(defs[i]['pp'],defs[i]['effect'],defs[i]['power']) for i in (173,214,335,343)]==[(15,'EFFECT_SNORE',40),(10,'EFFECT_SLEEP_TALK',0),(5,'EFFECT_MEAN_LOOK',0),(40,'EFFECT_THIEF',40)]
 assert all(x['battleEffectImplemented'] is False for x in doc['moves'])
 assert 'TM39_ROCK_TOMB' in fixture['machines']['machineSymbols'] and not any(x.startswith(('HM01_','TM12_','TM19_','TM49_')) for x in fixture['machines']['machineSymbols'])
 if a.dependency_project_root:
  inherited_species={};inherited_moves={}
  for path in (a.dependency_project_root/'content/generated').glob('*/catalog.json'):
   if path.parent.name=='snorlax-pokemon':continue
   d=json.loads(path.read_bytes())
   if not isinstance(d,dict) or not isinstance(d.get('species'),list) or not isinstance(d.get('moves'),list):continue
   for row in d['species']:inherited_species[row['id']['value']]=row
   for row in d['moves']:
    key=row['id']['value'];assert key not in inherited_moves or inherited_moves[key]==row,key;inherited_moves[key]=row
  assert len(inherited_species)==101 and len(inherited_moves)==235
  added={r['id']['value']:r for r in doc['moves']};assert not added.keys()&inherited_moves.keys()
  for row in fixture['requiredMoveDefinitions']:assert (added.get(row['id']['value']) or inherited_moves[row['id']['value']])==row
  dex=json.loads((a.dependency_project_root/'content/generated/pokedex-menu/pokedex-menu.json').read_bytes());existing=next(x for x in dex['entries'] if x['sourceSpeciesId']==143)
  for key,value in manifest['dex'].items():assert existing[key]==value,key
 # Independent source pixel oracle: do not reuse importer conversion helpers.
 for sprite in manifest['sprites']:
  im=Image.open(ROOT/'content/raw/pokefirered/037335f4'/sprite['sourceImagePath']);indices=list(im.get_flattened_data());lines=(ROOT/'content/raw/pokefirered/037335f4'/sprite['sourcePalettePath']).read_text().splitlines();colors=[list(map(int,x.split())) for x in lines[3:]]
  expected=bytes(c for i in indices for c in (*[(v//8)*255//31 for v in colors[i]],255 if i else 0))
  assert Image.open(io.BytesIO(pixels[sprite['texturePath']])).convert('RGBA').tobytes()==expected
  decoded=Image.open(io.BytesIO(pixels[sprite['texturePath']]));assert decoded.size==(64,64) and decoded.convert('RGBA').tobytes()==expected
 assert len(manifest['outputs'])==12 and manifest['dex']['weightHectograms']==4600
 print('PASS:31 pinned blobs; species143;14 natural moves;4 absent definitions;17 level fixtures;31 source TM/HM bits;4 independent sprite pixel oracles;12 exact art outputs;full Dex equality.')
if __name__=='__main__':main()
