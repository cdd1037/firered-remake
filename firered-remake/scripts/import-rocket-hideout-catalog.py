#!/usr/bin/env python3
"""Offline source-authenticated Rocket Hideout species, moves, trainer teams and art.
The complete source learnset is metadata; no battle effect is enabled by importing it.
"""
import argparse,hashlib,importlib.util,json,pathlib,re,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-rocket-hideout-pokemon-v1'
NEW_SPECIES=('Kangaskhan',)
ART_SPECIES=(('Kangaskhan',115),)
TEACHING_MOVES=('MOVE_TAUNT','MOVE_FRUSTRATION','MOVE_SNATCH')
TRAINERS=tuple('TEAM_ROCKET_GRUNT_'+str(i) for i in range(7,19))+('BOSS_GIOVANNI',)
SCRIPT_PATHS=tuple('data/maps/'+m+'/scripts.inc' for m in ('CeladonCity_GameCorner','RocketHideout_B1F','RocketHideout_B2F','RocketHideout_B3F','RocketHideout_B4F'))
RECIPE_SHA256='623292620105422aac4d72bc7946bd2f92f70e3a8db5dac14de2c1a0e50d9f6f'
def source_symbol(name):return name.upper()
def sha(b):return hashlib.sha256(b).hexdigest()
def canonical(v):return (json.dumps(v,ensure_ascii=False,sort_keys=True,separators=(',',':'))+'\n').encode()
def safe(root,p=None):
 if p is not None and (not p or pathlib.PurePosixPath(p).is_absolute() or ':' in p or '\\' in p or any(x in ('','.','..') for x in p.split('/'))):raise ValueError('Unsafe relative path')
 q=pathlib.Path(root).absolute() if p is None else pathlib.Path(root).absolute()/p
 if '..' in q.parts or any(x.is_symlink() for x in (q,*q.parents)):raise ValueError('Unsafe root or symlink')
 return q
def load(path,name):
 spec=importlib.util.spec_from_file_location(name,path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module);return module

def trainer_convert(lock,sources):
 text={p:b.decode() for p,b in sources.items() if not p.endswith(('.png','.wav'))};entries={e['path']:e for e in lock['files']}
 def one(pattern,p):
  matches=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(matches)!=1:raise ValueError('Expected exactly one match: '+pattern)
  return matches[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text[p].count('\n',0,m.start())+1,endLine=text[p].count('\n',0,m.end())+1,sha256=entries[p]['sha256'])
 def number(p,s):return int(one(r'^#define '+s+r'\s+(\d+)\b[^\n]*',p)[1])
 def encode(s):
  return [int(one("^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$",'charmap.txt')[1],16) for c in s]
 def expr(s):
  out=0
  for symbol in s.split(' | '):
   if symbol=='F_TRAINER_FEMALE':out|=1<<int(one(r'^#define F_TRAINER_FEMALE\s+\(1 << (\d+)\)[^\n]*','include/constants/trainers.h')[1])
   elif symbol.startswith('AI_SCRIPT_'):
    m=one(r'^#define '+symbol+r'\s+\(1 << (\d+)\)[^\n]*','include/constants/battle_ai.h');out|=1<<int(m[1])
   else:out|=number('include/constants/trainers.h',symbol)
  return out
 battleType=1<<int(one(r'^#define BATTLE_TYPE_TRAINER\s+\(1 << (\d+)\)[^\n]*','include/constants/battle.h')[1])
 for macro,expansion in (
   ('single',r'trainerbattle TRAINER_BATTLE_SINGLE, \trainer, 0, \intro_text, \lose_text'),
   ('no_intro',r'trainerbattle TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT, \trainer, 0, \lose_text')):
  body=one(r'^\t\.macro trainerbattle_'+macro+r' [^\n]+\n(.*?)^\t\.endm','asm/macros/event.inc')[1]
  if '\t'+expansion+'\n' not in body:raise ValueError('Battle macro parameters differ')
 defaultFlags=int(one(r'^#define NO_ITEM_DEFAULT_MOVES\(party\)[^\n]*\.partyFlags = (\d+)[ \t]*$','include/battle.h')[1])
 customFlags=1<<int(one(r'^#define F_TRAINER_PARTY_CUSTOM_MOVESET\s+\(1 << (\d+)\)[^\n]*','include/constants/trainers.h')[1])
 trainers=[];battles=[];resolved=[]
 for short in TRAINERS:
  s='TRAINER_'+short;p='src/data/trainers.h';m=one(r'^    \['+s+r'\] = \{\n(.*?)^    \},',p)
  fields=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M));name=re.fullmatch(r'_\("([A-Z. ]+)"\)',fields['trainerName'])[1]
  shape,partySymbol=re.fullmatch(r'(NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',fields['party']).groups();custom=shape=='NO_ITEM_CUSTOM_MOVES'
  pp='src/data/trainer_parties.h';party=one(r'^static const struct TrainerMonNoItem'+('Custom' if custom else 'Default')+'Moves '+partySymbol+r'\[\] = \{\n(.*?)^\};',pp)
  rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{([^}]+)\},\n)?    \},\n',party[1]))
  if ''.join(r[0] for r in rows)!=party[1]:raise ValueError('Unsupported party')
  mons=[];natural=[]
  for row in rows:
   iv,lv,sp=int(row[1]),int(row[2]),row[3];np='src/data/text/species_names.h';nm=one(r'^    \['+sp+r'\] = _\("([^"]+)"\),$',np);speciesName=nm[1]
   pointer=one(r'^    \['+sp+r'\] = (\w+),$','src/data/pokemon/level_up_learnset_pointers.h')[1]
   learn=one(r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};','src/data/pokemon/level_up_learnsets.h');moves=[]
   for level,move in re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)',learn[1]):
    if int(level)<=lv and move not in moves:moves=(moves+[move])[-4:]
   slots=row[4].split(', ') if custom else []
   if custom and len(slots)!=4:raise ValueError('Expected four custom slots')
   natural.append(dict(species=sp,level=lv,moves=slots if custom else moves,learnsetSource=ref('src/data/pokemon/level_up_learnsets.h',pointer,learn)))
   mons.append(dict(species=dict(value='pret:'+sp),sourceSpeciesId=number('include/constants/species.h',sp),speciesName=speciesName,encodedSpeciesName=encode(speciesName),level=lv,sourceIv=iv,heldItem='ITEM_NONE',customMoves=[dict(value='pret:'+x) for x in slots],source=ref(pp,partySymbol,party),speciesNameSource=ref(np,sp,nm)))
  if fields['doubleBattle']!='FALSE':raise ValueError('Unexpected double')
  items=[x for x in fields['items'].strip('{}').split(', ') if x]
  if items!=[]:raise ValueError('Unexpected trainer items')
  items+=['ITEM_NONE']*(4-len(items))
  money=one(r'^\s*\{'+fields['trainerClass']+r', (\d+)\},$','src/battle_main.c')
  trainers.append(dict(id=dict(value='pret:'+s),sourceTrainerId=number('include/constants/opponents.h',s),trainerClass=fields['trainerClass'],sourceTrainerClass=number('include/constants/trainers.h',fields['trainerClass']),trainerPic=fields['trainerPic'],sourceTrainerPic=number('include/constants/trainers.h',fields['trainerPic']),sourceTrainerName=name,encodedTrainerName=encode(name),encounterMusicGender=expr(fields['encounterMusic_gender']),items=items,doubleBattle=False,partyFlags=customFlags if custom else defaultFlags,aiFlags=expr(fields['aiFlags']),aiFlagSymbols=fields['aiFlags'].split(' | '),baseMoney=int(money[1]),party=mons,source=ref(p,s,m),moneySource=ref('src/battle_main.c',fields['trainerClass'],money)))
  candidates=[]
  for p in SCRIPT_PATHS:
   for label in re.finditer(r'^(\w+)::\n((?:(?!^\w+::).)*)',text[p],re.M|re.S):
    battle=re.search(r'^\ttrainerbattle_(single|no_intro) '+s+r', ([^\n]+)$',label[2],re.M)
    if battle:candidates.append((p,label,battle))
  if len(candidates)!=1:raise ValueError('Expected one initial trainer battle: '+s)
  p,label,battle=candidates[0];args=battle[2].split(', ');noIntro=battle[1]=='no_intro'
  if len(args) not in ((1,) if noIntro else (2,3)):raise ValueError('Unsupported battle arguments')
  defeat=args[0] if noIntro else args[1];intro='' if noIntro else args[0]
  post=re.search(r'^\tmsgbox (\w+)',label[2][battle.end():],re.M)
  postText=post[1] if post else ''
  if short.startswith('RIVAL_SS_ANNE_'):
   context=one(r'^SSAnne_2F_Corridor_EventScript_RivalTrigger::\n((?:(?!^\w+::).)*)',p)[1]
   speech=re.findall(r'^\tmsgbox (\w+)$',context,re.M)
   if speech!=['SSAnne_2F_Corridor_Text_RivalIntro','SSAnne_2F_Corridor_Text_RivalPostBattle']:raise ValueError('Unexpected rival context')
   intro,postText=speech
  battles.append(dict(trainer=dict(value='pret:'+s),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if noIntro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=battleType,scriptEntry=label[1],introText=intro,defeatText=defeat,victoryText='',postBattleText=postText,entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(p,label[1],label),postBattleScript=args[2] if len(args)>2 else None,musicOption=args[3] if len(args)>3 else None))
  resolved.append(dict(trainer=s,party=natural))
 moves=[];moveNames=[]
 for symbol in sorted({m for r in resolved for p in r['party'] for m in p['moves']} - {'MOVE_NONE'}):
  p='src/data/battle_moves.h';m=one(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',p)
  f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
  moves.append(dict(id=dict(value='pret:'+symbol),sourceMoveId=number('include/constants/moves.h',symbol),pp=int(f['pp']),power=int(f['power']),accuracy=int(f['accuracy']),type=f['type'],effect=f['effect'],target=f['target'],secondaryEffectChance=int(f['secondaryEffectChance']),priority=int(f['priority']),battleEffectImplemented=False,flags=f['flags'].split(' | ') if f['flags']!='0' else [],source=ref(p,symbol,m)))
  np='src/data/text/move_names.h';nm=one(r'^    \['+symbol+r'\]\s*= _\("([^"]+)"\),$',np)
  moveNames.append(dict(id=dict(value='pret:'+symbol),name=nm[1],source=ref(np,symbol,nm)))
 return dict(schemaVersion=1,profile='firered-rocket-hideout-trainers-v1',sourceCommit=COMMIT,completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved,moves=moves,moveNames=moveNames,limitations=['The thirteen Rocket Hideout teams with 36 original party members; no field or battle activation.','Rematches, double battles and the complete game trainer roster are not enabled.','Source declarations and party construction do not implement field callbacks, Rocket Hideout progression, trainer portraits or audio.'])

def metadata(lock, sources):
    text = {p: b.decode('utf-8') for p, b in sources.items() if not p.endswith(('.png', '.wav'))}
    entries = {e['path']: e for e in lock['files']}
    def one(pattern, value):
        matches = list(re.finditer(pattern, value, re.M | re.S))
        if len(matches) != 1: raise ValueError('Expected exactly one source match: ' + pattern)
        return matches[0]
    def ref(path, symbol, match):
        return dict(path=path, symbol=symbol, startLine=text[path].count('\n', 0, match.start()) + 1,
            endLine=text[path].count('\n', 0, match.end()) + 1, sha256=entries[path]['sha256'])
    def block(path, symbol):
        match = one(r'^    \[' + symbol + r'\] =\s*\n    \{\n(.*?)^    \},', text[path])
        fields = {}
        for line in match[1].splitlines():
            field = re.fullmatch(r'\s*\.(\w+) = (.*),', line)
            if not field or field[1] in fields: raise ValueError('Unexpected source field: ' + line)
            fields[field[1]] = field[2]
        return fields, ref(path, symbol, match)
    def ident(symbol): return dict(value='pret:' + symbol)
    def constant(path, symbol): return one(r'^#define ' + symbol + r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$', text[path])
    def number(path, symbol): return int(constant(path, symbol)[1])
    def name(path, symbol):
        m = one(r'^    \[' + symbol + r'\]\s*= _\("([A-Z ♀♂-]+)"\),$', text[path])
        return dict(id=ident(symbol), name=m[1], source=ref(path, symbol, m))
    species, names, deferred, move_symbols = [], [], [], set()
    stat_fields = ['HP', 'Attack', 'Defense', 'Speed', 'SpAttack', 'SpDefense']
    stat_keys = ['hp', 'attack', 'defense', 'speed', 'specialAttack', 'specialDefense']
    for name_ in NEW_SPECIES:
        symbol = 'SPECIES_' + source_symbol(name_)
        fields, source = block('src/data/pokemon/species_info.h', symbol)
        if fields['genderRatio'] not in ('PERCENT_FEMALE(25)', 'PERCENT_FEMALE(50)', 'PERCENT_FEMALE(75)', 'MON_MALE', 'MON_FEMALE', 'MON_GENDERLESS') or fields['growthRate'] not in ('GROWTH_MEDIUM_FAST', 'GROWTH_MEDIUM_SLOW', 'GROWTH_FAST', 'GROWTH_SLOW'):
            raise ValueError('Unsupported RockTunnel species profile')
        types = one(r'^\{(TYPE_\w+), (TYPE_\w+)\}$', fields['types'])
        abilities = one(r'^\{(ABILITY_\w+), (ABILITY_\w+)\}$', fields['abilities'])
        learnpath = 'src/data/pokemon/level_up_learnsets.h'
        pointer = one(r'^    \[' + symbol + r'\] = (\w+),$', text['src/data/pokemon/level_up_learnset_pointers.h'])[1]
        learn = one(r'^static const u16 ' + pointer + r'\[\] = \{\n(.*?)^\};', text[learnpath])
        moves = []
        for m in learn_rows(text[learnpath], learn):
            moves.append(dict(level=int(m[1]), move=ident(m[2]), source=ref(learnpath, pointer, m)))
            move_symbols.add(m[2])
        evolution_path = 'src/data/pokemon/evolution.h'
        rows = list(re.finditer(r'^    \[' + symbol + r'\]\s*= (\{\{.*?\}\}),$', text[evolution_path], re.M | re.S))
        evolutions = []
        if len(rows) > 1: raise ValueError('Duplicate evolution row')
        m = rows[0] if rows else None
        declarations = re.findall(r'\{(EVO_\w+),\s+([A-Z_0-9]+), (SPECIES_\w+)\}', m[1]) if m else []
        if m and (not declarations or re.sub(r'\s+', '', m[1]) != '{' + ','.join('{' + ','.join(d) + '}' for d in declarations) + '}'):
            raise ValueError('Unsupported or incomplete evolution declarations')
        expected = [('EVO_ITEM', 'ITEM_LEAF_STONE', 'SPECIES_EXEGGUTOR')] if symbol == 'SPECIES_EXEGGCUTE' else []
        if declarations != expected: raise ValueError('Celadon Gym source evolution differs')
        for method, parameter, target in declarations:
            if method == 'EVO_LEVEL' and target in {'SPECIES_' + source_symbol(n) for n in NEW_SPECIES}:
                evolutions.append(dict(level=int(parameter), target=ident(target), source=ref(evolution_path, symbol, m)))
            else:
                deferred.append(dict(species=ident(symbol), method=method, parameter=parameter, target=ident(target),
                    source=ref(evolution_path, symbol, m), executable=False,
                    reason='Evolution target is not imported in this profile; retained source metadata, not a battle level cap.'))
        species.append(dict(id=ident(symbol), sourceSpeciesId=number('include/constants/species.h', symbol),
            baseStats={k: int(fields['base' + s]) for k, s in zip(stat_keys, stat_fields)},
            type1=types[1], type2=types[2], catchRate=int(fields['catchRate']), experienceYield=int(fields['expYield']),
            evYield={k: int(fields['evYield_' + s]) for k, s in zip(stat_keys, stat_fields)},
            growthRate={'GROWTH_MEDIUM_FAST': 'mediumFast', 'GROWTH_MEDIUM_SLOW': 'mediumSlow', 'GROWTH_FAST': 'fast', 'GROWTH_SLOW': 'slow'}[fields['growthRate']],
            genderRatio={'PERCENT_FEMALE(25)': 63, 'PERCENT_FEMALE(50)': 127, 'PERCENT_FEMALE(75)': 191, 'MON_MALE': 0, 'MON_FEMALE': 254, 'MON_GENDERLESS': 255}[fields['genderRatio']],
            ability1=abilities[1], ability2=abilities[2], friendship=int(fields['friendship']),
            levelUpMoves=moves, evolutions=evolutions, source=source, learnsetSource=ref(learnpath, pointer, learn)))
        names.append(name('src/data/text/species_names.h', symbol))
    # Source trainer parties require definitions independently of these six complete learnsets.
    move_symbols.update(TEACHING_MOVES)
    move_symbols.update(m['id']['value'][5:] for m in trainer_convert(lock, sources)['moves'])
    moves, move_names = [], []
    for symbol in sorted(move_symbols):
        f, source = block('src/data/battle_moves.h', symbol)
        moves.append(dict(id=ident(symbol), sourceMoveId=number('include/constants/moves.h', symbol),
            pp=int(f['pp']), power=int(f['power']), type=f['type'], accuracy=int(f['accuracy']), effect=f['effect'],
            secondaryEffectChance=int(f['secondaryEffectChance']), target=f['target'], priority=int(f['priority']),
            flags=[] if f['flags'] == '0' else f['flags'].split(' | '), battleEffectImplemented=False, source=source))
        move_names.append(name('src/data/text/move_names.h', symbol))
    constants = []
    abilities = sorted({s[k] for s in species for k in ('ability1', 'ability2')} - {'ABILITY_NONE'})
    for path, symbol in [('include/constants/abilities.h', s) for s in abilities]:
        m = constant(path, symbol)
        constants.append(dict(symbol=symbol, value=int(m[1]), source=ref(path, symbol, m)))
    encoding_path = 'charmap.txt'
    english = one(r'\A.*\Z', text[encoding_path])
    encoding_source = ref(encoding_path, 'English name encoding, gender glyphs, spaces and EOS', english)
    encoded_names = []
    for row in names + move_names:
        values = []
        for char in row['name'] + '$':
            m = one(r"^'" + re.escape(char) + r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$", text[encoding_path])
            values.append(int(m[1], 16))
        encoded_names.append(dict(symbol=row['source']['symbol'], name=row['name'], bytes=values,
            source=row['source'], encodingSource=encoding_source))
    overrides = []
    return dict(schemaVersion=1, profile=PROFILE, sourceCommit=COMMIT, inputs=lock['files'], species=species,
        moves=moves, speciesNames=names, moveNames=move_names, constants=constants, encodedNames=encoded_names,
        deferredEvolutions=deferred, evolutionOverrides=overrides, naturalLevelLimit=None,
        itemEvolutionExecutorImplemented=False, levelEvolutionExecutorImplemented=False, tradeEvolutionExecutorImplemented=False,
        limitations=['One new Rocket Hideout species over the unchanged 98-species catalog; 99 species total.',
          'Full source learnsets, names, stats and evolutions are metadata; missing battle effects remain unavailable.',
          'Kangaskhan has no source evolution. Taunt, Frustration and Snatch are metadata only here.',
          'No natural level cap, move backfill, cry playback or audible-output claim.'])


def rebuild(project,target,check=False):
 global rock,learn_rows
 recipe_bytes=safe(ROOT,'scripts/rocket-hideout-catalog.recipe.json').read_bytes()
 if sha(recipe_bytes)!=RECIPE_SHA256:raise ValueError('Reviewed recipe hash differs')
 recipe=json.loads(recipe_bytes)
 for row in recipe['dependencies']:
  if sha(safe(project,row['path']).read_bytes())!=row['sha256']:raise ValueError('Predecessor or codec changed: '+row['path'])
 rock=load(safe(project,'scripts/import-rocktunnel-pokemon.py'),'rocket_existing_pokemon_codecs')
 rock.ROOT=project;rock.PROFILE=rock.base.PROFILE=PROFILE
 rock.NEW_SPECIES=rock.base.NEW_SPECIES=NEW_SPECIES;rock.ART_SPECIES=rock.base.ART_SPECIES=ART_SPECIES
 prior=json.loads(safe(project,'content/generated/celadon-gym-pokemon/supplement.json').read_bytes())
 rock.base.LEGACY_SPECIES=tuple(w['speciesId'][13:] for w in prior['weights'])
 if len(rock.base.LEGACY_SPECIES)!=98:raise ValueError('Exact 98 prior species required')
 learn_rows=rock.learn_rows
 results=[]
 for stem in ('rocket-hideout-pokemon','rocket-hideout-trainers'):
  lock_bytes=safe(ROOT,'scripts/'+stem+'.lock.json').read_bytes()
  if sha(lock_bytes)!=recipe['locks'][stem]:raise ValueError('Reviewed source lock hash differs')
  lock=json.loads(lock_bytes)
  if (lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'],lock['profile'],lock['gameEdition'],lock['romRevision'])!=(1,'pret/pokefirered',COMMIT,TREE,'firered-'+stem+'-v1','FIRERED',0):raise ValueError('Source identity differs')
  sources={}
  for e in lock['files']:
   b=safe(target,'content/raw/'+stem+'/'+e['path']).read_bytes()
   if len(b)!=e['byteLength'] or sha(b)!=e['sha256'] or hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()!=e['gitBlobSha1']:raise ValueError('Pinned raw bytes differ: '+e['path'])
   if e['path'] in sources:raise ValueError('Repeated input')
   sources[e['path']]=b
  trainers=trainer_convert(lock,sources)
  if len(trainers['trainers'])!=13 or sum(len(t['party']) for t in trainers['trainers'])!=36 or len(trainers['moves'])!=38:raise ValueError('Exact source trainer closure differs')
  if stem.endswith('pokemon'):
   doc=metadata(lock,sources);outputs=rock.base.art(lock,sources)
   art=json.loads(outputs['battle-art.json']);art['limitations'][0]='Kangaskhan original front/back frame zero, normal and shiny palettes.';outputs['battle-art.json']=canonical(art)
   outputs['catalog.json']=canonical(doc);outputs.update(rock.supplement(lock,sources))
   declarations=[];text=sources['src/data/pokemon/species_info.h'].decode();m=re.search(r'^    \[SPECIES_KANGASKHAN\] =\s*\n    \{\n(.*?)^    \},',text,re.M|re.S)
   declarations.append(dict(species=dict(value='pret:SPECIES_KANGASKHAN'),fields=dict(re.findall(r'^\s*\.(\w+) = (.*),$',m[1],re.M)),declaration=m[0],source=rock.battle.reference(lock,sources,'src/data/pokemon/species_info.h','SPECIES_KANGASKHAN',m[0])))
   outputs['source-species.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,species=declarations))
   old_moves={}
   for row in recipe['dependencies']:
    if row['path'].endswith('/catalog.json'):
     for move in json.loads(safe(project,row['path']).read_bytes()).get('moves',[]):
      if move['id']['value'] in old_moves and old_moves[move['id']['value']]!=move:raise ValueError('Conflicting prior move definition')
      old_moves[move['id']['value']]=move
   for move in doc['moves']:
    if move['id']['value'] in old_moves and old_moves[move['id']['value']]!=move:raise ValueError('Prior move changed in overlay')
   new_moves=[move for move in doc['moves'] if move['id']['value'] not in old_moves]
   outputs['scope.json']=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,naturalLevelLimit=None,trainers=trainers['resolvedMoves'],requiredTrainerMoves=[m['id']['value'][5:] for m in trainers['moves']],teachingMoves=list(TEACHING_MOVES),fullLearnsetMoves=sorted({m['move']['value'][5:] for s in doc['species'] for m in s['levelUpMoves']}),sourceDefinitions=doc['moves'],newMoveDefinitions=new_moves,speciesAbilities=['ABILITY_EARLY_BIRD'],newAbilities=['ABILITY_EARLY_BIRD'],effectsEnabledByThisCatalog=False,encountersEnabledByThisCatalog=False,noEvolutionMoveBackfill=True,limitations=['Fake Out and Mega Punch are the new enemy roster admissions; battle executors are separate.','Comet Punch, Dizzy Punch and Reversal are complete natural learnset metadata.','Taunt, Frustration and Snatch are metadata only until independently admitted.','No source empty MOVE_NONE slot is defined as a battle move.']))
  else:outputs={'trainers.json':canonical(trainers)}
  full=rock.battle.source_set_hash(lock['files']);profile='firered-'+stem+'-v1'
  manifest=canonical(dict(schemaVersion=1,profile=profile,sourceCommit=COMMIT,tree=TREE,lockSha256=sha(lock_bytes),fullSourceHash=full,inputCount=len(sources),upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False,outputs=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(outputs.items())]))
  outputs['manifest.json']=manifest
  raw_manifest=canonical(dict(schemaVersion=1,profile=profile,sourceCommit=COMMIT,tree=TREE,lockSha256=sha(lock_bytes),fullSourceHash=full,inputCount=len(sources)))
  materials={'content/generated/'+stem+'/'+p:b for p,b in outputs.items()};materials['content/raw/'+stem+'/manifest.json']=raw_manifest
  if 'manifests' in recipe and recipe['manifests'][stem]!=sha(manifest):raise ValueError('Reviewed generated output differs')
  for p,b in materials.items():
   dest=safe(target,p)
   if check:
    if dest.read_bytes()!=b:raise ValueError('Generated bytes differ: '+p)
   else:
    dest.parent.mkdir(parents=True,exist_ok=True);stage=safe(target,p+'.tmp');stage.write_bytes(b);stage.replace(dest)
  results.append(dict(stem=stem,inputCount=len(sources),outputCount=len(outputs)-1,manifestSha256=sha(manifest),rawManifestSha256=sha(raw_manifest),catalogSha256=sha(outputs['catalog.json'] if stem.endswith('pokemon') else outputs['trainers.json']),fullSourceHash=full))
 return results
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--target-root',type=pathlib.Path,default=ROOT);p.add_argument('--check',action='store_true');a=p.parse_args()
 print(json.dumps(rebuild(safe(a.project_root),safe(a.target_root),a.check),sort_keys=True))
