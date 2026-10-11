static int JumpAbility(u8 ability){memset(scriptBuffer,0,sizeof(scriptBuffer));scriptBuffer[1]=BS_TARGET;scriptBuffer[2]=ability;gBattlescriptCurrInstr=scriptBuffer;Cmd_jumpifability();return gBattlescriptCurrInstr==jumpTarget;}
static int JumpStatus(u32 status){memset(scriptBuffer,0,sizeof(scriptBuffer));scriptBuffer[1]=BS_TARGET;for(int i=0;i<4;i++)scriptBuffer[2+i]=(status>>(8*i))&255;gBattlescriptCurrInstr=scriptBuffer;Cmd_jumpifstatus2();return gBattlescriptCurrInstr==jumpTarget;}
static int JumpSafeguard(void){memset(scriptBuffer,0,sizeof(scriptBuffer));scriptBuffer[1]=BS_TARGET;scriptBuffer[2]=SIDE_STATUS_SAFEGUARD;scriptBuffer[3]=SIDE_STATUS_SAFEGUARD>>8;gBattlescriptCurrInstr=scriptBuffer;Cmd_jumpifsideaffecting();return gBattlescriptCurrInstr==jumpTarget;}
int main(void){
 jumpTarget=BattleScript_PresentHealTarget;
 int maxima[]={1,2,3,4,5,100,715};
 for(int m=0;m<7;m++)for(int full=0;full<2;full++)for(int flags=0;flags<4;flags++)for(int roll=0;roll<256;roll++){
  gBattlerTarget=1;gBattleMons[1].maxHP=maxima[m];gBattleMons[1].hp=full?maxima[m]:0;gMoveResultFlags=(flags&1?MOVE_RESULT_DOESNT_AFFECT_FOE:0)|(flags&2?MOVE_RESULT_MISSED:0);gDynamicBasePower=0;gBattleMoveDamage=0;randomCalls=0;nextRandom=roll;Cmd_presentdamagecalculation();
  printf("{\"kind\":\"present\",\"max\":%d,\"full\":%d,\"inputFlags\":%d,\"roll\":%d,\"power\":%d,\"damage\":%d,\"flags\":%d,\"branch\":%d,\"draws\":%d}\n",maxima[m],full,flags,roll,gDynamicBasePower,gBattleMoveDamage,gMoveResultFlags,*gBattlescriptCurrInstr,randomCalls);
 }
 for(int terrain=0;terrain<20;terrain++){gBattleTerrain=terrain;gBattlescriptCurrInstr=scriptBuffer;Cmd_getsecretpowereffect();printf("{\"kind\":\"secret\",\"terrain\":%d,\"effect\":%d}\n",terrain,gBattleCommunication[MOVE_EFFECT_BYTE]);}
 for(int serene=0;serene<2;serene++)for(int noEffect=0;noEffect<2;noEffect++)for(int roll=0;roll<100;roll++){
  gCurrentMove=290;gBattlerAttacker=0;gBattleMons[0].ability=serene?ABILITY_SERENE_GRACE:0;gBattleMoves[290].secondaryEffectChance=30;gMoveResultFlags=noEffect?MOVE_RESULT_DOESNT_AFFECT_FOE:0;gBattleCommunication[MOVE_EFFECT_BYTE]=MOVE_EFFECT_SLEEP;effectApplied=randomCalls=0;nextRandom=roll;gBattlescriptCurrInstr=scriptBuffer;Cmd_seteffectwithchance();printf("{\"kind\":\"chance\",\"serene\":%d,\"noEffect\":%d,\"roll\":%d,\"apply\":%d,\"draws\":%d}\n",serene,noEffect,roll,effectApplied,randomCalls);
 }
 // Original branch commands driven in the exact audited Teeter loop order.
 // Protection/accuracy are bounded host commands (100% unobstructed or Protect).
 for(int actor=0;actor<4;actor++)for(int mode=0;mode<32;mode++){
  gBattlerAttacker=actor;int visited=0,accuracy=0,confused=0,ability=0,sub=0,existing=0,guard=0,protect=0;
  memset(gBattleMons,0,sizeof(gBattleMons));gSideStatuses[0]=gSideStatuses[1]=(mode&8)?SIDE_STATUS_SAFEGUARD:0;
  for(int i=0;i<4;i++){gBattleMons[i].hp=(mode&16)?0:100;gBattleMons[i].ability=i==0&&(mode&1)?ABILITY_OWN_TEMPO:0;gBattleMons[i].status2=i==1&&(mode&2)?STATUS2_SUBSTITUTE:i==2&&(mode&4)?STATUS2_CONFUSION:0;}
  for(gBattlerTarget=0;gBattlerTarget<4;gBattlerTarget++){
   visited|=1<<gBattlerTarget;if(gBattlerTarget==gBattlerAttacker)continue;
   if(JumpAbility(ABILITY_OWN_TEMPO)){ability|=1<<gBattlerTarget;continue;}
   if(JumpStatus(STATUS2_SUBSTITUTE)){sub|=1<<gBattlerTarget;continue;}
   if(JumpStatus(STATUS2_CONFUSION)){existing|=1<<gBattlerTarget;continue;}
   if(gBattlerTarget==3&&(mode&4)){protect|=1<<gBattlerTarget;continue;}
   accuracy|=1<<gBattlerTarget;
   if(JumpSafeguard()){guard|=1<<gBattlerTarget;continue;}
   if(gBattleMons[gBattlerTarget].hp)confused|=1<<gBattlerTarget;
  }
  printf("{\"kind\":\"teeter\",\"actor\":%d,\"mode\":%d,\"visited\":%d,\"accuracy\":%d,\"confused\":%d,\"ability\":%d,\"substitute\":%d,\"existing\":%d,\"safeguard\":%d,\"protect\":%d}\n",actor,mode,visited,accuracy,confused,ability,sub,existing,guard,protect);
 }
 return 0;
}
