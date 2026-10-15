#!/usr/bin/env python3
"""Independent roster vectors and raw indexed-pixel checks; no engine launch or upstream execution."""
import argparse, importlib.util, json, pathlib, sys
sys.dont_write_bytecode=True
from PIL import Image
ROOT=pathlib.Path(__file__).resolve().parents[1]
checks=0
def check(value,message):
 global checks
 checks+=1
 if not value:raise AssertionError(message)
def read(path):return json.loads(pathlib.Path(path).read_bytes())
def main(project,target):
 spec=importlib.util.spec_from_file_location('rocket_catalog_importer',ROOT/'scripts/import-rocket-hideout-catalog.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
 result=imp.rebuild(project,target,check=True);check(len(result)==2,'empty source regeneration suite')
 fixture_bytes=(ROOT/'tests/Game.RocketHideoutCatalog.Tests/Fixtures/source-roster-vectors.json').read_bytes();vectors=json.loads(fixture_bytes)
 pokemon=read(target/'content/generated/rocket-hideout-pokemon/catalog.json');trainers=read(target/'content/generated/rocket-hideout-trainers/trainers.json');scope=read(target/'content/generated/rocket-hideout-pokemon/scope.json');sup=read(target/'content/generated/rocket-hideout-pokemon/supplement.json');art=read(target/'content/generated/rocket-hideout-pokemon/battle-art.json')
 check(len(vectors['trainers'])==13,'Empty expected roster');check(len(trainers['trainers'])==13,'trainer count');check(sum(len(t['party']) for t in trainers['trainers'])==36,'party count')
 byid={t['id']['value'][5:]:t for t in trainers['trainers']};resolved={t['trainer']:t for t in trainers['resolvedMoves']};enemies=set();empty=0
 for expected in vectors['trainers']:
  t=byid[expected['trainer']];check(t['sourceTrainerId']==expected['trainerId'],'trainer source ID');check(len(t['party'])==len(expected['mons']),'trainer party length');check(t['baseMoney']*4*t['party'][-1]['level']==expected['normalPayout'],'normal payout')
  check(t['aiFlags']==1 and t['aiFlagSymbols']==['AI_SCRIPT_CHECK_BAD_MOVE'],'AI flags');check(t['items']==['ITEM_NONE']*4 and not t['doubleBattle'],'trainer medicine/doubles');check(t['partyFlags']==(1 if expected['partyFormat']=='NO_ITEM_CUSTOM_MOVES' else 0),'party format')
  name_hash=0
  for index,(mon,exp) in enumerate(zip(t['party'],expected['mons'])):
   name_hash+=sum(t['encodedTrainerName'])+sum(mon['encodedSpeciesName']);personality=0x88+(name_hash<<8)
   check(personality==exp['personality'],'cumulative personality');check(personality%25==exp['nature'],'source nature');check(personality%2==exp['abilityIndex']==0,'source ability slot');check(mon['sourceIv']==exp['sourceIv'] and mon['sourceIv']*31//255==exp['generatedIv'],'source IV conversion');check(mon['species']['value']=='pret:'+exp['species'] and mon['level']==exp['level'] and mon['heldItem']==exp['heldItem'],'party identity')
   actual=resolved[t['id']['value'][5:]]['party'][index]['moves'];check(actual==exp['moves'],'exact initial moves')
   check(mon['customMoves']==([dict(value='pret:'+m) for m in exp['moves']] if t['partyFlags'] else []),'source custom slots')
   empty+=actual.count('MOVE_NONE');enemies.update(x for x in actual if x!='MOVE_NONE')
   if 'MOVE_NONE' in actual:check(len(actual)==4 and actual[-1]=='MOVE_NONE' and actual[:3]==['MOVE_SMOG','MOVE_TACKLE','MOVE_POISON_GAS'],'source three-move Koffing')
 check(empty==3,'exact empty source slot count');check(enemies==set(scope['requiredTrainerMoves']) and len(enemies)==38,'enemy move closure')
 giovanni=byid['TRAINER_BOSS_GIOVANNI'];check([m['sourceIv'] for m in giovanni['party']]==[250]*3,'Giovanni source IVs');check(giovanni['baseMoney']==25 and giovanni['sourceTrainerClass']==83 and giovanni['sourceTrainerPic']==108,'Giovanni source class/portrait')
 m=pokemon['species'][0];check(len(pokemon['species'])==1 and m['id']['value']=='pret:SPECIES_KANGASKHAN' and m['sourceSpeciesId']==115,'Kangaskhan identity');check(m['baseStats']==dict(hp=105,attack=95,defense=80,speed=90,specialAttack=40,specialDefense=80),'Kangaskhan stats');check(m['type1']==m['type2']=='TYPE_NORMAL' and m['ability1']=='ABILITY_EARLY_BIRD' and m['ability2']=='ABILITY_NONE' and m['genderRatio']==254,'Kangaskhan source traits');check(m['catchRate']==45 and m['experienceYield']==175 and m['growthRate']=='mediumFast' and m['friendship']==70,'Kangaskhan growth/capture data');check(m['evYield']==dict(hp=2,attack=0,defense=0,speed=0,specialAttack=0,specialDefense=0),'Kangaskhan EV yield');check(m['evolutions']==[] and pokemon['evolutionOverrides']==[] and pokemon['deferredEvolutions']==[],'No evolution mutation')
 check([(r['level'],r['move']['value'][10:]) for r in m['levelUpMoves']]==[(1,'COMET_PUNCH'),(1,'LEER'),(7,'BITE'),(13,'TAIL_WHIP'),(19,'FAKE_OUT'),(25,'MEGA_PUNCH'),(31,'RAGE'),(37,'ENDURE'),(43,'DIZZY_PUNCH'),(49,'REVERSAL')],'Full untruncated learnset')
 moves={m['id']['value'][10:]:m for m in pokemon['moves']};check(len(moves)==45 and 'NONE' not in moves,'Metadata move inventory');check(all(not m['battleEffectImplemented'] for m in moves.values()),'No executable effect assertion')
 expected_moves={'FAKE_OUT':(252,10,40,100,'TYPE_NORMAL','EFFECT_FAKE_OUT',0,1),'MEGA_PUNCH':(5,20,80,85,'TYPE_NORMAL','EFFECT_HIT',0,0),'COMET_PUNCH':(4,15,18,85,'TYPE_NORMAL','EFFECT_MULTI_HIT',0,0),'DIZZY_PUNCH':(146,10,70,100,'TYPE_NORMAL','EFFECT_CONFUSE_HIT',20,0),'REVERSAL':(179,15,1,100,'TYPE_FIGHTING','EFFECT_FLAIL',0,0),'TAUNT':(269,20,0,100,'TYPE_DARK','EFFECT_TAUNT',0,0),'FRUSTRATION':(218,20,1,100,'TYPE_NORMAL','EFFECT_FRUSTRATION',0,0),'SNATCH':(289,10,0,100,'TYPE_DARK','EFFECT_SNATCH',0,4)}
 for name,expected in expected_moves.items():check(tuple(moves[name][k] for k in ['sourceMoveId','pp','power','accuracy','type','effect','secondaryEffectChance','priority'])==expected,'Source definition '+name)
 check(moves['FAKE_OUT']['flags']==['FLAG_PROTECT_AFFECTED','FLAG_MIRROR_MOVE_AFFECTED'],'Fake Out has no source contact flag');check(moves['SNATCH']['target']=='MOVE_TARGET_DEPENDS' and moves['SNATCH']['flags']==[],'Snatch source target/flags')
 prior_species={};prior_moves={}
 for row in read(ROOT/'scripts/rocket-hideout-catalog.recipe.json')['dependencies']:
  if row['path'].endswith('/catalog.json'):
   old=read(project/row['path']);prior_species.update({m['id']['value']:m for m in old['species']});prior_moves.update({m['id']['value']:m for m in old['moves']})
 check(len(prior_species)==98 and len(prior_moves)==222,'Exact predecessor metadata set')
 for move in pokemon['moves']:
  if move['id']['value'] in prior_moves:check(move==prior_moves[move['id']['value']],'Inherited move bytes/metadata changed')
 check(len(set(prior_moves)|{m['id']['value'] for m in pokemon['moves']})==230,'Composed move count');check(len(set(prior_species)|{m['id']['value'] for m in pokemon['species']})==99,'Composed species count');check(sup['weights'][:98]==read(project/'content/generated/celadon-gym-pokemon/supplement.json')['weights'],'All prior 98 weights preserve exact metadata');check(sup['weights'][-1]['weightHectograms']==800,'Kangaskhan weight');check(not scope['effectsEnabledByThisCatalog'] and not scope['encountersEnabledByThisCatalog'] and scope['naturalLevelLimit'] is None,'Catalog does not enable battles/encounters or truncate learnset')
 raw=target/'content/raw/pokefirered/037335f4';out=target/'content/generated/rocket-hideout-pokemon'
 for sprite in art['sprites']:
  im=Image.open(raw/sprite['sourceImagePath']);check(im.mode=='P' and im.size==(64,64),'Source indexed sprite')
  lines=(raw/sprite['sourcePalettePath']).read_text().splitlines();colors=[tuple(map(int,line.split())) for line in lines[3:]];check(len(colors)==16,'Source palette16')
  # Independent pixel oracle uses source palette integers, transparent index zero and FireRed RGB5 expansion.
  expected=bytes(v for index in im.tobytes() for v in (*[c//8*255//31 for c in colors[index]],0 if index==0 else 255))
  check(Image.open(out/sprite['texturePath']).convert('RGBA').tobytes()==expected,'Rendered PNG equals original indexed source')
 check(len(art['sprites'])==4 and len(sup['icons'])==1 and len(sup['footprints'])==1 and len(sup['cries'])==1,'Supplement cardinality');check(all(not c['audioBytesImported'] and not c['playbackVerified'] for c in sup['cries']),'No cry playback claim')
 portrait=read(target/'content/generated/rocket-hideout-art/manifest.json');check(len(portrait['assets'])==2,'Rocket/Giovanni portrait count')
 for asset in portrait['assets']:
  stem='rocket_grunt_m' if asset['trainerPicture']=='ROCKET_GRUNT_M' else 'leader_giovanni';rawart=target/'content/raw/pokefirered/037335f4';pic=Image.open(rawart/('graphics/trainers/front_pics/'+stem+'_front_pic.png'));colors=[tuple(map(int,line.split())) for line in (rawart/('graphics/trainers/palettes/'+stem+'.pal')).read_text().splitlines()[3:]]
  expected=bytes(v for index in pic.tobytes() for v in (*[((c//8)<<3)|((c//8)>>2) for c in colors[index]],0 if index==0 else 255));dest=target/'content/generated/rocket-hideout-art'
  check(pic.mode=='P' and pic.size==(64,64),'Portrait indexed source dimensions');check(Image.open(dest/(asset['id']+'.png')).convert('RGBA').tobytes()==expected,'Portrait PNG decode matches source')
 check(checks>350,'Nonempty focused catalog checks')
 return dict(passed=True,checks=checks,trainers=13,partyMembers=36,species=99,catalogMoves=230,sourceEmptySlots=3,sourceRegeneration=result,limitations=['No engine/native/audio evidence.','Taunt, Frustration and Snatch remain metadata; imported metadata does not admit battle execution.'])
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--target-root',type=pathlib.Path,default=ROOT);a=p.parse_args();print(json.dumps(main(a.project_root.resolve(),a.target_root.resolve()),indent=2))
