int main(void) {
 printf("[");int first=1;
 for(int family=0;family<4;family++)for(int mode=0;mode<12;mode++)for(int si=0;si<4;si++) {
  unsigned initial=(unsigned[]){0,2,6,31}[si];
  memset(gBattleMons,0,sizeof(gBattleMons));memset(gStatuses3,0,sizeof(gStatuses3));memset(gDisableStructs,0,sizeof(gDisableStructs));
  gBattleMons[2].statStages[STAT_ACC]=gBattleMons[3].statStages[STAT_EVASION]=6;
  gBattleWeather=0;suppressed=0;gHitMarker=HITMARKER_IGNORE_ON_AIR;gMoveResultFlags=0;protectedTarget=mode==1;seed=initial;draws=0;
  if(mode==2)gStatuses3[3]=STATUS3_ON_AIR;
  if(mode==3)gStatuses3[3]=STATUS3_UNDERGROUND;
  if(mode==4)gStatuses3[3]=STATUS3_UNDERWATER;
  if(mode==5||mode==6){gBattleWeather=B_WEATHER_RAIN;suppressed=mode==6;}
  if(mode==7||mode==8){gBattleWeather=B_WEATHER_SUN;suppressed=mode==8;}
  if(mode==9){gStatuses3[3]=STATUS3_UNDERGROUND|STATUS3_ALWAYS_HITS;gDisableStructs[3].battlerWithSureHit=2;}
  if(mode==10){gBattleMons[2].statStages[STAT_ACC]=0;gBattleMons[3].item=1;}
  if(mode==11){gBattleWeather=B_WEATHER_RAIN;gStatuses3[3]=STATUS3_UNDERWATER;}
  gBattleMoves[1]=(struct Move){40,family==2?13:family==1?16:0,family==2?EFFECT_THUNDER:1,family==2?70:family==3?90:100,family==1?MOVE_TARGET_BOTH:0};
  gBattlescriptCurrInstr=script;Cmd_accuracycheck();
  printf("%s{\"family\":%d,\"mode\":%d,\"seed\":%u,\"hit\":%s,\"draws\":%d,\"finalSeed\":%u}",first?"":",",family,mode,initial,gMoveResultFlags?"false":"true",draws,seed);first=0;
 }
 printf("]\n");return 0;
}
