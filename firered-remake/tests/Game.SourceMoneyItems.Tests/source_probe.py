#!/usr/bin/env python3
"""Compile only locally extracted pinned source money expressions, never reference scripts."""
from pathlib import Path
import hashlib,json,re,subprocess,sys
reference,out=map(Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=True)
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==commit
sources={};pins={}
for name in ['src/battle_util.c','src/battle_main.c','src/battle_script_commands.c','include/battle_message.h']:
 b=subprocess.check_output(['git','show',commit+':'+name],cwd=reference);assert b==(reference/name).read_bytes()
 sources[name]=b.decode();pins[name]=hashlib.sha256(b).hexdigest()
util=sources['src/battle_util.c'];commands=sources['src/battle_script_commands.c']
entry=re.search(r'case HOLD_EFFECT_DOUBLE_PRIZE:\s*(gBattleStruct->moneyMultiplier = 2;)\s*break;',util).group(1)
assert 'gBattleStruct->moneyMultiplier = 1;' in sources['src/battle_main.c']
prize=re.search(r'moneyReward = (4 \* lastMonLevel \* gBattleStruct->moneyMultiplier[^;]+);',commands).group(1)
payday=re.search(r'u32 bonusMoney = ([^;]+);',commands[commands.index('static void Cmd_givepaydaymoney(void)\n{'):]).group(1)
macro=sources['include/battle_message.h'];hword=macro[macro.index('#define PREPARE_HWORD_NUMBER_BUFFER'):macro.index('#define PREPARE_WORD_NUMBER_BUFFER')]
assert 'textVar[2] = 2;' in hword and 'textVar[5] = (number & 0x0000FF00) >> 8;' in hword
c='''#include <stdint.h>
#include <stdio.h>
#define BATTLE_TYPE_DOUBLE 1
struct { int moneyMultiplier; } battle, *gBattleStruct=&battle;
struct {int value;} gTrainerMoneyTable[]={{6}};
int gBattleTypeFlags,lastMonLevel=11,i=0;uint16_t gPaydayMoney;
void Entry(void){ENTRY}
int main(void){int first=1;puts("[");int wallets[]={0,999990,999999},coins[]={0,1,125,32767,32768,65535};
for(int multi=1;multi<=2;multi++)for(int w=0;w<3;w++){
 gBattleStruct->moneyMultiplier=1;if(multi==2){Entry();Entry();}if(gBattleStruct->moneyMultiplier!=multi)return 3;
 for(int k=0;k<6;k++){gPaydayMoney=coins[k];uint32_t amount=PAYDAY;uint32_t money=wallets[w]+amount;if(money>999999)money=999999;
 printf("%s{\\"kind\\":\\"payday\\",\\"wallet\\":%d,\\"raw\\":%d,\\"multiplier\\":%d,\\"amount\\":%u,\\"display\\":%u,\\"money\\":%u}",first?"":",",wallets[w],coins[k],multi,amount,(uint16_t)amount,money);first=0;}
 for(gBattleTypeFlags=0;gBattleTypeFlags<2;gBattleTypeFlags++){uint32_t amount=PRIZE;uint32_t money=wallets[w]+amount;if(money>999999)money=999999;
 printf(",{\\"kind\\":\\"prize\\",\\"wallet\\":%d,\\"raw\\":%d,\\"multiplier\\":%d,\\"amount\\":%u,\\"display\\":%u,\\"money\\":%u}",wallets[w],264*(gBattleTypeFlags?2:1),multi,amount,amount,money);}}
puts("]");return 0;}
'''.replace('ENTRY',entry).replace('PAYDAY',payday).replace('PRIZE',prize)
(out/'source.c').write_text(c)
subprocess.run(['cc','-std=c99','-Wall','-Wextra','-Werror',str(out/'source.c'),'-o',str(out/'source')],check=True)
vectors=json.loads(subprocess.check_output([str(out/'source')],text=True));assert len(vectors)==48
result={'sourceCommit':commit,'pins':pins,'vectors':vectors}
(out/'source-vectors.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'passed':True,'vectors':len(vectors),'pins':pins}))
