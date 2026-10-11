int main(void){
 puts("{\"attract\":[");
 for(int k=0;k<6;k++){reset();gPlayerParty[0].personality=0;gEnemyParty[0].personality=1;
 if(k==1)gEnemyParty[0].personality=0;if(k==2)gEnemyParty[0].personality=255;if(k==3)gBattleMons[1].ability=ABILITY_OBLIVIOUS;if(k==4)gBattleMons[1].status2=STATUS2_INFATUATED_WITH(2);if(k==5){gBattlerAttacker=2;gBattlerTarget=3;gPlayerParty[1].personality=0;gEnemyParty[1].personality=1;}
 Cmd_tryinfatuating();printf("%s{\"case\":%d,\"success\":%s,\"status\":%u,\"abilities\":%d}",k?",":"",k,gBattlescriptCurrInstr==instructions+5?"true":"false",gBattleMons[gBattlerTarget].status2,abilities);}
 puts("],\"taunt\":[");for(int k=0;k<3;k++){reset();gDisableStructs[1].tauntTimer=k;Cmd_settaunt();printf("%s{\"before\":%d,\"success\":%s,\"after\":%d}",k?",":"",k,gBattlescriptCurrInstr==instructions+5?"true":"false",gDisableStructs[1].tauntTimer);}
 puts("],\"torment\":[");for(int k=0;k<2;k++){reset();gBattleMons[1].status2=k?STATUS2_TORMENT:0;Cmd_settorment();printf("%s{\"before\":%d,\"success\":%s,\"status\":%u}",k?",":"",k,gBattlescriptCurrInstr==instructions+5?"true":"false",gBattleMons[1].status2);}
 puts("],\"spite\":[");for(int k=0;k<10;k++){reset();gLastMoves[1]=MOVE_TACKLE;gBattleMons[1].moves[2]=MOVE_TACKLE;gBattleMons[1].pp[2]=k<4?k:8;roll=3;
 if(k==4)gLastMoves[1]=MOVE_UNAVAILABLE;if(k==5)gLastMoves[1]=MOVE_NONE;if(k==6)gLastMoves[1]=MOVE_GROWL;if(k==7)gBattleMons[1].status2=STATUS2_TRANSFORMED;if(k==8)gDisableStructs[1].mimickedMoves=4;if(k==9){gBattleMons[1].moves[0]=MOVE_TACKLE;gBattleMons[1].pp[0]=0;}
 Cmd_tryspiteppreduce();printf("%s{\"case\":%d,\"success\":%s,\"pp\":%d,\"draws\":%d,\"updates\":%d,\"cancels\":%d}",k?",":"",k,gBattlescriptCurrInstr==instructions+5?"true":"false",gBattleMons[1].pp[2],draws,updates,cancels);}
 puts("]}");return 0;
}
