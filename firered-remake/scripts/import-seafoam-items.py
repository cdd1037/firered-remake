#!/usr/bin/env python3
"""Import original Water Stone as a held/storage item; evolution use remains explicitly unimplemented.
REFERENCE DEPENDENCY OUTPUT [--check]. Reads pinned Git blobs only, never upstream scripts.
"""
import argparse,hashlib,io,json,re,subprocess
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/seafoam-items/';RAW='content/raw/seafoam-items/'
SHARED={
 'src/data/items.json':'content/raw/fossil-lab-rules/src/data/items.json',
 'include/constants/items.h':'content/raw/fossil-lab-rules/include/constants/items.h',
 'src/data/pokemon/item_effects.h':'content/raw/power-plant-pokemon/src/data/pokemon/item_effects.h',
 'include/constants/item_effects.h':'content/raw/power-plant-pokemon/include/constants/item_effects.h',
 'src/data/item_icon_table.h':'content/raw/fossil-lab-presentation/src/data/item_icon_table.h',
 'src/data/graphics/items.h':'content/raw/fossil-lab-presentation/src/data/graphics/items.h',
}
DEPENDENCIES={
 'content/generated/npc-trade-elyssa/items.json':'aa129f3505af6d62b10152ba8a7ff14f3721fc1e14b1cdd081427c1dd185cb69',
 'content/generated/npc-trade-mail/items.json':'fc36dc9cc2320abe8fed51ae0acc92bc0a6da80040a1aaea79782b39563a5988',
}
def sha(b):return hashlib.sha256(b).hexdigest()
def need(v,m):
 if not v:raise ValueError(m)
def encoded(v):return (json.dumps(v,indent=2,ensure_ascii=False)+'\n').encode()
def fingerprint(v):return sha(json.dumps(v,sort_keys=True,separators=(',',':'),ensure_ascii=False).encode())
def generate(reference,dependency):
 need(subprocess.check_output(['git','rev-parse',COMMIT+'^{tree}'],cwd=reference).decode().strip()==TREE,'Pinned tree')
 inputs={};raw={};texts={}
 def read(p):
  if p in texts:return texts[p]
  b=subprocess.check_output(['git','show',COMMIT+':'+p],cwd=reference);local=SHARED.get(p,RAW+p)
  if p in SHARED:need((dependency/local).read_bytes()==b,'Shared source bytes: '+p)
  else:raw[local]=b
  inputs[p]=dict(path=p,localPath=local,shared=p in SHARED,byteLength=len(b),sha256=sha(b),gitBlobSha1=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest());texts[p]=b;return b
 def text(p):return read(p).decode()
 def one(p,pattern):
  rows=list(re.finditer(pattern,text(p),re.M|re.S));need(len(rows)==1,'Exact declaration: '+pattern);return rows[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text(p).count('\n',0,m.start())+1,endLine=text(p).count('\n',0,m.end())+1,sha256=inputs[p]['sha256'])
 dependencies=[]
 for p,h in DEPENDENCIES.items():
  b=(dependency/p).read_bytes();need(sha(b)==h,'Frozen predecessor '+p);dependencies.append(dict(path=p,byteLength=len(b),sha256=h))
 p='src/data/items.json';symbol='ITEM_WATER_STONE';item=next(i for i in json.loads(text(p))['items'] if i['itemId']==symbol)
 m=one(p,r'^    \{\n      "english": "WATER STONE",.*?^    \}')
 need(int(one('include/constants/items.h',r'^#define ITEM_WATER_STONE\s+(\d+)\s*$')[1])==97,'Water Stone97')
 need(item['price']==2100 and item['pocket']=='POCKET_ITEMS' and item['importance']==0 and item['type']=='ITEM_TYPE_PARTY_MENU' and item['fieldUseFunc']=='FieldUseFunc_EvoItem' and item['holdEffect']=='HOLD_EFFECT_NONE','Original item semantics')
 source=ref(p,symbol,m);ident={'value':'pret:'+symbol}
 metadata=dict(id=ident,sourceItemSymbol=symbol,sourceItemId=97,name=item['english'],price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=item['type'],holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=True,canSell=True,canGive=True,source=source)
 effect_path='src/data/pokemon/item_effects.h';effect=one(effect_path,r'^static const u8 sItemEffect_WaterStone\[6\] = \{\n    \[4\] = ITEM4_EVO_STONE,\n\};')
 need('#define ITEM4_EVO_STONE         0x80' in text('include/constants/item_effects.h'),'Original evolution effect byte')
 addition={k:metadata[k] for k in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}
 addition.update(description=item['description_english'].replace('\\n','\n'),healHp=0,cureStatus=0,effectBytes=[0,0,0,0,128,0],useSupport=10,effectSource=ref(effect_path,'sItemEffect_WaterStone',effect),sourceMetadata=metadata)
 icon_path='src/data/item_icon_table.h';binding=one(icon_path,r'^    \[ITEM_WATER_STONE\]\s*= \{gItemIcon_WaterStone, gItemIconPalette_WaterStone\},$');graphics='src/data/graphics/items.h'
 image='graphics/items/icons/water_stone.png';palette='graphics/items/icon_palettes/water_stone.pal'
 need('gItemIcon_WaterStone[] = INCBIN_U32("graphics/items/icons/water_stone.4bpp.lz")' in text(graphics) and 'gItemIconPalette_WaterStone[] = INCBIN_U32("graphics/items/icon_palettes/water_stone.gbapal.lz")' in text(graphics),'Original icon binding')
 im=Image.open(io.BytesIO(read(image)));need(im.mode=='P' and im.size==(24,24) and max(im.tobytes())<16,'Original icon shape')
 lines=text(palette).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Original palette shape')
 colors=[tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
 rgba=bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0));buf=io.BytesIO();Image.frombytes('RGBA',(24,24),rgba).save(buf,format='PNG',compress_level=9);png=buf.getvalue()
 icon=dict(item=ident,width=24,height=24,rgbaPath=BASE+'water-stone.rgba',pngPath=BASE+'water-stone.png',rgbaSha256=sha(rgba),pngSha256=sha(png),sourcePaths=[image,palette],bindingSource=ref(icon_path,symbol,binding))
 items=dict(sourceCommit=COMMIT,profile='firered-seafoam-inventory-v1',previousItemCount=127,itemCount=128,additions=[addition],additionsSemanticSha256=fingerprint([addition]),shopsAdded=[],icons=[icon])
 source_inputs=dict(sourceCommit=COMMIT,sourceTree=TREE,inputs=[inputs[k] for k in sorted(inputs)],dependencyInputs=dependencies)
 files={BASE+'items.json':encoded(items),BASE+'source-input-hashes.json':encoded(source_inputs),icon['rgbaPath']:rgba,icon['pngPath']:png,**raw}
 lock=dict(sourceCommit=COMMIT,sourceTree=TREE,profile=items['profile'],importerSha256=sha(Path(__file__).read_bytes()),files=[dict(path=p,byteLength=len(b),sha256=sha(b)) for p,b in sorted(files.items())])
 files['scripts/seafoam-items.lock.json']=encoded(lock)
 return files,items

def main():
 p=argparse.ArgumentParser(description=__doc__)
 for arg in ['reference','dependency','output']:p.add_argument(arg,type=Path)
 p.add_argument('--check',action='store_true');a=p.parse_args();files,items=generate(a.reference,a.dependency)
 for p,b in files.items():
  target=a.output/p
  if a.check:need(target.read_bytes()==b,'Regeneration differs: '+p)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(checked=a.check,itemCount=128,addition=items['additionsSemanticSha256'],hashes={p:sha(b) for p,b in files.items()}),indent=2))
if __name__=='__main__':main()
