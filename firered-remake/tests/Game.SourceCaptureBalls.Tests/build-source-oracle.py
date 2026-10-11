#!/usr/bin/env python3
"""Extract pinned C multiplier/arithmetic/strict shake loop; run only our inert local witness."""
import argparse, hashlib, json, pathlib, re, subprocess
p=argparse.ArgumentParser();p.add_argument('repository',type=pathlib.Path);p.add_argument('output',type=pathlib.Path);a=p.parse_args()
commit='037335f4c725d7c9aecdac87066f2002b4bd7e14'
expected={
 'src/battle_script_commands.c':'3b7341db8cd58c54fb0b54b47e6eef89c5c05e8a60ab4e9b45bdc907845c19b3',
 'src/battle_main.c':'c290d325761d68c059f02dc21cc6178d4bbefa0d06ffddc8879341c090d37928',
 'include/battle.h':'bf64a16fb4e660f8a326eb352a511bb9e8fd54a74ebb03e328c08d5a23e5d8fa',
 'include/constants/pokemon.h':'9cabd203100ae7246bc595b7295e7e04145f6292abfab83c3bd0597a2cfbcfb5',
 'include/constants/map_types.h':'21b9dc465effd752f859c25d22fee121da8f56e245b1f65ad022af0f9a8008fa',
 'include/constants/items.h':'af17276346eaecc5a10ac60f14b22c33332ec8c6fb0ec82895bdc3649b678138',
 'src/random.c':'e7e90cc070d1fbf09579f5f91762cd8875d5bbe1d7a90f468bcd870df73b9de2',
 'include/random.h':'8a6049183598b2d865b5ff522abdb3d0e94012a127d724af46b9b7b69803f5b9'}
src={}
for path,sha in expected.items():
 raw=subprocess.check_output(['git','-C',str(a.repository),'show',commit+':'+path]);assert hashlib.sha256(raw).hexdigest()==sha,path
 assert (a.repository/path).read_bytes()==raw,'changed working source '+path
 src[path]=raw.decode()
a.output.mkdir(parents=True,exist_ok=False)
s=src['src/battle_script_commands.c']
block=s[s.index('        if (gLastUsedItem > ITEM_SAFARI_BALL)'):s.index('        if (gLastUsedItem != ITEM_SAFARI_BALL)',s.index('static void Cmd_handleballthrow(void)\n{'))]
# Instrument exact source expression boundaries; do not replace the multiplier branches or arithmetic.
block=block.replace('        odds = (catchRate * ballMultiplier / 10)', '        observedMultiplier = ballMultiplier;\n        observedBall = catchRate * ballMultiplier / 10;\n        odds = (catchRate * ballMultiplier / 10)')
block=block.replace('        if (gBattleMons[gBattlerTarget].status1 & (STATUS1_SLEEP | STATUS1_FREEZE))','        observedHp = odds;\n        if (gBattleMons[gBattlerTarget].status1 & (STATUS1_SLEEP | STATUS1_FREEZE))')
shake=re.search(r'            odds = Sqrt\(Sqrt\(16711680 / odds\)\);.*?(?=            BtlController_EmitBallThrowAnim\(BUFFER_A, shakes\);)',s,re.S)[0]
# Model the already documented original agbcc __udivsi3 denominator-zero behavior explicitly.
shake=shake.replace('16711680 / odds','original_udiv(16711680, odds)').replace('1048560 / odds','original_udiv(1048560, odds)')
shake=shake.replace('            for (shakes =','            threshold = odds;\n            for (shakes =')
shake=shake.replace('            if (gLastUsedItem == ITEM_MASTER_BALL)','            checks = shakes;\n            if (gLastUsedItem == ITEM_MASTER_BALL)')
# The independently saturated battle counter boundary is extracted separately.
inc=re.search(r'    if \(gBattleResults.battleTurnCounter < 0xFF\)\n        \+\+gBattleResults.battleTurnCounter;',src['src/battle_main.c'])[0]
assert 'u8 battleTurnCounter;' in src['include/battle.h']
def const(name,path):
 return re.search(r'^#define '+name+r'\s+([^\s/]+)',src[path],re.M)[1]
consts={n:const(n,'include/constants/items.h') for n in ['ITEM_'+x+'_BALL' for x in ['MASTER','ULTRA','GREAT','POKE','SAFARI','NET','DIVE','NEST','REPEAT','TIMER','LUXURY','PREMIER']]}
consts.update({n:const(n,'include/constants/pokemon.h') for n in ['TYPE_WATER','TYPE_BUG']})
consts['MAP_TYPE_UNDERWATER']=const('MAP_TYPE_UNDERWATER','include/constants/map_types.h')
random_fn=re.search(r'u16 Random\(void\)\n\{.*?\n\}',src['src/random.c'],re.S)[0].replace('return gRngValue >> 16;','rawDraws[drawCount++] = gRngValue >> 16;\n    return gRngValue >> 16;')
c=r'''#include <stdio.h>
#include <stdint.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32;
#define STATUS1_SLEEP 7
#define STATUS1_POISON 8
#define STATUS1_BURN 16
#define STATUS1_FREEZE 32
#define STATUS1_PARALYSIS 64
#define STATUS1_TOXIC_POISON 128
#define FLAG_GET_CAUGHT 1
#define BALL_3_SHAKES_SUCCESS 4
#define RAND_MULT 1103515245
#define ISO_RANDOMIZE1(val) (RAND_MULT * (val) + 24691)
'''+''.join('#define '+n+' '+v+'\n' for n,v in consts.items())+r'''
static struct { unsigned maxHP,hp,level,type1,type2,status1,species; } gBattleMons[1];
static struct { u8 battleTurnCounter; } gBattleResults;
static unsigned gBattlerTarget=0,currentMap,caught,observedMultiplier,observedBall,observedHp;
static u16 gLastUsedItem;
static u32 gRngValue,rawDraws[4]; static unsigned drawCount;
/* Original-layout Master byte is terrain[9] == 0; pointer indexing stays within this local array. */
static const u8 ballBytes[]={0,20,15,10,15};
static const u8 *sBallCatchBonuses=ballBytes+1;
#define IS_BATTLER_OF_TYPE(t,ty) (gBattleMons[t].type1==(ty)||gBattleMons[t].type2==(ty))
static unsigned GetCurrentMapType(void){return currentMap;}
static unsigned SpeciesToNationalPokedexNum(unsigned species){return species;}
static unsigned GetSetPokedexFlag(unsigned species,unsigned flag){(void)species;(void)flag;return caught;}
static u32 original_udiv(u32 n,u32 d){return d?n/d:0;}
static u32 Sqrt(u32 n){u32 lo=0,hi=65536;while(lo+1<hi){u32 m=lo+(hi-lo)/2;if((uint64_t)m*m<=n)lo=m;else hi=m;}return lo;}
'''+random_fn+'\nstatic void advance(void){\n'+inc+'\n}\n'+r'''
int main(void){unsigned item,hpMax,hp,rate,status,level,t1,t2,map,flag,counter,seed;
while(scanf("%u %u %u %u %u %u %u %u %u %u %u %u",&item,&hpMax,&hp,&rate,&status,&level,&t1,&t2,&map,&flag,&counter,&seed)==12){
 gLastUsedItem=item;gBattleMons[0].maxHP=hpMax;gBattleMons[0].hp=hp;gBattleMons[0].level=level;gBattleMons[0].type1=t1;gBattleMons[0].type2=t2;gBattleMons[0].status1=status;
 currentMap=map;caught=flag;gBattleResults.battleTurnCounter=counter;gRngValue=seed;drawCount=0;for(unsigned i=0;i<4;i++)rawDraws[i]=0;
 u8 ballMultiplier=0,catchRate=rate,shakes=4,checks=0;u32 odds;int threshold=-1;
'''+block+r'''
 u32 catchValue=odds;
 if(odds<=254){
'''+shake+r'''
 }
 advance();
 printf("%u %u %u %u %d %u %u %u %u %u %u %u %u %u\n",observedMultiplier,observedBall,observedHp,catchValue,threshold,checks,shakes,gRngValue,drawCount,rawDraws[0],rawDraws[1],rawDraws[2],rawDraws[3],gBattleResults.battleTurnCounter);
}return 0;}
'''
cf=a.output/'oracle.c';cf.write_text(c)
v=[]
def add(**kw):
 d=dict(ball=4,maxHp=101,hp=101,catchRate=45,status=0,level=40,type1=0,type2=0,mapType=1,caught=0,counter=0,seed=31);d.update(kw);v.append(d)
for ball in [1,2,3,4,6,7,8,9,10,11,12]:
 for status in [0,1,32,8,16,64,128]:
  for rate in [1,3,45,127,255]:
   for hp in [1,101]:
    for seed in [0,31,0xffffffff]:add(ball=ball,status=status,catchRate=rate,hp=hp,seed=seed)
for counter in range(256):add(ball=10,counter=counter,catchRate=255,hp=1)
for level in range(1,101):add(ball=8,level=level,catchRate=45,hp=51)
for t1 in range(18):
 for t2 in range(18):add(ball=6,type1=t1,type2=t2)
for mapType in range(10):add(ball=7,mapType=mapType)
for caught in [0,1]:add(ball=9,caught=caught)
# hp/catch guaranteed boundary and maximum valid numeric intermediates.
for rate in [254,255]:add(ball=4,catchRate=rate,maxHp=1,hp=1,status=128)
add(ball=4,catchRate=255,maxHp=65535,hp=1) # catch value 254, shake checks
add(ball=2,catchRate=128,maxHp=65535,hp=1) # catch value 255, no draws
for raw in [65534,65535]:
 seed=(((raw<<16)-24691)*pow(1103515245,-1,2**32))%(2**32)
 add(ball=4,catchRate=255,maxHp=65535,hp=1,seed=seed)
for ball in [7,8,10]:add(ball=ball,catchRate=255,maxHp=65535,hp=1,mapType=5,level=1,counter=30,status=32)
inputs=''.join(' '.join(map(str,d.values()))+'\n' for d in v).encode();(a.output/'inputs.txt').write_bytes(inputs)
outputs=[]
for name,flags in [('debug',['-O0']),('optimized',['-O2']),('ubsan',['-O1','-fsanitize=undefined','-fno-sanitize-recover=all'])]:
 exe=a.output/name;cmd=['flock','/tmp/firered-remake-build.lock','cc','-std=c11','-Wall','-Wextra','-Werror',*flags,str(cf),'-o',str(exe)];subprocess.run(cmd,check=True)
 outputs.append(subprocess.check_output([str(exe.resolve())],input=inputs))
assert outputs[0]==outputs[1]==outputs[2]
rows=[list(map(int,line.split())) for line in outputs[0].decode().splitlines()];assert len(rows)==len(v)
result=dict(sourceCommit=commit,sourceHashes=expected,adaptations=['instrumented observations only','safe original-layout Master byte','explicit original unsigned divide-by-zero stub','integer floor BIOS Sqrt stub','inert caught-species and map lookups'],fields=['multiplier','ballAdjusted','hpAdjusted','catchValue','threshold','checks','animation','finalSeed','drawCount','draw1','draw2','draw3','draw4','nextCounter'],vectors=[dict(input=d,output=r) for d,r in zip(v,rows)])
(a.output/'source-vectors.json').write_text(json.dumps(result,separators=(',',':'))+'\n')
(a.output/'manifest.json').write_text(json.dumps(dict(passed=True,count=len(v),sourceCommit=commit,sourceHashes=expected,oracleSha256=hashlib.sha256(c.encode()).hexdigest(),debugOptimizedSanitizedAgree=True),indent=2)+'\n')
print('SOURCE_CAPTURE_ORACLE_PASS',len(v))
