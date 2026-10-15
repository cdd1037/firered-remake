#!/usr/bin/env python3
"""Strict local text extraction for nine starter-family species; never run C or upstream tools."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import json,pathlib,re
from decimal import Decimal
ROOT=pathlib.Path(__file__).resolve().parent.parent
RAW=ROOT/'content/raw/pokefirered/037335f4'; OUT=ROOT/'content/generated/pokemon'
lock=json.loads((ROOT/'scripts/fetch-pokemon.lock.json').read_text(encoding='utf-8'))
if lock['commit']!='037335f4c725d7c9aecdac87066f2002b4bd7e14':raise ValueError('Unrecognized source commit')
sources={};meta={}
for e in lock['files']:
    p=pathlib.PurePosixPath(e['path'])
    if p.is_absolute() or '..' in p.parts:raise ValueError('Unsafe path')
    b=(RAW/p).read_bytes()
    sources[str(p)]=b.decode('utf-8');meta[str(p)]=e

def one(pattern,text):
    matches=list(re.finditer(pattern,text,re.M|re.S))
    if len(matches)!=1:raise ValueError(f'Expected one match for {pattern}; found {len(matches)}')
    return matches[0]
def ref(path,symbol,start,end):
    return dict(path=path,symbol=symbol,startLine=sources[path].count('\n',0,start)+1,endLine=sources[path].count('\n',0,end)+1)
def number(s):
    if not re.fullmatch(r'\d+',s):raise ValueError('Not an unsigned decimal: '+s)
    return int(s)
def block(path,symbol):
    m=one(r'^    \['+symbol+r'\] =\s*\n    \{\n(.*?)^    \},',sources[path])
    body=m.group(1)
    if '#' in body:raise ValueError('Conditional species/move definition needs explicit handling')
    fields={}
    for line in body.splitlines():
        v=re.fullmatch(r'\s*\.(\w+) = (.*),',line)
        if not v or v[1] in fields:raise ValueError('Unexpected/duplicate field: '+line)
        fields[v[1]]=v[2]
    return fields,ref(path,symbol,m.start(),m.end())
def pair(value,prefix):
    m=re.fullmatch(r'\{('+prefix+r'\w+), ('+prefix+r'\w+)\}',value)
    if not m:raise ValueError('Unexpected source pair: '+value)
    return m[1],m[2]
def constant(path,symbol):return number(one(r'^#define '+symbol+r'\s+(\d+)\s*$',sources[path])[1])
def ident(symbol):return {'value':'pret:'+symbol}
names=['BULBASAUR','IVYSAUR','VENUSAUR','CHARMANDER','CHARMELEON','CHARIZARD','SQUIRTLE','WARTORTLE','BLASTOISE']; species=[];move_symbols=set()
for i,name in enumerate(names,1):
    symbol='SPECIES_'+name;path='src/data/pokemon/species_info.h'; f,sref=block(path,symbol)
    sid=constant('include/constants/species.h',symbol)
    if sid!=i:raise ValueError('Unexpected source species ID')
    keys=['HP','Attack','Defense','Speed','SpAttack','SpDefense']; outkeys=['hp','attack','defense','speed','specialAttack','specialDefense']
    base={k:number(f['base'+s]) for k,s in zip(outkeys,keys)}
    yields={k:number(f['evYield_'+s]) for k,s in zip(outkeys,keys)}
    gender=one(r'^PERCENT_FEMALE\((\d+(?:\.\d+)?)\)$',f['genderRatio'])[1]
    ratio=min(254,int(Decimal(gender)*255/100))
    if f['growthRate']!='GROWTH_MEDIUM_SLOW':raise ValueError('Unexpected starter growth curve')
    types=pair(f['types'],'TYPE_');abilities=pair(f['abilities'],'ABILITY_')
    pointer=one(r'^    \['+symbol+r'\] = (\w+),$',sources['src/data/pokemon/level_up_learnset_pointers.h'])[1]
    lp='src/data/pokemon/level_up_learnsets.h';lm=one(r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};',sources[lp]);entries=[]
    lines=lm[1].splitlines()
    if lines[-1].strip()!='LEVEL_UP_END':raise ValueError('Missing learnset terminator')
    for line in lines[:-1]:
        e=re.fullmatch(r'    LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\),',line)
        if not e:raise ValueError('Unhandled learnset entry: '+line)
        level=number(e[1]);move=e[2];move_symbols.add(move)
        pos=sources[lp].index(line,lm.start(),lm.end())
        entries.append(dict(level=level,move=ident(move),source=ref(lp,pointer,pos,pos+len(line))))
    ep='src/data/pokemon/evolution.h';evs=[]
    em=list(re.finditer(r'^    \['+symbol+r'\]\s*= \{\{EVO_LEVEL, (\d+), (SPECIES_\w+)\}\},$',sources[ep],re.M))
    mentions=re.findall(r'^    \['+symbol+r'\]',sources[ep],re.M)
    if len(em)!=len(mentions) or len(em)>1:raise ValueError('Unsupported evolution method')
    for e in em:evs.append(dict(level=number(e[1]),target=ident(e[2]),source=ref(ep,symbol,e.start(),e.end())))
    species.append(dict(id=ident(symbol),sourceSpeciesId=sid,baseStats=base,type1=types[0],type2=types[1],catchRate=number(f['catchRate']),experienceYield=number(f['expYield']),evYield=yields,growthRate='mediumSlow',genderRatio=ratio,ability1=abilities[0],ability2=abilities[1],friendship=number(f['friendship']),levelUpMoves=entries,evolutions=evs,source=sref,learnsetSource=ref(lp,pointer,lm.start(),lm.end())))
moves=[]
for symbol in sorted(move_symbols):
    f,sref=block('src/data/battle_moves.h',symbol)
    if set(f)!=set(['effect','power','type','accuracy','pp','secondaryEffectChance','target','priority','flags']):raise ValueError('Unexpected move fields')
    priority=int(f['priority'])
    if not re.fullmatch(r'-?\d+',f['priority']):raise ValueError('Unexpected priority expression')
    flags=[] if f['flags']=='0' else f['flags'].split(' | ')
    if any(not re.fullmatch(r'FLAG_\w+',v) for v in flags):raise ValueError('Unexpected move flags')
    moves.append(dict(id=ident(symbol),sourceMoveId=constant('include/constants/moves.h',symbol),pp=number(f['pp']),power=number(f['power']),type=f['type'],accuracy=number(f['accuracy']),effect=f['effect'],secondaryEffectChance=number(f['secondaryEffectChance']),target=f['target'],priority=priority,flags=flags,battleEffectImplemented=False,source=sref))
limits=['Only nine starter-family species and their level-up moves are imported.','Move effects, abilities in battle, evolution execution, battle experience awards, and PP Ups are not implemented by this profile.','No TM/HM, egg, tutor, wild, trainer-party, nickname, met-location or original-save encoding profile is enabled.']
doc=dict(schemaVersion=1,profile='firered-starter-instances-v1',sourceCommit=lock['commit'],inputs=lock['files'],limitations=limits,species=species,moves=moves)
OUT.mkdir(parents=True,exist_ok=True)
data=(json.dumps(doc,indent=2,ensure_ascii=False)+'\n').encode();target=OUT/'starter-catalog.json';target.write_bytes(data)
manifest=dict(schemaVersion=1,repository=lock['repository'],commit=lock['commit'],inputs=lock['files'],outputs=[dict(path='content/generated/pokemon/starter-catalog.json')],counts=dict(species=len(species),moves=len(moves),levelUpEntries=sum(len(s['levelUpMoves']) for s in species)),upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False)
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n', encoding='utf-8', newline='\n')
print(json.dumps(dict(path=str(target.relative_to(ROOT)),counts=manifest['counts'])))
