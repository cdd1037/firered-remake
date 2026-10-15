#!/usr/bin/env python3
"""Pinned local source import. Never executes scripts from the reference checkout."""
import hashlib, json, re, subprocess, sys
from pathlib import Path
PIN='037335f4c725d7c9aecdac87066f2002b4bd7e14'
INPUTS={
'src/data/items.json':'d962ec3ea31391f9a30fa7d12bfb47f6519407bc2aad395c99baec52430c1489',
'include/constants/items.h':'af17276346eaecc5a10ac60f14b22c33332ec8c6fb0ec82895bdc3649b678138',
'src/data/pokemon/item_effects.h':'d249420ab4751c60b1ae246895dcee5bdfb5bae18f52a9c37975c6f7fb5d0d25',
'include/constants/item_effects.h':'71075ef690c612f81cb68d5fae62874e39d452208cc1e94890c3973f774fd0f9',
'src/pokemon.c':'116a470cd9ebfa226aa8d85c2eb63591e176d726837c67baaa1ac6260d50f2ec',
'src/item_use.c':'035ca3bdb70f5708dc8771d2bc6cc5450e775e3144278e7683aacba218cb9c98',
'src/party_menu.c':'8290dfd5b6444e743029ab76543ed5e4b5b32c1c2f475c686946545f934d5f9b',
'src/battle_main.c':'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928',
'src/battle_util.c':'87cf810eb2d58361d1beb1537b3bd71c9160a77c2a17d703399f6ed373bb5720',
'data/battle_scripts_2.s':'83e1211907cc9a21cd1d94e65a065af5abf683d4e88e7ee690e78faf40651133'}
def sha(b): return hashlib.sha256(b).hexdigest()
def main():
 reference,root=map(Path,sys.argv[1:]); assert subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()==PIN
 inputs=[]
 for path,expected in INPUTS.items():
  b=(reference/path).read_bytes()
  inputs.append(dict(path=path,byteLength=len(b),sha256=expected,gitBlobSha1=hashlib.sha1(b'blob '+str(len(b)).encode()+b'\0'+b).hexdigest()))
 defs={}
 for name,value in re.findall(r'^#define\s+(ITEM_\w+)\s+(\d+)\s*$',(reference/'include/constants/items.h').read_text(),re.M):defs[name]=int(value)
 rows=json.loads((reference/'src/data/items.json').read_text())['items']; effects=(reference/'src/data/pokemon/item_effects.h').read_text()
 # Compile the actual original initialized arrays; this imports the C preprocessor expressions without a second expression implementation.
 pointers=dict(re.findall(r'\[(ITEM_\w+) - ITEM_POTION\]\s*=\s*(sItemEffect_\w+)',effects))
 admitted=[r for r in rows if r['battleUseFunc'] in ('BattleUseFunc_Medicine','BattleUseFunc_Ether','BattleUseFunc_StatBooster')]
 generated=root/'evidence/c15-battle-items/import';generated.mkdir(parents=True,exist_ok=True)
 source='#include <stdio.h>\n#include <stdint.h>\n#include <stddef.h>\ntypedef uint8_t u8;\n#include "constants/items.h"\n#include "constants/item_effects.h"\n#include "'+str(reference/'src/data/pokemon/item_effects.h')+'"\nint main(void){\n'
 for r in admitted:
  arr=pointers[r['itemId']]; source+='printf("'+r['itemId']+'"); for(size_t i=0;i<sizeof('+arr+');i++) printf(" %u",'+arr+'[i]); puts("");\n'
 source+='return 0;}\n'; (generated/'arrays.c').write_text(source)
 subprocess.run(['cc','-std=c11','-O2','-I'+str(reference/'include'),str(generated/'arrays.c'),'-o',str(generated/'arrays')],check=True)
 arrays={parts[0]:list(map(int,parts[1:])) for parts in map(str.split,subprocess.check_output([str(generated/'arrays')],text=True).splitlines())}
 items=[]
 for row in rows:
  symbol=row['itemId']
  if symbol=='ITEM_NONE':continue
  item=dict(symbol=symbol,sourceItemId=defs[symbol],name=row['english'],pocket=row['pocket'],battleUsage=row['battleUsage'],battleUseFunction=row['battleUseFunc'],holdEffect=row['holdEffect'],holdEffectParam=row['holdEffectParam'],effectBytes=arrays.get(symbol,[]),reusable=symbol in ('ITEM_BLUE_FLUTE','ITEM_RED_FLUTE','ITEM_YELLOW_FLUTE','ITEM_POKE_FLUTE'))
  items.append(item)
 assert len({x['symbol'] for x in items})==len(items)
 items.sort(key=lambda x:x['sourceItemId'])
 semantic=PIN+'\n'+''.join('|'.join([x['symbol'],str(x['sourceItemId']),x['name'],x['pocket'],str(x['battleUsage']),x['battleUseFunction'],','.join(map(str,x['effectBytes'])),'1' if x['reusable'] else '0',x['holdEffect'],str(x['holdEffectParam'])])+'\n' for x in items)
 doc=dict(sourceCommit=PIN,semanticSha256=sha(semantic.encode()),items=items)
 output=(json.dumps(doc,ensure_ascii=False,separators=(',',':'))+'\n').encode(); path=root/'content/generated/source-battle-items/catalog.json';path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(output)
 manifest=dict(sourceCommit=PIN,inputs=inputs,output=dict(path='content/generated/source-battle-items/catalog.json',byteLength=len(output),sha256=sha(output)),semanticSha256=doc['semanticSha256'],rawRows=len(rows),identities=len(items),fixedBattleEffects=len(admitted)+1,battleUsable=sum(bool(r['battleUsage']) for r in rows),upstreamScriptsExecuted=False)
 print(json.dumps(manifest,indent=2))
if __name__=='__main__':main()
