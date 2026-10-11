"""Compile extracted, pinned selector commands. Stubs isolate selection and register writes;
this is not a battle VM. Never execute a reference-repository script."""
import hashlib, json, pathlib, re, subprocess, sys
reference, output = map(pathlib.Path, sys.argv[1:])
pin = '037335f4c725d7c9aecdac87066f2002b4bd7e14'
files = ['src/battle_script_commands.c', 'data/battle_scripts_1.s', 'include/constants/moves.h', 'src/battle_util.c']
raw = {}
for path in files:
    data = subprocess.check_output(['git', '-C', str(reference), 'show', pin + ':' + path])
    assert data == (reference / path).read_bytes(), path
    raw[path] = data
commands = raw[files[0]].decode(); scripts = raw[files[1]].decode()
def extract(name):
    marker = 'static void ' + name + '(void)\n{'
    start = commands.index(marker); pos = start + len(marker); depth = 1
    while depth:
        depth += (commands[pos] == '{') - (commands[pos] == '}'); pos += 1
    return commands[start:pos]
constants = dict((name,int(n)) for name,n in re.findall(r'#define (MOVE_\w+)\s+(\d+)\b',raw[files[2]].decode()))
table = commands.split('static const u16 sMovesForbiddenToCopy[] =',1)[1].split('};',1)[0]
forbidden = [constants[name] for name in re.findall(r'\bMOVE_\w+\b',table)]
assert forbidden == [118,165,166,102,68,243,182,197,203,194,214,168,266,289,270,343,271,264]
nature = commands.split('static const u16 sNaturePowerMoves[] =',1)[1].split('};',1)[0]
assert [constants[x] for x in re.findall(r'= (MOVE_\w+)',nature)] == [78,75,89,56,57,61,157,247,129,129]
assist = extract('Cmd_assistattackselect')
assert '((Random() & 0xFF) * chooseableMovesNo) >> 8' in assist
assert all(x not in assist for x in ['MON_DATA_HP','MON_DATA_PP','MON_DATA_STATUS'])
assert 'monId == gBattlerPartyIndexes[gBattlerAttacker]' in assist and 'SPECIES_EGG' in assist
assert '*(gBattleStruct->moveTarget + gBattlerAttacker) = targetBattler;' in raw['src/battle_util.c'].decode()
sleep = extract('Cmd_trychoosesleeptalkmove')
assert '~MOVE_LIMITATION_PP' in sleep and 'gCurrMovePos = movePosition;' in sleep
sleep_script = scripts.split('BattleScript_SleepTalkIsAsleep::',1)[1].split('BattleScript_EffectDestinyBond::',1)[0]
assert sleep_script.index('ppreduce') < sleep_script.index('HITMARKER_NO_PPDEDUCT') < sleep_script.index('trychoosesleeptalkmove') < sleep_script.index('jumptocalledmove TRUE')
header = r'''
#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32;
#define TRUE 1
#define MOVES_COUNT 355
#define MAX_MON_MOVES 4
#define MAX_BATTLERS_COUNT 4
#define METRONOME_FORBIDDEN_END 65535
#define MOVE_NONE 0
#define MOVE_UNAVAILABLE 65535
#define MOVE_FOCUS_PUNCH 264
#define MOVE_UPROAR 253
#define MOVE_LIMITATION_PP 2
#define HITMARKER_ATTACKSTRING_PRINTED 1
#define NO_TARGET_OVERRIDE 0
#define T1_READ_16(p) (*(const u16 *)(p))
static unsigned char script[32], *gBattlescriptCurrInstr;
#define T1_READ_PTR(p) (script)
static struct {unsigned char lastTakenMove[8],lastTakenMoveFrom[32];} history,*gBattleStruct=&history;
static struct {unsigned ppNotAffectedByPressure;} gSpecialStatuses[4];
static struct {unsigned effect;} gBattleMoves[355];
static unsigned char *gBattleScriptsForMoveEffects[1]={script};
static struct {u16 moves[4];} gBattleMons[4];
static unsigned gBattlersCount=4,gBattlerAttacker,gBattlerTarget,gCurrentMove,gCalledMove,gCurrMovePos,gHitMarker;
static const unsigned gBitTable[4]={1,2,4,8};
static unsigned limits,draws; static uint32_t state;
static unsigned Random(void){state=state*UINT32_C(0x41c64e6d)+UINT32_C(0x6073);++draws;return state>>16;}
static unsigned GetMoveTarget(unsigned move,unsigned unused){(void)move;(void)unused;return 1;}
static unsigned IsInvalidForSleepTalkOrAssist(unsigned m){return m==118||m==119||m==214||m==274;}
static unsigned IsTwoTurnsMove(unsigned m){return m==117||m==19||m==91||m==13||m==76||m==130||m==143||m==291||m==340;}
static unsigned CheckMoveLimitations(unsigned actor,unsigned bits,unsigned flags){(void)flags;for(unsigned i=0;i<4;i++)if(!gBattleMons[actor].moves[i])bits|=1u<<i;return bits|limits;}
static uint32_t seed_for(unsigned first){uint32_t inv=1;for(unsigned i=0;i<5;i++)inv*=2-UINT32_C(0x41c64e6d)*inv;return (((uint32_t)first<<16)-UINT32_C(0x6073))*inv;}
'''
header += 'static const u16 sMovesForbiddenToCopy[]={' + ','.join(map(str,forbidden)) + ',65535};\n'
source = header + '\n'.join(extract(n) for n in ['Cmd_metronome','Cmd_trymirrormove','Cmd_trychoosesleeptalkmove']) + r'''
int main(void){
 for(unsigned raw=0;raw<512;raw++) {state=seed_for(raw);draws=0;gHitMarker=1;Cmd_metronome();printf("M,%u,%u,%u,%u,%u\n",raw,seed_for(raw),gCurrentMove,state,draws);}
 for(unsigned mask=0;mask<16;mask++)for(unsigned raw=0;raw<16;raw++) {
  memset(&history,0,sizeof history);gBattlerAttacker=2;gHitMarker=1;gCurrentMove=119;gSpecialStatuses[2].ppNotAffectedByPressure=0;state=seed_for(raw);draws=0;gBattlescriptCurrInstr=script;
  u16 values[4]={33,45,0,39};for(unsigned i=0;i<4;i++)if(mask&(1u<<i))memcpy(history.lastTakenMoveFrom+2*8+i*2,&values[i],2);
  if(mask&4){u16 direct=52;memcpy(history.lastTakenMove+2*2,&direct,2);}
  Cmd_trymirrormove();printf("R,%u,%u,%u,%u,%u,%u,%u\n",mask,raw,seed_for(raw),gCurrentMove,state,draws,gSpecialStatuses[2].ppNotAffectedByPressure);
 }
 for(unsigned mask=0;mask<16;mask++)for(unsigned raw=0;raw<16;raw++) {
  gBattlerAttacker=0;gBattleMons[0].moves[0]=214;gBattleMons[0].moves[1]=45;gBattleMons[0].moves[2]=33;gBattleMons[0].moves[3]=0;limits=mask;gHitMarker=1;gCalledMove=0;gCurrMovePos=0;gBattlescriptCurrInstr=script;state=seed_for(raw);draws=0;
  Cmd_trychoosesleeptalkmove();printf("S,%u,%u,%u,%u,%u,%u,%u\n",mask,raw,seed_for(raw),gCalledMove,gCurrMovePos,state,draws);
 }
 return 0;
}
'''
output.mkdir(parents=True,exist_ok=True)
(output/'source-oracle.c').write_text(source)
variants=[]
for name,flags in [('optimized',['-O2']),('ubsan',['-O1','-fsanitize=undefined','-fno-sanitize-recover=undefined'])]:
    binary=output/name
    subprocess.run(['cc','-std=c11',*flags,str(output/'source-oracle.c'),'-o',str(binary)],check=True)
    variants.append(subprocess.check_output([str(binary)]))
assert variants[0]==variants[1]
(output/'source-vectors.csv').write_bytes(variants[0])
result={'sourceCommit':pin,'passed':True,'vectors':len(variants[0].splitlines()),'scope':'extracted Metronome/Mirror/SleepTalk selectors with isolated selection stubs; authenticated Assist/Nature commands',
        'sha256':{p:hashlib.sha256(raw[p]).hexdigest() for p in files},'generatedCHash':hashlib.sha256(source.encode()).hexdigest()}
(output/'source-proof.json').write_text(json.dumps(result,indent=2));print(json.dumps(result))
