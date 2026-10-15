#!/usr/bin/env python3
"""Import original Water Stone as a held/storage item; evolution use remains explicitly unimplemented.
REFERENCE DEPENDENCY OUTPUT [--check]. Reads pinned Git blobs only, never upstream scripts.
"""
from item_catalog import item_definition as shared_item, inventory_item, item_json
import argparse,io,json,re,subprocess
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14';TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/seafoam-items/';RAW='content/raw/pokefirered/037335f4/'
SHARED={
 'src/data/items.json':'content/raw/pokefirered/037335f4/src/data/items.json',
 'include/constants/items.h':'content/raw/pokefirered/037335f4/include/constants/items.h',
 'src/data/pokemon/item_effects.h':'content/raw/pokefirered/037335f4/src/data/pokemon/item_effects.h',
 'include/constants/item_effects.h':'content/raw/pokefirered/037335f4/include/constants/item_effects.h',
 'src/data/item_icon_table.h':'content/raw/pokefirered/037335f4/src/data/item_icon_table.h',
 'src/data/graphics/items.h':'content/raw/pokefirered/037335f4/src/data/graphics/items.h',
}
DEPENDENCIES=(
    'content/generated/npc-trade-elyssa/items.json',
    'content/generated/npc-trade-mail/items.json',
)
def need(v,m):
 if not v:raise ValueError(m)
def encoded(v):return (json.dumps(v,indent=2,ensure_ascii=False, default=item_json)+'\n').encode()
def generate(reference,dependency):
 inputs={};raw={};texts={}
 def read(p):
  if p in texts:return texts[p]
  b=subprocess.check_output(['git','show',COMMIT+':'+p],cwd=reference);local=SHARED.get(p,RAW+p)
  if p in SHARED:need((dependency/local).read_bytes()==b,'Shared source bytes: '+p)
  else:raw[local]=b
  inputs[p]=dict(path=p,localPath=local,shared=p in SHARED);texts[p]=b;return b
 def text(p):return read(p).decode()
 def one(p,pattern):
  rows=list(re.finditer(pattern,text(p),re.M|re.S));need(len(rows)==1,'Exact declaration: '+pattern);return rows[0]
 def ref(p,s,m):return dict(path=p,symbol=s,startLine=text(p).count('\n',0,m.start())+1,endLine=text(p).count('\n',0,m.end())+1)
 dependencies=[]
 for p in DEPENDENCIES:
  b=(dependency/p).read_bytes();dependencies.append(dict(path=p))
 p='src/data/items.json';symbol='ITEM_WATER_STONE';item=next(i for i in json.loads(text(p))['items'] if i['itemId']==symbol)
 m=one(p,r'^    \{\n      "english": "WATER STONE",.*?^    \}')
 need(int(one('include/constants/items.h',r'^#define ITEM_WATER_STONE\s+(\d+)\s*$')[1])==97,'Water Stone97')
 need(item['price']==2100 and item['pocket']=='POCKET_ITEMS' and item['importance']==0 and item['type']=='ITEM_TYPE_PARTY_MENU' and item['fieldUseFunc']=='FieldUseFunc_EvoItem' and item['holdEffect']=='HOLD_EFFECT_NONE','Original item semantics')
 source=ref(p,symbol,m);ident={'value':'pret:'+symbol}
 metadata=shared_item(symbol)
 effect_path='src/data/pokemon/item_effects.h';effect=one(effect_path,r'^static const u8 sItemEffect_WaterStone\[6\] = \{\n    \[4\] = ITEM4_EVO_STONE,\n\};')
 need('#define ITEM4_EVO_STONE         0x80' in text('include/constants/item_effects.h'),'Original evolution effect byte')
 addition=dict(item=metadata)
 addition.update(description=item['description_english'].replace('\\n', '\n'), healHp=0, cureStatus=0, effectBytes=[0, 0, 0, 0, 128, 0], useSupport=10)
 icon_path='src/data/item_icon_table.h';binding=one(icon_path,r'^    \[ITEM_WATER_STONE\]\s*= \{gItemIcon_WaterStone, gItemIconPalette_WaterStone\},$');graphics='src/data/graphics/items.h'
 image='graphics/items/icons/water_stone.png';palette='graphics/items/icon_palettes/water_stone.pal'
 need('gItemIcon_WaterStone[] = INCBIN_U32("graphics/items/icons/water_stone.4bpp.lz")' in text(graphics) and 'gItemIconPalette_WaterStone[] = INCBIN_U32("graphics/items/icon_palettes/water_stone.gbapal.lz")' in text(graphics),'Original icon binding')
 im=Image.open(io.BytesIO(read(image)));need(im.mode=='P' and im.size==(24,24) and max(im.tobytes())<16,'Original icon shape')
 lines=text(palette).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Original palette shape')
 colors=[tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
 rgba=bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0));buf=io.BytesIO();Image.frombytes('RGBA',(24,24),rgba).save(buf,format='PNG',compress_level=9);png=buf.getvalue()
 icon = dict(item=ident, width=24, height=24, texturePath=BASE + 'water-stone.png', sourcePaths=[image, palette], bindingSource=ref(icon_path, symbol, binding))
 items=dict(sourceCommit=COMMIT,profile='firered-seafoam-inventory-v1',previousItemCount=127,itemCount=128,additions=[addition],shopsAdded=[],icons=[icon])
 source_inputs=dict(sourceCommit=COMMIT,inputs=[inputs[k] for k in sorted(inputs)],dependencyInputs=dependencies)
 files = {BASE + 'items.json': encoded(items), BASE + 'source-input-hashes.json': encoded(source_inputs), icon['texturePath']: png, **raw}
 lock=dict(sourceCommit=COMMIT,profile=items['profile'],files=[dict(path=p) for p,b in sorted(files.items())])
 return files,items

def main():
 p=argparse.ArgumentParser(description=__doc__)
 for arg in ['reference','dependency','output']:p.add_argument(arg,type=Path)
 p.add_argument('--check',action='store_true');a=p.parse_args();files,items=generate(a.reference,a.dependency)
 for p,b in files.items():
  target=a.output/p
  if a.check:need(target.read_bytes()==b,'Regeneration differs: '+p)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
 print(json.dumps(dict(checked=a.check,itemCount=128),indent=2, default=item_json))
if __name__=='__main__':main()
