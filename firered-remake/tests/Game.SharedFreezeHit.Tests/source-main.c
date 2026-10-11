int main(void) {
    for (int mode=0;mode<12;mode++) for (int serene=0;serene<2;serene++) for (int roll=0;roll<100;roll++) {
        memset(gBattleMons,0,sizeof gBattleMons);memset(gBattleCommunication,0,sizeof gBattleCommunication);
        memset(gSideStatuses,0,sizeof gSideStatuses);gHitMarker=gBattleTypeFlags=gMoveResultFlags=gBattleWeather=0;
        draws=writes=cancels=0;weatherHasEffect=TRUE;nextRandom=(u16)roll;gBattlerAttacker=2;gBattlerTarget=3;
        gBattleMons[2].ability=serene?ABILITY_SERENE_GRACE:ABILITY_NONE;
        gBattleMons[3].hp=100;gBattleMons[3].type1=gBattleMons[3].type2=TYPE_NORMAL;
        if(mode==1)gBattleMons[3].type1=TYPE_ICE;
        if(mode==2)gBattleMons[3].ability=ABILITY_MAGMA_ARMOR;
        if(mode==3)gBattleMons[3].ability=ABILITY_SHIELD_DUST;
        if(mode==4)gSideStatuses[1]=SIDE_STATUS_SAFEGUARD;
        if(mode==5||mode==6)gBattleWeather=B_WEATHER_SUN_TEMPORARY;
        if(mode==6)weatherHasEffect=FALSE;
        if(mode==7)gBattleMons[3].status1=STATUS1_POISON;
        if(mode==8)gBattleMons[3].hp=0;
        if(mode==9)gBattleMons[3].status2=STATUS2_SUBSTITUTE;
        if(mode==10)gMoveResultFlags=MOVE_RESULT_DOESNT_AFFECT_FOE;
        gBattleMoves[0].secondaryEffectChance=10;gCurrentMove=0;
        gBattleCommunication[MOVE_EFFECT_BYTE]=MOVE_EFFECT_FREEZE;
        if(mode==11)gBattleCommunication[MOVE_EFFECT_BYTE]|=MOVE_EFFECT_CERTAIN;
        gBattlescriptCurrInstr=script;Cmd_seteffectwithchance();
        printf("{\"mode\":%d,\"serene\":%d,\"roll\":%d,\"status\":%u,\"draws\":%u,\"writes\":%u,\"cancels\":%u}\n",mode,serene,roll,gBattleMons[3].status1,draws,writes,cancels);
    }
    return 0;
}
