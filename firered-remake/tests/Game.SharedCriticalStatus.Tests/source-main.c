int main(void) {
 for(int effect=0;effect<3;effect++)for(int mode=0;mode<12;mode++)for(int serene=0;serene<2;serene++)for(int roll=0;roll<100;roll++){
  memset(gBattleMons,0,sizeof gBattleMons);memset(gBattleCommunication,0,sizeof gBattleCommunication);memset(gSideStatuses,0,sizeof gSideStatuses);
  gHitMarker=gBattleTypeFlags=gMoveResultFlags=gBattleWeather=0;draws=writes=cancels=0;weatherHasEffect=TRUE;
  nextRandom=roll;statusRandom=0;gBattlerAttacker=2;gBattlerTarget=3;
  gBattleMons[2].ability=serene?ABILITY_SERENE_GRACE:ABILITY_NONE;gBattleMons[3].hp=100;gBattleMons[3].type1=gBattleMons[3].type2=TYPE_NORMAL;
  if(mode==1)gBattleMons[3].ability=ABILITY_SHIELD_DUST;
  if(mode==2)gSideStatuses[1]=SIDE_STATUS_SAFEGUARD;
  if(mode==3)gBattleMons[3].status1=STATUS1_POISON;
  if(mode==4)gBattleMons[3].hp=0;
  if(mode==5)gBattleMons[3].status2=STATUS2_SUBSTITUTE;
  if(mode==6)gBattleMons[3].type1=TYPE_POISON;
  if(mode==7)gBattleMons[3].type1=TYPE_STEEL;
  if(mode==8)gBattleMons[3].ability=ABILITY_IMMUNITY;
  if(mode==9)gBattleMons[3].type1=TYPE_FIRE;
  if(mode==10)gBattleMons[3].ability=ABILITY_WATER_VEIL;
  if(mode==11)gMoveResultFlags=MOVE_RESULT_DOESNT_AFFECT_FOE;
  gBattleMoves[0].secondaryEffectChance=effect==2?30:10;gCurrentMove=0;gBattleCommunication[MOVE_EFFECT_BYTE]=effect==0?MOVE_EFFECT_BURN:effect==1?MOVE_EFFECT_POISON:MOVE_EFFECT_TOXIC;gBattlescriptCurrInstr=script;
  Cmd_seteffectwithchance();
  printf("{\"effect\":%d,\"mode\":%d,\"serene\":%d,\"roll\":%d,\"status\":%u,\"draws\":%u,\"writes\":%u,\"synchronize\":%u,\"noEffect\":%u}\n",effect,mode,serene,roll,gBattleMons[3].status1,draws,writes,(gHitMarker&HITMARKER_SYNCHRONISE_EFFECT)!=0,(gMoveResultFlags&MOVE_RESULT_DOESNT_AFFECT_FOE)!=0);
 }
 return 0;
}
