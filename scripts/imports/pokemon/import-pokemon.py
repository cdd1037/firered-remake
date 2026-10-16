#!/usr/bin/env python3
"""Strict local text extraction for nine starter-family species; never run C or upstream tools."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT


import argparse
import json
import pathlib
import re
from decimal import Decimal
from pokemon_import_io import load_recipe, read_sources, safe, preflight_directory, write_files

ROOT = _PROJECT_ROOT
COMMIT = '037335f4c725d7c9aecdac87066f2002b4bd7e14'


def convert(lock, raw_sources):
    sources = {path: data.decode('utf-8') for path, data in raw_sources.items()}
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
            entries.append(dict(level=level,move=ident(move)))
        ep='src/data/pokemon/evolution.h';evs=[]
        em=list(re.finditer(r'^    \['+symbol+r'\]\s*= \{\{EVO_LEVEL, (\d+), (SPECIES_\w+)\}\},$',sources[ep],re.M))
        mentions=re.findall(r'^    \['+symbol+r'\]',sources[ep],re.M)
        if len(em)!=len(mentions) or len(em)>1:raise ValueError('Unsupported evolution method')
        for e in em:evs.append(dict(level=number(e[1]), target=ident(e[2])))
        species.append(dict(id=ident(symbol), sourceSpeciesId=sid, baseStats=base, type1=types[0], type2=types[1], catchRate=number(f['catchRate']), experienceYield=number(f['expYield']), evYield=yields, growthRate='mediumSlow', genderRatio=ratio, ability1=abilities[0], ability2=abilities[1], friendship=number(f['friendship']), levelUpMoves=entries, evolutions=evs))
    moves=[]
    for symbol in sorted(move_symbols):
        f,sref=block('src/data/battle_moves.h',symbol)
        if set(f)!=set(['effect','power','type','accuracy','pp','secondaryEffectChance','target','priority','flags']):raise ValueError('Unexpected move fields')
        priority=int(f['priority'])
        if not re.fullmatch(r'-?\d+',f['priority']):raise ValueError('Unexpected priority expression')
        flags=[] if f['flags']=='0' else f['flags'].split(' | ')
        if any(not re.fullmatch(r'FLAG_\w+',v) for v in flags):raise ValueError('Unexpected move flags')
        moves.append(ident(symbol))
    doc=dict(schemaVersion=1, species=[dict(id=row['id'], evolutions=row['evolutions']) for row in species], moves=moves)
    return {
        'starters.json': (json.dumps(doc, separators=(',', ':'), ensure_ascii=False) + '\n').encode(),
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--raw', type=pathlib.Path, default=ROOT / 'content/raw/pokefirered/037335f4')
    parser.add_argument('--out', type=pathlib.Path, default=ROOT / 'content/generated/data/pokemon/starters.json')
    parser.add_argument('--lock', type=pathlib.Path, default=ROOT / 'scripts/recipes/source/fetch-pokemon.lock.json')
    args = parser.parse_args()
    recipe = load_recipe(args.lock, commit=COMMIT)
    raw, out = safe(args.raw), safe(args.out)
    if out.suffix != ".json":
        raise ValueError("Starter output must be a JSON file")
    preflight_directory(raw)
    preflight_directory(out.parent)
    if raw == out or raw in out.parents or out in raw.parents:
        raise ValueError('Raw and output paths must be disjoint')
    sources = read_sources(recipe, raw)
    output = convert(recipe, sources)['starters.json']
    write_files(out.parent, {out.name: output})
    print(json.dumps(dict(inputCount=len(sources), outputCount=1), sort_keys=True))


if __name__ == '__main__':
    main()
