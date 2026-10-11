static int ScriptBranch(void)
{
    if (gBattlescriptCurrInstr == script) return 0;
    if (gBattlescriptCurrInstr == BattleScript_BideStoringEnergy) return 1;
    if (gBattlescriptCurrInstr == BattleScript_BideAttack) return 2;
    if (gBattlescriptCurrInstr == BattleScript_BideNoEnergyToAttack) return 3;
    if (gBattlescriptCurrInstr == failureScript) return -1;
    return (int)(gBattlescriptCurrInstr - script);
}

int main(void)
{
    for (u8 slot = 0; slot < 4; slot++) {
        Reset(); gBattlerAttacker = slot; gCurrentMove = MOVE_BIDE;
        gBattleMons[slot].status2 = STATUS2_FOCUS_ENERGY;
        gTakenDmg[slot] = 99; gTakenDmgByBattler[slot] = 3;
        Cmd_setbide();
        printf("{\"kind\":\"bide-setup\",\"slot\":%u,\"status2\":%u,\"duration\":%u,\"damage\":%d,\"lastSource\":%u,\"lockedMove\":%u,\"cursor\":%d}\n",
            slot, gBattleMons[slot].status2, (gBattleMons[slot].status2 & STATUS2_BIDE) >> 8,
            gTakenDmg[slot], gTakenDmgByBattler[slot], gLockedMoves[slot], ScriptBranch());
    }
    for (u8 mode = 0; mode < 6; mode++) {
        Reset(); gBattleMons[0].status2 = STATUS2_FOCUS_ENERGY | STATUS2_MULTIPLETURNS;
        if (mode != 5) gBattleMons[0].status2 |= STATUS2_BIDE_TURN(mode == 0 ? 2 : 1);
        gTakenDmg[0] = mode == 3 ? 0 : 17; gTakenDmgByBattler[0] = 3;
        if (mode == 2) gAbsentBattlerFlags = gBitTable[3];
        if (mode == 4) { gTakenDmgByBattler[0] = 2; gBattleMons[2].hp = 0; }
        u8 effect = SourceBideCanceller();
        printf("{\"kind\":\"bide-step\",\"case\":%u,\"duration\":%u,\"multipleTurns\":%u,\"damage\":%d,\"target\":%u,\"move\":%u,\"branch\":%d,\"effect\":%u,\"tracker\":%u,\"retargetCalls\":%u}\n",
            mode, (gBattleMons[0].status2 & STATUS2_BIDE) >> 8,
            (gBattleMons[0].status2 & STATUS2_MULTIPLETURNS) != 0, gBattleScripting.bideDmg,
            gBattlerTarget, gCurrentMove, ScriptBranch(), effect, gBattleStruct->atkCancellerTracker, retargetCalls);
    }
    for (u8 mode = 0; mode < 6; mode++) {
        Reset(); gCurrentMove = MOVE_PERISH_SONG;
        gBattleMons[0].moves[2] = MOVE_PERISH_SONG; gBattleMons[0].pp[2] = mode == 4 ? 2 : 5;
        gAbsentBattlerFlags = gBitTable[2]; gBattleMons[3].hp = 0;
        for (u8 i = 0; i < 4; i++) {
            gStatuses3[i] = STATUS3_ROOTED;
            gDisableStructs[i].perishSongTimer = gDisableStructs[i].perishSongTimerStartValue = 7;
            if ((mode == 1 && i == 0) || (mode == 2 && (i & 1) == 0) || mode == 5) {
                gStatuses3[i] |= STATUS3_PERISH_SONG;
                gDisableStructs[i].perishSongTimer = 1; gDisableStructs[i].perishSongTimerStartValue = 3;
            }
            if ((mode == 1 && i == 1) || (mode == 2 && (i & 1))) gBattleMons[i].ability = ABILITY_SOUNDPROOF;
            if (mode == 4 || (mode == 5 && (i == 1 || i == 2))) gBattleMons[i].ability = ABILITY_PRESSURE;
        }
        if (mode == 3) gBattlersCount = 2;
        Cmd_trysetperishsong();
        printf("{\"kind\":\"perish-setup\",\"case\":%u,\"battlers\":%u,\"absent\":%u,\"status3\":[%u,%u,%u,%u],\"timers\":[%u,%u,%u,%u],\"starts\":[%u,%u,%u,%u],\"cursor\":%d,\"pp\":%u,\"ppWrites\":%u}\n",
            mode, gBattlersCount, gAbsentBattlerFlags, gStatuses3[0], gStatuses3[1], gStatuses3[2], gStatuses3[3],
            gDisableStructs[0].perishSongTimer, gDisableStructs[1].perishSongTimer,
            gDisableStructs[2].perishSongTimer, gDisableStructs[3].perishSongTimer,
            gDisableStructs[0].perishSongTimerStartValue, gDisableStructs[1].perishSongTimerStartValue,
            gDisableStructs[2].perishSongTimerStartValue, gDisableStructs[3].perishSongTimerStartValue,
            ScriptBranch(), gBattleMons[0].pp[2], ppWrites);
    }
    const u8 attackers[] = {0,0,0,0,3,3,0,0,0,0};
    const u8 targets[] = {1,2,0,3,0,1,1,1,1,1};
    for (u8 mode = 0; mode < 10; mode++) {
        Reset(); gBattlerAttacker = targets[mode];
        gBattleMons[gBattlerAttacker].status2 = STATUS2_FOCUS_ENERGY;
        if (mode != 6) Cmd_setdestinybond();
        gBattlerAttacker = attackers[mode]; gBattlerTarget = targets[mode];
        gHitMarker = HITMARKER_CHARGING;
        if (mode == 7) gHitMarker |= HITMARKER_GRUDGE;
        if (mode == 8) gHitMarker |= HITMARKER_PASSIVE_DAMAGE;
        if (mode == 9) gBattlerPositions[1] = B_POSITION_PLAYER_RIGHT;
        TrySetDestinyBondToHappen();
        printf("{\"kind\":\"destiny-trigger\",\"case\":%u,\"attacker\":%u,\"target\":%u,\"attackerSide\":%u,\"targetSide\":%u,\"armed\":%u,\"trigger\":%u,\"hitMarker\":%u,\"status2\":%u}\n",
            mode, gBattlerAttacker, gBattlerTarget, GetBattlerSide(gBattlerAttacker), GetBattlerSide(gBattlerTarget),
            (gBattleMons[gBattlerTarget].status2 & STATUS2_DESTINY_BOND) != 0,
            (gHitMarker & HITMARKER_DESTINYBOND) != 0, gHitMarker, gBattleMons[gBattlerTarget].status2);
    }
    for (u8 slot = 0; slot < 4; slot++) {
        Reset(); gBattlerAttacker = slot; gStatuses3[slot] = STATUS3_ROOTED;
        Cmd_trysetgrudge(); int first = ScriptBranch(); u32 firstStatus = gStatuses3[slot];
        gBattlescriptCurrInstr = script; Cmd_trysetgrudge();
        printf("{\"kind\":\"grudge-setup\",\"slot\":%u,\"firstCursor\":%d,\"secondCursor\":%d,\"firstStatus3\":%u,\"secondStatus3\":%u}\n",
            slot, first, ScriptBranch(), firstStatus, gStatuses3[slot]);
    }
    for (u8 slot = 0; slot < 4; slot++) {
        Reset(); gBattlerAttacker = slot;
        gBattleMons[slot].status2 = STATUS2_FOCUS_ENERGY | STATUS2_DESTINY_BOND;
        gStatuses3[slot] = STATUS3_ROOTED | STATUS3_GRUDGE;
        SourceFlagsCanceller();
        printf("{\"kind\":\"action-flags-clear\",\"slot\":%u,\"status2\":%u,\"status3\":%u,\"tracker\":%u}\n",
            slot, gBattleMons[slot].status2, gStatuses3[slot], gBattleStruct->atkCancellerTracker);
    }
    return 0;
}
