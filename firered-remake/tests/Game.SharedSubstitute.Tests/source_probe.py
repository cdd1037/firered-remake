#!/usr/bin/env python3
"""Compile authenticated, bounded pret C excerpts into independent Substitute vectors.

Usage: python3 source_probe.py PINNED_REFERENCE OUTPUT_DIRECTORY
Only this generated C is executed. No reference repository scripts or ROM run.
"""
import argparse
import collections
import hashlib
import json
import pathlib
import re
import subprocess

COMMIT = "037335f4c725d7c9aecdac87066f2002b4bd7e14"
HERE = pathlib.Path(__file__).resolve().parent
OUTPUTS = ("id step attackerHp targetHp attackerSubHp targetSubHp attackerSub targetSub damage hpDealt "
           "receipt physical special taken chooser attackerWrapped gatePassed gateCalls fades rngCount "
           "itemEffect roughEffect alreadyHas statResult targetAttack hitMarker targetStatus2 effectByte slot2Status2 slot3Status2").split()
INPUT_MAP = {
    "attacker": "gBattlerAttacker", "target": "gBattlerTarget", "count": "gBattlersCount",
    "attackerHp": "gBattleMons[gBattlerAttacker].hp", "targetHp": "gBattleMons[gBattlerTarget].hp",
    "attackerMaxHp": "gBattleMons[gBattlerAttacker].maxHP", "targetMaxHp": "gBattleMons[gBattlerTarget].maxHP",
    "attackerStatus2": "gBattleMons[gBattlerAttacker].status2", "targetStatus2": "gBattleMons[gBattlerTarget].status2",
    "attackerSubHp": "gDisableStructs[gBattlerAttacker].substituteHP", "targetSubHp": "gDisableStructs[gBattlerTarget].substituteHP",
    "damage": "gBattleMoveDamage", "receipt": "gSpecialStatuses[gBattlerTarget].dmg",
    "physical": "gSpecialStatuses[gBattlerTarget].physicalDmg", "special": "gSpecialStatuses[gBattlerTarget].specialDmg",
    "hitMarker": "gHitMarker", "resultFlags": "gMoveResultFlags", "effectByte": "gBattleCommunication[MOVE_EFFECT_BYTE]",
    "targetAbility": "gBattleMons[gBattlerTarget].ability", "attackerAbility": "gBattleMons[gBattlerAttacker].ability",
    "moveEffect": "gBattleMoves[gCurrentMove].effect", "moveType": "gBattleMoves[gCurrentMove].type",
    "item": "gBattleMons[gBattlerAttacker].item", "item0": "gBattleMons[0].item", "draw": "draw",
    "targetAttack": "gBattleMons[gBattlerTarget].statStages[STAT_ATK]",
    "slot2Status2": "gBattleMons[2].status2", "slot2SubHp": "gDisableStructs[2].substituteHP",
    "slot3Status2": "gBattleMons[3].status2", "slot3SubHp": "gDisableStructs[3].substituteHP",
    "secondaryChance": "gBattleMoves[gCurrentMove].secondaryEffectChance",
}
OPERATIONS = {0: "setsubstitute command", 1: "datahpupdate (arg 0 target, 1 attacker)",
              2: "SetMoveEffect leading gates (arg primary)", 3: "MOVEEND_SUBSTITUTE loop",
              4: "SwitchInClearSetData through disable restoration", 5: "FaintClearSetData through disable clear",
              6: "ChangeStatBuffs target Attack (arg encoded signed stage change)",
              7: "ITEMEFFECT_KINGSROCK_SHELLBELL", 8: "ABILITY_ROUGH_SKIN case", 9: "script already-substitute guard plus setsubstitute",
              10: "seteffectwithchance command including RNG before gate"}

def sha(b): return hashlib.sha256(b).hexdigest()

def extract(text, signature):
    m = re.search(rb"(?m)^" + re.escape(signature.encode()) + rb"\s*\n\{", text)
    assert m, signature
    p, depth, state = text.index(b"{", m.start()), 0, "code"
    while p < len(text):
        c, pair = text[p:p+1], text[p:p+2]
        if state == "line":
            if c == b"\n": state = "code"
        elif state == "comment":
            if pair == b"*/": state = "code"; p += 1
        elif state in ("string", "char"):
            if c == b"\\": p += 1
            elif c == (b'"' if state == "string" else b"'"): state = "code"
        elif pair == b"//": state = "line"; p += 1
        elif pair == b"/*": state = "comment"; p += 1
        elif c == b'"': state = "string"
        elif c == b"'": state = "char"
        elif c == b"{": depth += 1
        elif c == b"}":
            depth -= 1
            if depth == 0: return text[m.start():p+1]
        p += 1
    raise AssertionError(signature)

def vectors(n):
    rows = []
    sub, wrap = n("STATUS2_SUBSTITUTE"), n("STATUS2_WRAPPED")
    def add(group, name, inputs, steps):
        rows.append(dict(id=len(rows), group=group, name=name, input=inputs,
                         steps=[dict(op=s[0], arg=s[1] if len(s)>1 else 0, updates=s[2] if len(s)>2 else {}) for s in steps]))
    for mx, hp in [(1,1),(2,2),(3,2),(4,1),(4,2),(99,24),(99,25),(100,25),(100,26),(101,26),(103,26),(104,27),(255,64),(404,102)]:
        add("creation", f"max{mx}_hp{hp}", dict(attackerMaxHp=mx,attackerHp=hp,attackerStatus2=wrap), [(0,)] + ([(1,1)] if hp > max(1,mx//4) else []))
    add("creation", "already_present_skips_cost", dict(attackerStatus2=sub|wrap,attackerSubHp=20), [(9,)])
    add("creation", "already_faded_flag_still_blocks", dict(attackerStatus2=sub,attackerSubHp=0), [(9,)])
    for actor,target,count in [(0,1,2),(1,0,2),(2,3,4),(3,2,4),(0,3,4)]:
        add("identity", f"create_actor{actor}_count{count}", dict(attacker=actor,target=target,count=count,attackerHp=80,attackerMaxHp=103), [(9,), (1,1)])
    for hp,damage in [(25,0),(25,1),(25,24),(25,25),(25,26),(1,999),(0,16)]:
        add("interception", f"sub{hp}_damage{damage}", dict(targetSubHp=hp,targetStatus2=sub,damage=damage), [(1,), (2,0,{"effectByte": n("MOVE_EFFECT_POISON")}), (3,)])
    add("interception", "no_status_flag_does_not_intercept", dict(targetSubHp=25,damage=16), [(1,)])
    add("interception", "missed_keeps_sub_and_sentinel", dict(targetSubHp=25,targetStatus2=sub,damage=16,resultFlags=n("MOVE_RESULT_MISSED")), [(1,)])
    add("interception", "first_receipt_preserved", dict(targetSubHp=25,targetStatus2=sub,damage=16,receipt=7), [(1,)])
    add("interception", "sentinel_preserved", dict(targetSubHp=25,targetStatus2=sub,damage=16,receipt=65535), [(1,)])
    for actor,target,count in [(0,1,2),(2,3,4),(3,2,4)]:
        add("identity", f"break_actor{actor}_target{target}", dict(attacker=actor,target=target,count=count,targetSubHp=10,targetStatus2=sub,damage=16), [(1,), (3,)])
    for effect in ["MOVE_EFFECT_POISON","MOVE_EFFECT_FLINCH","MOVE_EFFECT_ATK_MINUS_1","MOVE_EFFECT_RECHARGE"]:
        for self_effect in [False,True]:
            add("effect_gates", f"{effect}_{'user' if self_effect else 'target'}", dict(targetStatus2=sub,attackerStatus2=sub,effectByte=n(effect)|(n("MOVE_EFFECT_AFFECTS_USER") if self_effect else 0)), [(2,)])
    add("effect_gates", "ignore_hp_marker_does_not_bypass_status", dict(targetStatus2=sub,effectByte=n("MOVE_EFFECT_POISON"),hitMarker=n("HITMARKER_IGNORE_SUBSTITUTE")), [(2,)])
    add("effect_gates", "primary_does_not_bypass_status", dict(targetStatus2=sub,effectByte=n("MOVE_EFFECT_POISON")), [(2,1)])
    add("effect_gates", "faded_flag_blocks_until_cleanup", dict(targetStatus2=sub,targetSubHp=0,effectByte=n("MOVE_EFFECT_POISON")), [(2,), (3,), (2,)])
    for sub_hp in [25,0]:
        for certain in [False,True]:
            add("secondary_rng", f"sub{sub_hp}_{'certain' if certain else 'chance'}",dict(targetStatus2=sub,targetSubHp=sub_hp,effectByte=n("MOVE_EFFECT_POISON")|(n("MOVE_EFFECT_CERTAIN") if certain else 0),secondaryChance=10,draw=10),[(10,)])
    add("secondary_rng", "chance_failed_still_consumes_draw",dict(targetStatus2=sub,targetSubHp=25,effectByte=n("MOVE_EFFECT_POISON"),secondaryChance=10,draw=11),[(10,)])
    add("secondary_rng", "no_effect_still_consumes_draw",dict(targetStatus2=sub,targetSubHp=25,effectByte=n("MOVE_EFFECT_POISON"),secondaryChance=10,draw=0,resultFlags=n("MOVE_RESULT_MISSED")),[(10,)])
    for damage in [-20,7]:
        add("bypass_hp", f"ignore_marker_damage{damage}", dict(targetSubHp=25,targetStatus2=sub,damage=damage,hitMarker=n("HITMARKER_IGNORE_SUBSTITUTE")|n("HITMARKER_PASSIVE_DAMAGE")), [(1,)])
    add("multihit", "break_then_next_hit_reaches_real_hp", dict(targetSubHp=8,targetStatus2=sub,damage=12,effectByte=n("MOVE_EFFECT_POISON")), [(1,), (2,), (3,), (1,0,{"damage":12}), (2,)])
    for hp in [25,0]:
        add("stat_primitive", f"stat_drop_sub{hp}_no_internal_gate", dict(targetStatus2=sub,targetSubHp=hp), [(6,144)])
        add("stat_primitive", f"stat_raise_sub{hp}_no_internal_gate", dict(targetStatus2=sub,targetSubHp=hp), [(6,16)])
    for baton in [False,True]:
        for hp in [0,1,25,100]:
            add("departure", f"{'baton' if baton else 'switch'}_sub{hp}", dict(targetStatus2=sub|wrap,targetSubHp=hp,moveEffect=n("EFFECT_BATON_PASS") if baton else 0,targetHp=20,targetMaxHp=20,targetAttack=10), [(4,)])
    add("departure", "faint_clears_substitute", dict(targetHp=0,targetStatus2=sub|wrap,targetSubHp=25,targetAttack=10), [(5,)])
    add("cleanup", "all_four_slots_cleaned_by_flag_and_hp", dict(targetStatus2=sub,targetSubHp=0,slot2Status2=sub,slot2SubHp=10,slot3Status2=sub,slot3SubHp=0), [(3,)])
    for item in ["ITEM_KINGS_ROCK","ITEM_SHELL_BELL"]:
        for damage in [0,8,32]:
            add("held_receipts", f"{item}_sub_damage{damage}", dict(targetStatus2=sub,targetSubHp=16,damage=damage,item=n(item),attackerHp=50), [(1,), (7,)])
    add("held_receipts", "king_stale_real_receipt_draws_then_sub_blocks", dict(targetStatus2=sub,targetSubHp=16,damage=8,item=n("ITEM_KINGS_ROCK"),physical=5), [(1,), (7,)])
    for damage,sub_hp in [(0,16),(8,16),(32,16),(8,0)]:
        add("contact_receipts", f"rough_skin_damage{damage}_sub{sub_hp}", dict(targetStatus2=sub,targetSubHp=sub_hp,damage=damage), [(1,), (8,)])
    add("contact_receipts", "rough_skin_stale_real_receipt_is_retained", dict(targetStatus2=sub,targetSubHp=16,damage=8,physical=5), [(1,), (8,)])
    return rows

def run(root, out):
    assert subprocess.check_output(["git","-C",str(root),"rev-parse","HEAD"],text=True).strip() == COMMIT
    paths = ["src/battle_script_commands.c","src/battle_main.c","src/battle_util.c","include/battle.h","include/battle_util.h","include/battle_message.h","data/battle_scripts_1.s","src/data/items.json"]
    paths += [str(p.relative_to(root)) for p in sorted((root/"include/constants").glob("*.h"))]
    source = {p:(root/p).read_bytes() for p in paths}
    for p,b in source.items():
        assert b == subprocess.check_output(["git","-C",str(root),"show",f"{COMMIT}:{p}"]), p
    definitions = {}
    for p,b in source.items():
        for m in re.finditer(rb"(?m)^#define[ \t]+([A-Za-z_]\w*)(?:\([^\n]*?\))?[^\n]*(?:\\\n[^\n]*)*",b):
            definitions.setdefault(m[1].decode(),m.group().decode())
    def number(name):
        v=definitions[name].split(None,2)[2].split("//")[0].strip()
        for t in set(re.findall(r"\b[A-Z][A-Z_0-9]*\b",v)):
            v=re.sub(r"\b"+t+r"\b",str(number(t)),v)
        assert re.fullmatch(r"[0-9a-fA-FxX() <>|&+*/~^-]+",v),(name,v)
        return int(eval(v,{"__builtins__":{}},{}))
    excerpts=[]
    def record(path,name,body):
        start=source[path].index(body); line=source[path][:start].count(b"\n")+1
        excerpts.append(dict(path=path,name=name,line=line,sha256=sha(body),bytes=len(body),url=f"https://github.com/pret/pokefirered/blob/{COMMIT}/{path}#L{line}"))
        return body
    cpath,mpath,upath="src/battle_script_commands.c","src/battle_main.c","src/battle_util.c"
    commands,main,util=source[cpath],source[mpath],source[upath]
    bodies=[]
    for sig in ["static void Cmd_setsubstitute(void)","static void Cmd_datahpupdate(void)","static u8 ChangeStatBuffs(s8 statValue, u8 statId, u8 flags, const u8 *BS_ptr)"]:
        bodies.append(record(cpath,sig,extract(commands,sig)))
    gates=extract(commands,"void SetMoveEffect(bool8 primary, u8 certain)")
    gates=gates[gates.index(b"    bool32 statusChanged"):gates.index(b"    if (gBattleCommunication[MOVE_EFFECT_BYTE] <= PRIMARY_STATUS_MOVE_EFFECT)")]
    bodies.append(b"static void SetMoveEffect(bool8 primary, u8 certain) { gateCalls++;\n"+record(cpath,"SetMoveEffect complete leading gates",gates)+b"    gatePassed++;\n}\n")
    bodies.append(record(cpath,"static void Cmd_seteffectwithchance(void)",extract(commands,"static void Cmd_seteffectwithchance(void)")))
    cleanup=extract(commands,"static void Cmd_moveend(void)")
    cleanup=cleanup[cleanup.index(b"            for (i = 0; i < gBattlersCount; i++)",cleanup.index(b"case MOVEEND_SUBSTITUTE:")):cleanup.index(b"            gBattleScripting.moveendState++;",cleanup.index(b"case MOVEEND_SUBSTITUTE:"))]
    bodies.append(b"static void ProbeMoveEndSubstitute(void) { int i;\n"+record(cpath,"MOVEEND_SUBSTITUTE exact loop",cleanup)+b"}\n")
    switch=extract(main,"void SwitchInClearSetData(void)")
    switch=switch[switch.index(b"    struct DisableStruct"):switch.index(b"    gMoveResultFlags = 0;")]
    bodies.append(b"static void ProbeSwitchIn(void) {\n"+record(mpath,"SwitchInClearSetData through disable restoration",switch)+b"}\n")
    faint=extract(main,"void FaintClearSetData(void)")
    faint=faint[faint.index(b"    s32 i;"):faint.index(b"    gProtectStructs[gActiveBattler].protected")]
    bodies.append(b"static void ProbeFaint(void) {\n"+record(mpath,"FaintClearSetData through disable clear",faint)+b"}\n")
    items=extract(util,"u8 ItemBattleEffects(u8 caseID, u8 battlerId, bool8 moveTurn)")
    preamble=items[items.index(b"    int i = 0;"):items.index(b"    switch (caseID)")]
    branch=items[items.index(b"    case ITEMEFFECT_KINGSROCK_SHELLBELL:"):items.rindex(b"    }\n\n    return effect;")]
    bodies.append(b"static u8 ProbeHitItems(u8 caseID, u8 battlerId, bool8 moveTurn) {\n"+record(upath,"ItemBattleEffects preamble",preamble)+b"switch(caseID) {\n"+record(upath,"ITEMEFFECT_KINGSROCK_SHELLBELL complete case",branch)+b"}\nreturn effect;\n}\n")
    rough=util[util.index(b"            case ABILITY_ROUGH_SKIN:"):util.index(b"            case ABILITY_EFFECT_SPORE:")]
    bodies.append(b"static u8 ProbeRoughSkin(u16 move) { u8 effect=0; switch(ABILITY_ROUGH_SKIN) {\n"+record(upath,"ABILITY_ROUGH_SKIN complete case",rough)+b"}\nreturn effect;\n}\n")
    structure=b"\n".join(record("include/battle.h",s,extract(source["include/battle.h"],s))+b";" for s in ["struct DisableStruct","struct SpecialStatus"])
    prefix=(HERE/"source-prefix.h").read_bytes(); main_fixture=(HERE/"source-main.c").read_bytes()
    rows=vectors(number)
    defaults=dict(attacker=0,target=1,count=4,attackerHp=80,targetHp=80,attackerMaxHp=100,targetMaxHp=100,attackerStatus2=0,targetStatus2=0,attackerSubHp=0,targetSubHp=0,damage=0,receipt=0,physical=0,special=0,hitMarker=0,resultFlags=0,effectByte=0,targetAbility=0,attackerAbility=0,moveEffect=0,moveType=number("TYPE_NORMAL"),item=0,item0=0,draw=0,targetAttack=6,slot2Status2=0,slot2SubHp=0,slot3Status2=0,slot3SubHp=0,secondaryChance=0)
    # These defaults are represented in every JSON input for straightforward consumers.
    generated_main="int main(void) {\n"
    for row in rows:
        row["input"]={**defaults,**row["input"]}
        generated_main+="ResetFixture();\n"
        # Slot-specific setup comes before attacker/target-specific setup, as they may alias.
        keys=["attacker","target","count","slot2Status2","slot2SubHp","slot3Status2","slot3SubHp","item0"]
        keys += [k for k in row["input"] if k not in keys]
        for k in keys: generated_main+=f"{INPUT_MAP[k]} = {row['input'][k]};\n"
        for step,s in enumerate(row["steps"]):
            for k,v in s["updates"].items(): generated_main+=f"{INPUT_MAP[k]} = {v};\n"
            generated_main+=f"ApplyOperation({s['op']},{s['arg']}); Record({row['id']},{step});\n"
    generated_main+="return 0; }\n"
    # Retain the authenticated item table including battler-zero parameter semantics.
    item_data=json.loads(source["src/data/items.json"])["items"]
    table="static const struct {u8 effect, parameter;} sourceItems[] = {\n"
    for item in item_data: table+=f"{{{number(item['holdEffect'])},{item['holdEffectParam']}}},\n"
    table+="};\n"
    code=b"\n".join(bodies)
    names=set(re.findall(r"\b[A-Z][A-Z_0-9]*\b",(prefix+structure+code+main_fixture).decode()))
    excluded={"TRUE","FALSE","BUFFER_A","REQUEST_HP_BATTLE","PREPARE_STAT_BUFFER","INCREMENT_RETURN"}
    selected={}
    while names:
        name=names.pop()
        if name in selected or name in excluded or name not in definitions: continue
        selected[name]=definitions[name]
        names |= set(re.findall(r"\b[A-Z][A-Z_0-9]*\b",selected[name]))-selected.keys()
    constants="\n".join(selected[k] for k in sorted(selected))+"\n#define ITEM_NO_EFFECT 0\n"
    generated=prefix.replace(b"/* SOURCE_CONSTANTS */",constants.encode()).replace(b"/* SOURCE_STRUCTURES */",structure).replace(b"/* SOURCE_ITEM_TABLE */",table.encode())+b"\n"+code+b"\n"+main_fixture+b"\n"+generated_main.encode()
    out.mkdir(parents=True,exist_ok=True); cfile=out/"substitute-oracle.c"; cfile.write_bytes(generated)
    checks=[]; stdout=None
    for label,opts in [("optimized",["-O2"]),("ubsan",["-O1","-fsanitize=undefined","-fno-sanitize-recover=all"])]:
        binary=out/("substitute-oracle-"+label)
        cmd=["cc","-std=c99","-Wall","-Wextra","-Werror","-Wno-sign-compare","-Wno-unused-variable","-Wno-unused-but-set-variable","-Wno-unused-parameter",*opts,str(cfile),"-o",str(binary)]
        subprocess.run(cmd,check=True)
        result=subprocess.run([str(binary)],text=True,capture_output=True,check=True)
        assert not result.stderr
        if stdout is None: stdout=result.stdout
        else: assert stdout == result.stdout
        checks.append(dict(name=label,command=cmd,binarySha256=sha(binary.read_bytes()),resultSha256=sha(result.stdout.encode())))
    for row in rows: row["expected"]=[]
    for line in stdout.splitlines():
        values=list(map(int,line.split(","))); assert len(values)==len(OUTPUTS)
        result=dict(zip(OUTPUTS,values)); rows[result["id"]]["expected"].append(result)
    for row in rows: assert len(row["steps"])==len(row["expected"])
    fixed = {
        "max100_hp25": [(0,dict(attackerHp=25,attackerSubHp=0,attackerSub=0,attackerWrapped=1,chooser=1))],
        "max100_hp26": [(0,dict(attackerHp=26,attackerSubHp=25,attackerSub=1,attackerWrapped=0)),(1,dict(attackerHp=1,attackerSubHp=25))],
        "max2_hp2": [(1,dict(attackerHp=1,attackerSubHp=1))],
        "already_present_skips_cost": [(0,dict(alreadyHas=1,attackerHp=80,attackerSubHp=20,attackerWrapped=1))],
        "sub25_damage0": [(0,dict(targetHp=80,targetSubHp=25,hpDealt=0,receipt=0,physical=0,special=0,fades=0))],
        "sub25_damage26": [(0,dict(targetHp=80,targetSubHp=0,targetSub=1,hpDealt=25,receipt=25,physical=0,special=0,fades=1)),(1,dict(gatePassed=0)),(2,dict(targetSub=0))],
        "sub0_damage16": [(0,dict(targetHp=64,targetSubHp=0,targetSub=1,physical=16))],
        "missed_keeps_sub_and_sentinel": [(0,dict(targetSubHp=25,receipt=65535,physical=0))],
        "first_receipt_preserved": [(0,dict(receipt=7,targetSubHp=9))],
        "sentinel_preserved": [(0,dict(receipt=65535,targetSubHp=9))],
        "MOVE_EFFECT_POISON_user": [(0,dict(gatePassed=1))],
        "MOVE_EFFECT_POISON_target": [(0,dict(gatePassed=0))],
        "ignore_hp_marker_does_not_bypass_status": [(0,dict(gatePassed=0))],
        "sub25_chance": [(0,dict(rngCount=1,gateCalls=1,gatePassed=0,effectByte=0))],
        "sub0_chance": [(0,dict(rngCount=1,gateCalls=1,gatePassed=0))],
        "sub25_certain": [(0,dict(rngCount=0,gateCalls=1,gatePassed=0))],
        "chance_failed_still_consumes_draw": [(0,dict(rngCount=1,gateCalls=0))],
        "no_effect_still_consumes_draw": [(0,dict(rngCount=1,gateCalls=0))],
        "ignore_marker_damage-20": [(0,dict(targetHp=100,targetSubHp=25,targetSub=1,receipt=0,physical=0,hitMarker=0))],
        "ignore_marker_damage7": [(0,dict(targetHp=73,targetSubHp=25,targetSub=1,receipt=0,physical=0,hitMarker=0))],
        "break_then_next_hit_reaches_real_hp": [(1,dict(targetHp=80,gatePassed=0,targetSub=1)),(4,dict(targetHp=68,physical=12,receipt=8,gatePassed=1,targetSub=0))],
        "stat_drop_sub25_no_internal_gate": [(0,dict(targetAttack=5,statResult=0,targetSub=1))],
        "stat_raise_sub25_no_internal_gate": [(0,dict(targetAttack=7,statResult=0,targetSub=1))],
        "baton_sub100": [(0,dict(targetHp=20,targetSubHp=100,targetSub=1,targetAttack=10))],
        "switch_sub25": [(0,dict(targetSubHp=0,targetSub=0,targetAttack=6))],
        "faint_clears_substitute": [(0,dict(targetHp=0,targetSubHp=0,targetSub=0,targetAttack=6))],
        "ITEM_KINGS_ROCK_sub_damage32": [(1,dict(rngCount=0,gateCalls=0))],
        "ITEM_SHELL_BELL_sub_damage32": [(1,dict(itemEffect=1,damage=-2,receipt=0))],
        "ITEM_SHELL_BELL_sub_damage0": [(1,dict(itemEffect=0,damage=0))],
        "king_stale_real_receipt_draws_then_sub_blocks": [(1,dict(rngCount=1,gateCalls=1,gatePassed=0,physical=5))],
        "rough_skin_damage32_sub16": [(1,dict(roughEffect=0,physical=0))],
        "rough_skin_damage8_sub0": [(1,dict(roughEffect=1,damage=6,physical=8))],
        "rough_skin_stale_real_receipt_is_retained": [(1,dict(roughEffect=1,damage=6,physical=5))],
        "all_four_slots_cleaned_by_flag_and_hp": [(0,dict(targetSub=0,slot2Status2=number("STATUS2_SUBSTITUTE"),slot3Status2=0))],
    }
    by_name={r["name"]:r for r in rows}
    for name,observations in fixed.items():
        for step,fields in observations:
            for field,value in fields.items(): assert by_name[name]["expected"][step][field] == value,(name,step,field,value,by_name[name]["expected"][step])
    contracts=[]
    def contract(name,path,body,ordered=(),forbidden=()):
        p=0
        for token in ordered: p=body.index(token.encode(),p)+len(token)
        for token in forbidden: assert token.encode() not in body,(name,token)
        record(path,name,body); contracts.append(dict(name=name,ordered=list(ordered),forbidden=list(forbidden)))
    scripts=source["data/battle_scripts_1.s"]
    def script(start,end):
        a=re.search(rb"(?m)^"+start.encode()+rb"::?\s*$",scripts).start(); b=re.search(rb"(?m)^"+end.encode()+rb"::?\s*$",scripts).start()
        return scripts[a:b]
    contract("creation script spends PP before existing check", "data/battle_scripts_1.s",script("BattleScript_EffectSubstitute","BattleScript_EffectRecharge"),["ppreduce","STATUS2_SUBSTITUTE","setsubstitute","datahpupdate BS_ATTACKER"])
    contract("fade script does not clear status flag", "data/battle_scripts_1.s",script("BattleScript_SubstituteFade","BattleScript_BerryCurePrlzEnd2"),["B_ANIM_SUBSTITUTE_FADE","return"],["bicword","STATUS2_SUBSTITUTE"])
    contract("ability and held phases before substitute cleanup",cpath,extract(commands,"static void Cmd_moveend(void)"),["case MOVEEND_ON_DAMAGE_ABILITIES:","case MOVEEND_KINGSROCK_SHELLBELL:","case MOVEEND_SUBSTITUTE:"])
    contract("multi-hit cleanup before next hit", "data/battle_scripts_1.s",script("BattleScript_EffectMultiHit","BattleScript_EffectConversion"),["datahpupdate BS_TARGET","moveendto MOVEEND_NEXT_TARGET","decrementmultihit"])
    contract("stat primitive has no substitute check",cpath,extract(commands,"static u8 ChangeStatBuffs(s8 statValue, u8 statId, u8 flags, const u8 *BS_ptr)"),[],["STATUS2_SUBSTITUTE","substituteHP"])
    contract("Focus Band draw precedes Substitute survival-adjustment block",cpath,extract(commands,"static void Cmd_adjustnormaldamage(void)"),["(Random() % 100) < param","focusBanded = 1","!(gBattleMons[gBattlerTarget].status2 & STATUS2_SUBSTITUTE)"])
    contract("secondary chance draws before later Substitute gate",cpath,extract(commands,"static void Cmd_seteffectwithchance(void)"),["Random() % 100 <= percentChance","SetMoveEffect(FALSE, 0)"])
    contract("Pain Split command itself blocks Substitute",cpath,extract(commands,"static void Cmd_painsplitdmgcalc(void)"),["if (!(gBattleMons[gBattlerTarget].status2 & STATUS2_SUBSTITUTE))"])
    blocks=[]; bypasses=[]
    labels=list(re.finditer(rb"(?m)^([A-Za-z_][A-Za-z_0-9]*):{1,2}\s*$",scripts))
    for i,m in enumerate(labels):
        body=scripts[m.start():labels[i+1].start() if i+1<len(labels) else len(scripts)]
        for line in body.splitlines():
            if b"jumpifstatus2" in line and b"STATUS2_SUBSTITUTE" in line:
                blocks.append(dict(label=m[1].decode(),instruction=line.strip().decode(),line=scripts[:scripts.index(line,m.start())].count(b"\n")+1))
    for effect,command in [
        ("Disable","Cmd_disablelastusedattack"),("Encore","Cmd_trysetencore"),
        ("Attract","Cmd_tryinfatuating"),("Torment","Cmd_settorment"),("Taunt","Cmd_settaunt"),
        ("Haze","Cmd_normalisebuffs"),("Roar","Cmd_forcerandomswitch"),("Foresight","Cmd_setforesight"),
        ("PerishSong","Cmd_trysetperishsong"),("PsychUp","Cmd_copyfoestats"),
        ("SkillSwap","Cmd_tryswapabilities"),("Transform","Cmd_transformdataexecution"),("Spite","Cmd_tryspiteppreduce"),
    ]:
        # Script ranges are resolved by their next effect label, not a hand-authored command model.
        start=re.search(rb"(?m)^BattleScript_Effect"+effect.encode()+rb"::\s*$",scripts).start()
        next_effect=re.search(rb"(?m)^BattleScript_Effect[A-Za-z_0-9]+::\s*$",scripts[start+1:])
        body=scripts[start:start+1+next_effect.start()]
        contract("bypass script "+effect,"data/battle_scripts_1.s",body,[],["STATUS2_SUBSTITUTE","seteffectprimary","seteffectsecondary"])
        contract("bypass command "+effect,cpath,extract(commands,"static void "+command+"(void)"),[],["STATUS2_SUBSTITUTE","substituteHP","SetMoveEffect("])
        bypasses.append(dict(effect=effect,scriptSha256=sha(body)))
    artifact=dict(sourceCommit=COMMIT,operations={str(k):v for k,v in OPERATIONS.items()},groups=[dict(name=k,vectors=[r for r in rows if r["group"]==k]) for k in dict.fromkeys(r["group"] for r in rows)])
    encoded=json.dumps(artifact,indent=2)+"\n"; (out/"source-vectors.json").write_text(encoded)
    (out/"source-vectors.jsonl").write_text("".join(json.dumps(r,separators=(",",":"))+"\n" for r in rows))
    manifest=dict(sourceCommit=COMMIT,vectorCount=len(rows),observationCount=sum(len(r["steps"]) for r in rows),groups=dict(collections.Counter(r["group"] for r in rows)),sourceFiles={p:sha(b) for p,b in source.items()},excerpts=excerpts,scriptContracts=contracts,scriptSubstituteBlocks=blocks,bypassScripts=bypasses,compilerChecks=checks,vectorsSha256=sha(encoded.encode()),generatedCSha256=sha(generated),outputFields=OUTPUTS,harnessSha256={n:sha((HERE/n).read_bytes()) for n in ["source_probe.py","source-prefix.h","source-main.c"]},boundaries=["Full unchanged setsubstitute, datahpupdate and ChangeStatBuffs functions; exact leading SetMoveEffect gates only (gatePassed reports reaching effect dispatch, not successful status application).","Exact selected contact/item branches and item parameter preamble; other abilities/items omitted. King's Rock uses original SetMoveEffect gates with later flinch dispatch omitted.","Exact switch/faint prefixes through substitute restoration/clear, not full entry/exit engine.","Script scheduling, controller calls, presentation, protect check and RNG are fixtures. Explicit operation steps schedule extracted C; no full battle/ROM/native UI proof claimed.","Every vector records default state plus explicit step updates. Output counters are cumulative within each vector.","Source DisableStruct is extracted exactly: substituteHP is u8. Inputs use legal gameplay HP bounds."])
    manifest["fixedAssertions"] = fixed
    (out/"source-probe.json").write_text(json.dumps(manifest,indent=2)+"\n")
    print(json.dumps(dict(vectorCount=len(rows),observations=manifest["observationCount"],output=str(out),sha256=manifest["vectorsSha256"])))

if __name__ == "__main__":
    parser=argparse.ArgumentParser(); parser.add_argument("reference",type=pathlib.Path); parser.add_argument("output",type=pathlib.Path)
    a=parser.parse_args(); run(a.reference,a.output)
