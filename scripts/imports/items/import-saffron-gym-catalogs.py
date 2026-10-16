#!/usr/bin/env python3
"""Regenerate Saffron Gym source metadata from immutable pret Git blobs, without upstream execution."""
import sys as _bootstrap_sys
from pathlib import Path as _BootstrapPath
_bootstrap_sys.path.insert(0, str(_BootstrapPath(__file__).resolve().parents[3] / 'scripts/lib'))
from project_paths import PROJECT_ROOT as _PROJECT_ROOT

from shared_trainers import trainer_json
from trainer_import import convert_battles
from item_catalog import item_definition as shared_item, inventory_item, item_json
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
def canonical(doc): return (json.dumps(doc, sort_keys=True, indent=2, default=item_json)+'\n').encode()
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

def trainer_convert(sources):
 recipe = dict(trainers=list(TRAINERS), scripts=list(SCRIPT_PATHS), moves='party')
 return convert_battles(recipe, sources)

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
 codecpath=safe(dependency,'scripts/imports/presentation/import-lab-battle-ui.py')
 spec=importlib.util.spec_from_file_location('_saffron_gym_items',codecpath);codec=importlib.util.module_from_spec(spec);spec.loader.exec_module(codec)
 w,h,indices,_=codec.indexed_png(ib);rgba=codec.rgba(indices,codec.palette(pb));png=codec.png(w,h,rgba);need((w,h)==(24,24),'TM04 icon size differs')
 def reference(path,symbol):return dict(path=path,symbol=symbol,startLine=1,endLine=max(1,src.data[path].count(b'\n')))
 return dict(item=dict(value='pret:ITEM_TM04'), width=w, height=h, pngPath='item_tm04.png'),png

def metadata(src,dependency):
 text={p:src.text(p) for p in META_PATHS};ident=lambda s:dict(value='pret:'+s)
 def one(p,pattern):
  ms=list(re.finditer(pattern,text[p],re.M|re.S));need(len(ms)==1,'Expected one source match: '+p+' '+pattern);return ms[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text[p].count('\n',0,m.start())+1,endLine=text[p].count('\n',0,m.end())+1)
 def number(p,s):return int(one(p,r'^#define '+s+r'[ \t]+(\d+)\b[^\n]*')[1])
 proofbytes=safe(dependency,'content/generated/data/pokemon/canonical/catalog.json').read_bytes()
 proof=json.loads(proofbytes)
 selected=[next(s for s in proof['species'] if s['sourceSpeciesId']==i) for i in (80,)]
 names=[];encoded=[];machines=[];held=[];evolutions=[]
 for s in selected:
  symbol=s['id']['value'][5:];p='src/data/text/species_names.h';m=one(p,r'^    \['+symbol+r'\]\s*= _\("([^"\n]+)"\),$');name=m[1];r=ref(p,symbol,m)
  names.append(dict(id=s['id'],name=name,source=r))
  codes=[int(one('charmap.txt',"^'"+re.escape(c)+r"'[ \t]+=[ \t]+([A-F0-9]{2})[ \t]*$")[1],16) for c in name+'$']
  encoded.append(dict(symbol=symbol,name=name,bytes=codes,source=r,encodingSource=ref('charmap.txt','English name encoding and EOS',one('charmap.txt',r'\A.*\Z'))))
  p='src/data/pokemon/tmhm_learnsets.h';m=one(p,r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),');need(not re.sub(r'TMHM\(\w+\)|\||\s|0','',m[1]),'Unhandled machine syntax');machines.append(dict(species=s['id'],machineSymbols=re.findall(r'TMHM\((\w+)\)',m[1])))
  meta=next(m for m in proof['metadata'] if m['id']==s['id']);held.append(dict(species=s['id'],common=meta['itemCommon'],rare=meta['itemRare']));evolutions.append(dict(species=s['id'],sourceEvolutions=meta['sourceEvolutions']))
 pokemon=dict(schemaVersion=1,species=[],opponentSpecies=[dict(id=row['id'],evolutions=row['evolutions']) for row in selected],speciesNames=names,encodedNames=encoded,machines=machines,heldItems=held,sourceEvolutions=evolutions,requiredMoves=sorted({json.dumps(m['move'],sort_keys=True, default=item_json) for s in selected for m in s['levelUpMoves']}))
 pokemon['requiredMoves']=[json.loads(m) for m in pokemon['requiredMoves']]
 catalog = shared_machines.load_catalog(dependency)
 sourceitems=json.loads(text['src/data/items.json'])['items'];items=[];new=shared_machines.machine_definitions(dependency, 'content/generated/data/regions/saffron-gym/machines.json')
 for short in ITEMS:
  symbol='ITEM_'+short;item=next(i for i in sourceitems if i['itemId']==symbol);p='src/data/items.json';m=one(p,r'^    \{\n      "english": "'+re.escape(item['english'])+r'",.*?^    \}');r=ref(p,symbol,m)
  definition=shared_item(symbol)
  declaration=item['description_english'].replace(chr(92)+'n',chr(10));description=declaration
  if short.startswith('TM'):
   p='src/move_descriptions.c';m=one(p,r'^const u8 gMoveDescription_'+item['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1])
  wrapped=dict(item=definition,description=description);items.append(dict(metadata=wrapped, effectBytes=[]))
 itemdoc=dict(schemaVersion=1,items=items)
 machinedoc=dict(schemaVersion=1,inheritedItemIds=[ident('ITEM_'+x) for x in MACHINES],additions=[],newMachines=new)
 return pokemon,itemdoc,machinedoc

def main():
 global TRAINERS
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--reference-root',type=Path,required=True);ap.add_argument('--project-root',type=Path,default=_PROJECT_ROOT);ap.add_argument('--dependency-root',type=Path,required=True);ap.add_argument('--check',action='store_true');a=ap.parse_args()
 src=Source(a.reference_root)
 for p in META_PATHS+list(SCRIPT_PATHS):src.read(p)
 TRAINERS=tuple(n[8:] for p in SCRIPT_PATHS for n in re.findall(r'trainerbattle_(?:single|no_intro) (TRAINER_\w+)',src.text(p)))
 need(len(TRAINERS)==len(set(TRAINERS))==8,'Expected8 source trainers')
 trainers=trainer_convert(src.data);need(sum(len(t['party']) for t in trainers['trainers'])==20,'Expected20 original trainer members')
 pokemon,items,machines=metadata(src,a.dependency_root)
 icon,png=tm04_icon(src,a.dependency_root);items['icons']=[icon]
 lock=src.lock();lock['gameEdition']='FIRERED';lock['romRevision']=0
 docs={'trainers.json':trainers,'pokemon.json':pokemon,'items.json':items,'machines.json':machines}
 output={f'content/generated/data/regions/saffron-gym/{p}':(trainer_json(a.project_root/'content/generated', d, check=a.check) if p == 'trainers.json' else canonical(d)) for p,d in docs.items()};output['scripts/recipes/story/saffron-gym-catalogs.lock.json']=canonical(lock);output['content/generated/data/regions/saffron-gym/item_tm04.png']=png
 output.update({'content/raw/pokefirered/037335f4/'+p:b for p,b in src.data.items()})
 manifest=dict(schemaVersion=1,outputs=[dict(path=p) for p,b in sorted(output.items()) if p.startswith('content/generated/')])
 output['content/generated/data/regions/saffron-gym/manifest.json']=canonical(manifest)
 for rel,b in output.items():
  p=safe(a.project_root,rel)
  if a.check:need(p.read_bytes()==b,'Regeneration differs: '+rel)
  else:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 print(json.dumps(dict(trainers=len(TRAINERS),members=20,sourceFiles=len(src.entries),items=len(items['items']),machines=len(MACHINES)+len(machines['newMachines']),outputs=manifest['outputs']), default=item_json))
if __name__=='__main__':main()
