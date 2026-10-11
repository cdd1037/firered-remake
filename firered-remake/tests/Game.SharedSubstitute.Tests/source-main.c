/* Fixture scheduling is explicit, not an emulation of the battle script VM. */
static void ResetFixture(void) {
    memset(gBattleMons, 0, sizeof(gBattleMons));
    memset(gDisableStructs, 0, sizeof(gDisableStructs));
    memset(gSpecialStatuses, 0, sizeof(gSpecialStatuses));
    memset(gProtectStructs, 0, sizeof(gProtectStructs));
    memset(gSideTimers, 0, sizeof(gSideTimers));
    memset(gEnigmaBerries, 0, sizeof(gEnigmaBerries));
    memset(gBattleCommunication, 0, sizeof(gBattleCommunication));
    memset(gTakenDmg, 0, sizeof(gTakenDmg));
    memset(gTakenDmgByBattler, 0, sizeof(gTakenDmgByBattler));
    memset(gSideStatuses, 0, sizeof(gSideStatuses));
    memset(gStatuses3, 0, sizeof(gStatuses3));
    memset(&battle, 0, sizeof(battle));
    memset(&gBattleScripting, 0, sizeof(gBattleScripting));
    memset(gBattleMoves, 0, sizeof(gBattleMoves));
    gBattleControllerExecFlags = gHitMarker = gBattleTypeFlags = 0;
    gMoveResultFlags = 0; gCurrentMove = 1; gBattlerAttacker = 0; gBattlerTarget = 1; gBattlersCount = 4;
    gBattleMoveDamage = gHpDealt = 0;
    gatePassed = gateCalls = rngCount = pushCount = fadeCount = alreadyHas = itemEffect = roughEffect = 0;
    draw = 0; statResult = 255;
    for (u8 i=0; i<4; i++) {
        gBattleMons[i].hp = 80; gBattleMons[i].maxHP = 100;
        for (u8 j=0;j<8;j++) gBattleMons[i].statStages[j] = DEFAULT_STAT_STAGE;
    }
    gBattleMoves[gCurrentMove].type = TYPE_NORMAL;
    gBattleMoves[gCurrentMove].flags = FLAG_MAKES_CONTACT | FLAG_KINGS_ROCK_AFFECTED;
}
static void ApplyOperation(unsigned op, unsigned arg) {
    gBattlescriptCurrInstr = command;
    command[1] = BS_TARGET;
    switch (op) {
    case 0: Cmd_setsubstitute(); break;
    case 1:
        command[1] = arg ? BS_ATTACKER : BS_TARGET;
        Cmd_datahpupdate();
        if (gBattlescriptCurrInstr == BattleScript_SubstituteFade) fadeCount++;
        break;
    case 2: SetMoveEffect(arg != 0, 0); break;
    case 3: ProbeMoveEndSubstitute(); break;
    case 4: gActiveBattler = gBattlerTarget; ProbeSwitchIn(); break;
    case 5: gActiveBattler = gBattlerTarget; ProbeFaint(); break;
    case 6: statResult = ChangeStatBuffs((s8)arg, STAT_ATK, STAT_CHANGE_ALLOW_PTR, command); break;
    case 7: itemEffect = ProbeHitItems(ITEMEFFECT_KINGSROCK_SHELLBELL, 0, FALSE); break;
    case 8: roughEffect = ProbeRoughSkin(gCurrentMove); break;
    case 9:
        /* Exact script's pre-command already-present guard. */
        if (gBattleMons[gBattlerAttacker].status2 & STATUS2_SUBSTITUTE) alreadyHas++;
        else Cmd_setsubstitute();
        break;
    case 10: Cmd_seteffectwithchance(); break;
    default: exit(3);
    }
}
static void Record(unsigned id, unsigned step) {
    printf("%u,%u,%u,%u,%u,%u,%u,%u,%ld,%ld,%ld,%ld,%ld,%lu,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%lu,%lu,%u,%lu,%lu\n",
        id, step, gBattleMons[gBattlerAttacker].hp, gBattleMons[gBattlerTarget].hp,
        gDisableStructs[gBattlerAttacker].substituteHP, gDisableStructs[gBattlerTarget].substituteHP,
        !!(gBattleMons[gBattlerAttacker].status2 & STATUS2_SUBSTITUTE), !!(gBattleMons[gBattlerTarget].status2 & STATUS2_SUBSTITUTE),
        (long)gBattleMoveDamage, (long)gHpDealt, (long)gSpecialStatuses[gBattlerTarget].dmg,
        (long)gSpecialStatuses[gBattlerTarget].physicalDmg, (long)gSpecialStatuses[gBattlerTarget].specialDmg,
        (unsigned long)gTakenDmg[gBattlerTarget], gBattleCommunication[MULTISTRING_CHOOSER],
        !!(gBattleMons[gBattlerAttacker].status2 & STATUS2_WRAPPED), gatePassed, gateCalls, fadeCount, rngCount,
        itemEffect, roughEffect, alreadyHas, statResult, gBattleMons[gBattlerTarget].statStages[STAT_ATK],
        (unsigned long)gHitMarker, (unsigned long)gBattleMons[gBattlerTarget].status2,
        gBattleCommunication[MOVE_EFFECT_BYTE], (unsigned long)gBattleMons[2].status2, (unsigned long)gBattleMons[3].status2);
}
