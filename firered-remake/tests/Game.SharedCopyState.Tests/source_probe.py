#!/usr/bin/env python3
"""Authenticate and compile unchanged original copy commands with bounded data/controller shims."""
import hashlib,json,re,subprocess,sys
from pathlib import Path
root,out=map(Path,sys.argv[1:]);out.mkdir(parents=True,exist_ok=True)
pin='037335f4c725d7c9aecdac87066f2002b4bd7e14'
paths=['src/battle_script_commands.c','src/battle_main.c','data/battle_scripts_1.s','include/battle.h','include/constants/moves.h','src/data/battle_moves.h','include/constants/battle.h']
texts={};hashes={}
for path in paths:
 b=(root/path).read_bytes();assert b==subprocess.check_output(['git','show',pin+':'+path],cwd=root),path
 texts[path]=b.decode();hashes[path]=hashlib.sha256(b).hexdigest()
src=texts[paths[0]]
names=['Cmd_transformdataexecution','IsMoveUncopyableByMimic','Cmd_mimicattackcopy','Cmd_copymovepermanently']
fragments=[re.search(r'^static (?:void|bool8) '+name+r'\([^\n]*\)\n\{.*?^\}',src,re.M|re.S).group() for name in names]
shims=r'''
#include <stdint.h>
#include <stdio.h>
#include <stddef.h>
#include <string.h>
typedef uint8_t u8;typedef uint16_t u16;typedef uint32_t u32;typedef int32_t s32;typedef uint8_t bool8;
#define MAX_MON_MOVES 4
#define STATUS2_TRANSFORMED 0x200000
#define STATUS3_SEMI_INVULNERABLE ((1 << 6) | (1 << 7) | (1 << 18))
#define MOVE_RESULT_FAILED 1
#define MULTISTRING_CHOOSER 0
#define B_MSG_TRANSFORM_FAILED 1
#define B_MSG_TRANSFORMED 2
#define BUFFER_A 0
#define RESET_MOVE_SELECTION 0
#define REQUEST_MOVES_PP_BATTLE 0
#define PREPARE_SPECIES_BUFFER(a,b) ((void)0);
#define PREPARE_MOVE_BUFFER(a,b) ((void)0);
#define T1_READ_PTR(x) failScript
#define MIMIC_FORBIDDEN_END 0xFFFE
#define METRONOME_FORBIDDEN_END 0xFFFF
struct BattlePokemon {
 u16 species,attack,defense,speed,spAttack,spDefense,moves[4];u32 ivBits;int8_t statStages[8];
 u8 ability,type1,type2,unknown,pp[4];u16 hp;u8 level,friendship;u16 maxHP,item;
 u8 nickname[11],ppBonuses,otName[8];u32 experience,personality,status1,status2,otId;
};
_Static_assert(offsetof(struct BattlePokemon,pp)==0x24,"source copy boundary");
struct MovePpInfo {u16 moves[4];u8 pp[4];u8 ppBonuses;};
struct {u8 pp;} gBattleMoves[355];struct BattlePokemon gBattleMons[4];
struct {u16 disabledMove;u8 disableTimer;u32 transformedMonPersonality;u8 mimickedMoves;} gDisableStructs[4];
u16 gChosenMove,gLastMoves[4],gLastPrintedMoves[4];u32 gStatuses3[4];u8 gBattleCommunication[8],gMoveResultFlags,gBattlerAttacker,gBattlerTarget,gActiveBattler,gCurrMovePos;
u8 script[64],failScript[64],*gBattlescriptCurrInstr;const u32 gBitTable[]={1,2,4,8};struct MovePpInfo partyWrite;int writeCount;
void BtlController_EmitResetActionMoveSelection(int a,int b){(void)a;(void)b;}void MarkBattlerForControllerExec(int a){(void)a;}
void BtlController_EmitSetMonData(int a,int b,int c,int size,void *data){(void)a;(void)b;(void)c;(void)size;memcpy(&partyWrite,data,sizeof partyWrite);writeCount++;}
'''
defines='\n'.join(line for line in texts['include/constants/moves.h'].splitlines() if re.match(r'#define MOVE_\w+\s+[0-9]+',line))
forbidden=re.search(r'static const u16 sMovesForbiddenToCopy\[\] =\n\{.*?^\};',src,re.M|re.S).group()
main=r'''
void setup(int actor,int target){memset(gBattleMons,0,sizeof gBattleMons);memset(gDisableStructs,0,sizeof gDisableStructs);memset(gStatuses3,0,sizeof gStatuses3);memset(gLastMoves,0,sizeof gLastMoves);memset(gLastPrintedMoves,0,sizeof gLastPrintedMoves);memset(&partyWrite,0,sizeof partyWrite);writeCount=0;gMoveResultFlags=0;gBattlerAttacker=actor;gBattlerTarget=target;gCurrMovePos=0;gBattlescriptCurrInstr=script;gChosenMove=MOVE_MIMIC;gBattleMons[actor].moves[0]=MOVE_MIMIC;gBattleMons[actor].pp[0]=9;gBattleMons[actor].ppBonuses=3;}
int main(void){for(int i=0;i<355;i++)gBattleMoves[i].pp=35;gBattleMoves[MOVE_SKETCH].pp=1;gBattleMoves[MOVE_GROWL].pp=40;gBattleMoves[MOVE_TRANSFORM].pp=10;
printf("{\"copy\":[");int comma=0;
int vals[]={MOVE_NONE,MOVE_UNAVAILABLE,MOVE_STRUGGLE,MOVE_SKETCH,MOVE_MIMIC,MOVE_METRONOME,MOVE_TACKLE};
for(int mode=0;mode<2;mode++)for(int a=0;a<4;a++)for(int k=0;k<7;k++)for(int transformed=0;transformed<2;transformed++){
setup(a,a^1);gBattleMons[a].moves[0]=mode?MOVE_SKETCH:MOVE_MIMIC;gBattleMons[a].pp[0]=mode?0:transformed?4:9;gLastMoves[a^1]=gLastPrintedMoves[a^1]=vals[k];if(transformed)gBattleMons[a].status2|=STATUS2_TRANSFORMED;
if(mode)Cmd_copymovepermanently();else Cmd_mimicattackcopy();
printf("%s{\"mode\":%d,\"actor\":%d,\"prior\":%d,\"transformed\":%d,\"success\":%s,\"move\":%d,\"pp\":%d,\"mask\":%d,\"chosen\":%d,\"writes\":%d}",comma++?",":"",mode,a,vals[k],transformed,gBattlescriptCurrInstr!=failScript?"true":"false",gBattleMons[a].moves[0],gBattleMons[a].pp[0],gDisableStructs[a].mimickedMoves,gChosenMove,writeCount);
}
printf("],\"transform\":[");comma=0;for(int a=0;a<4;a++)for(int blocked=0;blocked<3;blocked++){
setup(a,a^1);struct BattlePokemon *o=&gBattleMons[a],*d=&gBattleMons[a^1];o->species=132;o->hp=17;o->maxHP=61;o->level=20;o->personality=123;o->otId=42;o->status1=8;o->item=0;d->species=94;d->ability=26;d->type1=d->type2=11;d->attack=170;d->moves[0]=MOVE_TACKLE;d->moves[1]=MOVE_SKETCH;d->moves[2]=MOVE_TRANSFORM;d->personality=456;gDisableStructs[a].mimickedMoves=3;if(blocked==1)d->status2|=STATUS2_TRANSFORMED;if(blocked==2)gStatuses3[a^1]=1 << 7;
Cmd_transformdataexecution();printf("%s{\"actor\":%d,\"blocked\":%d,\"success\":%s,\"species\":%d,\"hp\":%d,\"maxHP\":%d,\"personality\":%u,\"pp\":[%d,%d,%d],\"mask\":%d}",comma++?",":"",a,blocked,gMoveResultFlags?"false":"true",o->species,o->hp,o->maxHP,o->personality,o->pp[0],o->pp[1],o->pp[2],gDisableStructs[a].mimickedMoves);
}printf("]}\n");return 0;}
'''
pp_init=''.join('gBattleMoves['+name+'].pp='+pp+';' for name,pp in re.findall(r'\[(MOVE_\w+)\]\s*=\s*\{.*?\.pp\s*=\s*(\d+)',texts['src/data/battle_moves.h'],re.S))
assert len(re.findall(r'gBattleMoves',pp_init))==355
main=main.replace('for(int i=0;i<355;i++)gBattleMoves[i].pp=35;gBattleMoves[MOVE_SKETCH].pp=1;gBattleMoves[MOVE_GROWL].pp=40;gBattleMoves[MOVE_TRANSFORM].pp=10;',pp_init)
code=shims+defines+'\n'+forbidden+'\n'+'\n\n'.join(fragments)+'\n'+main
(out/'source-oracle.c').write_text(code)
for suffix,flags in [('optimized',['-O2']),('ubsan',['-O1','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
 subprocess.run(['cc','-std=c11','-Wall','-Wextra','-Werror','-Wno-sign-compare',*flags,str(out/'source-oracle.c'),'-o',str(out/suffix)],check=True)
 result=subprocess.check_output([str(out/suffix)]);json.loads(result);(out/(suffix+'.json')).write_bytes(result)
assert (out/'optimized.json').read_bytes()==(out/'ubsan.json').read_bytes()
checks=['four unchanged source command fragments compiled','112 Mimic/Sketch source outcomes','12 Transform source outcomes','optimized and UBSan outputs identical']
(out/'manifest.json').write_text(json.dumps({'passed':True,'sourceCommit':pin,'sourceHashes':hashes,'fragmentHashes':dict(zip(names,[hashlib.sha256(s.encode()).hexdigest() for s in fragments])),'checks':checks},indent=2)+'\n')
print('PASS source command extraction/oracle')
