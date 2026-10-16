#!/usr/bin/env python3
"""Rebuild private Celadon Gym catalog additions from pinned local Git blobs or checked raw cache.
Uses authenticated existing project image codecs; never executes source repository programs.
"""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from runtime_json import runtime_json_bytes
from item_catalog import item_definition as shared_item, item_json

import argparse,json,pathlib,re,sys
import pokemon_import as pokemon
from pokemon_presentation import convert as presentation_outputs
from pokemon_import_io import safe, load_recipe, read_sources
from image_io import indexed_png, png
sys.dont_write_bytecode=True
ROOT=_PROJECT_ROOT
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
def canonical(data):return (json.dumps(data,sort_keys=True,separators=(',',':'),ensure_ascii=False, default=item_json)+'\n').encode()


def item_outputs(lock,sources,project):
    text={p:b.decode() for p,b in sources.items() if not p.endswith('.png')};entries={e['path']:e for e in lock['files']}
    rules=[]
    def match(path,pattern):
        found=list(re.finditer(pattern,text[path],re.M|re.S))
        if len(found)!=1:raise ValueError('Nonunique source match '+path+' '+pattern)
        return found[0]
    def ref(path,symbol,m=None):return dict(path=path,symbol=symbol,startLine=1 if m is None else text[path].count('\n',0,m.start())+1,endLine=max(1,text.get(path,'').count('\n')) if m is None else text[path].count('\n',0,m.end())+1)
    def ident(s):return dict(value='pret:'+s)
    def constant(path,symbol):
        m=match(path,r'^#define '+symbol+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');rules.append(ref(path,symbol,m));return int(m[1])
    i=next(x for x in json.loads(text['src/data/items.json'])['items'] if x['itemId']=='ITEM_TM19');path='src/data/items.json'
    m=match(path,r'^    \{\n      "english": "TM19",.*?^    \}');source=ref(path,'ITEM_TM19',m)
    definition=shared_item('ITEM_TM19')
    path='src/move_descriptions.c';m=match(path,r'^const u8 gMoveDescription_GigaDrain\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
    wrapped=dict(item=definition,description=json.loads(m[1]))
    path='src/data/party_menu.h';m=match(path,r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};');mapping=re.findall(r'\bMOVE_\w+\b',m[1]);rules.append(ref(path,'sTMHMMoves',m))
    if len(mapping)!=58 or mapping[18]!='MOVE_GIGA_DRAIN':raise ValueError('TM19 source move mapping differs')
    path='src/data/battle_moves.h';m=match(path,r'^    \[MOVE_GIGA_DRAIN\] =\n    \{(.*?)^    \},');rules.append(ref(path,'MOVE_GIGA_DRAIN',m))
    if not re.search(r'\.pp = 5,',m[1]):raise ValueError('Giga Drain source PP differs')
    path='src/data/item_icon_table.h';m=match(path,r'^    \[ITEM_TM19\][ \t]*= \{(\w+), (\w+)\},');binding=ref(path,'ITEM_TM19',m);icon_symbols=m.groups();paths=[]
    for symbol in icon_symbols:
        path='src/data/graphics/items.h';m=match(path,r'^const u32 '+symbol+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(path,symbol,m));paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
    image,palette=paths
    w,h,indices,_=indexed_png(sources[image]);rgba=pokemon.rgba(indices,pokemon.icon_palette(sources[palette]));image_png=png(w,h,rgba)
    if (w,h)!=(24,24):raise ValueError('TM19 icon size differs')
    icon=dict(item=ident('ITEM_TM19'), width=w, height=h, pngPath='item_tm19.png')
    for path,symbol,pattern in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','RemoveBagItem',r'^bool8 RemoveBagItem\(.*?^\}'),('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}'),('src/pokemon.c','CanMonLearnTMHM',r'^u32 CanMonLearnTMHM\([^;]*?\n\{.*?^\}'),('data/maps/CeladonCity_Gym/scripts.inc','CeladonCity_Gym_EventScript_GiveTM19',r'^CeladonCity_Gym_EventScript_GiveTM19::.*?^\tend$')]:rules.append(ref(path,symbol,match(path,pattern)))
    return {'celadon-gym-items.json':runtime_json_bytes(dict(schemaVersion=1,items=[dict(metadata=wrapped, effectBytes=[])],icons=[icon]), sort_keys=True, default=item_json),
      'item_tm19.png':image_png}


def load_sources(stem, raw, repo):
    lock = load_recipe(ROOT / ('scripts/recipes/' + ('pokemon' if stem.endswith('pokemon') else 'trainers' if stem.endswith('trainers') else 'items') + '/' + stem + '.lock.json'),
                       commit=COMMIT, profile='firered-' + stem + '-v1')
    return lock, read_sources(lock, raw, repo)


def materialize(project,target,repo=None,check=False):
    project,target=safe(project),safe(target);results=[]
    for stem in ('celadon-gym-pokemon','celadon-gym-items'):
        raw=safe(target,'content/raw/pokefirered/037335f4');out=safe(target, 'content/generated/' + ('sprites/pokemon/celadon-gym' if stem.endswith('pokemon') else 'data/items/celadon-gym'))
        if repo is not None:
            repo=safe(repo)
            if any(p==repo or p in repo.parents or repo in p.parents for p in (raw,out)):raise ValueError('Output overlaps source repository')
        lock,sources=load_sources(stem,raw,repo)
        profile='firered-'+stem+'-v1'
        outputs=presentation_outputs('celadon-gym',lock,sources) if stem.endswith('pokemon') else item_outputs(lock,sources,project)
        if 'manifest.json' not in outputs:
            outputs['manifest.json']=canonical(dict(schemaVersion=1, inputCount=len(sources), outputs=[dict(path=p) for p in sorted(outputs)]))
        materials={(out,p):b for p,b in outputs.items()}
        if repo is not None:materials.update({(raw,p):b for p,b in sources.items()})
        if check:
            for (root,path),data in materials.items():
                if safe(root,path).read_bytes()!=data:raise ValueError('Materialized output differs: '+path)
        else:
            targets=[]
            for (root,path),data in materials.items():
                destination=safe(root,path);stage=safe(root,path+'.tmp')
                if any(p.exists() and not p.is_file() for p in (destination,stage)):raise ValueError('Nonregular output path')
                targets.append((destination,stage,data))
            for destination,stage,data in targets:destination.parent.mkdir(parents=True,exist_ok=True);stage.write_bytes(data);stage.replace(destination)
        results.append(dict(stem=stem,inputCount=len(sources),outputCount=len(outputs)))
    return results

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--project-root',type=pathlib.Path,default=ROOT);parser.add_argument('--target-root',type=pathlib.Path,default=ROOT);parser.add_argument('--reference-repo',type=pathlib.Path);parser.add_argument('--check',action='store_true');args=parser.parse_args()
    print(json.dumps(materialize(args.project_root,args.target_root,args.reference_repo,args.check),sort_keys=True, default=item_json))
