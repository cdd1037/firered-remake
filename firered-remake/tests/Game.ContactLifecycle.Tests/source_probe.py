#!/usr/bin/env python3
"""Build a bounded host oracle from authenticated original FireRed contact C.

Usage: source_probe.py PINNED_REFERENCE OUTPUT_DIRECTORY
The generated contact-oracle accepts one numeric row as argv, or whitespace rows
on stdin. Input order is FIELDS below; every output is a JSON object. This script
is local test code. No script from the reference repository is executed.
"""
import argparse
import collections
import hashlib
import json
from pathlib import Path
import re
import subprocess

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
FIELDS = ("id seed attacker target defenderAbility moveFlags resultFlags confusionSelfDamage "
          "physicalDmg specialDmg attackerHp attackerMaxHp targetHp attackerStatus attackerStatus2 "
          "attackerAbility type1 type2 safeguard uproarMask initialDamage repeatCount").split()
HEADERS = ["include/constants/abilities.h", "include/constants/pokemon.h", "include/constants/battle.h",
           "include/constants/battle_script_commands.h", "include/constants/moves.h",
           "include/constants/battle_string_ids.h"]
SOURCES = ["src/battle_util.c", "src/battle_script_commands.c", "src/random.c", "include/random.h",
           "include/battle.h", "data/battle_scripts_1.s", "asm/macros/battle_script.inc", *HEADERS]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def extract(source, signature):
    match = re.search(rb"(?m)^" + re.escape(signature.encode()) + rb"\s*\n\{", source)
    assert match, signature
    pos, depth, state = source.index(b"{", match.start()), 0, "code"
    while pos < len(source):
        ch, pair = source[pos:pos+1], source[pos:pos+2]
        if state == "line":
            if ch == b"\n": state = "code"
        elif state == "comment":
            if pair == b"*/": state = "code"; pos += 1
        elif state in ("string", "char"):
            if ch == b"\\": pos += 1
            elif ch == (b'"' if state == "string" else b"'"): state = "code"
        elif pair == b"//": state = "line"; pos += 1
        elif pair == b"/*": state = "comment"; pos += 1
        elif ch == b'"': state = "string"
        elif ch == b"'": state = "char"
        elif ch == b"{": depth += 1
        elif ch == b"}":
            depth -= 1
            if depth == 0: return source[match.start():pos+1]
        pos += 1
    raise AssertionError(signature)


PREFIX = r'''
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <errno.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
typedef int32_t s32; typedef uint8_t bool8; typedef uint32_t bool32;
#define TRUE 1
#define FALSE 0
/* CONSTANTS */
struct Mon {u16 hp,maxHP; u32 status1,status2; u8 ability,type1,type2;} gBattleMons[4];
struct Protect {s32 physicalDmg,specialDmg; u8 physicalBattlerId,specialBattlerId,confusionSelfDmg;} gProtectStructs[4];
struct Disable {u16 substituteHP; u8 rolloutTimer,furyCutterCounter;} gDisableStructs[4];
struct Scripting {u8 battler;} gBattleScripting;
struct Battle {u8 dynamicMoveType,synchronizeMoveEffect;} battle,*gBattleStruct=&battle;
struct Move {u8 flags,type;} gBattleMoves[1];
struct SpecialStatus gSpecialStatuses[4];
u8 gBattlerAttacker,gBattlerTarget,gEffectBattler,gActiveBattler,gLastUsedAbility,gMoveResultFlags;
u8 gBattleCommunication[8],gBattlersCount=4,gTakenDmgByBattler[4];
u16 gCurrentMove;
u32 gHitMarker,gSideStatuses[2],gBattleTypeFlags,gBattleControllerExecFlags,gStatuses3[4],gTakenDmg[4],gRngValue;
s32 gBattleMoveDamage,gHpDealt;
static u8 command[8];
static const u8 BattleScript_RoughSkinActivates[8]={0},BattleScript_ApplySecondaryEffect[8]={0};
static const u8 BattleScript_PSNPrevention[8]={0},BattleScript_BRNPrevention[8]={0},BattleScript_PRLZPrevention[8]={0};
static const u8 BattleScript_SubstituteFade[8]={0};
static const u8 BattleScript_MoveEffectSleep[8]={0},BattleScript_MoveEffectPoison[8]={0};
static const u8 BattleScript_MoveEffectBurn[8]={0},BattleScript_MoveEffectParalysis[8]={0};
static const u8 *const sMoveEffectBS_Ptrs[8]={
 [MOVE_EFFECT_SLEEP]=BattleScript_MoveEffectSleep,[MOVE_EFFECT_POISON]=BattleScript_MoveEffectPoison,
 [MOVE_EFFECT_BURN]=BattleScript_MoveEffectBurn,[MOVE_EFFECT_PARALYSIS]=BattleScript_MoveEffectParalysis};
const u8 *gBattlescriptCurrInstr;
static unsigned drawCount,pushCount,statusWrites,hpWrites,cancelCalls,effectCalls,effectTotal,faintCalls;
static u16 draws[4096];
static u8 GetBattlerPosition(u8 battler){return battler;}
static u8 GetBattlerSide(u8 battler){return battler&1;}
static u8 GetBattlerForBattleScript(u8 battler){
 if(battler==BS_ATTACKER)return gBattlerAttacker;
 if(battler==BS_TARGET)return gBattlerTarget;
 fputs("unsupported script battler\n",stderr);exit(3);
}
static void BattleScriptPushCursor(void){pushCount++;}
static void BattleScriptPush(const u8 *p){(void)p;pushCount++;}
static void RecordAbilityBattle(u8 battler,u8 ability){(void)battler;(void)ability;}
#define BUFFER_A 0
#define REQUEST_STATUS_BATTLE 1
#define REQUEST_HP_BATTLE 2
static void BtlController_EmitSetMonData(u8 a,u8 request,u8 c,unsigned size,const void *p){
 (void)a;(void)c;(void)size;(void)p;
 if(request==REQUEST_STATUS_BATTLE)statusWrites++;
 if(request==REQUEST_HP_BATTLE)hpWrites++;
}
static void MarkBattlerForControllerExec(u8 battler){(void)battler;}
#define INCREMENT_RETURN {gBattlescriptCurrInstr++;return;}
'''


MAIN = r'''
static void run(long long *v){
 memset(gBattleMons,0,sizeof(gBattleMons));memset(gProtectStructs,0,sizeof(gProtectStructs));
 memset(gDisableStructs,0,sizeof(gDisableStructs));memset(gSpecialStatuses,0,sizeof(gSpecialStatuses));
 memset(gSideStatuses,0,sizeof(gSideStatuses));memset(gStatuses3,0,sizeof(gStatuses3));
 memset(gTakenDmg,0,sizeof(gTakenDmg));memset(gTakenDmgByBattler,0,sizeof(gTakenDmgByBattler));
 memset(gBattleCommunication,0,sizeof(gBattleCommunication));memset(&battle,0,sizeof(battle));
 drawCount=pushCount=statusWrites=hpWrites=cancelCalls=effectCalls=effectTotal=faintCalls=0;
 gHitMarker=0;gBattleTypeFlags=0;gBattleControllerExecFlags=0;gHpDealt=0;
 gEffectBattler=gBattleScripting.battler=gActiveBattler=255;
 for(unsigned i=0;i<4;i++){gBattleMons[i].hp=70+i;gBattleMons[i].maxHP=100+i;}
 gRngValue=v[1];gBattlerAttacker=v[2];gBattlerTarget=v[3];gLastUsedAbility=v[4];
 gBattleMons[gBattlerTarget].ability=v[4];gBattleMoves[0].flags=v[5];gMoveResultFlags=v[6];
 gProtectStructs[gBattlerAttacker].confusionSelfDmg=v[7];
 gSpecialStatuses[gBattlerTarget].physicalDmg=v[8];gSpecialStatuses[gBattlerTarget].specialDmg=v[9];
 gBattleMons[gBattlerAttacker].hp=v[10];gBattleMons[gBattlerAttacker].maxHP=v[11];
 gBattleMons[gBattlerTarget].hp=v[12];gBattleMons[gBattlerAttacker].status1=v[13];
 gBattleMons[gBattlerAttacker].status2=v[14];gBattleMons[gBattlerAttacker].ability=v[15];
 gBattleMons[gBattlerAttacker].type1=v[16];gBattleMons[gBattlerAttacker].type2=v[17];
 if(v[18])gSideStatuses[gBattlerAttacker&1]=SIDE_STATUS_SAFEGUARD;
 for(unsigned i=0;i<4;i++)if(v[19]&(1<<i))gBattleMons[i].status2|=STATUS2_UPROAR;
 gBattleMoveDamage=v[20];gCurrentMove=0;gBattleMoves[0].type=TYPE_NORMAL;
 for(int phase=0;phase<v[21];phase++){
  gBattlescriptCurrInstr=command;gLastUsedAbility=v[4];
  unsigned effect=ProbeContact();effectTotal+=effect;
  if(effect&&gBattlescriptCurrInstr==BattleScript_RoughSkinActivates){
   gHitMarker|=HITMARKER_IGNORE_SUBSTITUTE|HITMARKER_PASSIVE_DAMAGE;
   command[1]=BS_ATTACKER;gBattlescriptCurrInstr=command;Cmd_datahpupdate();
   if(gBattleMons[gBattlerAttacker].hp==0)faintCalls++;
  }else if(effect&&gBattlescriptCurrInstr==BattleScript_ApplySecondaryEffect){
   effectCalls++;Cmd_seteffectsecondary();
  }
 }
 printf("{\"id\":%lld,\"effect\":%u,\"drawCount\":%u,\"rng\":%u,\"damage\":%d,\"hpDealt\":%d,",v[0],effectTotal,drawCount,gRngValue,gBattleMoveDamage,gHpDealt);
 printf("\"moveEffect\":%u,\"hitMarker\":%u,\"effectBattler\":%u,\"scriptingBattler\":%u,\"activeBattler\":%u,",gBattleCommunication[MOVE_EFFECT_BYTE],gHitMarker,gEffectBattler,gBattleScripting.battler,gActiveBattler);
 printf("\"statusWrites\":%u,\"hpWrites\":%u,\"pushCount\":%u,\"cancelCalls\":%u,\"setEffectCalls\":%u,\"faintCalls\":%u,\"synchronizeMoveEffect\":%u,",statusWrites,hpWrites,pushCount,cancelCalls,effectCalls,faintCalls,battle.synchronizeMoveEffect);
 printf("\"consumedDraws\":[");for(unsigned i=0;i<drawCount;i++)printf("%s%u",i?",":"",draws[i]);printf("],\"slots\":[");
 for(unsigned i=0;i<4;i++)printf("%s{\"hp\":%u,\"status\":%u,\"status2\":%u,\"receipt\":%d,\"physicalDmg\":%d,\"specialDmg\":%d,\"takenDamage\":%u}",i?",":"",gBattleMons[i].hp,gBattleMons[i].status1,gBattleMons[i].status2,gSpecialStatuses[i].dmg,gSpecialStatuses[i].physicalDmg,gSpecialStatuses[i].specialDmg,gTakenDmg[i]);
 puts("]}");
}
int main(int argc,char **argv){
 long long values[22];char *end;
 if(argc!=1&&argc!=23){fputs("expected 22 fields\n",stderr);return 2;}
 if(argc==23){for(unsigned i=0;i<22;i++){errno=0;values[i]=strtoll(argv[i+1],&end,10);if(errno||*end)return 2;}}
 do{
  if(argc==1){for(unsigned i=0;i<22;i++){int n=scanf("%lld",&values[i]);if(n==EOF&&i==0)return 0;if(n!=1)return 2;}}
  if(values[1]<0||values[1]>UINT32_MAX||values[2]<0||values[2]>3||values[3]<0||values[3]>3||values[2]==values[3]||values[21]<1||values[21]>8)return 2;
  if(values[4]!=ABILITY_ROUGH_SKIN&&values[4]!=ABILITY_EFFECT_SPORE&&values[4]!=ABILITY_POISON_POINT&&values[4]!=ABILITY_FLAME_BODY)return 2;
  if(values[10]<0||values[10]>65535||values[11]<1||values[11]>65535||values[12]<0||values[12]>65535)return 2;
  if(values[5]<0||values[5]>255||values[6]<0||values[6]>255||values[15]<0||values[15]>=ABILITIES_COUNT)return 2;
  run(values);
 }while(argc==1);
 return 0;
}
'''


def vectors():
    result = []
    def add(group, name, **changes):
        row = dict(zip(FIELDS, [len(result), 0, 0, 1, 24, 1, 0, 0, 16, 0, 50, 100, 80,
                               0, 0, 0, 0, 0, 0, 0, 16, 1]))
        row.update(changes)
        assert set(row) == set(FIELDS)
        result.append({"group": group, "name": name, "input": row})

    # Choose seeds from original LCG outputs; expectations always come from C.
    def stream(seed, count=12):
        values = []
        for _ in range(count):
            seed = (1103515245 * seed + 24691) & 0xffffffff
            values.append(seed >> 16)
        return values
    chosen = {}
    for seed in range(100000):
        draws = stream(seed)
        chosen.setdefault("third_" + str(draws[0] % 3), seed)
        chosen.setdefault("tenth_" + str(draws[0] % 10), seed)
        if draws[0] % 10 == 0:
            nonzero = next((i for i in range(1, len(draws)) if draws[i] & 3), None)
            if nonzero:
                chosen.setdefault("spore_" + str(draws[nonzero] & 3), seed)
                if nonzero > 1: chosen.setdefault("spore_reroll_" + str(draws[nonzero] & 3), seed)
                if draws[nonzero] & 3 == 1 and nonzero + 1 < len(draws):
                    chosen.setdefault("sleep_duration_" + str((draws[nonzero+1] & 3)+2), seed)
        if len(chosen) == 23: break
    assert all(k in chosen for k in ["third_0", "third_1", "third_2", "spore_1", "spore_2", "spore_3", "spore_reroll_1"])
    for ability, label in [(24,"rough_skin"),(27,"effect_spore"),(38,"poison_point"),(49,"flame_body")]:
        seed = chosen["spore_1" if ability == 27 else "third_0"]
        base = {"defenderAbility": ability, "seed": seed}
        for attacker in range(4):
            for target in range(4):
                if attacker != target: add(label, f"{label}_slots_{attacker}_{target}", **(base | {"attacker":attacker,"target":target}))
        for name, changes in [
            ("miss", {"resultFlags":1}), ("immune", {"resultFlags":8}), ("failed", {"resultFlags":32}),
            ("super_effective", {"resultFlags":2}), ("no_contact", {"moveFlags":0}),
            ("attacker_fainted", {"attackerHp":0}), ("holder_fainted", {"targetHp":0}),
            ("confusion_self_damage", {"confusionSelfDamage":1}), ("no_turn_damage", {"physicalDmg":0}),
            ("special_turn_damage", {"physicalDmg":0,"specialDmg":16}),
            ("zero_current_damage_retains_receipt", {"initialDamage":0}),
            ("already_poisoned", {"attackerStatus":8}), ("already_asleep", {"attackerStatus":3}),
            ("shield_dust", {"attackerAbility":19}), ("safeguard", {"safeguard":1}),
            ("attacker_substitute", {"attackerStatus2":0x1000000}),
            ("three_callbacks_same_receipt", {"repeatCount":3}),
        ]: add(label, label + "_" + name, **(base | changes))
    for maximum in [1,15,16,31,32,100,160,65535]:
        add("rough_skin", f"rough_skin_maxhp_{maximum}", attackerMaxHp=maximum, attackerHp=min(50,maximum))
    for hp in [1,6,7]: add("rough_skin", f"rough_skin_attacker_hp_{hp}", attackerHp=hp)
    for ability,label in [(38,"poison_point"),(49,"flame_body")]:
        for remainder in range(3): add(label,f"{label}_modulo_{remainder}",defenderAbility=ability,seed=chosen["third_"+str(remainder)])
    for remainder in range(10):
        add("effect_spore",f"effect_spore_modulo_{remainder}",defenderAbility=27,seed=chosen["tenth_"+str(remainder)])
    for choice in [1,2,3]:
        for reroll in [False,True]:
            key="spore_"+("reroll_" if reroll else "")+str(choice)
            add("effect_spore",key,defenderAbility=27,seed=chosen[key])
    for turns in range(2,6):
        add("effect_spore",f"sleep_duration_{turns}",defenderAbility=27,seed=chosen[f"sleep_duration_{turns}"])
    add("status_guards","sleep_cancels_locked_moves",defenderAbility=27,seed=chosen["spore_1"],attackerStatus2=0x1f00)
    add("status_guards","spore_grass_type_not_immune",defenderAbility=27,seed=chosen["spore_1"],type1=12)
    guards = [(27,"sleep",chosen["spore_1"],[("vital_spirit",{"attackerAbility":72}),("insomnia",{"attackerAbility":15}),
              ("soundproof_uproar",{"attackerAbility":43,"uproarMask":8}),("all_slot_uproar",{"uproarMask":4})]),
              (27,"spore_poison",chosen["spore_2"],[("poison_type",{"type1":3}),("steel_type",{"type2":8}),("immunity",{"attackerAbility":17})]),
              (27,"paralysis",chosen["spore_3"],[("limber",{"attackerAbility":7}),("electric_type_not_immune",{"type1":13}),("ground_type_not_immune",{"type2":4})]),
              (38,"poison",chosen["third_0"],[("poison_type",{"type1":3}),("steel_type",{"type2":8}),("immunity",{"attackerAbility":17})]),
              (49,"burn",chosen["third_0"],[("fire_type",{"type1":10}),("fire_second_type",{"type2":10}),("water_veil",{"attackerAbility":41})])]
    for ability,label,seed,cases in guards:
        for name,changes in cases: add("status_guards",label+"_"+name,defenderAbility=ability,seed=seed,**changes)
    return result


def run(root, out):
    assert subprocess.check_output(["git","-C",str(root),"rev-parse","HEAD"],text=True).strip() == COMMIT
    src = {path:(root/path).read_bytes() for path in SOURCES}
    for path,data in src.items():
        assert data == subprocess.check_output(["git","-C",str(root),"show",COMMIT+":"+path]), path
    excerpts,contracts = [],[]
    def record(path,name,body):
        start=src[path].index(body);line=src[path][:start].count(b"\n")+1
        excerpts.append({"path":path,"name":name,"line":line,"bytes":len(body),"sha256":sha(body),
                         "url":f"https://github.com/pret/pokefirered/blob/{COMMIT}/{path}#L{line}"})
        return body
    def contract(path,name,body,ordered=(),forbidden=()):
        position=0
        for token in ordered: position=body.index(token.encode(),position)+len(token)
        for token in forbidden: assert token.encode() not in body,(name,token)
        record(path,name,body)
        contracts.append({"name":name,"ordered":list(ordered),"forbidden":list(forbidden),"sha256":sha(body)})
    util,commands,scripts=src["src/battle_util.c"],src["src/battle_script_commands.c"],src["data/battle_scripts_1.s"]
    constants=b"\n".join(src[path] for path in HEADERS)+b"\n"
    for name in ["TARGET_TURN_DAMAGED","IS_BATTLER_OF_TYPE","GET_BATTLER_SIDE","IS_TYPE_PHYSICAL","F_DYNAMIC_TYPE_1","DYNAMIC_TYPE_MASK"]:
        macro=re.search(rb"(?m)^#define "+name.encode()+rb"(?:\s|\()[^\n]*",src["include/battle.h"])
        assert macro,name
        constants+=record("include/battle.h",name,macro.group())+b"\n"
    constants+=record("include/battle.h","SpecialStatus",extract(src["include/battle.h"],"struct SpecialStatus"))+b";\n"
    for name in ["RAND_MULT","ISO_RANDOMIZE1"]:
        macro=re.search(rb"(?m)^#define "+name.encode()+rb"(?:\s|\()[^\n]*",src["include/random.h"])
        assert macro,name
        constants+=record("include/random.h",name,macro.group())+b"\n"
    status_table=commands[commands.index(b"static const u32 sStatusFlagsForMoveEffects"):commands.index(b"static const u8 *const sMoveEffectBS_Ptrs")].rstrip()
    constants+=record("src/battle_script_commands.c","status flags table",status_table)+b"\n"
    selected=[]
    for ability,next_ability in [("ROUGH_SKIN","EFFECT_SPORE"),("EFFECT_SPORE","POISON_POINT"),("POISON_POINT","STATIC"),("FLAME_BODY","CUTE_CHARM")]:
        start=util.index(("            case ABILITY_"+ability+":").encode())
        end=util.index(("            case ABILITY_"+next_ability+":").encode(),start)
        selected.append(record("src/battle_util.c","ABILITY_"+ability,util[start:end]))
    contact=b"static u8 ProbeContact(void)\n{\n    u8 effect=0;u16 move=0;\n    switch(gLastUsedAbility)\n    {\n"+b"".join(selected)+b"    }\n    return effect;\n}\n"
    full_effect=extract(commands,"void SetMoveEffect(bool8 primary, u8 certain)")
    primary=full_effect[:full_effect.index(b"    else\n    {\n        if (gBattleMons[gEffectBattler].status2 & sStatusFlagsForMoveEffects")]
    # Keep each included source excerpt byte-for-byte; omit unreachable freeze/toxic cases.
    segments=[primary[:primary.index(b"        case STATUS1_FREEZE:")],
              primary[primary.index(b"        case STATUS1_PARALYSIS:"):primary.index(b"        case STATUS1_TOXIC_POISON:")],
              primary[primary.index(b"        }\n        if (statusChanged == TRUE)"):]]
    for index,body in enumerate(segments): record("src/battle_script_commands.c",f"SetMoveEffect primary contact segment {index}",body)
    effect=b"".join(segments)+b"}\n"
    random=record("src/random.c","Random",extract(src["src/random.c"],"u16 Random(void)"))
    random+=b'''\nstatic u16 ProbeRandom(void){u16 value=Random();if(drawCount>=4096)exit(4);draws[drawCount++]=value;return value;}\n#define Random ProbeRandom\n'''
    cancel=record("src/battle_util.c","CancelMultiTurnMoves",extract(util,"void CancelMultiTurnMoves(u8 battler)"))
    cancel+=b'''\nstatic void ProbeCancel(u8 battler){cancelCalls++;CancelMultiTurnMoves(battler);}\n#define CancelMultiTurnMoves ProbeCancel\n'''
    hp=record("src/battle_script_commands.c","Cmd_datahpupdate",extract(commands,"static void Cmd_datahpupdate(void)"))
    secondary=record("src/battle_script_commands.c","Cmd_seteffectsecondary",extract(commands,"static void Cmd_seteffectsecondary(void)"))
    def script_part(start,end):
        a=re.search(rb"(?m)^"+start.encode()+rb"::?\s*$",scripts).start()
        b=re.search(rb"(?m)^"+end.encode()+rb"::?\s*$",scripts).start()
        return scripts[a:b]
    contract("src/battle_script_commands.c","contact precedes immunity and attacker Synchronize and hit items",extract(commands,"static void Cmd_moveend(void)"),
             ["case MOVEEND_ON_DAMAGE_ABILITIES:","AbilityBattleEffects(ABILITYEFFECT_ON_DAMAGE, gBattlerTarget, 0, 0, 0)",
              "case MOVEEND_IMMUNITY_ABILITIES:","case MOVEEND_SYNCHRONIZE_ATTACKER:","case MOVEEND_KINGSROCK_SHELLBELL:"])
    contract("data/battle_scripts_1.s","Rough Skin is passive, ignores substitute, updates and faints attacker",script_part("BattleScript_RoughSkinActivates","BattleScript_CuteCharmActivates"),
             ["HITMARKER_IGNORE_SUBSTITUTE | HITMARKER_PASSIVE_DAMAGE","datahpupdate BS_ATTACKER","tryfaintmon BS_ATTACKER","return"],["moveend","seteffectsecondary"])
    contract("data/battle_scripts_1.s","contact status script invokes secondary then returns",script_part("BattleScript_ApplySecondaryEffect","BattleScript_SynchronizeActivates"),["seteffectsecondary","return"])
    contract("data/battle_scripts_1.s","multihit per-hit contact and final phase excludes contact",script_part("BattleScript_EffectMultiHit","BattleScript_EffectConversion"),
             ["jumpifhasnohp BS_ATTACKER","jumpifhasnohp BS_TARGET","gChosenMove, MOVE_SLEEP_TALK", "jumpifstatus BS_ATTACKER, STATUS1_SLEEP",
              "movevaluescleanup","datahpupdate BS_TARGET","moveendto MOVEEND_NEXT_TARGET","decrementmultihit","moveendcase MOVEEND_SYNCHRONIZE_TARGET","moveendfrom MOVEEND_IMMUNITY_ABILITIES"])
    contract("data/battle_scripts_1.s","Triple Kick per-hit contact and final phase excludes contact",script_part("BattleScript_EffectTripleKick","BattleScript_EffectThief"),
             ["jumpifhasnohp BS_ATTACKER","jumpifhasnohp BS_TARGET","gChosenMove, MOVE_SLEEP_TALK", "jumpifstatus BS_ATTACKER, STATUS1_SLEEP",
              "datahpupdate BS_TARGET","moveendto MOVEEND_NEXT_TARGET","moveendfrom MOVEEND_UPDATE_LAST_MOVES"])
    contract("src/battle_script_commands.c","MoveValuesCleanUp retains turn receipts",extract(commands,"static void MoveValuesCleanUp(void)"),["gMoveResultFlags = 0;"],["gSpecialStatuses","gBattleMoveDamage"])
    generated=PREFIX.encode().replace(b"/* CONSTANTS */",constants)+b"\n"+random+b"\n"+cancel+b"\n"+effect+b"\n"+secondary+b"\n"+hp+b"\n"+contact+MAIN.encode()
    out.mkdir(parents=True,exist_ok=True)
    cfile=out/"contact-oracle.c";cfile.write_bytes(generated)
    rows=vectors();encoded="".join(" ".join(str(v["input"][f]) for f in FIELDS)+"\n" for v in rows)
    (out/"input-vectors.txt").write_text(encoded)
    checks=[];stdout=None
    for name,options in [("optimized",["-O2"]),("ubsan",["-O1","-fsanitize=undefined","-fno-sanitize-recover=all"])]:
        binary=out/("contact-oracle" if name=="optimized" else "contact-oracle-ubsan")
        command=["cc","-std=c11","-Wall","-Wextra","-Werror","-Wno-unused-variable",*options,str(cfile),"-o",str(binary)]
        subprocess.run(command,check=True)
        result=subprocess.run([str(binary)],input=encoded,text=True,capture_output=True,check=True)
        assert not result.stderr,result.stderr
        if stdout is None: stdout=result.stdout
        else: assert stdout==result.stdout
        checks.append({"name":name,"command":command,"binarySha256":sha(binary.read_bytes()),"outputSha256":sha(result.stdout.encode())})
    lines=stdout.splitlines();assert len(lines)==len(rows) and rows
    for row,line in zip(rows,lines):
        row["expected"]=json.loads(line);assert row["expected"]["id"]==row["input"]["id"]
    by_name={row["name"]:row for row in rows}
    fixed={"rough_skin_maxhp_15":{"damage":1},"rough_skin_maxhp_16":{"damage":1},"rough_skin_maxhp_32":{"damage":2},
           "rough_skin_attacker_hp_6":{"faintCalls":1,"hpDealt":6},"rough_skin_holder_fainted":{"effect":1},
           "effect_spore_no_turn_damage":{"drawCount":0},"poison_point_already_poisoned":{"drawCount":1,"statusWrites":0},
           "flame_body_shield_dust":{"statusWrites":1},"flame_body_safeguard":{"statusWrites":1},
           "flame_body_attacker_substitute":{"statusWrites":1},"poison_immunity":{"statusWrites":0},
           "sleep_all_slot_uproar":{"statusWrites":0},"sleep_soundproof_uproar":{"statusWrites":1},
           "paralysis_electric_type_not_immune":{"statusWrites":1},"burn_water_veil":{"statusWrites":0}}
    for name,values in fixed.items():
        for key,value in values.items(): assert by_name[name]["expected"][key]==value,(name,key,by_name[name]["expected"])
    for name, hp in [("rough_skin_attacker_hp_1", 1), ("rough_skin_attacker_hp_6", 6)]:
        case = by_name[name]; observed = case["expected"]
        assert observed["damage"] == 6 and observed["hpDealt"] == hp
        assert observed["slots"][case["input"]["attacker"]]["takenDamage"] == 6
    for row in rows:
        inp,exp=row["input"],row["expected"]
        if inp["defenderAbility"]==24:
            assert exp["drawCount"]==0
            assert exp["slots"][inp["attacker"]]["receipt"]==0
            assert exp["slots"][inp["attacker"]]["physicalDmg"]==0
            assert exp["slots"][inp["attacker"]]["specialDmg"]==0
    for turns in range(2,6): assert by_name[f"sleep_duration_{turns}"]["expected"]["slots"][0]["status"]==turns
    assert by_name["sleep_cancels_locked_moves"]["expected"]["slots"][0]["status2"]==0
    jsonl="".join(json.dumps(row,separators=(",",":"))+"\n" for row in rows)
    (out/"source-vectors.jsonl").write_text(jsonl)
    report={"sourceCommit":COMMIT,"sourceFiles":{path:sha(data) for path,data in src.items()},"excerpts":excerpts,
            "scriptContracts":contracts,"compilerChecks":checks,"inputFields":FIELDS,"vectorCount":len(rows),
            "groups":dict(collections.Counter(row["group"] for row in rows)),"fixedAssertions":fixed,
            "generatedCSha256":sha(generated),"vectorsSha256":sha(jsonl.encode()),"harnessSha256":sha(Path(__file__).read_bytes()),
            "boundaries":["Four complete unchanged AbilityBattleEffects cases are compiled; surrounding ability dispatch and unrelated abilities are omitted.",
              "Original Random function and LCG macros are compiled; a wrapper records the actual ordered u16 outputs without supplying or replacing them.",
              "Original SetMoveEffect gates, sleep/poison/burn/paralysis cases and primary-status commit are compiled unchanged; freeze/toxic/volatile cases omitted.",
              "Original CancelMultiTurnMoves, Cmd_seteffectsecondary and complete Cmd_datahpupdate are compiled unchanged.",
              "Four host slots use identity battler-position mapping. Script stack and controller presentation are observable stubs; no ROM, ARM ABI, native UI or full engine claim.",
              "Rough Skin script's marker/HP steps are explicitly driven by the host after source assertions; faintCalls records required tryfaintmon dispatch, not original faint cleanup.",
              "Status synchronization scheduling is source-asserted and its pending marker observed; Synchronize reflection itself is outside this oracle.",
              "repeatCount explicitly supplies repeated eligible callback phases over retained state; original multihit loop scheduling is source-asserted, not executed.",
              "Input physical/special turn-damage receipts are supplied directly; move accuracy, damage calculation, Substitute damage routing and source action scheduling are outside this oracle."]}
    (out/"source-probe.json").write_text(json.dumps(report,indent=2)+"\n")
    print(json.dumps({"vectorCount":len(rows),"output":str(out),"vectorsSha256":report["vectorsSha256"]}))


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference",type=Path);parser.add_argument("output",type=Path)
    args=parser.parse_args();run(args.reference,args.output)
