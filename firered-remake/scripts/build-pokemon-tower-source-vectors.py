#!/usr/bin/env python3
"""Build a tiny local oracle from exact pinned C functions; never execute reference scripts."""
import argparse, hashlib, json, pathlib, re, subprocess
BASE=pathlib.Path(__file__).resolve().parents[1]
COMMIT='037335f4c725d7c9aecdac87066f2002b4bd7e14'
a=argparse.ArgumentParser();a.add_argument('--reference-root',required=True);args=a.parse_args();ref=pathlib.Path(args.reference_root)
paths=['src/battle_script_commands.c','src/battle_util.c','src/battle_main.c','src/pokemon.c','src/random.c','include/random.h','data/battle_scripts_1.s','src/data/battle_moves.h','src/wild_encounter.c','src/battle_setup.c','src/data/pokemon/species_info.h','include/constants/battle.h']
source={p:subprocess.check_output(['git','-C',str(ref),'show',COMMIT+':'+p]) for p in paths}
lock={p:{'sha256':hashlib.sha256(b).hexdigest(),'length':len(b)} for p,b in source.items()}
lockpath=BASE/'tests/Game.PokemonTowerBattle.Tests/source-lock.json'
if lockpath.exists():assert json.loads(lockpath.read_text())=={'sourceCommit':COMMIT,'files':lock},'source-lock changed'
else:lockpath.write_text(json.dumps({'sourceCommit':COMMIT,'files':lock},indent=2)+'\n')
s={p:b.decode() for p,b in source.items()}
def fn(path,signature):
 t=s[path];start=t.index(signature+'\n{');brace=t.index('{',start);depth=1;end=brace+1
 while depth:
  depth += (t[end]=='{')-(t[end]=='}');end+=1
 return t[start:end]
spite=fn('src/battle_script_commands.c','static void Cmd_tryspiteppreduce(void)')
curse=fn('src/battle_script_commands.c','static void Cmd_cursetarget(void)')
bond=fn('src/battle_script_commands.c','static void TrySetDestinyBondToHappen(void)')
random=fn('src/random.c','u16 Random(void)')
# These precise fragments appear at the specified source sites, independently from the C# executor.
assert 'gBattleMoveDamage = gBattleMons[gActiveBattler].maxHP / 4;' in s['src/battle_util.c']
assert 'attack = (attack * (attackerHoldEffectParam + 100)) / 100;' in s['src/pokemon.c']
assert '*encounterRate = *encounterRate * 2 / 3;' in s['src/wild_encounter.c']
assert 'delta = (150 * delta) / 100;' in s['src/pokemon.c']
header=r'''#include <stdio.h>
#include <stdint.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32;
#define MOVE_NONE 0
#define MOVE_UNAVAILABLE 65535
#define MAX_MON_MOVES 4
#define STATUS2_TRANSFORMED 0x200000
#define STATUS2_CURSED 0x10000000
#define STATUS2_DESTINY_BOND 0x400000
#define HITMARKER_GRUDGE 1
#define HITMARKER_DESTINYBOND 2
#define BUFFER_A 0
#define REQUEST_PPMOVE1_BATTLE 0
#define STR_CONV_MODE_LEFT_ALIGN 0
#define PREPARE_MOVE_BUFFER(a,b)
#define PREPARE_BYTE_NUMBER_BUFFER(a,b,c)
#define T1_READ_PTR(x) (failure)
#define ISO_RANDOMIZE1(v) (1103515245U*(v)+24691U)
u32 gRngValue,gHitMarker;int gBattlerTarget=1,gBattlerAttacker=0,gActiveBattler=0,gBattleMoveDamage;
u16 gLastMoves[2];const u32 gBitTable[]={1,2,4,8};
struct {u16 moves[4];u8 pp[4];u32 status2;int maxHP;} gBattleMons[2];
struct {u8 mimickedMoves;} gDisableStructs[2];
u8 bytes[32],failure[32],gBattleTextBuff1[16],gBattleTextBuff2[16];const u8 *gBattlescriptCurrInstr;
int persisted,cancelled;
void ConvertIntToDecimalStringN(u8 *a,int b,int c,int d){}
void BtlController_EmitSetMonData(int a,int b,int c,int d,const void *e){persisted++;}
void MarkBattlerForControllerExec(int a){}
void CancelMultiTurnMoves(int a){cancelled++;}
int GetBattlerSide(int b){return b&1;}
'''
main=r'''
int main(void){
printf("{\"spite\":[");int first=1;
for(int seed=0;seed<32;seed++)for(int pp=0;pp<=5;pp++)for(int history=0;history<3;history++){
 memset(gBattleMons,0,sizeof gBattleMons);memset(gDisableStructs,0,sizeof gDisableStructs);gRngValue=seed;persisted=cancelled=0;gBattlescriptCurrInstr=bytes;
 gBattleMons[1].moves[0]=150;gBattleMons[1].pp[0]=pp;gLastMoves[1]=history==0?0:history==1?150:65535;Cmd_tryspiteppreduce();
 printf("%s[%d,%d,%d,%d,%u,%d,%d,%d]",first?"":",",seed,pp,history,gBattleMons[1].pp[0],gRngValue,persisted,cancelled,gBattlescriptCurrInstr!=failure);first=0;
}
printf("],\"curse\":[");first=1;for(int hp=1;hp<=255;hp++)for(int existing=0;existing<2;existing++){
 gBattleMons[0].maxHP=hp;gBattleMons[1].status2=existing?STATUS2_CURSED:0;gBattleMoveDamage=-1;gBattlescriptCurrInstr=bytes;Cmd_cursetarget();
 printf("%s[%d,%d,%d,%d]",first?"":",",hp,existing,gBattleMoveDamage,gBattlescriptCurrInstr!=failure);first=0;
}
printf("],\"bond\":[");first=1;for(int active=0;active<2;active++)for(int same=0;same<2;same++)for(int grudge=0;grudge<2;grudge++){
 gBattlerAttacker=same?1:0;gBattleMons[1].status2=active?STATUS2_DESTINY_BOND:0;gHitMarker=grudge?HITMARKER_GRUDGE:0;TrySetDestinyBondToHappen();
 printf("%s[%d,%d,%d,%d]",first?"":",",active,same,grudge,!!(gHitMarker&HITMARKER_DESTINYBOND));first=0;
}
printf("],\"marowak\":[");for(int seed=0;seed<32;seed++){
 gRngValue=seed;u32 pid;int draws=0;
 do{u32 low=Random();u32 high=Random();pid=low|(high<<16);draws+=2;}while(pid%25!=12||(pid&255)>=127);
 printf("%s[%d,%u,%u,%d]",seed?",":"",seed,pid,gRngValue,draws);
}
printf("],\"scriptRng\":[");first=1;for(int seed=0;seed<32;seed++)for(int mode=0;mode<5;mode++){
 gRngValue=seed;int draws=0;u16 roll;
 do{roll=Random();draws++;}while((roll&3)!=0);int choices=draws;int accuracy=-1,critical=-1,damage=-1,secondary=-1,pp=-1;
 /* Source script schedules: Spite, Curse, NightShade, ShadowPunch, Bonemerang. */
 if(mode==0){accuracy=Random();pp=Random();draws+=2;}
 if(mode==2){accuracy=Random();secondary=Random();draws+=2;}
 if(mode==3){critical=Random();damage=Random();secondary=Random();draws+=3;}
 if(mode==4){accuracy=Random();draws++;if(accuracy%100+1<=90){critical=Random();damage=Random();Random();Random();secondary=Random();draws+=5;}}
 int ready=Random();draws++;
 printf("%s[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d]",first?"":",",seed,mode,choices,accuracy,critical,damage,secondary,pp,ready,draws,gRngValue,mode==4&&accuracy%100+1>90);first=0;
}
printf("]}\n");return 0;
}
'''
output=BASE/'tests/Game.PokemonTowerBattle.Tests';output.mkdir(parents=True,exist_ok=True)
cpath=output/'source-vectors.c';cpath.write_text(header+random+'\n'+spite+'\n'+curse+'\n'+bond+'\n'+main)
subprocess.run(['flock','/tmp/firered-remake-build.lock','cc','-std=c99','-O0',str(cpath),'-o',str(BASE/'tests/Game.PokemonTowerBattle.Tests/source-vectors.local')],check=True)
data=json.loads(subprocess.check_output([str(BASE/'tests/Game.PokemonTowerBattle.Tests/source-vectors.local')]))
data={'sourceCommit':COMMIT,'sources':lock,**data}
(output/'source-vectors.json').write_text(json.dumps(data,separators=(',',':'))+'\n')
print(json.dumps({'sourceFiles':len(source),'spiteVectors':len(data['spite']),'curseVectors':len(data['curse']),'bondVectors':len(data['bond']),'marowakVectors':len(data['marowak']),'scriptRngVectors':len(data['scriptRng'])}))
