static unsigned DrawCount(u32 before,u32 after){unsigned n=0;while(before!=after&&n<32){before=ISO_RANDOMIZE1(before);n++;}return n;}
int main(void){
 const u16 moves[]={MOVE_HYPER_BEAM,MOVE_FRENZY_PLANT,MOVE_BLAST_BURN,MOVE_HYDRO_CANNON,MOVE_THRASH,MOVE_OUTRAGE,MOVE_PETAL_DANCE};
 const u32 seeds[]={0,31};
 for(unsigned i=0;i<7;i++)for(unsigned s=0;s<2;s++)for(unsigned repeated=0;repeated<(i<4?1u:2u);repeated++){
  Reset(seeds[s]);gCurrentMove=moves[i];gBattleMons[0].pp[0]=5;
  if(repeated)gBattleMons[0].status2=STATUS2_MULTIPLETURNS|STATUS2_LOCK_CONFUSE_TURN(2);
  bool8 hit=Random()%100+1<=gBattleMoves[gCurrentMove].accuracy;
  if(!repeated||!hit)Cmd_ppreduce();
  if(hit){
   if(i>=4)Cmd_confuseifrepeatingattackends();
   else gBattleCommunication[MOVE_EFFECT_BYTE]=MOVE_EFFECT_RECHARGE|MOVE_EFFECT_AFFECTS_USER|MOVE_EFFECT_CERTAIN;
   (void)Random();(void)Random(); /* critcalc and adjustnormaldamage; exact script order asserted */
   Cmd_seteffectwithchance();
  }
  printf("{\"kind\":\"action\",\"move\":%u,\"seed\":%u,\"repeated\":%u,\"hit\":%u,\"pp\":%u,\"ppWrites\":%u,\"remaining\":%u,\"recharge\":%u,\"finalSeed\":%u,\"draws\":%u}\n",moves[i],seeds[s],repeated,hit,gBattleMons[0].pp[0],ppWrites,(gBattleMons[0].status2&STATUS2_LOCK_CONFUSE)>>10,gDisableStructs[0].rechargeTimer,gRngValue,DrawCount(seeds[s],gRngValue));
 }
 for(unsigned mode=0;mode<6;mode++){
  Reset(31);gBattleMons[0].status2=STATUS2_MULTIPLETURNS|STATUS2_LOCK_CONFUSE_TURN(mode==0?2:1);
  if(mode==2)gBattleMons[0].ability=ABILITY_OWN_TEMPO;
  if(mode==3)gBattleMons[0].status2|=STATUS2_CONFUSION_TURN(3);
  if(mode==4)gProtectStructs[0].targetNotAffected=1;
  if(mode==5)gProtectStructs[0].prlzImmobility=1;
  SourceThrashResidual();
  printf("{\"kind\":\"residual\",\"case\":%u,\"remaining\":%u,\"confusion\":%u,\"multiple\":%u,\"announcements\":%u,\"finalSeed\":%u,\"draws\":%u}\n",mode,(gBattleMons[0].status2&STATUS2_LOCK_CONFUSE)>>10,gBattleMons[0].status2&STATUS2_CONFUSION,(gBattleMons[0].status2&STATUS2_MULTIPLETURNS)!=0,announcements,gRngValue,DrawCount(31,gRngValue));
 }
 for(unsigned actor=0;actor<4;actor++)for(unsigned absent=0;absent<2;absent++){
  Reset(31);gBattlerAttacker=actor;gAbsentBattlerFlags=absent?(1u<<(actor^1)):0;SourceRandomTarget();
  printf("{\"kind\":\"target\",\"actor\":%u,\"absent\":%u,\"target\":%u,\"finalSeed\":%u,\"draws\":%u}\n",actor,gAbsentBattlerFlags,gBattlerTarget,gRngValue,DrawCount(31,gRngValue));
 }
 Reset(31);gBattleMons[0].status2=STATUS2_MULTIPLETURNS|STATUS2_LOCK_CONFUSE|STATUS2_RECHARGE|STATUS2_FOCUS_ENERGY;gDisableStructs[0].rechargeTimer=2;
 CancelMultiTurnMoves(0);
 printf("{\"kind\":\"cancel\",\"status2\":%u,\"recharge\":%u,\"draws\":%u}\n",gBattleMons[0].status2,gDisableStructs[0].rechargeTimer,DrawCount(31,gRngValue));
 return 0;
}
