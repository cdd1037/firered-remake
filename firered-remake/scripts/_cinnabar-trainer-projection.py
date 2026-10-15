"""Test fixture conversion from source preparation; does not execute upstream code."""
import hashlib,json,re,sys
from pathlib import Path
base,source,out=map(Path,sys.argv[1:])
decl=json.loads((source/'content/generated/cinnabar-source-preparation/declarations.json').read_text())
text={}; hashes={}
def read(p):
 if p not in text:
  choices=[source/'content/raw/pokefirered/037335f4'/p,base/'content/raw/pokefirered/037335f4'/p]
  f=next((x for x in choices if x.exists()),None)
  if f is None: raise ValueError('missing source '+p)
  data=f.read_bytes();text[p]=data.decode();hashes[p]=hashlib.sha256(data).hexdigest()
 return text[p]
def one(p,pattern):
 matches=list(re.finditer(pattern,read(p),re.M|re.S))
 if len(matches)!=1:raise ValueError((p,pattern,len(matches)))
 return matches[0]
def ref(p,s,m):return dict(path=p,symbol=s,startLine=read(p).count('\n',0,m.start())+1,endLine=read(p).count('\n',0,m.end())+1,sha256=hashes[p])
def num(p,s):return int(one(p,r'^#define '+s+r'\s+(\d+)\b[^\n]*')[1])
def encode(s):return [int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in s]
def expr(s):
 n=0
 for v in s.split(' | '):
  p='include/constants/battle_ai.h' if v.startswith('AI_SCRIPT_') else 'include/constants/trainers.h'
  if v.startswith('AI_SCRIPT_') or v=='F_TRAINER_FEMALE':n|=1<<int(one(p,r'^#define '+v+r'\s+\(1 << (\d+)\)[^\n]*')[1])
  else:n|=num(p,v)
 return n
def identity(s):return dict(value='pret:'+s)
trainers=[];battles=[];resolved=[]
for d in decl['trainers']:
 symbol=d['trainer'];p='src/data/trainers.h';m=one(p,r'^    \['+symbol+r'\] = \{\n(.*?)^    \},');f=d['fields'];name=re.fullmatch(r'_\("([A-Z.& ]+)"\)',f['trainerName'])[1]
 shape,partySymbol=re.fullmatch(r'(NO_ITEM_(?:DEFAULT|CUSTOM)_MOVES)\((\w+)\)',f['party']).groups();custom=shape=='NO_ITEM_CUSTOM_MOVES'
 pp='src/data/trainer_parties.h';party=one(pp,r'^static const struct TrainerMonNoItem'+('Custom' if custom else 'Default')+'Moves '+partySymbol+r'\[\] = \{\n(.*?)^\};')
 rows=list(re.finditer(r'    \{\n        \.iv = (\d+),\n        \.lvl = (\d+),\n        \.species = (SPECIES_\w+),\n(?:        \.moves = \{([^}]+)\},\n)?    \},\n',party[1]));assert ''.join(r[0] for r in rows)==party[1]
 mons=[]
 for row in rows:
  iv,lv,sp=int(row[1]),int(row[2]),row[3];np='src/data/text/species_names.h';nm=one(np,r'^    \['+sp+r'\] = _\("([^"]+)"\),$');sn=nm[1]
  mons.append(dict(species=identity(sp),sourceSpeciesId=num('include/constants/species.h',sp),speciesName=sn,encodedSpeciesName=encode(sn),level=lv,sourceIv=iv,heldItem='ITEM_NONE',customMoves=[identity(x) for x in row[4].split(', ')] if custom else [],source=ref(pp,partySymbol,party),speciesNameSource=ref(np,sp,nm)))
 items=[x for x in f['items'].strip('{}').split(', ') if x];items+=['ITEM_NONE']*(4-len(items));money=one('src/battle_main.c',r'^\s*\{'+f['trainerClass']+r', (\d+)\},$')
 trainers.append(dict(id=identity(symbol),sourceTrainerId=num('include/constants/opponents.h',symbol),trainerClass=f['trainerClass'],sourceTrainerClass=num('include/constants/trainers.h',f['trainerClass']),trainerPic=f['trainerPic'],sourceTrainerPic=num('include/constants/trainers.h',f['trainerPic']),sourceTrainerName=name,encodedTrainerName=encode(name),encounterMusicGender=expr(f['encounterMusic_gender']),items=items,doubleBattle=f['doubleBattle']=='TRUE',partyFlags=1 if custom else 0,aiFlags=expr(f['aiFlags']),aiFlagSymbols=f['aiFlags'].split(' | '),baseMoney=int(money[1]),party=mons,source=ref(p,symbol,m),moneySource=ref('src/battle_main.c',f['trainerClass'],money)))
 resolved.append(dict(trainer=symbol,party=[dict(species=x['species'],level=x['level'],moves=x['resolvedMoves']) for x in d['party']]))
for d in decl['battleOccurrences']:
 if d['rematch']:continue
 p=d['sourceRef']['path'];label=one(p,r'^'+d['script']+r'::\n((?:(?!^\w+::).)*)');args=d['arguments'][1:];noIntro=d['opcode']=='trainerbattle_no_intro';double=d['opcode']=='trainerbattle_double'
 mode='TRAINER_BATTLE_DOUBLE' if double else 'TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if noIntro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'
 post=re.search(r'^\tmsgbox (\w+)',label[1],re.M)
 row=dict(trainer=identity(d['trainer']),mode=num('include/constants/battle_setup.h',mode),rivalFlags=0,battleTypeFlags=9 if double else 8,scriptEntry=d['script'],introText='' if noIntro else args[0],defeatText=args[0] if noIntro else args[1],victoryText='',postBattleText=post[1] if post else '',entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoDoubleTrainerBattle' if double else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(p,d['script'],label),postBattleScript=args[2] if not double and len(args)>2 else None,musicOption=args[3] if not double and len(args)>3 else None)
 if double:row['notEnoughMonsText']=args[2]
 battles.append(row)
assert len(trainers)==24 and sum(len(t['party']) for t in trainers)==59 and len(battles)==31
out.mkdir(parents=True,exist_ok=True)
(out/'trainers.json').write_text(json.dumps(dict(sourceCommit=decl['sourceCommit'],profile='firered-cinnabar-trainers-v1',completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved),indent=2)+'\n')
print('Converted24 trainers/59 members/31 battle entries to',out/'trainers.json')
