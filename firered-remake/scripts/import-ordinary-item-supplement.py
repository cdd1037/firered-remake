#!/usr/bin/env python3
"""Import original ordinary item supplement from fixed pret Git blobs, without running upstream code.
REFERENCE DEPENDENCY OUTPUT [--check]. No predecessor catalog or raw content tree is duplicated.
"""
import argparse,  io, json, re, subprocess
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/ordinary-item-supplement/'
RAW='content/raw/pokefirered/037335f4/'
PROFILE='firered-ordinary-item-supplement-v1'
SHARED={
 'src/data/items.json':'content/raw/pokefirered/037335f4/src/data/items.json',
 'include/constants/items.h':'content/raw/pokefirered/037335f4/include/constants/items.h',
 'src/data/item_icon_table.h':'content/raw/pokefirered/037335f4/src/data/item_icon_table.h',
 'src/data/graphics/items.h':'content/raw/pokefirered/037335f4/src/data/graphics/items.h',
 'src/data/pokemon/item_effects.h':'content/raw/pokefirered/037335f4/src/data/pokemon/item_effects.h',
 'include/constants/item_effects.h':'content/raw/pokefirered/037335f4/include/constants/item_effects.h',
 'src/item_use.c':'content/raw/pokefirered/037335f4/src/item_use.c',
}
def need(value,message):
 if not value:raise ValueError(message)
def encoded(value):return (json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode()
def generate(reference,dependency):
 inputs,raw,cache={},{},{}
 def read(path):
  if path in cache:return cache[path]
  data=subprocess.check_output(['git','show',COMMIT+':'+path],cwd=reference);local=SHARED.get(path,RAW+path)
  if path in SHARED:need((dependency/local).read_bytes()==data,'Shared source bytes: '+path)
  else:raw[local]=data
  inputs[path]=dict(path=path,localPath=local,shared=path in SHARED);cache[path]=data;return data
 def text(path):return read(path).decode()
 def one(path,pattern):
  matches=list(re.finditer(pattern,text(path),re.M|re.S));need(len(matches)==1,'Exact source declaration: '+pattern);return matches[0]
 def source(path,symbol,match):return dict(path=path,symbol=symbol,startLine=text(path).count('\n',0,match.start())+1,endLine=text(path).count('\n',0,match.end())+1)
 path='src/data/items.json';symbol='ITEM_PP_MAX';item=next(i for i in json.loads(text(path))['items'] if i['itemId']==symbol)
 block=one(path,r'^    \{\n      "english": "PP MAX",.*?^    \}')
 item_id=int(one('include/constants/items.h',r'^#define ITEM_PP_MAX\s+(\d+)\s*$')[1])
 need(item_id==71 and item['price']==9800 and item['pocket']=='POCKET_ITEMS' and item['importance']==0 and item['type']=='ITEM_TYPE_PARTY_MENU' and item['fieldUseFunc']=='FieldUseFunc_PpUp' and item['battleUsage']==0 and item['battleUseFunc']=='NULL' and item['holdEffect']=='HOLD_EFFECT_NONE','Original PP Max semantics')
 src=source(path,symbol,block);ident=dict(value='pret:'+symbol)
 metadata=dict(id=ident,sourceItemSymbol=symbol,sourceItemId=item_id,name=item['english'],price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=item['type'],holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=True,canSell=True,canGive=True,source=src)
 effect_path='src/data/pokemon/item_effects.h';effect=one(effect_path,r'^static const u8 sItemEffect_PPMax\[9\] = \{.*?^\};')
 need(effect[0]=='static const u8 sItemEffect_PPMax[9] = {\n    [5] = ITEM5_PP_MAX | ITEM5_FRIENDSHIP_ALL,\n    VITAMIN_FRIENDSHIP_CHANGE(6),\n};','PP Max exact effect bytes layout')
 constants='include/constants/item_effects.h'
 flags=[int(one(constants,r'^#define '+name+r'\s+(0x[0-9A-Fa-f]+)\s*$')[1],16) for name in ['ITEM5_PP_MAX','ITEM5_FRIENDSHIP_LOW','ITEM5_FRIENDSHIP_MID','ITEM5_FRIENDSHIP_HIGH']]
 need(one(constants,r'^#define ITEM5_FRIENDSHIP_ALL[^\n]+')[0].endswith('(ITEM5_FRIENDSHIP_LOW | ITEM5_FRIENDSHIP_MID | ITEM5_FRIENDSHIP_HIGH)'),'Friendship flag binding')
 vitamin=one(effect_path,r'^#define VITAMIN_FRIENDSHIP_CHANGE\(i\).*?Friendship change, high \*/')
 friendship=[int(x) for x in re.findall(r'\[\(i\) \+ \d\] = (\d+)',vitamin[0])];need(friendship==[5,3,2],'Original vitamin friendship changes')
 need('[ITEM_PP_MAX - ITEM_POTION]        = sItemEffect_PPMax,' in text(effect_path),'Original effect table binding')
 effects=[0,0,0,0,0,flags[0]|flags[1]|flags[2]|flags[3],*friendship]
 addition={key:metadata[key] for key in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}
 addition.update(description=item['description_english'].replace('\\n','\n'),healHp=0,cureStatus=0,effectBytes=effects,useSupport=10,effectSource=source(effect_path,'sItemEffect_PPMax',effect),sourceMetadata=metadata)
 use='src/item_use.c';field=one(use,r'^void FieldUseFunc_PpUp\(u8 taskId\)\n\{.*?^\}')
 need('gItemUseCB = ItemUseCB_PPUp;' in field[0] and 'DoSetUpItemUseCallback(taskId);' in field[0],'Original field callback')
 icon_path='src/data/item_icon_table.h';binding=one(icon_path,r'^    \[ITEM_PP_MAX\]\s*= \{gItemIcon_PPMax, gItemIconPalette_PPMax\},$')
 graphics=text('src/data/graphics/items.h');need('gItemIcon_PPMax[] = INCBIN_U32("graphics/items/icons/pp_max.4bpp.lz")' in graphics and 'gItemIconPalette_PPMax[] = INCBIN_U32("graphics/items/icon_palettes/pp_max.gbapal.lz")' in graphics,'Original icon binding')
 image='graphics/items/icons/pp_max.png';palette='graphics/items/icon_palettes/pp_max.pal'
 im=Image.open(io.BytesIO(read(image)));need(im.mode=='P' and im.size==(24,24) and max(im.tobytes())<16,'Original icon indices')
 lines=text(palette).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Original palette')
 colors=[tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
 rgba=bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0));stream=io.BytesIO();Image.frombytes('RGBA',(24,24),rgba).save(stream,format='PNG',compress_level=9);png=stream.getvalue()
 icon = dict(item=ident, width=24, height=24, texturePath=BASE + 'pp-max.png', sourcePaths=[image, palette], bindingSource=source(icon_path, symbol, binding))
 items=dict(sourceCommit=COMMIT,profile=PROFILE,previousItemCount=129,itemCount=130,additions=[addition],shopsAdded=[],icons=[icon])
 facts=dict(sourceCommit=COMMIT,fieldUseImplemented=False,fieldUseSupport='NoFieldUse',reason='The original PP Up/PP Max move-selection effect is not implemented.',fieldUseSource=source(use,'FieldUseFunc_PpUp',field),effectSource=source(effect_path,'sItemEffect_PPMax',effect),friendshipSource=source(effect_path,'VITAMIN_FRIENDSHIP_CHANGE',vitamin),effectBytes=effects)
 evidence=dict(sourceCommit=COMMIT,inputs=[inputs[k] for k in sorted(inputs)],dependencyInputs=[])
 files = {BASE + 'items.json': encoded(items), BASE + 'source-input-hashes.json': encoded(evidence), BASE + 'source-contract.json': encoded(facts), icon['texturePath']: png, **raw}
 files['scripts/ordinary-item-supplement.lock.json']=encoded(dict(sourceCommit=COMMIT,profile=PROFILE,files=[dict(path=p) for p,b in sorted(files.items())]))
 return files,items
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__)
 for arg in ['reference','dependency','output']:parser.add_argument(arg,type=Path)
 parser.add_argument('--check',action='store_true');args=parser.parse_args();files,items=generate(args.reference,args.dependency)
 for path,data in files.items():
  target=args.output/path
  if args.check:need(target.read_bytes()==data,'Regeneration differs: '+path)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
 print(json.dumps(dict(checked=args.check,itemCount=130),indent=2))
