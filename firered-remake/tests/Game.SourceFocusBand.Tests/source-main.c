/* Input is exactly 24 decimal integer fields, described in schema.json.
 * All resets/assignments below are fixture setup, never source substitutions.
 * Each reset decorates all SpecialStatus bytes so clear-all has a meaningful
 * observable effect; the four focusBanded bits themselves start at zero.
 */
enum { ID, OP, PRESERVE, TARGET, HP, DAMAGE, ITEM, SUBSTITUTE, ENDURE, STICKY,
       FALSE_SWIPE, ATTACKER_LEVEL, TARGET_LEVEL, STURDY, SURE_HIT,
       FLAGS, LAST_ITEM, DRAW0, DRAW1, ENIGMA_EFFECT, ENIGMA_PARAM,
       MOVE_ACCURACY, ATTACKER, STATUS_COUNT, FIELD_COUNT };

static void ResetFixture(void) {
    memset(gBattleMons, 0, sizeof(gBattleMons));
    memset(gEnigmaBerries, 0, sizeof(gEnigmaBerries));
    memset(gProtectStructs, 0, sizeof(gProtectStructs));
    memset(gDisableStructs, 0, sizeof(gDisableStructs));
    memset(gStatuses3, 0, sizeof(gStatuses3));
    memset(gTakenDmg, 0, sizeof(gTakenDmg));
    memset(gTakenDmgByBattler, 0, sizeof(gTakenDmgByBattler));
    memset(gBattleMoves, 0, sizeof(gBattleMoves));
    gHpDealt = 0; gBattleControllerExecFlags = 0; battle.dynamicMoveType = 0;
    memset(gSpecialStatuses, 0x5a, sizeof(gSpecialStatuses));
    for (unsigned i = 0; i < 4; ++i) gSpecialStatuses[i].focusBanded = 0;
    memset(gBattleCommunication, 0x33, sizeof(gBattleCommunication));
    gBattleScripting.dmgMultiplier = 3;
    gCritMultiplier = 2;
    gHitMarker = HITMARKER_DESTINYBOND | HITMARKER_SYNCHRONISE_EFFECT | (1u << 31);
    gMoveResultFlags = 0; gLastUsedItem = 0; gLastUsedAbility = 0;
    gPotentialItemEffectBattler = 255; gActiveBattler = 255;
}

static int ReadRow(char *line, long row[FIELD_COUNT]) {
    char *cursor = line, *end;
    for (unsigned i = 0; i < FIELD_COUNT; i++) {
        errno = 0;
        row[i] = strtol(cursor, &end, 10);
        if (end == cursor || errno || row[i] < -1000000 || row[i] > 1000000) return 0;
        cursor = end;
    }
    while (*cursor == ' ' || *cursor == '\t' || *cursor == '\r' || *cursor == '\n') cursor++;
    if (*cursor) return 0;
    if (row[ID] < 0 || row[OP] < 0 || row[OP] > 7 || row[PRESERVE] < 0 || row[PRESERVE] > 1
        || row[TARGET] < 0 || row[TARGET] > 3 || row[ATTACKER] < 0 || row[ATTACKER] > 3
        || row[ATTACKER] == row[TARGET] || row[HP] < 1 || row[HP] > 65535
        || row[ITEM] < 0 || row[ITEM] > 65535 || row[STICKY] < -1 || row[STICKY] > 1
        || row[FLAGS] < -1 || row[FLAGS] > 255 || row[LAST_ITEM] < -1 || row[LAST_ITEM] > 65535
        || row[DRAW0] < 0 || row[DRAW0] > 65535 || row[DRAW1] < 0 || row[DRAW1] > 65535
        || row[ENIGMA_EFFECT] < 0 || row[ENIGMA_EFFECT] > 255 || row[ENIGMA_PARAM] < 0 || row[ENIGMA_PARAM] > 255
        || row[MOVE_ACCURACY] < 0 || row[MOVE_ACCURACY] > 255
        || row[ATTACKER_LEVEL] < 1 || row[ATTACKER_LEVEL] > 100 || row[TARGET_LEVEL] < 1 || row[TARGET_LEVEL] > 100
        || row[SURE_HIT] < 0 || row[SURE_HIT] > 2 || row[STATUS_COUNT] < 1 || row[STATUS_COUNT] > 4) return 0;
    const unsigned booleans[] = {SUBSTITUTE, ENDURE, FALSE_SWIPE, STURDY};
    for (unsigned i = 0; i < sizeof(booleans)/sizeof(booleans[0]); i++)
        if (row[booleans[i]] < 0 || row[booleans[i]] > 1) return 0;
    return 1;
}

int main(void) {
    char line[1024]; long row[FIELD_COUNT]; unsigned lines = 0;
    while (fgets(line, sizeof(line), stdin)) {
        lines++;
        if (!ReadRow(line, row)) { fprintf(stderr, "invalid input line %u\n", lines); return 2; }
        if (!row[PRESERVE]) ResetFixture();
        rngCount = itemRecords = abilityRecords = 0;
        recordBattler = recordEffect = 255;
        consumedDraws[0] = consumedDraws[1] = 0;
        suppliedDraws[0] = (u16)row[DRAW0]; suppliedDraws[1] = (u16)row[DRAW1];
        gBattlerTarget = (u8)row[TARGET]; gBattlerAttacker = (u8)row[ATTACKER];
        gBattlersCount = (u8)row[STATUS_COUNT];
        gBattleMons[gBattlerTarget].hp = (u16)row[HP];
        gBattleMons[gBattlerTarget].maxHP = (u16)row[HP];
        gBattleMons[gBattlerTarget].item = (u16)row[ITEM];
        gBattleMons[gBattlerTarget].status2 = row[SUBSTITUTE] ? STATUS2_SUBSTITUTE : 0;
        gBattleMons[gBattlerTarget].ability = row[STURDY] ? ABILITY_STURDY : 0;
        gBattleMons[gBattlerTarget].level = (u8)row[TARGET_LEVEL];
        gBattleMons[gBattlerAttacker].level = (u8)row[ATTACKER_LEVEL];
        gProtectStructs[gBattlerTarget].endured = (u8)row[ENDURE];
        if (row[STICKY] >= 0) gSpecialStatuses[gBattlerTarget].focusBanded = (u8)row[STICKY];
        gBattleMoves[0].effect = row[FALSE_SWIPE] ? EFFECT_FALSE_SWIPE : 0;
        gBattleMoves[0].accuracy = (u8)row[MOVE_ACCURACY];
        gCurrentMove = 0;
        gStatuses3[gBattlerTarget] = row[SURE_HIT] ? STATUS3_ALWAYS_HITS : 0;
        gDisableStructs[gBattlerTarget].battlerWithSureHit = row[SURE_HIT] == 1 ? gBattlerAttacker : (gBattlerAttacker + 1) % 4;
        gEnigmaBerries[gBattlerTarget].holdEffect = (u8)row[ENIGMA_EFFECT];
        gEnigmaBerries[gBattlerTarget].holdEffectParam = (u8)row[ENIGMA_PARAM];
        if (row[FLAGS] >= 0) gMoveResultFlags = (u8)row[FLAGS];
        if (row[LAST_ITEM] >= 0) gLastUsedItem = (u16)row[LAST_ITEM];
        gBattleMoveDamage = (s32)row[DAMAGE];
        gBattlescriptCurrInstr = command;
        switch (row[OP]) {
            case 0: Cmd_adjustnormaldamage(); break;
            case 1: Cmd_adjustnormaldamage2(); break;
            case 2: Cmd_adjustsetdamage(); break;
            case 3: Cmd_tryKO(); break;
            case 4: MoveValuesCleanUp(); break;
            case 5: SpecialStatusesClear(); break;
            case 6: break; /* observation-only identity; no claimed original command */
            case 7: Cmd_datahpupdate(); break;
        }
        int destination = gBattlescriptCurrInstr == BattleScript_SturdyPreventsOHKO ? 10
            : gBattlescriptCurrInstr == koFailure ? 11
            : gBattlescriptCurrInstr == BattleScript_SubstituteFade ? 12 : (int)(gBattlescriptCurrInstr - command);
        unsigned zeroMask = 0;
        for (unsigned i = 0; i < 4; ++i) {
            const u8 *bytes = (const u8 *)&gSpecialStatuses[i];
            unsigned any = 0;
            for (unsigned j = 0; j < sizeof(struct SpecialStatus); j++) any |= bytes[j];
            if (!any) zeroMask |= 1u << i;
        }
        printf("%ld,%ld,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%u,%d,%u,%u,%u,%u,%u,%u,%lu,%u,%u,%u,%u\n",
            row[ID], (long)gBattleMoveDamage, gMoveResultFlags, gLastUsedItem, gBattleMons[gBattlerTarget].item,
            gSpecialStatuses[0].focusBanded, gSpecialStatuses[1].focusBanded,
            gSpecialStatuses[2].focusBanded, gSpecialStatuses[3].focusBanded,
            itemRecords, abilityRecords, rngCount, consumedDraws[0], consumedDraws[1], recordBattler, recordEffect,
            destination, gPotentialItemEffectBattler, gActiveBattler, gBattleScripting.dmgMultiplier,
            gCritMultiplier, gBattleCommunication[MOVE_EFFECT_BYTE], gBattleCommunication[MISS_TYPE],
            (unsigned long)gHitMarker, gBattleCommunication[MULTISTRING_CHOOSER], zeroMask,
            gLastUsedAbility, gBattleMons[gBattlerTarget].hp);
    }
    if (ferror(stdin)) return 6;
    return 0;
}
