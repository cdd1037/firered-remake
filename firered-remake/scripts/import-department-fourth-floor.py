#!/usr/bin/env python3
"""Import only original Retro Mail and the complete 4F shelf contract from pinned Git blobs.
REFERENCE DEPENDENCY OUTPUT [--check]. Shared source files remain in DEPENDENCY.
"""
import argparse,  io, json, re, subprocess
from pathlib import Path
from PIL import Image
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
TREE='7515cd4067ea55f8cdbc73624478afd2736c58b9'
BASE='content/generated/department-fourth-floor/'
RAW='content/raw/pokefirered/037335f4/'
PROFILE='firered-department-fourth-floor-v1'
SHARED={
 'src/data/items.json':'content/raw/pokefirered/037335f4/src/data/items.json',
 'include/constants/items.h':'content/raw/pokefirered/037335f4/include/constants/items.h',
 'src/data/item_icon_table.h':'content/raw/pokefirered/037335f4/src/data/item_icon_table.h',
 'src/data/graphics/items.h':'content/raw/pokefirered/037335f4/src/data/graphics/items.h',
 'src/item_use.c':'content/raw/pokefirered/037335f4/src/item_use.c',
 'data/maps/CeladonCity_DepartmentStore_4F/scripts.inc':'content/raw/pokefirered/037335f4/data/maps/CeladonCity_DepartmentStore_4F/scripts.inc',
 'data/maps/CeladonCity_DepartmentStore_4F/map.json':'content/raw/pokefirered/037335f4/data/maps/CeladonCity_DepartmentStore_4F/map.json',
 'src/data/pokemon/evolution.h':'content/raw/pokefirered/037335f4/src/data/pokemon/evolution.h',
}
def need(value,message):
 if not value: raise ValueError(message)
def encoded(value): return (json.dumps(value,indent=2,ensure_ascii=False)+'\n').encode()
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
 path='src/data/items.json';symbol='ITEM_RETRO_MAIL';source_items=json.loads(text(path))['items'];item=next(i for i in source_items if i['itemId']==symbol)
 block=one(path,r'^    \{\n      "english": "RETRO MAIL",.*?^    \}')
 item_id=int(one('include/constants/items.h',r'^#define ITEM_RETRO_MAIL\s+(\d+)\s*$')[1])
 need(item_id==132 and item['price']==50 and item['pocket']=='POCKET_ITEMS' and item['importance']==0 and item['type']=='ITEM_TYPE_MAIL' and item['fieldUseFunc']=='FieldUseFunc_Mail' and item['battleUsage']==0 and item['battleUseFunc']=='NULL' and item['holdEffect']=='HOLD_EFFECT_NONE' and item['secondaryId']==11,'Original Retro Mail semantics')
 src=source(path,symbol,block);ident=dict(value='pret:'+symbol)
 metadata=dict(id=ident,sourceItemSymbol=symbol,sourceItemId=item_id,name=item['english'],price=item['price'],importance=item['importance'],registrability=item['registrability'],pocket=item['pocket'],type=item['type'],holdEffect=item['holdEffect'],holdEffectParam=item['holdEffectParam'],fieldUseFunction=item['fieldUseFunc'],battleUsage=item['battleUsage'],battleUseFunction=item['battleUseFunc'],secondaryId=item['secondaryId'],canToss=True,canSell=True,canGive=True,source=src)
 addition={key:metadata[key] for key in ['id','sourceItemSymbol','sourceItemId','name','price','pocket','importance','type','fieldUseFunction','battleUsage','battleUseFunction','holdEffectParam','source']}
 addition.update(description=item['description_english'].replace('\\n','\n'),healHp=0,cureStatus=0,effectBytes=[],useSupport=10,effectSource=None,sourceMetadata=metadata)
 use='src/item_use.c';field=one(use,r'^void FieldUseFunc_Mail\(u8 taskId\)\n\{.*?^\}');preview=one(use,r'^static void CB2_CheckMail\(void\)\n\{.*?^\}')
 need('ItemMenu_SetExitCallback(CB2_CheckMail);' in field[0] and 'ReadMail(&mail, CB2_BagMenuFromStartMenu, FALSE);' in preview[0],'Original blank Mail preview callback')
 shelf_path='data/maps/CeladonCity_DepartmentStore_4F/scripts.inc';shelf=one(shelf_path,r'^CeladonCity_DepartmentStore_4F_Items::\n(?:\t\.2byte [A-Z_]+\n)+')
 symbols=re.findall(r'\.2byte (ITEM_\w+)',shelf[0]);expected=['ITEM_POKE_DOLL','ITEM_RETRO_MAIL','ITEM_FIRE_STONE','ITEM_THUNDER_STONE','ITEM_WATER_STONE','ITEM_LEAF_STONE','ITEM_NONE'];need(symbols==expected,'Exact complete source shelf')
 shelf_items=[]
 for sym in symbols[:-1]:
  original=next(i for i in source_items if i['itemId']==sym);number=int(one('include/constants/items.h',r'^#define '+sym+r'\s+(\d+)\s*$')[1])
  shelf_items.append(dict(symbol=sym,sourceItemId=number,name=original['english'],price=original['price'],pocket=original['pocket'],type=original['type'],fieldUseFunction=original['fieldUseFunc']))
 clerk=one(shelf_path,r'^CeladonCity_DepartmentStore_4F_EventScript_Clerk::\n.*?\tend\n');need('\tpokemart CeladonCity_DepartmentStore_4F_Items\n' in clerk[0],'Original clerk binding')
 map_path='data/maps/CeladonCity_DepartmentStore_4F/map.json';actors=json.loads(text(map_path))['object_events'];actor=actors[2]
 need(len(actors)==3 and actor['graphics_id']=='OBJ_EVENT_GFX_CLERK' and actor['script']=='CeladonCity_DepartmentStore_4F_EventScript_Clerk' and (actor['x'],actor['y'],actor['elevation'],actor['movement_type'],actor['flag'])==(3,13,0,'MOVEMENT_TYPE_FACE_UP','0'),'Original clerk actor')
 evolution_path='src/data/pokemon/evolution.h';evolutions=[]
 for match in re.finditer(r'^    \[(SPECIES_\w+)\]\s*=\s*\{(.*?)\}\},?$',text(evolution_path),re.M|re.S):
  for stone,target in re.findall(r'\{EVO_ITEM, (ITEM_\w+), (SPECIES_\w+)',match[2]):
   if stone in symbols[2:-1]:evolutions.append(dict(species=match[1],item=stone,target=target,source=source(evolution_path,match[1],match)))
 need(len(evolutions)==14,'All original four-stone targets, including two non-Kanto branches')
 icon_path='src/data/item_icon_table.h';binding=one(icon_path,r'^    \[ITEM_RETRO_MAIL\]\s*= \{gItemIcon_RetroMail, gItemIconPalette_RetroMail\},$')
 graphics=text('src/data/graphics/items.h');need('gItemIcon_RetroMail[] = INCBIN_U32("graphics/items/icons/retro_mail.4bpp.lz")' in graphics and 'gItemIconPalette_RetroMail[] = INCBIN_U32("graphics/items/icon_palettes/retro_mail.gbapal.lz")' in graphics,'Original icon binding')
 image='graphics/items/icons/retro_mail.png';palette='graphics/items/icon_palettes/retro_mail.pal'
 im=Image.open(io.BytesIO(read(image)));need(im.mode=='P' and im.size==(24,24) and max(im.tobytes())<16,'Original icon indices')
 lines=text(palette).splitlines();need(lines[:3]==['JASC-PAL','0100','16'] and len(lines)==19,'Original palette')
 colors=[tuple(((int(c)>>3)<<3)|((int(c)>>3)>>2) for c in line.split()) for line in lines[3:]]
 rgba=bytes(v for index in im.tobytes() for v in (*colors[index],255 if index else 0));stream=io.BytesIO();Image.frombytes('RGBA',(24,24),rgba).save(stream,format='PNG',compress_level=9);png=stream.getvalue()
 icon = dict(item=ident, width=24, height=24, texturePath=BASE + 'retro-mail.png', sourcePaths=[image, palette], bindingSource=source(icon_path, symbol, binding))
 items=dict(sourceCommit=COMMIT,profile=PROFILE,previousItemCount=130,itemCount=131,additions=[addition],shopsAdded=[],icons=[icon])
 facts=dict(sourceCommit=COMMIT,stockId='CeladonCity_DepartmentStore_4F_Items',items=shelf_items,terminator='ITEM_NONE',source=source(shelf_path,'CeladonCity_DepartmentStore_4F_Items',shelf),clerkSource=source(shelf_path,'CeladonCity_DepartmentStore_4F_EventScript_Clerk',clerk),martLine=22,clerkActor=dict(sourceLocalId=3,**actor),clerkMapSource=dict(path=map_path),mailPreviewSource=source(use,'CB2_CheckMail',preview),mailFieldUseSource=source(use,'FieldUseFunc_Mail',field),mailComposeImplemented=False,mailBlankPreviewImplemented=False,inventoryAndSaleOnly=True,evolutionExecutionImplementedByThisImporter=False,evolutions=evolutions)
 evidence=dict(sourceCommit=COMMIT,inputs=[inputs[k] for k in sorted(inputs)],dependencyInputs=[])
 files = {BASE + 'items.json': encoded(items), BASE + 'source-input-hashes.json': encoded(evidence), BASE + 'source-contract.json': encoded(facts), icon['texturePath']: png, **raw}
 return files,items
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__)
 for arg in ['reference','dependency','output']:parser.add_argument(arg,type=Path)
 parser.add_argument('--check',action='store_true');args=parser.parse_args();files,items=generate(args.reference,args.dependency)
 for path,data in files.items():
  target=args.output/path
  if args.check:need(target.read_bytes()==data,'Regeneration differs: '+path)
  else:target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
 print(json.dumps(dict(checked=args.check,itemCount=131),indent=2))
