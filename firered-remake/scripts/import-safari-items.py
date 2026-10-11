#!/usr/bin/env python3
"""Offline source Safari items and bounded24-machine metadata."""
import argparse,base64,hashlib,importlib.util,io,json,pathlib,re,subprocess,sys
from PIL import Image
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('safari_meta',ROOT/'scripts/import-safari-pokemon.py');meta=importlib.util.module_from_spec(spec);spec.loader.exec_module(meta)
spec=importlib.util.spec_from_file_location('safari_art',ROOT/'scripts/import-safari-art.py');art=importlib.util.module_from_spec(spec);spec.loader.exec_module(art)
COMMIT=meta.COMMIT;TREE=meta.TREE;PROFILE='firered-safari-items-v1';LOCK_SHA='038989466424735694c3096f68e8c0834f115d747efda8e08526f59f5b7978ca';INPUT_COUNT=28
BASELINE_SHA='3775e076364c67d145c5754e88907d1bdf3de334b7e714e90a63b86cb87b396d'
MACHINES=('TM39','TM05','TM09','TM46','TM03','TM45','TM43','TM28','TM31','TM44','HM01','TM34','HM05','TM40','TM19','TM12','TM21','TM49','TM06')

def authenticated(root,reference=None):
 b=meta.safe(root,'scripts/safari-items.lock.json').read_bytes();meta.require(meta.sha(b)==LOCK_SHA,'Safari items source lock differs');lock=json.loads(b)
 meta.require((lock['schemaVersion'],lock['repository'],lock['commit'],lock['tree'],lock['profile'],len(lock['files']))==(1,'pret/pokefirered',COMMIT,TREE,PROFILE,INPUT_COUNT),'Safari items source scope differs')
 if reference:meta.require(subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD']).decode().strip()==COMMIT,'Reference HEAD differs')
 raw={}
 for row in lock['files']:
  p=meta.safe(root,'content/raw/safari-items/'+row['path']);b=subprocess.check_output(['git','-C',str(reference),'show',COMMIT+':'+row['path']]) if reference else p.read_bytes()
  meta.require((len(b),meta.sha(b),hashlib.sha1(f'blob {len(b)}\0'.encode()+b).hexdigest())==(row['byteLength'],row['sha256'],row['gitBlobSha1']),'Safari item source bytes differ: '+row['path']);raw[row['path']]=b
  if reference:p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(b)
 return lock,raw

def generate(root,dependency_root=None):
 lock,raw=authenticated(root);text={p:b.decode() for p,b in raw.items() if not p.endswith('.png')};inv={r['path']:r for r in lock['files']};rules=[]
 def one(p,pattern):
  ms=list(re.finditer(pattern,text[p],re.M|re.S));meta.require(len(ms)==1,'Expected one Safari item match: '+p+' '+pattern);return ms[0]
 def ref(p,s,m=None):return dict(path=p,symbol=s,startLine=1 if m is None else text[p].count('\n',0,m.start())+1,endLine=1 if m is None else text[p].count('\n',0,m.end())+1,sha256=inv[p]['sha256'])
 def ident(s):return dict(value='pret:'+s)
 def number(p,s):
  m=one(p,r'^#define '+s+r'[ \t]+(\d+)[ \t]*(?://[^\n]*)?$');return int(m[1])
 baseline=meta.safe(dependency_root or root,'scripts/fuchsia-gym-baseline.lock.json').read_bytes();meta.require(meta.sha(baseline)==BASELINE_SHA,'Safari predecessor species source proof differs');old=json.loads(baseline)['species'];all_species=sorted(old+list(meta.SPECIES),key=lambda s:number('include/constants/species.h','SPECIES_'+s));meta.require(len(set(all_species))==110,'Expected110 species')
 def compatibility(s,tm):
  symbol='SPECIES_'+s;p='src/data/pokemon/tmhm_learnsets.h';m=one(p,r'^    \['+symbol+r'\]\s*= TMHM_LEARNSET\((.*?)\),');tokens=re.findall(r'TMHM\((\w+)\)',m[1]);meta.require(not re.sub(r'TMHM\(\w+\)|\||\s|0','',m[1]),'Unhandled machine syntax')
  return dict(species=ident(symbol),sourceSpeciesId=number('include/constants/species.h',symbol),canLearn=any(t.startswith(tm+'_') for t in tokens),source=ref(p,symbol,m))
 outputs={};items=[];icons=[];new_machines=[];source_items=json.loads(text['src/data/items.json'])['items']
 p='src/data/party_menu.h';mm=one(p,r'^static const u16 sTMHMMoves\[\] =\n\{(.*?)^\};');mapping=re.findall(r'MOVE_\w+',mm[1]);meta.require(len(mapping)==58,'Source58 machine mapping differs');rules.append(ref(p,'sTMHMMoves',mm))
 for short in ('HM03','HM04','GOLD_TEETH','TM11','TM47','TM32','MAX_POTION','FULL_RESTORE','LEAF_STONE','PROTEIN','QUICK_CLAW','MAX_REVIVE'):
  symbol='ITEM_'+short;item=next(i for i in source_items if i['itemId']==symbol);p='src/data/items.json';im=one(p,r'^    \{\n      "english": "'+re.escape(item['english'])+r'",.*?^    \}');source=ref(p,symbol,im)
  definition=dict(id=ident(symbol),sourceItemSymbol=symbol,sourceItemId=number('include/constants/items.h',symbol),name=item['english'],price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=str(item['type']),holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=item['importance']==0,canSell=item['price']!=0,canGive=item['importance']==0,source=source)
  declaration=item['description_english'].replace(chr(92)+'n',chr(10));description=declaration;description_source=source
  if short.startswith(('HM','TM')):
   p='src/move_descriptions.c';m=one(p,r'^const u8 gMoveDescription_'+item['moveId']+r'\[\] = _\(("(?:[^"\\]|\\.)*")\);$');description=json.loads(m[1]);description_source=ref(p,'gMoveDescription_'+item['moveId'],m)
  wrapped=dict(item=definition,description=description,declarationDescription=declaration,descriptionSource=description_source);items.append(dict(metadata=wrapped,effectBytes=[],effectSource=None))
  p='src/data/item_icon_table.h';im=one(p,r'^    \['+symbol+r'\][ \t]*= \{(\w+), (\w+)\},');binding=ref(p,symbol,im);image_symbol,palette_symbol=im.groups();paths=[]
  for asset_symbol in (image_symbol,palette_symbol):
   p='src/data/graphics/items.h';am=one(p,r'^const u32 '+asset_symbol+r'\[\][ \t]*= INCBIN_U32\("([^"]+)"\);');rules.append(ref(p,asset_symbol,am));paths.append(am[1].replace('.4bpp.lz','.png').replace('.gbapal.lz','.pal'))
  image_path,palette_path=paths;image=Image.open(io.BytesIO(raw[image_path]));meta.require(image.mode=='P' and image.size==(24,24),'Source icon shape differs');palette=[tuple(map(int,line.split())) for line in text[palette_path].splitlines()[3:]];pixels=bytes(c for i in image.get_flattened_data() for c in (*[(v>>3)*255//31 for v in palette[i]],0 if i==0 else 255));png=art.png(24,24,pixels);out='item_'+short.lower()+'.png';outputs[out]=png
  icons.append(dict(item=ident(symbol),width=24,height=24,rgbaBase64=base64.b64encode(pixels).decode(),rgbaSha256=meta.sha(pixels),pngPath=out,pngSha256=meta.sha(png),source=ref(image_path,image_symbol),paletteSource=ref(palette_path,palette_symbol),bindingSource=binding))
  if short.startswith(('HM','TM')):
   move=mapping[(49 if short.startswith('HM') else -1)+int(short[2:])];meta.require(move=={'Surf':'MOVE_SURF','Strength':'MOVE_STRENGTH','SunnyDay':'MOVE_SUNNY_DAY','SteelWing':'MOVE_STEEL_WING','DoubleTeam':'MOVE_DOUBLE_TEAM'}[item['moveId']],'TMHM source move binding differs');p='src/data/battle_moves.h';m=one(p,r'^    \['+move+r'\] =\n    \{(.*?)^    \},');pp=int(re.search(r'\.pp = (\d+),',m[1])[1]);rules.append(ref(p,move,m));new_machines.append(dict(machine=wrapped,move=ident(move),sourceMoveId=number('include/constants/moves.h',move),movePp=pp,compatibility=[compatibility(s,short) for s in all_species]))
 additions=[dict(item=ident('ITEM_'+item),compatibility=[compatibility(s,item) for s in meta.SPECIES]) for item in MACHINES]
 itemdoc=dict(schemaVersion=1,profile=PROFILE,sourceCommit=COMMIT,inputs=lock['files'],rulesSources=rules,items=items,icons=icons,limitations=['Twelve source Safari item declarations/icons; ordinary medicine/vitamin effect byte arrays remain separate source-kernel data.','Metadata does not grant story rewards, teach moves, consume HMs, enable field moves or expand item services.'],outputs=[dict(path=p,byteLength=len(b),sha256=meta.sha(b)) for p,b in sorted(outputs.items())])
 machinedoc=dict(schemaVersion=1,profile='firered-safari-machines-v1',sourceCommit=COMMIT,baselineSpeciesSha256=BASELINE_SHA,inheritedItemIds=[ident('ITEM_'+item) for item in MACHINES],additions=additions,newMachines=new_machines,limitations=['Exact24 machines and110 species; preserve inherited19x107 cells, append3 source cells each, add five source110 matrices for HM03/HM04/TM11/TM47/TM32.','Raw teaching candidate requires separate Domain authentication; field Surf and Strength are separate permissions.'])
 outputs['items.json']=meta.canonical(itemdoc);outputs['machines.json']=meta.canonical(machinedoc)
 return {'content/generated/safari-items/'+p:b for p,b in outputs.items()}
if __name__=='__main__':
 p=argparse.ArgumentParser(description=__doc__);p.add_argument('--project-root',type=pathlib.Path,default=ROOT);p.add_argument('--dependency-root',type=pathlib.Path);p.add_argument('--reference-root',type=pathlib.Path);p.add_argument('--check',action='store_true');a=p.parse_args()
 if a.reference_root:authenticated(a.project_root,a.reference_root)
 for rel,b in generate(a.project_root,a.dependency_root).items():
  path=meta.safe(a.project_root,rel)
  if a.check:meta.require(path.read_bytes()==b,'Safari generated item differs: '+rel)
  else:path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(b)
  print(meta.sha(b),len(b),rel)
