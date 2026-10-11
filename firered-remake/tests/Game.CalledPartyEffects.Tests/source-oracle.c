/* Bounded command transcription, not a GBA emulator.
 * FireRed 037335f4c725d7c9aecdac87066f2002b4bd7e14:
 * battle_script_commands.c:7003,7699,7995,8110,8571,8681,8800,8884,8999,9366
 * battle_main.c:312,2329. Conversion2 intentionally uses approved modern candidates and one engine draw. */
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#define LIMIT 20000
static uint32_t state; static unsigned draws; static uint16_t raw[LIMIT];
static uint16_t next(void) { if(draws==LIMIT) abort(); state=state*1103515245u+24691u; return raw[draws++]=(uint16_t)(state>>16); }
static void reset(uint32_t seed){state=seed;draws=0;}
static void trace(void){printf("\"seed\":%u,\"raw\":[",state);for(unsigned i=0;i<draws;i++)printf("%s%u",i?",":"",raw[i]);printf("]");}
static int chart[112][3]={
    {0,5,5},
    {0,8,5},
    {10,10,5},
    {10,11,5},
    {10,12,20},
    {10,15,20},
    {10,6,20},
    {10,5,5},
    {10,16,5},
    {10,8,20},
    {11,10,20},
    {11,11,5},
    {11,12,5},
    {11,4,20},
    {11,5,20},
    {11,16,5},
    {13,11,20},
    {13,13,5},
    {13,12,5},
    {13,4,0},
    {13,2,20},
    {13,16,5},
    {12,10,5},
    {12,11,20},
    {12,12,5},
    {12,3,5},
    {12,4,20},
    {12,2,5},
    {12,6,5},
    {12,5,20},
    {12,16,5},
    {12,8,5},
    {15,11,5},
    {15,12,20},
    {15,15,5},
    {15,4,20},
    {15,2,20},
    {15,16,20},
    {15,8,5},
    {15,10,5},
    {1,0,20},
    {1,15,20},
    {1,3,5},
    {1,2,5},
    {1,14,5},
    {1,6,5},
    {1,5,20},
    {1,17,20},
    {1,8,20},
    {3,12,20},
    {3,3,5},
    {3,4,5},
    {3,5,5},
    {3,7,5},
    {3,8,0},
    {4,10,20},
    {4,13,20},
    {4,12,5},
    {4,3,20},
    {4,2,0},
    {4,6,5},
    {4,5,20},
    {4,8,20},
    {2,13,5},
    {2,12,20},
    {2,1,20},
    {2,6,20},
    {2,5,5},
    {2,8,5},
    {14,1,20},
    {14,3,20},
    {14,14,5},
    {14,17,0},
    {14,8,5},
    {6,10,5},
    {6,12,20},
    {6,1,5},
    {6,3,5},
    {6,2,5},
    {6,14,20},
    {6,7,5},
    {6,17,20},
    {6,8,5},
    {5,10,20},
    {5,15,20},
    {5,1,5},
    {5,4,5},
    {5,2,20},
    {5,6,20},
    {5,8,5},
    {7,0,0},
    {7,14,20},
    {7,17,5},
    {7,8,5},
    {7,7,20},
    {16,16,20},
    {16,8,5},
    {17,1,5},
    {17,14,20},
    {17,7,20},
    {17,17,5},
    {17,8,5},
    {8,10,5},
    {8,11,5},
    {8,13,5},
    {8,15,20},
    {8,5,20},
    {8,8,5},
    {254,254,0},
    {0,7,0},
    {1,7,0},
    {255,255,0},
};
static void conversion(unsigned seed){
 reset(seed);int i;int types[3]={0,10,11}; do{do{i=next()&3;}while(i>=3);}while(types[i]==0);
 printf("{\"input\":%u,\"type\":%d,",seed,types[i]);trace();printf("}");
}
static int candidates(int attack,int current1,int current2,int output[18]){
 int count=0;for(int defense=0;defense<18;defense++){
  if(defense==9||defense==current1||defense==current2)continue;
  for(int row=0;row<112;row++)if(chart[row][0]==attack&&chart[row][1]==defense&&chart[row][2]<=5){output[count++]=defense;break;}
 }return count;
}
static void conversion2(unsigned seed,int attack,int current1,int current2){
 reset(seed);int options[18];int count=candidates(attack,current1,current2,options);int result=count?options[next()%count]:-1;
 printf("{\"input\":%u,\"type\":%d,\"attack\":%d,\"current1\":%d,\"current2\":%d,\"candidates\":[",seed,result,attack,current1,current2);
 for(int i=0;i<count;i++){printf("%s%d",i?",":"",options[i]);}printf("],");trace();printf("}");
}
static void beatup(unsigned seed){
 reset(seed);while(next()%2==0){};(void)next();int damage[2],crit[2],percent[2];int attack[2]={45,100},level[2]={35,50};
 for(int i=0;i<2;i++){damage[i]=attack[i]*10*(level[i]*2/5+2)/30/50+2;crit[i]=next()%16==0;if(crit[i])damage[i]*=2;percent[i]=100-next()%16;damage[i]=damage[i]*percent[i]/100;if(damage[i]==0)damage[i]=1;}
 printf("{\"input\":%u,\"damage\":[%d,%d],\"critical\":[%d,%d],\"percent\":[%d,%d],",seed,damage[0],damage[1],crit[0],crit[1],percent[0],percent[1]);trace();printf("}");
}
int main(void){
 printf("{\"sourceCommit\":\"037335f4c725d7c9aecdac87066f2002b4bd7e14\",\"conversion\":[");
 for(unsigned i=0;i<32;i++){if(i)printf(",");conversion(i);}printf("],\"conversion2\":[");
 for(unsigned i=0;i<256;i++){if(i)printf(",");conversion2(i,10,0,0);}printf("],\"beatup\":[");
 for(unsigned i=0;i<32;i++){if(i)printf(",");beatup(i);}printf("],\"conversion2edge\":[");
 conversion2(606994,16,0,0);printf(",");conversion2(1,16,8,13);
 printf("],\"conversion2candidateTypes\":[");
 for(int attack=0;attack<18;attack++){if(attack)printf(",");int options[18];int count=candidates(attack,-1,-1,options);printf("[");for(int i=0;i<count;i++)printf("%s%d",i?",":"",options[i]);printf("]");}
 printf("]}\n");return 0;
}
