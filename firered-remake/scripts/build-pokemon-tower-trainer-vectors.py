#!/usr/bin/env python3
"""Compile authenticated original NPC construction, RNG and initial-move functions with memory-only data stubs."""
import argparse,importlib.util,json,pathlib,re,subprocess,tempfile,hashlib,sys
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('tower_trainers',ROOT/'scripts/import-pokemon-tower-trainers.py');imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
def function(text,name):
 m=re.search(r'^(?:static )?\w+ '+name+r'\([^\n]*\)\n\{',text,re.M)
 if not m:raise ValueError('Missing source function '+name)
 i=m.end();level=1
 while level:
  if text[i]=='{':level+=1
  elif text[i]=='}':level-=1
  i+=1
 return text[m.start():i]
def build(project,target,check=False):
 lock,raw=imp.read_sources(project);text={p:b.decode() for p,b in raw.items()};doc=imp.trainer_convert(lock,raw)
 p=text['src/pokemon.c'];battle=text['src/battle_main.c']
 blocks={name:function(p,name) for name in ['GiveMoveToBoxMon','DeleteFirstMoveAndGiveMoveToBoxMon','GiveBoxMonInitialMoveset']}
 blocks['CreateNPCTrainerParty']=function(battle,'CreateNPCTrainerParty');blocks['Random']=function(text['src/random.c'],'Random')
 m=re.search(r'        u32 shinyValue;\n        do\n        \{.*?        \} while \(shinyValue < SHINY_ODDS\);',p,re.S);assert m
 blocks['CreateBoxMonNoShiny']=m[0]
 header=r'''#include <stdint.h>
#include <stdio.h>
#include <string.h>
typedef uint8_t u8; typedef uint16_t u16; typedef uint32_t u32; typedef int32_t s32;
#define TRUE 1
#define EOS 255
#define MAX_MON_MOVES 4
#define MAX_PER_STAT_IVS 31
#define F_TRAINER_FEMALE 128
#define F_TRAINER_PARTY_CUSTOM_MOVESET 1
#define F_TRAINER_PARTY_HELD_ITEM 2
#define BATTLE_TYPE_TRAINER 8
#define BATTLE_TYPE_BATTLE_TOWER 256
#define BATTLE_TYPE_EREADER_TRAINER 2048
#define BATTLE_TYPE_TRAINER_TOWER 65536
#define TRAINER_SECRET_BASE 1024
#define OT_ID_RANDOM_NO_SHINY 2
#define MON_DATA_MOVE1 10
#define MON_DATA_MOVE2 11
#define MON_DATA_PP1 20
#define MON_DATA_PP2 21
#define MON_DATA_PP_BONUSES 30
#define MON_DATA_SPECIES 31
#define MON_DATA_HELD_ITEM 32
#define MON_HAS_MAX_MOVES 0xffff
#define MON_ALREADY_KNOWS_MOVE 0xfffe
#define LEVEL_UP_END 0xffff
#define LEVEL_UP_MOVE_LV 0xfe00
#define LEVEL_UP_MOVE_ID 0x1ff
#define SHINY_ODDS 8
#define GET_SHINY_VALUE(o,p) (((o)>>16)^((o)&0xffff)^((p)>>16)^((p)&0xffff))
#define ISO_RANDOMIZE1(v) (1103515245u*(v)+24691u)
#define Random32() (Random() | ((u32)Random() << 16))
struct BoxPokemon {u16 species,moves[4];u8 level,iv,pp[4],ppBonuses;u32 personality,ot;};
struct Pokemon {struct BoxPokemon box;};
struct TrainerMonNoItemDefaultMoves {u16 iv;u8 lvl;u16 species;};
struct TrainerMonNoItemCustomMoves {u16 iv;u8 lvl;u16 species,moves[4];};
struct TrainerMonItemDefaultMoves {u16 iv;u8 lvl;u16 species,heldItem;};
struct TrainerMonItemCustomMoves {u16 iv;u8 lvl;u16 species,heldItem,moves[4];};
union TrainerMonPtr {const struct TrainerMonNoItemDefaultMoves *NoItemDefaultMoves;const struct TrainerMonNoItemCustomMoves *NoItemCustomMoves;const struct TrainerMonItemDefaultMoves *ItemDefaultMoves;const struct TrainerMonItemCustomMoves *ItemCustomMoves;};
struct Trainer {u8 partyFlags,encounterMusic_gender,trainerName[12],doubleBattle,partySize;union TrainerMonPtr party;};
static struct Pokemon gEnemyParty[6];static u32 gBattleTypeFlags=BATTLE_TYPE_TRAINER,gRngValue;static unsigned drawCount;
static void ZeroEnemyPartyMons(void){memset(gEnemyParty,0,sizeof(gEnemyParty));}
static u32 GetBoxMonData(struct BoxPokemon*b,int f,void*unused){(void)unused;if(f==MON_DATA_SPECIES)return b->species;if(f==MON_DATA_PP_BONUSES)return b->ppBonuses;if(f>=MON_DATA_MOVE1&&f<MON_DATA_MOVE1+4)return b->moves[f-MON_DATA_MOVE1];if(f>=MON_DATA_PP1&&f<MON_DATA_PP1+4)return b->pp[f-MON_DATA_PP1];return 0;}
static void SetBoxMonData(struct BoxPokemon*b,int f,const void*v){if(f==MON_DATA_PP_BONUSES)b->ppBonuses=*(u8*)v;else if(f>=MON_DATA_MOVE1&&f<MON_DATA_MOVE1+4)b->moves[f-MON_DATA_MOVE1]=*(u16*)v;else if(f>=MON_DATA_PP1&&f<MON_DATA_PP1+4)b->pp[f-MON_DATA_PP1]=*(u8*)v;}
static void SetMonData(struct Pokemon*b,int f,const void*v){SetBoxMonData(&b->box,f,v);}
static u8 GetLevelFromBoxMonExp(struct BoxPokemon*b){return b->level;}
'''
 tables=[];train=[];ids=[];species={}
 for t in doc['trainers']:
  tid=t['sourceTrainerId'];ids.append(tid)
  tables.append('static const struct TrainerMonNoItemDefaultMoves party_'+str(tid)+'[]={'+','.join('{'+','.join(str(m[k]) for k in ['sourceIv','level','sourceSpeciesId'])+'}' for m in t['party'])+'};')
  train.append(f'[{tid}]={{0,{t["encounterMusicGender"]},{{'+','.join(map(str,t['encodedTrainerName']+[255]))+'},0,'+str(len(t['party']))+',{.NoItemDefaultMoves=party_'+str(tid)+'}}')
  for m in t['party']:species[m['sourceSpeciesId']]=m
 tables.append('static const struct Trainer gTrainers[1025]={'+','.join(train)+'};')
 tables.append('static const u8 gSpeciesNames[512][16]={'+','.join('['+str(i)+']={'+','.join(map(str,m['encodedSpeciesName']+[255]))+'}' for i,m in species.items())+'};')
 definitions={m:int(n) for m,n in re.findall(r'^#define (MOVE_\w+)\s+(\d+)\b',text['include/constants/moves.h'],re.M)}
 learnPointers=[]
 for i,m in species.items():
  symbol=m['species']['value'][5:];pointer=re.search(r'^    \['+symbol+r'\] = (\w+),$',text['src/data/pokemon/level_up_learnset_pointers.h'],re.M)[1]
  body=re.search(r'^static const u16 '+pointer+r'\[\] = \{\n(.*?)^\};',text['src/data/pokemon/level_up_learnsets.h'],re.M|re.S)[1]
  moves=[(int(lv)<<9)|definitions[move] for lv,move in re.findall(r'LEVEL_UP_MOVE\((\d+), (MOVE_\w+)\)',body)]
  tables.append('static const u16 learn_'+str(i)+'[]={'+','.join(map(str,moves+[65535]))+'};');learnPointers.append('['+str(i)+']=learn_'+str(i))
 tables.append('static const u16 *gLevelUpLearnsets[512]={'+','.join(learnPointers)+'};')
 pp=[]
 for sym,num in definitions.items():
  move=re.search(r'^    \['+sym+r'\] =\s*\n    \{\n(.*?)^    \},',text['src/data/battle_moves.h'],re.M|re.S)
  if move:pp.append('['+str(num)+']={'+re.search(r'\.pp = (\d+)',move[1])[1]+'}')
 tables.append('static const struct {u8 pp;} gBattleMoves[512]={'+','.join(pp)+'};')
 code=header+'\n'.join(tables)+'\n'+blocks['Random'].replace('Random(void)','SourceRandom(void)')+'\nstatic u16 Random(void){drawCount++;return SourceRandom();}\n'+'\n'.join(blocks[n] for n in ['GiveMoveToBoxMon','DeleteFirstMoveAndGiveMoveToBoxMon','GiveBoxMonInitialMoveset'])
 code+='\nstatic void CreateMon(struct Pokemon *mon,u16 species,u8 level,u8 fixedIV,u8 hasFixedPersonality,u32 personality,u8 otIdType,u32 fixedOtId){(void)hasFixedPersonality;(void)otIdType;(void)fixedOtId;u32 value;\n'+blocks['CreateBoxMonNoShiny']+'\nmon->box.species=species;mon->box.level=level;mon->box.iv=fixedIV;mon->box.personality=personality;mon->box.ot=value;GiveBoxMonInitialMoveset(&mon->box);}\n'+blocks['CreateNPCTrainerParty']
 code+='\nstatic int ids[]={'+','.join(map(str,ids))+'};\n'
 # Include a rejection seed for each first member. The source C, not this search, supplies expected output.
 seeds={0,1,0x12345678,0xffffffff}
 for t in doc['trainers']:
  pid=(0x78 if t['encounterMusicGender']&128 else 0x88)+(sum(t['encodedTrainerName']+t['party'][0]['encodedSpeciesName'])<<8)
  for seed in range(200000):
   a=(seed*1103515245+24691)&0xffffffff;b=(a*1103515245+24691)&0xffffffff;ot=(a>>16)|((b>>16)<<16)
   if ((ot>>16)^(ot&65535)^(pid>>16)^(pid&65535))<8:seeds.add(seed);break
  else:raise ValueError('No rejection seed')
 code+='static u32 seeds[]={'+','.join(str(x)+'u' for x in sorted(seeds))+'};\n'
 code+=r'''int main(void){puts("[");int comma=0;for(unsigned j=0;j<sizeof(ids)/sizeof(ids[0]);j++)for(unsigned k=0;k<sizeof(seeds)/sizeof(seeds[0]);k++){gRngValue=seeds[k];drawCount=0;u8 count=CreateNPCTrainerParty(gEnemyParty,ids[j]);printf("%s{\"trainerId\":%d,\"seed\":%u,\"after\":%u,\"draws\":%u,\"party\":[",comma?",\n":"",ids[j],seeds[k],gRngValue,drawCount);comma=1;for(int i=0;i<count;i++){struct BoxPokemon*m=&gEnemyParty[i].box;printf("%s{\"species\":%u,\"level\":%u,\"iv\":%u,\"personality\":%u,\"ot\":%u,\"moves\":[%u,%u,%u,%u],\"pp\":[%u,%u,%u,%u]}",i?",":"",m->species,m->level,m->iv,m->personality,m->ot,m->moves[0],m->moves[1],m->moves[2],m->moves[3],m->pp[0],m->pp[1],m->pp[2],m->pp[3]);}printf("]}");}puts("\n]");return 0;}'''
 directory=target/'tests/Game.PokemonTowerTrainers.Tests';directory.mkdir(parents=True,exist_ok=True)
 with tempfile.TemporaryDirectory() as tmp:
  src=pathlib.Path(tmp)/'oracle.c';exe=pathlib.Path(tmp)/'oracle';src.write_text(code)
  subprocess.run(['cc','-std=c11','-O0','-Wall','-Wextra','-Werror',str(src),'-o',str(exe)],check=True)
  vectors=json.loads(subprocess.check_output([str(exe)]))
 output=imp.canonical(dict(schemaVersion=1,sourceCommit=imp.COMMIT,sourceLockSha256=imp.LOCK_SHA256,extractedBlocks=[dict(name=n,sha256=hashlib.sha256(b.encode()).hexdigest()) for n,b in blocks.items()],harness='Original NPC, move queue, no-shiny loop and RNG; memory-only accessors and CreateMon capture stub. Does not independently verify stats, battle or callback execution.',vectors=vectors))
 for path,data in [('source-vectors.c',code.encode()),('source-vectors.json',output)]:
  dest=directory/path
  if check:
   if dest.read_bytes()!=data:raise ValueError('Source vector differs: '+path)
  else:dest.write_bytes(data)
 return dict(passed=True,vectors=len(vectors),sourceTeams=len(ids),seeds=len(seeds),rejectionVectors=sum(v['draws']>2*len(v['party']) for v in vectors),sha256=hashlib.sha256(output).hexdigest())
if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--project-root',type=pathlib.Path,required=True);a.add_argument('--target-root',type=pathlib.Path,default=ROOT);a.add_argument('--check',action='store_true');v=a.parse_args();print(json.dumps(build(v.project_root,v.target_root,v.check),sort_keys=True))
