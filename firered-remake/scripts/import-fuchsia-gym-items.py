#!/usr/bin/env python3
"""Authenticated one-item Fuchsia Gym TM06 source candidate; no gameplay activation."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)
from raw_source_paths import raw_metadata_directory
import argparse,base64,importlib.util,json,pathlib,re,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
PROFILE='firered-fuchsia-gym-items-v1'
def canonical(d):return (json.dumps(d,sort_keys=True,separators=(',',':'),ensure_ascii=False)+'\n').encode()
def safe(root,rel):
 if pathlib.PurePosixPath(rel).is_absolute() or '\\' in rel or ':' in rel or any(x in ('','.','..') for x in rel.split('/')):raise ValueError('Unsafe path')
 p=pathlib.Path(root).absolute()/rel
 if any(x.is_symlink() for x in (p,*p.parents)):raise ValueError('Symlink rejected')
 return p
def load_module(path, name):
 spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m
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
    def compatibility(symbol,tm):
        path='src/data/pokemon/tmhm_learnsets.h';m=match(path,r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),');tokens=re.findall(r'TMHM\((\w+)\)',m[1])
        if re.sub(r'TMHM\(\w+\)|\||\s|0','',m[1]) or len(tokens)!=len(set(tokens)):raise ValueError('Unknown machine compatibility syntax')
        return dict(species=ident(symbol),sourceSpeciesId=constant('include/constants/species.h',symbol),canLearn=tm in tokens,source=ref(path,symbol,m))
    values=[];symbols=['SPECIES_'+s for s in baseline['species']]
    if len(symbols)!=107 or len(set(symbols))!=107:raise ValueError('Baseline species scope differs')
    i=next(x for x in json.loads(text['src/data/items.json'])['items'] if x['itemId']=='ITEM_TM06');path='src/data/items.json'
    m=match(path,r'^    \{\n      "english": "TM06",.*?^    \}');source=ref(path,'ITEM_TM06',m)
    definition=dict(id=ident('ITEM_TM06'),sourceItemSymbol='ITEM_TM06',sourceItemId=constant('include/constants/items.h','ITEM_TM06'),name=i['english'],price=i['price'],importance=i['importance'],registrability=i['registrability'],pocket=i['pocket'],type=str(i['type']),holdEffect=i['holdEffect'],holdEffectParam=i['holdEffectParam'],fieldUseFunction=i['fieldUseFunc'],battleUsage=i['battleUsage'],battleUseFunction=i['battleUseFunc'],secondaryId=i['secondaryId'],canToss=i['importance']==0,canSell=i['price']!=0,canGive=i['importance']==0,source=source)
    path='src/move_descriptions.c';m=match(path,r'^const u8 gMoveDescription_Toxic\[\] = _\(("(?:[^"\\]|\\.)*")\);$')
    wrapped=dict(item=definition,description=json.loads(m[1]),declarationDescription=i['description_english'].replace('\\n','\n'),descriptionSource=ref(path,'gMoveDescription_Toxic',m))
    path='src/data/party_menu.h';m=match(path,r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};');mapping=re.findall(r'\bMOVE_\w+\b',m[1]);rules.append(ref(path,'sTMHMMoves',m))
    if len(mapping)!=58 or mapping[5]!='MOVE_TOXIC':raise ValueError('TM06 source move mapping differs')
    values.append(dict(machine=wrapped,move=ident('MOVE_TOXIC'),sourceMoveId=constant('include/constants/moves.h','MOVE_TOXIC'),movePp=10,compatibility=[compatibility(s,'TM06_TOXIC') for s in symbols]))
    path='src/data/battle_moves.h';m=match(path,r'^    \[MOVE_TOXIC\] =\n    \{(.*?)^    \},');rules.append(ref(path,'MOVE_TOXIC',m))
    if not re.search(r'\.pp = 10,',m[1]):raise ValueError('Toxic source PP differs')
    path='src/data/item_icon_table.h';m=match(path,r'^    \[ITEM_TM06\][ \t]*= \{(\w+), (\w+)\},');binding=ref(path,'ITEM_TM06',m);icon_symbols=m.groups();paths=[]
    for symbol in icon_symbols:
        path='src/data/graphics/items.h';m=match(path,r'^const u32 '+symbol+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(path,symbol,m));paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
    image,palette=paths;codec=load_module(project / 'scripts/import-lab-battle-ui.py', 'fuchsia_gym_item_codec')
    w,h,indices,_=codec.indexed_png(sources[image]);rgba=codec.rgba(indices,codec.palette(sources[palette]));png=codec.png(w,h,rgba)
    if (w,h)!=(24,24):raise ValueError('TM06 icon size differs')
    icon=dict(item=ident('ITEM_TM06'),width=w,height=h,pngPath='item_tm06.png',source=ref(image,icon_symbols[0]),paletteSource=ref(palette,icon_symbols[1]),bindingSource=binding)
    for path,symbol,pattern in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','RemoveBagItem',r'^bool8 RemoveBagItem\(.*?^\}'),('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}'),('src/pokemon.c','CanMonLearnTMHM',r'^u32 CanMonLearnTMHM\([^;]*?\n\{.*?^\}'),('data/maps/FuchsiaCity_Gym/scripts.inc','FuchsiaCity_Gym_EventScript_GiveTM06',r'^FuchsiaCity_Gym_EventScript_GiveTM06::.*?^\tend$')]:rules.append(ref(path,symbol,match(path,pattern)))
    limitations=['Exactly one source TM06/Toxic reward item with its original 24x24 icon and 107 source compatibility rows.','Metadata does not enable the reward, teaching, battle effects, field permissions or a campaign profile.']
    return {'fuchsia-gym-items.json':canonical(dict(schemaVersion=1,profile='firered-fuchsia-gym-items-v1',sourceCommit=COMMIT,inputs=lock['files'],rulesSources=rules,items=[dict(metadata=wrapped,effectBytes=[],effectSource=None)],icons=[icon],limitations=limitations)),
      'fuchsia-gym-machines.json':canonical(dict(schemaVersion=1,profile='firered-fuchsia-gym-machines-v1',sourceCommit=COMMIT,inputs=lock['files'],rulesSources=rules,machines=values,icons=[icon],limitations=limitations)),'item_tm06.png':png}

def run(root,reference=None,check=False):
 global baseline
 root=pathlib.Path(root).absolute();safe(root,"sentinel");root=root.resolve();lockBytes=safe(ROOT,'scripts/fuchsia-gym-items.lock.json').read_bytes()
 lock=json.loads(lockBytes)
 if (lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], lock['gameEdition'], lock['romRevision'], len(lock['files'])) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0, 16):raise ValueError('Source identity differs')
 baselineBytes=safe(ROOT,'scripts/fuchsia-gym-baseline.lock.json').read_bytes()
 baseline=json.loads(baselineBytes)
 for e in baseline['catalogDependencies']:
  b=safe(ROOT,e['path']).read_bytes()
 raw=safe(root,'content/raw/pokefirered/037335f4');out=safe(root,'content/generated/fuchsia-gym-items')
 def overlaps(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 for p in ('scripts','src','tests','.git','content/sources'):
  if overlaps(raw,ROOT/p) or overlaps(out,ROOT/p):raise ValueError('Protected project path overlap')
 for kind in ('raw','generated'):
  for directory in (ROOT/'content'/kind).iterdir():
   if directory.is_dir() and directory.name!='fuchsia-gym-items' and (overlaps(raw,directory.resolve()) or overlaps(out,directory.resolve())):raise ValueError('Historical content trees are protected')
 if reference:
  reference=pathlib.Path(reference).resolve()
  if overlaps(reference,raw) or overlaps(reference,out):raise ValueError('Reference overlap')
  if subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD']).decode().strip() != COMMIT:raise ValueError('Reference identity differs')
 sources={}
 for e in lock['files']:
  p=e['path']
  if reference:
   b=subprocess.check_output(['git','-C',str(reference),'cat-file','blob',COMMIT + ':' + e['path']])
  else:b=safe(raw,p).read_bytes()
  if p in sources:raise ValueError('Duplicate input')
  sources[p]=b
 outputs=item_outputs(lock,sources,ROOT)
 manifest=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=16,upstreamScriptsExecuted=False,romDownloaded=False,publiclyUploaded=False,outputs=[dict(path=p) for p,b in sorted(outputs.items())]))
 outputs['manifest.json']=manifest
 rawManifest=canonical(dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputCount=16))
 materials={(out,p):b for p,b in outputs.items()}
 if reference:materials.update({(raw,p):b for p,b in sources.items()});materials[(raw_metadata_directory(raw, 'fuchsia-gym-items'),'manifest.json')]=rawManifest
 for (directory,path),data in materials.items():
  target=safe(directory,path)
  if check:
   if target.read_bytes()!=data:raise ValueError('Generated bytes differ: '+path)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
 return dict(profile=PROFILE,inputCount=16,outputCount=len(outputs))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--target-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.target_root,a.reference_repo,a.check),sort_keys=True))
