static void Reset(void) {
    memset(gBattleMons,0,sizeof(gBattleMons));memset(gDisableStructs,0,sizeof(gDisableStructs));
    memset(gProtectStructs,0,sizeof(gProtectStructs));memset(gSpecialStatuses,0,sizeof(gSpecialStatuses));
    memset(gStatuses3,0,sizeof(gStatuses3));memset(gLockedMoves,0,sizeof(gLockedMoves));
    memset(gBattleCommunication,0,sizeof(gBattleCommunication));
    gBattlerAttacker=0;gBattlerTarget=1;gEffectBattler=0;gCurrMovePos=0;
    gHitMarker=0;gMoveResultFlags=0;pressureCount=0;gBattleControllerExecFlags=0;
    gBattlescriptCurrInstr=script;gBattleWeather=0;weatherHasEffect=TRUE;ppWrites=0;failedChecks=0;
}
int main(void) {
    const u16 moves[]={MOVE_FLY,MOVE_BOUNCE,MOVE_DIG,MOVE_DIVE,MOVE_SOLAR_BEAM,MOVE_RAZOR_WIND,MOVE_SKY_ATTACK,MOVE_SKULL_BASH};
    for (unsigned i=0;i<sizeof(moves)/sizeof(moves[0]);i++) {
        Reset();gCurrentMove=moves[i];gBattleMons[0].pp[0]=1;
        gBattleMons[0].status2=STATUS2_FOCUS_ENERGY;gStatuses3[0]=STATUS3_ROOTED;
        Cmd_ppreduce();SourceCharge();Cmd_setsemiinvulnerablebit();
        u32 charged=gBattleMons[0].status2,semi=gStatuses3[0];u8 firstPp=gBattleMons[0].pp[0];
        gBattleCommunication[MOVE_EFFECT_BYTE]=MOVE_EFFECT_CHARGING;Cmd_clearstatusfromeffect();
        gHitMarker|=HITMARKER_NO_PPDEDUCT;Cmd_clearsemiinvulnerablebit();Cmd_ppreduce();
        printf("{\"kind\":\"lifecycle\",\"move\":%u,\"twoTurn\":%u,\"chargeStatus2\":%u,\"chargeStatus3\":%u,\"lockedMove\":%u,\"chargingTurn\":%u,\"ppAfterCharge\":%u,\"ppAfterRelease\":%u,\"releaseStatus2\":%u,\"releaseStatus3\":%u,\"ppWrites\":%u}\n",gCurrentMove,IsTwoTurnsMove(gCurrentMove),charged,semi,gLockedMoves[0],gProtectStructs[0].chargingTurn,firstPp,gBattleMons[0].pp[0],gBattleMons[0].status2,gStatuses3[0],ppWrites);
    }
    for (unsigned i=0;i<4;i++) {
        Reset();gCurrentMove=i<2?MOVE_FLY:MOVE_RAZOR_WIND;gBattleMons[0].pp[0]=5;
        gBattleMons[1].ability=ABILITY_PRESSURE;pressureCount=2;
        gSpecialStatuses[0].ppNotAffectedByPressure=i==3;Cmd_ppreduce();u8 initial=gBattleMons[0].pp[0];
        if(i==1){gHitMarker|=HITMARKER_NO_PPDEDUCT;}Cmd_ppreduce();
        printf("{\"kind\":\"pp\",\"case\":%u,\"move\":%u,\"firstPp\":%u,\"secondPp\":%u,\"writes\":%u}\n",i,gCurrentMove,initial,gBattleMons[0].pp[0],ppWrites);
    }
    /* This executes the sun script's extra PP command after a stored charge. */
    Reset();gCurrentMove=MOVE_SOLAR_BEAM;gBattleMons[0].pp[0]=5;
    Cmd_ppreduce();SourceCharge();u8 initial=gBattleMons[0].pp[0];
    SourceCharge();Cmd_ppreduce();gBattleCommunication[MOVE_EFFECT_BYTE]=MOVE_EFFECT_CHARGING;Cmd_clearstatusfromeffect();gHitMarker|=HITMARKER_NO_PPDEDUCT;Cmd_ppreduce();
    printf("{\"kind\":\"sun-on-release-pp\",\"afterCharge\":%u,\"afterRelease\":%u,\"writes\":%u}\n",initial,gBattleMons[0].pp[0],ppWrites);
    Reset();gBattleMons[0].status2=STATUS2_MULTIPLETURNS|STATUS2_LOCK_CONFUSE|STATUS2_UPROAR|STATUS2_BIDE|STATUS2_FOCUS_ENERGY;
    gStatuses3[0]=STATUS3_SEMI_INVULNERABLE|STATUS3_ROOTED;gDisableStructs[0].rolloutTimer=5;gDisableStructs[0].furyCutterCounter=3;
    CancelMultiTurnMoves(0);
    printf("{\"kind\":\"cancel\",\"status2\":%u,\"status3\":%u,\"rollout\":%u,\"furyCutter\":%u}\n",gBattleMons[0].status2,gStatuses3[0],gDisableStructs[0].rolloutTimer,gDisableStructs[0].furyCutterCounter);
    const u32 states[]={STATUS3_ON_AIR,STATUS3_UNDERGROUND,STATUS3_UNDERWATER};
    const u32 ignores[]={HITMARKER_IGNORE_ON_AIR,HITMARKER_IGNORE_UNDERGROUND,HITMARKER_IGNORE_UNDERWATER};
    for(unsigned state=0;state<3;state++)for(unsigned mode=0;mode<4;mode++) {
        Reset();gCurrentMove=mode==3?MOVE_AERIAL_ACE:MOVE_POUND;gStatuses3[1]=states[state];
        if(mode==1)gHitMarker=ignores[state];
        if(mode==2){gStatuses3[1]|=STATUS3_ALWAYS_HITS;gDisableStructs[1].battlerWithSureHit=0;}
        bool8 handled=AccuracyCalcHelper(gCurrentMove);
        printf("{\"kind\":\"semi-accuracy\",\"state\":%u,\"mode\":%u,\"handled\":%u,\"missed\":%u,\"remainingIgnore\":%u}\n",state,mode,handled,(gMoveResultFlags&MOVE_RESULT_MISSED)!=0,gHitMarker);
    }
    return 0;
}
