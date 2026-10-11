int main(void) {
 for (int op=0;op<4;op++) for(int level=1;level<=100;level+=11) for(int s=0;s<32;s++) {
  memset(gBattleMons,0,sizeof(gBattleMons));gBattlerAttacker=2;gBattlerTarget=3;
  gBattleMons[2].level=level;gBattleMons[2].hp=level;gBattleMons[3].hp=s*7+1;
  seed=s;rngCount=0;gBattleMoveDamage=777;gBattlescriptCurrInstr=command;
  if(op==0)Cmd_psywavedamageeffect();else if(op==1)Cmd_dmgtolevel();else if(op==2)Cmd_damagetohalftargethp();else Cmd_setdamagetohealthdifference();
  printf("{\"kind\":\"math\",\"op\":%d,\"level\":%d,\"targetHp\":%d,\"seed\":%d,\"damage\":%d,\"failed\":%d,\"draws\":%u,\"finalSeed\":%u}\n",op,level,s*7+1,s,gBattleMoveDamage,gBattlescriptCurrInstr==koFailure,rngCount,seed);
 }
 for(int swipe=0;swipe<2;swipe++)for(int endure=0;endure<2;endure++)for(int sub=0;sub<2;sub++)for(int immune=0;immune<2;immune++)for(int hp=1;hp<=20;hp+=19)for(int damage=0;damage<=40;damage+=20){
  memset(gBattleMons,0,sizeof(gBattleMons));memset(gSpecialStatuses,0,sizeof(gSpecialStatuses));memset(gProtectStructs,0,sizeof(gProtectStructs));
  gBattlerAttacker=2;gBattlerTarget=3;gBattleMons[3].hp=hp;gBattleMons[3].status2=sub?STATUS2_SUBSTITUTE:0;gDisableStructs[3].substituteHP=sub?10:0;
  gProtectStructs[3].endured=endure;gBattleMoves[0].effect=swipe?EFFECT_FALSE_SWIPE:0;gCurrentMove=0;gBattleMoves[0].type=0;
  gMoveResultFlags=immune?MOVE_RESULT_DOESNT_AFFECT_FOE:0;gBattleMoveDamage=damage;gBattlescriptCurrInstr=command;gHpDealt=999;Cmd_adjustsetdamage();
  int adjusted=gBattleMoveDamage,flags=gMoveResultFlags;const u8 hpCommand[8]={0,BS_TARGET};gBattlescriptCurrInstr=hpCommand;Cmd_datahpupdate();
  printf("{\"kind\":\"adjust\",\"swipe\":%d,\"endure\":%d,\"substitute\":%d,\"immune\":%d,\"hp\":%d,\"damage\":%d,\"adjusted\":%d,\"flags\":%d,\"afterHp\":%d,\"hpDealt\":%d,\"receipt\":%d,\"physical\":%d}\n",swipe,endure,sub,immune,hp,damage,adjusted,flags,gBattleMons[3].hp,gHpDealt,gSpecialStatuses[3].dmg,gSpecialStatuses[3].physicalDmg);
 }
 return 0;
}
