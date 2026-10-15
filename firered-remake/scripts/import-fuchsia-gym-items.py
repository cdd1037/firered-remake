#!/usr/bin/env python3
"""Authenticated one-item Fuchsia Gym TM06 source candidate; no gameplay activation."""
import sys as _path_sys
from pathlib import Path as _SourcePath
_script_directory = str(_SourcePath(__file__).resolve().parent)
if _script_directory not in _path_sys.path:
    _path_sys.path.insert(0, _script_directory)

import argparse,base64,importlib.util,json,pathlib,re,subprocess
ROOT=pathlib.Path(__file__).resolve().parents[1]
import machine_catalog as shared_machines

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
    catalog = shared_machines.load_catalog(project)
    wrapped = catalog.machines['pret:ITEM_TM06']['machine']
    values = shared_machines.machine_definitions(project, 'content/generated/fuchsia-gym-items/fuchsia-gym-machines.json', 'machines')
    path='src/data/item_icon_table.h';m=match(path,r'^    \[ITEM_TM06\][ \t]*= \{(\w+), (\w+)\},');binding=ref(path,'ITEM_TM06',m);icon_symbols=m.groups();paths=[]
    for symbol in icon_symbols:
        path='src/data/graphics/items.h';m=match(path,r'^const u32 '+symbol+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(path,symbol,m));paths.append(m[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
    image,palette=paths;codec=load_module(project / 'scripts/import-lab-battle-ui.py', 'fuchsia_gym_item_codec')
    w,h,indices,_=codec.indexed_png(sources[image]);rgba=codec.rgba(indices,codec.palette(sources[palette]));png=codec.png(w,h,rgba)
    if (w,h)!=(24,24):raise ValueError('TM06 icon size differs')
    icon=dict(item=ident('ITEM_TM06'),width=w,height=h,pngPath='item_tm06.png',source=ref(image,icon_symbols[0]),paletteSource=ref(palette,icon_symbols[1]),bindingSource=binding)
    for path,symbol,pattern in [('src/item.c','AddBagItem',r'^bool8 AddBagItem\(.*?^\}'),('src/item.c','RemoveBagItem',r'^bool8 RemoveBagItem\(.*?^\}'),('src/party_menu.c','Task_LearnedMove',r'^static void Task_LearnedMove\([^;]*?\n\{.*?^\}'),('src/pokemon.c','CanMonLearnTMHM',r'^u32 CanMonLearnTMHM\([^;]*?\n\{.*?^\}'),('data/maps/FuchsiaCity_Gym/scripts.inc','FuchsiaCity_Gym_EventScript_GiveTM06',r'^FuchsiaCity_Gym_EventScript_GiveTM06::.*?^\tend$')]:rules.append(ref(path,symbol,match(path,pattern)))
    limitations=['Exactly one source TM06/Toxic reward item with its original 24x24 icon and 107 source compatibility rows.','Metadata does not enable the reward, teaching, battle effects, field permissions or a campaign profile.']
    return {'fuchsia-gym-items.json':canonical(dict(schemaVersion=1,profile='firered-fuchsia-gym-items-v1',sourceCommit=COMMIT,inputs=lock['files'],items=[dict(metadata=wrapped,effectBytes=[],effectSource=None)],icons=[icon],limitations=limitations)),
      'fuchsia-gym-machines.json':canonical(dict(schemaVersion=1,profile='firered-fuchsia-gym-machines-v1',sourceCommit=COMMIT,inputs=lock['files'],machines=values,icons=[icon],limitations=limitations)),'item_tm06.png':png}

def run(root,reference=None,check=False):
 root=pathlib.Path(root).absolute();safe(root,"sentinel");root=root.resolve();lockBytes=safe(ROOT,'scripts/fuchsia-gym-items.lock.json').read_bytes()
 lock=json.loads(lockBytes)
 if (lock['schemaVersion'], lock['repository'], lock['commit'], lock['profile'], lock['gameEdition'], lock['romRevision'], len(lock['files'])) != (1, 'pret/pokefirered', COMMIT, PROFILE, 'FIRERED', 0, 16):raise ValueError('Source identity differs')
 raw=safe(root,'content/raw/pokefirered/037335f4');out=safe(root,'content/generated/fuchsia-gym-items')
 def overlaps(a,b):return a==b or a.is_relative_to(b) or b.is_relative_to(a)
 for p in ('scripts','src','tests','.git','scripts/import-config'):
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
 if reference:materials.update({(raw,p):b for p,b in sources.items()})
 for (directory,path),data in materials.items():
  target=safe(directory,path)
  if check:
   if target.read_bytes()!=data:raise ValueError('Generated bytes differ: '+path)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
 return dict(profile=PROFILE,inputCount=16,outputCount=len(outputs))
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--target-root',type=pathlib.Path,default=ROOT);p.add_argument('--reference-repo',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args();print(json.dumps(run(a.target_root,a.reference_repo,a.check),sort_keys=True))
