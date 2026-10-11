int main(void){
 printf("{\"vectors\":[");int sep=0;
 for(int side=0;side<2;side++)for(int start=0;start<3;start++){
  gBattlerAttacker=side;gBattleMons[side].level=50;gPaydayMoney=start==0?0:start==1?65500:65535;gBattleCommunication[MOVE_EFFECT_BYTE]=MOVE_EFFECT_PAYDAY;
  PayDay();printf("%s{\"kind\":\"payday\",\"side\":%d,\"before\":%d,\"after\":%d}",sep++?",":"",side,start==0?0:start==1?65500:65535,gPaydayMoney);
 }
 for(int actor=0;actor<4;actor++){
  gBattlerAttacker=actor;gBattleMons[actor].status2=STATUS2_WRAPPED;gStatuses3[actor]=STATUS3_LEECHSEED|STATUS3_LEECHSEED_BATTLER;gSideStatuses[actor&1]=SIDE_STATUS_SPIKES;gSideTimers[actor&1].spikesAmount=3;
  Cmd_rapidspinfree();int wrapped=gBattleMons[actor].status2!=0;Cmd_rapidspinfree();int seeded=gStatuses3[actor]!=0;Cmd_rapidspinfree();int spikes=gSideTimers[actor&1].spikesAmount;
  printf(",{\"kind\":\"rapidspin\",\"actor\":%d,\"wrapped\":%d,\"seeded\":%d,\"spikes\":%d}",actor,wrapped,seeded,spikes);
 }
 for(int mode=0;mode<9;mode++)for(int actor=0;actor<4;actor++){
  memset(gBattleMons,0,sizeof(gBattleMons));memset(gStatuses3,0,sizeof(gStatuses3));gBattleTypeFlags=0;gActiveBattler=actor;int opponent=actor^1;
  if(mode==1)gBattleMons[opponent].ability=ABILITY_SHADOW_TAG;
  if(mode==2)gBattleMons[opponent].ability=ABILITY_ARENA_TRAP;
  if(mode==3){gBattleMons[opponent].ability=ABILITY_ARENA_TRAP;gBattleMons[actor].type=TYPE_FLYING;}
  if(mode==4){gBattleMons[actor^2].ability=ABILITY_MAGNET_PULL;gBattleMons[actor].type=TYPE_STEEL;}
  if(mode>=5)gBattleMons[actor].status2=STATUS2_WRAPPED;
  if(mode==6)gBattleMons[actor].ability=ABILITY_RUN_AWAY;
  if(mode==7)gBattleMons[actor].item=ITEM_SMOKE_BALL;
  if(mode==8)gStatuses3[actor]=STATUS3_ROOTED;
  printf(",{\"kind\":\"escape\",\"mode\":%d,\"actor\":%d,\"blocked\":%d}",mode,actor,IsRunningFromBattleImpossible()!=BATTLE_RUN_SUCCESS);
 }
 for(int source=0;source<4;source++)for(int target=0;target<4;target++)for(int sound=0;sound<2;sound++){
  memset(gBattleMons,0,sizeof(gBattleMons));gBattleMons[source].status2=STATUS2_UPROAR;gBattleMons[target].ability=sound?ABILITY_SOUNDPROOF:ABILITY_NONE;
  printf(",{\"kind\":\"uproar\",\"source\":%d,\"target\":%d,\"soundproof\":%d,\"blocked\":%d}",source,target,sound,UproarWakeUpCheck(target));
 }
 puts("],\"complete\":true}");return 0;
}
