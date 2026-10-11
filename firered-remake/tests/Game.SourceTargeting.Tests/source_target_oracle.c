/* Harness for exact source extraction by source_probe.py from pinned pret/pokefirered
 * 037335f4c725d7c9aecdac87066f2002b4bd7e14. */
#include <stdint.h>
#include <stdio.h>
enum { SELECTED=0, DEPENDS=1, USER_OR_SELECTED=2, RANDOM=4, BOTH=8, USER=16, ALL=32, FIELD=64 };
static uint32_t seed,draws;
static unsigned Random(void) { seed=seed*1103515245u+24691u; draws++; return seed>>16; }
static int absent,lightning,hp,followTimer,followTarget,actor,selected,targetMode,electric,redirected;
static int side(int t) { return t&1; }
/* SOURCE_FUNCTIONS */
int main(void) {
 int modes[]={SELECTED,RANDOM,BOTH,ALL,USER,USER_OR_SELECTED,FIELD,DEPENDS};
 for(int kind=0;kind<2;kind++)for(int scenario=0;scenario<12;scenario++)for(int a=0;a<4;a++)for(int m=0;m<8;m++)for(unsigned s=1;s<=16;s++) {
  if(kind==0&&m==5)continue;
  actor=a;selected=(scenario==7?actor^2:actor^1);targetMode=modes[m];electric=scenario>=5&&scenario<=10&&(m==0||m==4);
  if(targetMode==USER||targetMode==USER_OR_SELECTED)selected=actor;
  if(targetMode==BOTH||targetMode==ALL||targetMode==FIELD||targetMode==DEPENDS)selected=side(actor)^1;
  absent=scenario==1?1<<(side(actor)^1):scenario==2?1<<((side(actor)^1)+2):scenario==3?1<<selected:0;
  hp=scenario==4?15&~(1<<selected):scenario==10?15&~(1<<(side(actor)^1)):15;
  lightning=scenario==5?1<<((side(actor)^1)+2):scenario==6?1<<selected:scenario==7?(side(actor)==0?10:5):scenario==8||scenario==10?1<<((side(actor)^1)+2):0;
  followTimer=scenario>=8;followTarget=side(actor)^1;seed=s;draws=0;redirected=0;
  int t=kind?caller():learned();
  printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%u,%d,%u,%u,%d\n",kind,actor,selected,targetMode,electric,absent,hp,lightning,followTimer,followTarget,s,t,seed,draws,redirected);
 }
 return 0;
}
