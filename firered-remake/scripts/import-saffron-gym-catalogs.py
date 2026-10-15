#!/usr/bin/env python3
"""Regenerate Saffron Gym source metadata from immutable pret Git blobs, without upstream execution."""
from pathlib import Path
import argparse, base64, importlib.util, collections,  json, os, re, struct, subprocess, sys
sys.dont_write_bytecode=True
import machine_catalog as shared_machines

COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-saffron-gym-catalogs-v1'
CORE=['SaffronCity_Gym']
SCRIPT_PATHS=tuple('data/maps/'+n+'/scripts.inc' for n in CORE)
TRAINERS=()
def canonical(doc): return (json.dumps(doc, sort_keys=True, indent=2)+'\n').encode()
def directory(name): return re.sub(r'route(\d+)', r'route-\1', re.sub(r'([a-z])([A-Z])',r'\1-\2',name).replace('_','-').lower())
def git(repo, *args):
    env={k:v for k,v in os.environ.items() if not k.startswith('GIT_')}
    env.update(GIT_CONFIG_NOSYSTEM='1',GIT_CONFIG_GLOBAL=os.devnull,GIT_NO_REPLACE_OBJECTS='1',GIT_OPTIONAL_LOCKS='0')
    return subprocess.check_output(['git','--no-replace-objects','--no-optional-locks','-C',str(repo),*args],env=env)

class LazyBlobs(dict):
    def __init__(self, owner): super().__init__(); self.owner=owner
    def __missing__(self, path): return self.owner.read(path)

class Source:
    def __init__(self, reference):
        self.reference=reference
        if git(reference, 'rev-parse', 'HEAD').decode().strip() != COMMIT: raise ValueError('Source HEAD/tree differs')
        self.tree={}
        for row in git(reference,'ls-tree','-rz',COMMIT).split(b'\0'):
            if row:
                fields,path=row.split(b'\t'); self.tree[path.decode()]=tuple(fields.decode().split())
        self.data=LazyBlobs(self);self.entries={};self.roles=collections.defaultdict(set)
    def read(self,path,role='source-dependency'):
        if path not in self.tree or self.tree[path][:2]!=('100644','blob'): raise ValueError('Not a regular source blob: '+path)
        self.roles[path].add(role)
        if path not in self.data:
            b=git(self.reference,'cat-file','blob',self.tree[path][2])
            self.data[path]=b;self.entries[path]=dict(path=path)
        return self.data[path]
    def text(self,path):return self.read(path).decode()
    def json(self,path):return json.loads(self.read(path))
    def units(self,path,code):
        b=self.read(path);size=struct.calcsize(code)
        if len(b)%size:raise ValueError('Misaligned source binary')
        return list(struct.unpack('<'+str(len(b)//size)+code,b))
    def ref(self,path,symbol,line=None):
        self.read(path)
        v=dict(kind='pret',repository='pret/pokefirered',commit=COMMIT,path=path,symbol=symbol)
        if line is not None:v['line']=line
        return v
    def lock(self):
        return dict(schemaVersion=1,repository='pret/pokefirered',commit=COMMIT,profile=PROFILE,files=[dict(**self.entries[p],roles=sorted(self.roles[p])) for p in sorted(self.entries)])

def trainer_convert(lock,sources):
 text={p:b.decode() for p,b in sources.items() if not p.endswith(('.png','.wav'))};entries={e['path']:e for e in lock['files']}
 def one(pattern,p):
  matches=list(re.finditer(pattern,text[p],re.M|re.S))
  if len(matches)!=1:raise ValueError('Expected exactly one match: '+pattern)
  return matches[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text[p].count('\n',0,m.start())+1,endLine=text[p].count('\n',0,m.end())+1)
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
  if items!=(['ITEM_HYPER_POTION','ITEM_HYPER_POTION','ITEM_FULL_HEAL'] if short=='LEADER_SABRINA' else []):raise ValueError('Unexpected trainer items')
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
  if len(args) not in ((1,) if noIntro else (2,3,4)):raise ValueError('Unsupported battle arguments')
  defeat=args[0] if noIntro else args[1];intro='' if noIntro else args[0]
  post=re.search(r'^\tmsgbox (\w+)',label[2][battle.end():],re.M)
  postText=post[1] if post else ''
  battles.append(dict(trainer=dict(value='pret:'+s),mode=number('include/constants/battle_setup.h','TRAINER_BATTLE_SINGLE_NO_INTRO_TEXT' if noIntro else 'TRAINER_BATTLE_CONTINUE_SCRIPT_NO_MUSIC' if len(args)>3 and args[3]=='NO_MUSIC' else 'TRAINER_BATTLE_CONTINUE_SCRIPT' if len(args)>2 else 'TRAINER_BATTLE_SINGLE'),rivalFlags=0,battleTypeFlags=battleType,scriptEntry=label[1],introText=intro,defeatText=defeat,victoryText='',postBattleText=postText,entryScript='EventScript_DoNoIntroTrainerBattle' if noIntro else 'EventScript_TryDoNormalTrainerBattle',callback='CB2_EndTrainerBattle',source=ref(p,label[1],label),postBattleScript=args[2] if len(args)>2 else None,musicOption=args[3] if len(args)>3 else None))
  resolved.append(dict(trainer=s,party=natural))
 moves=[];moveNames=[]
 for symbol in sorted({m for r in resolved for p in r['party'] for m in p['moves']} - {'MOVE_NONE'}):
  p='src/data/battle_moves.h';m=one(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',p)
  f=dict(re.findall(r'^\s*\.(\w+) = (.+),$',m[1],re.M))
  moves.append(dict(id=dict(value='pret:'+symbol),sourceMoveId=number('include/constants/moves.h',symbol),pp=int(f['pp']),power=int(f['power']),accuracy=int(f['accuracy']),type=f['type'],effect=f['effect'],target=f['target'],secondaryEffectChance=int(f['secondaryEffectChance']),priority=int(f['priority']),battleEffectImplemented=False,flags=f['flags'].split(' | ') if f['flags']!='0' else [],source=ref(p,symbol,m)))
  np='src/data/text/move_names.h';nm=one(r'^    \['+symbol+r'\]\s*= _\("([^"]+)"\),$',np)
  moveNames.append(dict(id=dict(value='pret:'+symbol),name=nm[1],source=ref(np,symbol,nm)))
 return dict(schemaVersion=1,profile='firered-saffron-gym-trainers-v1',sourceCommit=COMMIT,completeSourceRoster=False,trainers=trainers,battles=battles,resolvedMoves=resolved,moves=moves,moveNames=moveNames,limitations=['Exactly8 Saffron Gym trainer teams with20 source members; metadata grants no battle activation.','Rematches, double battles and the complete game trainer roster are not enabled.','Source declarations and party construction do not implement field callbacks, Saffron Gym progression, trainer portraits or audio.'])

META_PATHS=['charmap.txt','include/battle.h','include/constants/battle.h','include/constants/battle_ai.h','include/constants/battle_setup.h','include/constants/trainers.h','include/constants/opponents.h','include/constants/species.h','include/constants/moves.h','include/constants/items.h','src/data/trainers.h','src/data/trainer_parties.h','src/battle_main.c','src/data/pokemon/species_info.h','src/data/pokemon/evolution.h','src/data/pokemon/level_up_learnsets.h','src/data/pokemon/level_up_learnset_pointers.h','src/data/pokemon/tmhm_learnsets.h','src/data/battle_moves.h','src/data/text/species_names.h','src/data/text/move_names.h','src/data/items.json','src/data/party_menu.h','src/move_descriptions.c','asm/macros/event.inc','data/scripts/item_ball_scripts.inc']
MACHINES=('TM39','TM05','TM09','TM46','TM03','TM45','TM43','TM28','TM31','TM44','HM01','TM34','HM05','TM40','TM19','TM12','TM21','TM49','TM06','HM03','HM04','TM11','TM47','TM32','TM01','TM08','TM41')
ITEMS=('TM04','MAX_REPEL')

def need(ok,message):
 if not ok:raise ValueError(message)
def safe(root,rel):
 need(not Path(rel).is_absolute() and '\\' not in rel and not any(p in ('','.','..') for p in rel.split('/')),'Unsafe path')
 p=Path(root).absolute()/rel
 need(not any(t.is_symlink() for t in (p,*p.parents)),'Symlink denied')
 return p

def tm04_icon(src,dependency):
 p='src/data/item_icon_table.h';text=src.text(p);matches=list(re.finditer(r'^    \[ITEM_TM04\][ \t]*= \{(\w+), (\w+)\},',text,re.M));need(len(matches)==1,'TM04 icon binding differs');binding=matches[0];symbols=binding.groups()
 bindingref=dict(path=p,symbol='ITEM_TM04',startLine=text.count('\n',0,binding.start())+1,endLine=text.count('\n',0,binding.end())+1)
 graphics=src.text('src/data/graphics/items.h');paths=[]
 for symbol in symbols:
  matches=list(re.finditer(r'^const u32 '+symbol+r'\[\][ \t]*= INCBIN_U32\("([^"\n]+)"\);',graphics,re.M));need(len(matches)==1,'TM04 graphic symbol differs');paths.append(matches[0][1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
 image,palette=paths;ib=src.read(image);pb=src.read(palette)
 codecpath=safe(dependency,'scripts/import-lab-battle-ui.py')
 spec=importlib.util.spec_from_file_location('_saffron_gym_items',codecpath);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 w,h,indices,_=codec.indexed_png(ib);rgba=codec.rgba(indices,codec.palette(pb));png=codec.png(w,h,rgba);need((w,h)==(24,24),'TM04 icon size differs')
 def reference(path,symbol):return dict(path=path,symbol=symbol,startLine=1,endLine=max(1,src.data[path].count(b'\n')))
 return dict(item=dict(value='pret:ITEM_TM04'),width=w,height=h,pngPath='item_tm04.png',source=reference(image,symbols[0]),paletteSource=reference(palette,symbols[1]),bindingSource=bindingref),png

def metadata(src,dependency):
 text={p:src.text(p) for p in META_PATHS};ident=lambda s:dict(value='pret:'+s)
 def one(p,pattern):
  ms=list(re.finditer(pattern,text[p],re.M|re.S));need(len(ms)==1,'Expected one source match: '+p+' '+pattern);return ms[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text[p].count('\n',0,m.start())+1,endLine=text[p].count('\n',0,m.end())+1)
 def number(p,s):return int(one(p,r'^#define '+s+r'[ \t]+(\d+)\b[^\n]*')[1])
 proofbytes=safe(dependency,'content/generated/canonical-pokemon/catalog.json').read_bytes()
 proof=json.loads(proofbytes)
 selected=[next(s for s in proof['species'] if s['sourceSpeciesId']==i) for i in (80,)]
 names=[];encoded=[];machines=[];held=[];evolutions=[]
 for s in selected:
  symbol=s['id']['value'][5:];p='src/data/text/species_names.h';m=one(p,r'^    \['+symbol+r'\]\s*= _\("([^"\n]+)"\),$');name=m[1];r=ref(p,symbol,m)
  names.append(dict(id=s['id'],name=name,source=r))
  codes=[int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in name+'$']
  encoded.append(dict(symbol=symbol,name=name,bytes=codes,source=r,encodingSource=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'))))
  p='src/data/pokemon/tmhm_learnsets.h';m=one(p,r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),');need(not re.sub(r'TMHM\(\w+\)|\||\s|0','',m[1]),'Unhandled machine syntax');machines.append(dict(species=s['id'],machineSymbols=re.findall(r'TMHM\((\w+)\)',m[1]),source=ref(p,symbol,m)))
  meta=next(m for m in proof['metadata'] if m['id']==s['id']);held.append(dict(species=s['id'],common=meta['originalFields']['itemCommon'],rare=meta['originalFields']['itemRare'],source=s['source']));evolutions.append(dict(species=s['id'],sourceEvolutions=meta['sourceEvolutions']))
 pokemon=dict(schemaVersion=1,profile='firered-saffron-gym-pokemon-v1',sourceCommit=COMMIT,species=[],opponentSpecies=selected,speciesNames=names,encodedNames=encoded,machines=machines,heldItems=held,sourceEvolutions=evolutions,requiredMoves=sorted({json.dumps(m['move'],sort_keys=True) for s in selected for m in s['levelUpMoves']}),limitations=['Only Slowbro80 is projected as enemy-only; inherited113 owned species are unchanged.','Full natural source moves are retained; metadata does not admit battles, gifts, evolution or teaching.'])
 pokemon['requiredMoves']=[json.loads(m) for m in pokemon['requiredMoves']]
 catalog = shared_machines.load_catalog(dependency)
 sourceitems=json.loads(text['src/data/items.json'])['items'];items=[];new=shared_machines.machine_definitions(dependency, 'content/generated/saffron-gym-catalogs/machines.json')
 for short in ITEMS:
  symbol='ITEM_'+short;item=next(i for i in sourceitems if i['itemId']==symbol);p='src/data/items.json';m=one(p,r'^    \{\n      "english": "'+re.escape(item['english'])+r'",.*?^    \}');r=ref(p,symbol,m)
  definition=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=number('include/constants/items.h',symbol),name=item['english'],price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=str(item['type']),holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=item['importance']==0,canSell=item['price']!=0,canGive=item['importance']==0,source=r)
  declaration=item['description_english'].replace(chr(92)+'n',chr(10));description=declaration
  if short.startswith('TM'):
   p='src/move_descriptions.c';m=one(p,r'^const u8 gMoveDescription_'+item['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1])
  wrapped=dict(item=definition,description=description);items.append(dict(metadata=wrapped,effectBytes=[],effectSource=None))
 itemdoc=dict(schemaVersion=1,profile='firered-saffron-gym-items-v1',sourceCommit=COMMIT,items=items,limitations=['TM04 and MAX_REPEL are exact original item declarations; metadata does not grant rewards or field-use support.','Inherited item objects must remain unchanged; only missing identities may extend the shop.'])
 machinedoc=dict(schemaVersion=1,profile='firered-saffron-gym-machines-v1',sourceCommit=COMMIT,inheritedItemIds=[ident('ITEM_'+x) for x in MACHINES],additions=[],newMachines=new,limitations=['All27 inherited113-species matrices retain identity; TM04 adds exactly113 owned rows and excludes enemy-only species.'])
 return pokemon,itemdoc,machinedoc

def main():
 global TRAINERS
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,default=Path(__file__).absolute().parents[1]);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
 src=Source(a.reference_root)
 for p in META_PATHS+list(SCRIPT_PATHS):src.read(p)
 TRAINERS=tuple(n[8:] for p in SCRIPT_PATHS for n in re.findall(r'trainerbattle_(?:single|no_intro) (TRAINER_\w+)',src.text(p)))
 need(len(TRAINERS)==len(set(TRAINERS))==8,'Expected8 source trainers')
 trainers=trainer_convert(src.lock(),src.data);need(sum(len(t['party']) for t in trainers['trainers'])==20,'Expected20 original trainer members')
 pokemon,items,machines=metadata(src,a.dependency_root)
 icon,png=tm04_icon(src,a.dependency_root);items['icons']=[icon]
 lock=src.lock();lock['gameEdition']='FIRERED';lock['romRevision']=0
 docs={'trainers.json':trainers,'pokemon.json':pokemon,'items.json':items,'machines.json':machines}
 output={f'content/generated/saffron-gym-catalogs/{p}':canonical(d) for p,d in docs.items()};output['scripts/saffron-gym-catalogs.lock.json']=canonical(lock);output['content/generated/saffron-gym-catalogs/item_tm04.png']=png
 output.update({'content/raw/pokefirered/037335f4/'+p:b for p,b in src.data.items()})
 manifest=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,outputs=[dict(path=p) for p,b in sorted(output.items()) if p.startswith('content/generated/')])
 output['content/generated/saffron-gym-catalogs/manifest.json']=canonical(manifest)
 for rel,b in output.items():
  p=safe(a.project_root,rel)
  if a.check:need(p.read_bytes()==b,'Regeneration differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 print(json.dumps(dict(trainers=len(TRAINERS),members=20,sourceFiles=len(src.entries),items=len(items['items']),machines=len(MACHINES)+len(machines['newMachines']),outputs=manifest['outputs'])))
if __name__=='__main__':main()
