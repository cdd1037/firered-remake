/* Reconstructed independent source arithmetic oracle.
 * Source arithmetic transcription: pokemon.c CalculateBaseDamage 2520-2606,
 * 2646, battle_script_commands.c typecalc/adjustnormaldamage. Neutral stages,
 * neutral defenders, no weather/items. Reads independent actual battle stats. */
#include <stdio.h>
int main(void) {
 int attack,level,power,defense,physical,screen,count,both,critical,stab,percent,plus,explosion;
 while(scanf("%d %d %d %d %d %d %d %d %d %d %d %d %d",&attack,&level,&power,&defense,&physical,&screen,&count,&both,&critical,&stab,&percent,&plus,&explosion)==13){
  if(plus&&!physical)attack=150*attack/100;
  if(explosion){defense/=2;if(defense==0)defense=1;}
  int damage=attack*power*(2*level/5+2)/defense/50;
  if(screen&&!critical){if(count==2)damage=2*(damage/3);else damage/=2;}
  if(both&&count==2)damage/=2;
  if(physical&&damage==0)damage=1;
  damage+=2;if(critical)damage*=2;if(stab)damage=damage*15/10;
  damage=damage*percent/100;if(damage==0)damage=1;
  printf("%d\n",damage);fflush(stdout);
 }
 return 0;
}
